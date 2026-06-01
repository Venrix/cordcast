import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:path/path.dart' as p;

import '../models/config.dart';

class SettingsProvider extends ChangeNotifier {
  Config config = Config();

  static String get _configPath {
    final localAppData = Platform.environment['LOCALAPPDATA'] ??
        Platform.environment['HOME'] ??
        '.';
    return p.join(localAppData, 'CordCast', 'config.json');
  }

  Future<void> init() async {
    try {
      final file = File(_configPath);
      if (!await file.exists()) return;
      final json = jsonDecode(await file.readAsString()) as Map<String, dynamic>;
      config = Config.fromJson(json);
    } catch (_) {}
  }

  Future<void> save() async {
    try {
      final file = File(_configPath);
      await file.parent.create(recursive: true);
      await file.writeAsString(
        const JsonEncoder.withIndent('  ').convert(config.toJson()),
      );
    } catch (_) {}
  }
}
