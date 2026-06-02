import 'dart:convert';
import 'dart:io';

import 'package:package_info_plus/package_info_plus.dart';

class UpdateInfo {
  final String version;
  final String downloadUrl;
  final String htmlUrl;

  UpdateInfo({
    required this.version,
    required this.downloadUrl,
    required this.htmlUrl,
  });
}

class UpdateService {
  static const _repo = 'Venrix/CordCast';

  static bool get isPortable {
    final exeDir = File(Platform.resolvedExecutable).parent.path;
    return File('$exeDir${Platform.pathSeparator}.portable').existsSync();
  }

  /// Check GitHub for a newer release. Returns [UpdateInfo] if one exists,
  /// or `null` if already on latest (or running a dev build).
  static Future<UpdateInfo?> checkForUpdate() async {
    final info = await PackageInfo.fromPlatform();
    final current = info.version;
    if (current == '0.0.0') return null; // dev build, skip

    final client = HttpClient();
    try {
      final request = await client.getUrl(
        Uri.parse('https://api.github.com/repos/$_repo/releases/latest'),
      );
      request.headers.set('Accept', 'application/vnd.github.v3+json');
      final response = await request.close();
      if (response.statusCode != 200) return null;

      final body = await response.transform(utf8.decoder).join();
      final release = jsonDecode(body) as Map<String, dynamic>;
      final tag = (release['tag_name'] as String).replaceFirst('v', '');
      final htmlUrl = release['html_url'] as String;

      if (!_isNewer(tag, current)) return null;

      final assets = release['assets'] as List;
      final asset = assets.cast<Map<String, dynamic>>().firstWhere(
            (a) => (a['name'] as String).endsWith('-windows-setup.exe'),
            orElse: () => <String, dynamic>{},
          );
      final downloadUrl = asset['browser_download_url'] as String?;
      if (downloadUrl == null) return null;

      return UpdateInfo(
        version: tag,
        downloadUrl: downloadUrl,
        htmlUrl: htmlUrl,
      );
    } finally {
      client.close();
    }
  }

  /// Download the installer and run it silently, then exit the app.
  static Future<void> downloadAndInstall(
    UpdateInfo update,
    void Function(double progress) onProgress,
  ) async {
    final tempDir = Directory.systemTemp;
    final downloadPath =
        '${tempDir.path}${Platform.pathSeparator}cordcast-${update.version}-windows-setup.exe';

    final client = HttpClient();
    try {
      final request = await client.getUrl(Uri.parse(update.downloadUrl));
      final response = await request.close();

      final totalBytes = response.contentLength;
      var receivedBytes = 0;
      final sink = File(downloadPath).openWrite();

      await for (final chunk in response) {
        sink.add(chunk);
        receivedBytes += chunk.length;
        if (totalBytes > 0) {
          onProgress(receivedBytes / totalBytes);
        }
      }
      await sink.close();
    } finally {
      client.close();
    }

    await Process.start(
      downloadPath,
      ['/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/CLOSEAPPLICATIONS'],
      mode: ProcessStartMode.detached,
    );
    exit(0);
  }

  /// Returns true if [remote] is newer than [local] (semver comparison).
  static bool _isNewer(String remote, String local) {
    final r = remote.split('.').map(int.tryParse).toList();
    final l = local.split('.').map(int.tryParse).toList();
    for (var i = 0; i < 3; i++) {
      final rv = i < r.length ? (r[i] ?? 0) : 0;
      final lv = i < l.length ? (l[i] ?? 0) : 0;
      if (rv > lv) return true;
      if (rv < lv) return false;
    }
    return false;
  }
}
