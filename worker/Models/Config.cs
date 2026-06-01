using System.Text.Json.Serialization;

namespace CordCastWorker.Models;

public class Config
{
    [JsonPropertyName("botToken")]
    public string BotToken { get; set; } = "";

    [JsonPropertyName("autoLogin")]
    public bool AutoLogin { get; set; }

    [JsonPropertyName("speakEnabled")]
    public bool SpeakEnabled { get; set; } = true;

    [JsonPropertyName("recordingDevice")]
    public string? RecordingDevice { get; set; }

    [JsonPropertyName("listenEnabled")]
    public bool ListenEnabled { get; set; }

    [JsonPropertyName("playbackDevice")]
    public string? PlaybackDevice { get; set; }

    [JsonPropertyName("speakThresholdEnabled")]
    public bool SpeakThresholdEnabled { get; set; }

    [JsonPropertyName("speakThreshold")]
    public double SpeakThreshold { get; set; } = 0.5;

    [JsonPropertyName("guildConfigs")]
    public List<GuildConfig> GuildConfigs { get; set; } = new();

    public GuildConfig GetGuildConfig(string guildId)
    {
        var cfg = GuildConfigs.FirstOrDefault(g => g.GuildId == guildId);
        if (cfg is null)
        {
            cfg = new GuildConfig { GuildId = guildId };
            GuildConfigs.Add(cfg);
        }
        return cfg;
    }
}
