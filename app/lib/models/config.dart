import 'guild_config.dart';

class Config {
  String botToken;
  bool autoLogin;
  bool speakEnabled;
  String? recordingDevice;
  bool listenEnabled;
  String? playbackDevice;
  bool speakThresholdEnabled;
  double speakThreshold;
  List<GuildConfig> guildConfigs;

  Config({
    this.botToken = '',
    this.autoLogin = false,
    this.speakEnabled = true,
    this.recordingDevice,
    this.listenEnabled = false,
    this.playbackDevice,
    this.speakThresholdEnabled = false,
    this.speakThreshold = 0.5,
    List<GuildConfig>? guildConfigs,
  }) : guildConfigs = guildConfigs ?? [];

  factory Config.fromJson(Map<String, dynamic> json) => Config(
        botToken: json['botToken'] as String? ?? '',
        autoLogin: json['autoLogin'] as bool? ?? false,
        speakEnabled: json['speakEnabled'] as bool? ?? true,
        recordingDevice: json['recordingDevice'] as String?,
        listenEnabled: json['listenEnabled'] as bool? ?? false,
        playbackDevice: json['playbackDevice'] as String?,
        speakThresholdEnabled: json['speakThresholdEnabled'] as bool? ?? false,
        speakThreshold: (json['speakThreshold'] as num?)?.toDouble() ?? 0.5,
        guildConfigs: ((json['guildConfigs'] as List<dynamic>?) ?? [])
            .map((e) => GuildConfig.fromJson(e as Map<String, dynamic>))
            .toList(),
      );

  Map<String, dynamic> toJson() => {
        'botToken': botToken,
        'autoLogin': autoLogin,
        'speakEnabled': speakEnabled,
        'recordingDevice': recordingDevice,
        'listenEnabled': listenEnabled,
        'playbackDevice': playbackDevice,
        'speakThresholdEnabled': speakThresholdEnabled,
        'speakThreshold': speakThreshold,
        'guildConfigs': guildConfigs.map((g) => g.toJson()).toList(),
      };

  GuildConfig guildConfig(String guildId) {
    return guildConfigs.firstWhere(
      (g) => g.guildId == guildId,
      orElse: () {
        final cfg = GuildConfig(guildId: guildId);
        guildConfigs.add(cfg);
        return cfg;
      },
    );
  }
}
