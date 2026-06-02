using Discord;
using Discord.Audio;
using Discord.WebSocket;
using CordCastWorker.Models;

namespace CordCastWorker;

public class BotService : IAsyncDisposable
{
    private DiscordSocketClient? _client;
    private readonly AudioService _audio;
    private Config? _config;

    // guildId → AudioOutStream for sending to Discord
    private readonly Dictionary<ulong, AudioOutStream> _audioStreams = new();
    private readonly Dictionary<ulong, IAudioClient> _audioClients = new();
    private readonly SemaphoreSlim _audioLock = new(1, 1);

    public BotService(AudioService audio)
    {
        _audio = audio;
        _audio.AudioFrameReady += OnAudioFrameReady;
    }

    public async Task StartAsync(Config config)
    {
        _config = config;

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
            LogLevel = LogSeverity.Info,
            EnableVoiceDaveEncryption = true, // DAVE E2EE mandatory since Mar 2026; needs libdave.dll
        });

        _client.Log += msg =>
        {
            var text = msg.Exception != null ? $"{msg.Message}: {msg.Exception}" : msg.Message;
            Logger.Write(msg.Severity.ToString(), msg.Source, text);
            return Task.CompletedTask;
        };

        _client.Ready += OnReady;
        _client.SlashCommandExecuted += OnSlashCommand;
        _client.UserVoiceStateUpdated += OnVoiceStateUpdated;
        _client.LatencyUpdated += OnLatencyUpdated;

        await _client.LoginAsync(TokenType.Bot, config.BotToken);
        await _client.StartAsync();
    }

    private async Task OnReady()
    {
        await RegisterSlashCommandsAsync();

        // Auto-join configured channels
        foreach (var guild in _client!.Guilds)
        {
            var guildCfg = _config!.GetGuildConfig(guild.Id.ToString());
            if (guildCfg.AutoJoinAudioChannelId is { } channelIdStr &&
                ulong.TryParse(channelIdStr, out var channelId))
            {
                var channel = guild.GetVoiceChannel(channelId);
                if (channel is not null)
                    await JoinChannelAsync(guild, channel);
            }
        }

        _audio.Init();
        ApplyAudioConfig();
        EmitGuildsUpdated();

        Ipc.Emit("ready", new Dictionary<string, object?>
        {
            ["gatewayPing"] = _client.Latency,
        });
    }

    private Task OnLatencyUpdated(int old, int current)
    {
        Ipc.Emit("ping_updated", new Dictionary<string, object?>
        {
            ["gatewayPing"] = current,
            ["audioPing"] = 0,
        });
        return Task.CompletedTask;
    }

    private Task OnVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        _ = Task.Run(async () =>
        {
            if (_config is null) return;

            foreach (var guild in _client!.Guilds)
            {
                var guildCfg = _config.GetGuildConfig(guild.Id.ToString());
                if (guildCfg.FollowedUserId != user.Id.ToString()) continue;

                if (after.VoiceChannel is not null)
                    await JoinChannelAsync(guild, after.VoiceChannel);
                else
                    await LeaveGuildAudioAsync(guild.Id);
            }

            EmitGuildsUpdated();
        });
        return Task.CompletedTask;
    }

    private Task OnSlashCommand(SocketSlashCommand cmd)
    {
        _ = Task.Run(() => Commands.SlashCommandHandler.HandleAsync(cmd, this, _config!));
        return Task.CompletedTask;
    }

    public async Task JoinChannelAsync(SocketGuild guild, IVoiceChannel channel)
    {
        // Remove existing connection under lock, then stop it outside the lock
        // so OnAudioFrameReady isn't blocked during the network roundtrip.
        IAudioClient? oldClient = null;
        await _audioLock.WaitAsync();
        try
        {
            if (_audioClients.TryGetValue(guild.Id, out oldClient))
            {
                _audioStreams.Remove(guild.Id);
                _audioClients.Remove(guild.Id);
            }
        }
        finally
        {
            _audioLock.Release();
        }
        if (oldClient is not null)
            await oldClient.StopAsync();

        // ConnectAsync is long-running (UDP handshake) -- do NOT hold the lock here
        Logger.Write("INFO", "BotService", $"Joining {channel.Name} in {guild.Name}");
        var audioClient = await channel.ConnectAsync();

        audioClient.Disconnected += ex =>
        {
            Logger.Write("ERROR", "Voice", $"Disconnected: {ex?.Message ?? "no exception"}");
            return Task.CompletedTask;
        };

        var pcmStream = audioClient.CreatePCMStream(AudioApplication.Voice, bitrate: 96000, bufferMillis: 200);

        // Always subscribe; AudioService.ReceiveDiscordAudio drops frames when
        // listen is off, so this can be toggled live without rejoining voice.
        audioClient.StreamCreated += (userId, stream) =>
        {
            _ = Task.Run(async () =>
            {
                var buf = new byte[3840];
                while (true)
                {
                    int read = await stream.ReadAsync(buf);
                    if (read == 0) break;
                    _audio.ReceiveDiscordAudio(buf[..read]);
                }
            });
            return Task.CompletedTask;
        };

        // Store new connection
        await _audioLock.WaitAsync();
        try
        {
            _audioClients[guild.Id] = audioClient;
            _audioStreams[guild.Id] = pcmStream;
        }
        finally
        {
            _audioLock.Release();
        }
    }

    public async Task LeaveGuildAudioAsync(ulong guildId)
    {
        await _audioLock.WaitAsync();
        try
        {
            if (_audioClients.TryGetValue(guildId, out var client))
            {
                _audioStreams.Remove(guildId);
                _audioClients.Remove(guildId);
                await client.StopAsync();
            }
        }
        finally
        {
            _audioLock.Release();
        }
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
            // Write to all guild streams in parallel — sequential writes would
            // delay later guilds by however long the first write takes, causing
            // Discord to drop frames for those guilds due to timing jitter.
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
        var guilds = _client.Guilds.Select(g =>
        {
            _audioClients.TryGetValue(g.Id, out var ac);
            var channel = ac is not null
                ? g.VoiceChannels.FirstOrDefault(c =>
                    c.Users.Any(u => u.Id == _client.CurrentUser.Id))
                : null;
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

    public DiscordSocketClient? Client => _client;
    public Config? Config => _config;

    private async Task RegisterSlashCommandsAsync()
    {
        var cmds = Commands.SlashCommandHandler.BuildCommands();
        await _client!.BulkOverwriteGlobalApplicationCommandsAsync(cmds);
    }

    public async Task StopAsync()
    {
        await LeaveAllAsync();
        if (_client is not null)
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
        }
        Ipc.Emit("disconnected");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _audio.AudioFrameReady -= OnAudioFrameReady;
        _client?.Dispose();
    }
}
