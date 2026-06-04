using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using CordCastWorker.Models;

namespace CordCastWorker;

public class BotService : IAsyncDisposable
{
    private GatewayClient? _client;
    private readonly AudioService _audio;
    private Config? _config;

    private readonly Dictionary<ulong, OpusEncodeStream> _audioStreams = new();
    private readonly Dictionary<ulong, VoiceClient> _audioClients = new();
    private readonly Dictionary<ulong, ulong> _channelIds = new();
    private readonly SemaphoreSlim _audioLock = new(1, 1);

    public BotService(AudioService audio)
    {
        _audio = audio;
        _audio.AudioFrameReady += OnAudioFrameReady;
    }

    public async Task StartAsync(Config config)
    {
        _config = config;

        _client = new GatewayClient(new BotToken(config.BotToken), new GatewayClientConfiguration
        {
            Intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
        });

        _client.Ready += OnReady;
        _client.InteractionCreate += OnInteraction;
        _client.VoiceStateUpdate += OnVoiceStateUpdate;
        _client.LatencyUpdate += latency =>
        {
            Ipc.Emit("ping_updated", new Dictionary<string, object?>
            {
                ["gatewayPing"] = (int)latency.TotalMilliseconds,
                ["audioPing"] = 0,
            });
            return default;
        };

        await _client.StartAsync();
    }

    private async ValueTask OnReady(ReadyEventArgs args)
    {
        await RegisterSlashCommandsAsync();

        foreach (var guild in _client!.Cache.Guilds.Values)
        {
            var guildCfg = _config!.GetGuildConfig(guild.Id.ToString());
            if (guildCfg.AutoJoinAudioChannelId is { } channelIdStr &&
                ulong.TryParse(channelIdStr, out var channelId))
            {
                await JoinChannelAsync(guild, channelId);
            }
        }

        _audio.Init();
        ApplyAudioConfig();
        EmitGuildsUpdated();

        Ipc.Emit("ready", new Dictionary<string, object?>
        {
            ["gatewayPing"] = (int)_client.Latency.TotalMilliseconds,
        });
    }

    private ValueTask OnVoiceStateUpdate(VoiceState voiceState)
    {
        _ = Task.Run(async () =>
        {
            if (_config is null) return;

            var guildCfg = _config.GetGuildConfig(voiceState.GuildId.ToString());
            if (guildCfg.FollowedUserId != voiceState.UserId.ToString()) return;

            if (!_client!.Cache.Guilds.TryGetValue(voiceState.GuildId, out var guild)) return;

            if (voiceState.ChannelId.HasValue)
                await JoinChannelAsync(guild, voiceState.ChannelId.Value);
            else
                await LeaveGuildAudioAsync(voiceState.GuildId);

            EmitGuildsUpdated();
        });
        return default;
    }

    private ValueTask OnInteraction(Interaction interaction)
    {
        if (interaction is SlashCommandInteraction cmd)
            _ = Task.Run(() => Commands.SlashCommandHandler.HandleAsync(cmd, this, _config!));
        return default;
    }

    public async Task JoinChannelAsync(Guild guild, ulong channelId)
    {
        // Remove existing connection under lock, then tear down outside the lock
        // so OnAudioFrameReady isn't blocked during network operations.
        VoiceClient? oldClient = null;
        OpusEncodeStream? oldStream = null;
        await _audioLock.WaitAsync();
        try
        {
            if (_audioClients.TryGetValue(guild.Id, out oldClient))
            {
                _audioStreams.TryGetValue(guild.Id, out oldStream);
                _audioStreams.Remove(guild.Id);
                _audioClients.Remove(guild.Id);
                _channelIds.Remove(guild.Id);
            }
        }
        finally
        {
            _audioLock.Release();
        }
        if (oldStream is not null) await oldStream.DisposeAsync();
        if (oldClient is not null)
        {
            await oldClient.CloseAsync();
            oldClient.Dispose();
        }

        Logger.Write("INFO", "BotService", $"Joining channel {channelId} in {guild.Name}");

        // JoinVoiceChannelAsync handles UpdateVoiceState + VoiceStateUpdate/VoiceServerUpdate
        // coordination but returns an unconnected client — StartAsync completes the WebSocket handshake.
        var voiceClient = await _client!.JoinVoiceChannelAsync(guild.Id, channelId);
        await voiceClient.StartAsync();
        await voiceClient.EnterSpeakingStateAsync(new SpeakingProperties(SpeakingFlags.Microphone));

        var encodeStream = new OpusEncodeStream(
            voiceClient.CreateVoiceStream(), PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);

        voiceClient.Disconnect += async _ =>
        {
            Logger.Write("WARN", "Voice", $"Disconnected from {guild.Name}");
            await _audioLock.WaitAsync();
            try
            {
                if (_audioClients.TryGetValue(guild.Id, out var current) && ReferenceEquals(current, voiceClient))
                {
                    _audioStreams.Remove(guild.Id);
                    _audioClients.Remove(guild.Id);
                    _channelIds.Remove(guild.Id);
                }
            }
            finally
            {
                _audioLock.Release();
            }
            EmitGuildsUpdated();
        };

        voiceClient.VoiceReceive += args =>
        {
            _audio.ReceiveDiscordAudio(args.Frame.ToArray());
            return default;
        };

        await _audioLock.WaitAsync();
        try
        {
            _audioClients[guild.Id] = voiceClient;
            _audioStreams[guild.Id] = encodeStream;
            _channelIds[guild.Id] = channelId;
        }
        finally
        {
            _audioLock.Release();
        }
    }

    public async Task LeaveGuildAudioAsync(ulong guildId)
    {
        VoiceClient? client = null;
        OpusEncodeStream? stream = null;
        await _audioLock.WaitAsync();
        try
        {
            if (_audioClients.TryGetValue(guildId, out client))
            {
                _audioStreams.TryGetValue(guildId, out stream);
                _audioStreams.Remove(guildId);
                _audioClients.Remove(guildId);
                _channelIds.Remove(guildId);
            }
        }
        finally
        {
            _audioLock.Release();
        }
        if (stream is not null) await stream.DisposeAsync();
        if (client is not null)
        {
            await client.CloseAsync();
            client.Dispose();
        }
        if (_client is not null)
            await _client.UpdateVoiceStateAsync(new VoiceStateProperties(guildId, null));
    }

    public async Task LeaveAllAsync()
    {
        var ids = _audioClients.Keys.ToList();
        foreach (var id in ids)
            await LeaveGuildAudioAsync(id);
    }

    private async void OnAudioFrameReady(object? sender, byte[] pcm)
    {
        await _audioLock.WaitAsync();
        try
        {
            await Task.WhenAll(_audioStreams.Values.Select(async stream =>
            {
                try { await stream.WriteAsync(pcm); }
                catch { /* guild disconnected */ }
            }));
        }
        finally
        {
            _audioLock.Release();
        }
    }

    private void ApplyAudioConfig()
    {
        if (_config is null) return;
        _audio.SetSpeak(_config.SpeakEnabled, _config.RecordingDevice,
            _config.SpeakThresholdEnabled, _config.SpeakThreshold);
        _audio.SetListen(_config.ListenEnabled, _config.PlaybackDevice);
    }

    public void UpdateGuildConfig(string guildId, GuildConfig cfg)
    {
        if (_config is null) return;
        _config.GuildConfigs.RemoveAll(g => g.GuildId == guildId);
        _config.GuildConfigs.Add(cfg);
    }

    public void EmitGuildsUpdated()
    {
        if (_client is null) return;
        var guilds = _client.Cache.Guilds.Values.Select(g =>
        {
            _channelIds.TryGetValue(g.Id, out var chId);
            var channel = chId != 0 ? g.Channels.GetValueOrDefault(chId) : null;
            return new Dictionary<string, object?>
            {
                ["id"] = g.Id.ToString(),
                ["name"] = g.Name,
                ["channelId"] = channel?.Id.ToString(),
                ["channelName"] = channel?.Name,
            };
        }).ToList();

        Ipc.Emit("guilds_updated", new Dictionary<string, object?> { ["guilds"] = guilds });
    }

    public ulong GetCurrentChannelId(ulong guildId) => _channelIds.GetValueOrDefault(guildId);

    public GatewayClient? Client => _client;
    public Config? Config => _config;

    private async Task RegisterSlashCommandsAsync()
    {
        var cmds = Commands.SlashCommandHandler.BuildCommands();
        await _client!.Rest.BulkOverwriteGlobalApplicationCommandsAsync(_client.Id, cmds);
    }

    public async Task StopAsync()
    {
        await LeaveAllAsync();
        if (_client is not null)
            await _client.CloseAsync();
        Ipc.Emit("disconnected");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _audio.AudioFrameReady -= OnAudioFrameReady;
        _client?.Dispose();
    }
}
