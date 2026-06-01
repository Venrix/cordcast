class GuildInfo {
  final String id;
  final String name;
  final String? channelId;
  final String? channelName;

  const GuildInfo({
    required this.id,
    required this.name,
    this.channelId,
    this.channelName,
  });

  factory GuildInfo.fromJson(Map<String, dynamic> json) => GuildInfo(
        id: json['id'] as String,
        name: json['name'] as String,
        channelId: json['channelId'] as String?,
        channelName: json['channelName'] as String?,
      );
}
