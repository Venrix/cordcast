import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/audio_device.dart';
import '../models/guild_config.dart';
import '../models/guild_info.dart';
import '../services/bot_worker_service.dart';
import 'settings_provider.dart';

enum BotStatus { disconnected, connecting, connected, error }

class BotProvider extends ChangeNotifier {
  final BotWorkerService _worker;
  final SettingsProvider _settings;

  BotStatus status = BotStatus.disconnected;
  String? errorMessage;
  int gatewayPing = 0;
  int audioPing = 0;
  List<GuildInfo> guilds = [];
  List<AudioDevice> recordingDevices = [];
  List<AudioDevice> playbackDevices = [];

  StreamSubscription? _eventSub;

  BotProvider(this._worker, this._settings);

  Future<void> init({bool autoStart = false}) async {
    await _worker.start();
    _eventSub = _worker.events.listen(_handleEvent);
    _worker.listDevices();

    if (autoStart && _settings.config.botToken.isNotEmpty) {
      startBot();
    }
  }

  void _handleEvent(Map<String, dynamic> event) {
    final type = event['event'] as String?;
    switch (type) {
      case 'ready':
        status = BotStatus.connected;
        gatewayPing = (event['gatewayPing'] as num?)?.toInt() ?? 0;
        errorMessage = null;

      case 'ping_updated':
        gatewayPing = (event['gatewayPing'] as num?)?.toInt() ?? gatewayPing;
        audioPing = (event['audioPing'] as num?)?.toInt() ?? audioPing;

      case 'guilds_updated':
        final raw = event['guilds'] as List<dynamic>? ?? [];
        guilds = raw.map((e) => GuildInfo.fromJson(e as Map<String, dynamic>)).toList();

      case 'devices_listed':
        final rawRec = event['recording'] as List<dynamic>? ?? [];
        final rawPlay = event['playback'] as List<dynamic>? ?? [];
        recordingDevices = rawRec
            .map((e) => AudioDevice.fromJson(e as Map<String, dynamic>))
            .toList();
        playbackDevices = rawPlay
            .map((e) => AudioDevice.fromJson(e as Map<String, dynamic>))
            .toList();
        _restoreDeviceSelections();

      case 'disconnected':
        status = BotStatus.disconnected;
        guilds = [];
        gatewayPing = 0;
        audioPing = 0;

      case 'error':
        status = BotStatus.error;
        errorMessage = event['message'] as String? ?? 'Unknown error';
    }
    notifyListeners();
  }

  void _restoreDeviceSelections() {
    final cfg = _settings.config;
    if (cfg.recordingDevice != null &&
        !recordingDevices.any((d) => d.name == cfg.recordingDevice)) {
      cfg.recordingDevice = recordingDevices.isNotEmpty ? recordingDevices.first.name : null;
    }
    if (cfg.playbackDevice != null &&
        !playbackDevices.any((d) => d.name == cfg.playbackDevice)) {
      cfg.playbackDevice = playbackDevices.isNotEmpty ? playbackDevices.first.name : null;
    }
  }

  void startBot() {
    if (status == BotStatus.connecting || status == BotStatus.connected) return;
    if (_settings.config.botToken.isEmpty) {
      status = BotStatus.error;
      errorMessage = 'Bot token is required.';
      notifyListeners();
      return;
    }
    status = BotStatus.connecting;
    errorMessage = null;
    notifyListeners();
    _worker.startBot(_settings.config.toJson());
  }

  void stopBot() {
    _worker.stopBot();
  }

  void refreshDevices() {
    _worker.listDevices();
  }

  void updateGuildConfig(GuildConfig guildConfig) {
    _settings.config.guildConfigs.removeWhere((g) => g.guildId == guildConfig.guildId);
    _settings.config.guildConfigs.add(guildConfig);
    _settings.save();
    _worker.updateGuildConfig(guildConfig.guildId, guildConfig.toJson());
    notifyListeners();
  }

  @override
  void dispose() {
    _eventSub?.cancel();
    _worker.dispose();
    super.dispose();
  }
}
