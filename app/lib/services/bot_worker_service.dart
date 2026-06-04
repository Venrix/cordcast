import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:path/path.dart' as p;

class BotWorkerService {
  Process? _process;
  StreamController<Map<String, dynamic>>? _eventController;
  StreamSubscription? _stdoutSub;
  StreamSubscription? _stderrSub;
  bool _disposed = false;

  Stream<Map<String, dynamic>> get events =>
      _eventController?.stream ?? const Stream.empty();

  String get _workerPath {
    final exeDir = p.dirname(Platform.resolvedExecutable);
    final workerName = Platform.isWindows ? 'CordCastWorker.exe' : 'CordCastWorker';
    return p.join(exeDir, 'data', 'flutter_assets', 'assets', 'worker', workerName);
  }

  Future<void> start() async {
    _disposed = false;
    _eventController = StreamController<Map<String, dynamic>>.broadcast();

    if (!Platform.isWindows) {
      await Process.run('chmod', ['+x', _workerPath]);
    }

    _process = await Process.start(
      _workerPath,
      [],
      mode: ProcessStartMode.normal,
    );

    _stdoutSub = _process!.stdout
        .transform(utf8.decoder)
        .transform(const LineSplitter())
        .listen(_handleLine, onError: (_) {});

    _stderrSub = _process!.stderr
        .transform(utf8.decoder)
        .transform(const LineSplitter())
        .listen((_) {});

    _process!.exitCode.then((code) {
      if (!_disposed) {
        _eventController?.add({'event': 'error', 'message': 'Worker exited unexpectedly (code $code)'});
      }
    });
  }

  void _handleLine(String line) {
    line = line.trim();
    if (line.isEmpty) return;
    try {
      final decoded = jsonDecode(line);
      if (decoded is Map<String, dynamic>) {
        _eventController?.add(decoded);
      }
    } catch (_) {}
  }

  void _send(Map<String, dynamic> cmd) {
    final process = _process;
    if (process == null) return;
    try {
      process.stdin.writeln(jsonEncode(cmd));
    } catch (_) {}
  }

  void startBot(Map<String, dynamic> config) =>
      _send({'command': 'start', 'config': config});

  void stopBot() => _send({'command': 'stop'});

  void listDevices() => _send({'command': 'list_devices'});

  void setSpeak({
    required bool enabled,
    String? deviceName,
    required bool thresholdEnabled,
    required double threshold,
  }) =>
      _send({
        'command': 'set_speak',
        'enabled': enabled,
        if (deviceName != null) 'deviceName': deviceName,
        'thresholdEnabled': thresholdEnabled,
        'threshold': threshold,
      });

  void setListen({required bool enabled, String? deviceName}) =>
      _send({
        'command': 'set_listen',
        'enabled': enabled,
        if (deviceName != null) 'deviceName': deviceName,
      });

  void updateGuildConfig(String guildId, Map<String, dynamic> config) =>
      _send({'command': 'update_guild_config', 'guildId': guildId, 'config': config});

  void dispose() {
    _disposed = true;
    try {
      _send({'command': 'exit'});
    } catch (_) {}
    _stdoutSub?.cancel();
    _stderrSub?.cancel();
    Future.delayed(const Duration(seconds: 2), () => _process?.kill());
    _eventController?.close();
  }
}
