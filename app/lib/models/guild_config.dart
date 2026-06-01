class GuildConfig {
  final String guildId;
  Set<String> commandChannelIds;
  String? autoJoinAudioChannelId;
  String? followedUserId;

  GuildConfig({
    required this.guildId,
    Set<String>? commandChannelIds,
    this.autoJoinAudioChannelId,
    this.followedUserId,
  }) : commandChannelIds = commandChannelIds ?? {};

  factory GuildConfig.fromJson(Map<String, dynamic> json) => GuildConfig(
        guildId: json['guildId'] as String,
        commandChannelIds: ((json['commandChannelIds'] as List<dynamic>?) ?? [])
            .map((e) => e as String)
            .toSet(),
        autoJoinAudioChannelId: json['autoJoinAudioChannelId'] as String?,
        followedUserId: json['followedUserId'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'guildId': guildId,
        'commandChannelIds': commandChannelIds.toList(),
        'autoJoinAudioChannelId': autoJoinAudioChannelId,
        'followedUserId': followedUserId,
      };
}
