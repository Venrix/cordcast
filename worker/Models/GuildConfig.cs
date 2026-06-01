using System.Text.Json.Serialization;

namespace CordCastWorker.Models;

public class GuildConfig
{
    [JsonPropertyName("guildId")]
    public string GuildId { get; set; } = "";

    [JsonPropertyName("commandChannelIds")]
    public HashSet<string> CommandChannelIds { get; set; } = new();

    [JsonPropertyName("autoJoinAudioChannelId")]
    public string? AutoJoinAudioChannelId { get; set; }

    [JsonPropertyName("followedUserId")]
    public string? FollowedUserId { get; set; }

    public bool IsCommandAllowed(string channelId) =>
        CommandChannelIds.Count == 0 || CommandChannelIds.Contains(channelId);
}
