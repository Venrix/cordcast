import 'dart:io';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/bot_provider.dart';
import '../providers/settings_provider.dart';
import '../services/update_service.dart';
import '../theme/app_theme.dart';
import 'full_width_dropdown.dart';
import 'hover_text_field.dart';

class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  late TextEditingController _tokenController;

  UpdateInfo? _updateInfo;
  bool _checkingUpdate = false;
  bool _downloading = false;
  double _downloadProgress = 0;
  String? _updateError;

  @override
  void initState() {
    super.initState();
    final settings = context.read<SettingsProvider>();
    _tokenController = TextEditingController(text: settings.config.botToken);
  }

  @override
  void dispose() {
    _tokenController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final settings = context.watch<SettingsProvider>();
    final bot = context.watch<BotProvider>();
    final cfg = settings.config;

    final recDeviceNames = ['(none)', ...bot.recordingDevices.map((d) => d.name)];
    final playDeviceNames = ['(none)', ...bot.playbackDevices.map((d) => d.name)];

    return Scaffold(
      backgroundColor: AppTheme.background,
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Authentication
            _SectionCard(
              label: 'AUTHENTICATION',
              children: [
                HoverTextField(
                  controller: _tokenController,
                  label: 'Bot Token',
                  obscureText: true,
                  onChanged: (v) {
                    cfg.botToken = v;
                    settings.save();
                  },
                ),
                const SizedBox(height: 12),
                _SwitchRow(
                  label: 'Auto-login on startup',
                  value: cfg.autoLogin,
                  onChanged: (v) {
                    setState(() => cfg.autoLogin = v);
                    settings.save();
                  },
                ),
              ],
            ),
            const SizedBox(height: 12),

            // Speak (Send Audio)
            _SectionCard(
              label: 'SPEAK — SEND AUDIO TO DISCORD',
              children: [
                _SwitchRow(
                  label: 'Enable speak',
                  value: cfg.speakEnabled,
                  onChanged: (v) {
                    setState(() => cfg.speakEnabled = v);
                    settings.save();
                  },
                ),
                const SizedBox(height: 12),
                AnimatedOpacity(
                  duration: const Duration(milliseconds: 150),
                  opacity: cfg.speakEnabled ? 1.0 : 0.4,
                  child: IgnorePointer(
                    ignoring: !cfg.speakEnabled,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const _FieldLabel('RECORDING DEVICE'),
                        const SizedBox(height: 4),
                        FullWidthDropdown<String>(
                          value: cfg.recordingDevice ?? '(none)',
                          items: recDeviceNames,
                          labelOf: (v) => v,
                          onChanged: (v) {
                            setState(() => cfg.recordingDevice = v == '(none)' ? null : v);
                            settings.save();
                          },
                        ),
                        const SizedBox(height: 12),
                        _SwitchRow(
                          label: 'Voice threshold',
                          value: cfg.speakThresholdEnabled,
                          onChanged: (v) {
                            setState(() => cfg.speakThresholdEnabled = v);
                            settings.save();
                          },
                        ),
                        if (cfg.speakThresholdEnabled) ...[
                          const SizedBox(height: 8),
                          Slider(
                            value: cfg.speakThreshold,
                            min: 0.0,
                            max: 1.0,
                            divisions: 20,
                            activeColor: AppTheme.accent,
                            inactiveColor: AppTheme.surfaceVariant,
                            label: cfg.speakThreshold.toStringAsFixed(2),
                            onChanged: (v) {
                              setState(() => cfg.speakThreshold = v);
                            },
                            onChangeEnd: (_) => settings.save(),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),

            // Listen (Receive Audio)
            _SectionCard(
              label: 'LISTEN — RECEIVE AUDIO FROM DISCORD',
              children: [
                _SwitchRow(
                  label: 'Enable listen',
                  value: cfg.listenEnabled,
                  onChanged: (v) {
                    setState(() => cfg.listenEnabled = v);
                    settings.save();
                  },
                ),
                const SizedBox(height: 12),
                AnimatedOpacity(
                  duration: const Duration(milliseconds: 150),
                  opacity: cfg.listenEnabled ? 1.0 : 0.4,
                  child: IgnorePointer(
                    ignoring: !cfg.listenEnabled,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const _FieldLabel('PLAYBACK DEVICE'),
                        const SizedBox(height: 4),
                        FullWidthDropdown<String>(
                          value: cfg.playbackDevice ?? '(none)',
                          items: playDeviceNames,
                          labelOf: (v) => v,
                          onChanged: (v) {
                            setState(() => cfg.playbackDevice = v == '(none)' ? null : v);
                            settings.save();
                          },
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),

            // Updates (Windows installer builds only)
            if (Platform.isWindows && !UpdateService.isPortable) ...[
              const SizedBox(height: 12),
              _SectionCard(
                label: 'UPDATES',
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              _updateInfo != null
                                  ? 'Update available: v${_updateInfo!.version}'
                                  : _updateError != null
                                      ? _updateError!
                                      : _checkingUpdate
                                          ? 'Checking for updates...'
                                          : 'Check for new releases on GitHub.',
                              style: TextStyle(
                                color: _updateInfo != null
                                    ? AppTheme.accent
                                    : _updateError != null
                                        ? AppTheme.statusError
                                        : AppTheme.textPrimary,
                                fontSize: 13,
                              ),
                            ),
                            if (_downloading) ...[
                              const SizedBox(height: 8),
                              ClipRRect(
                                borderRadius: BorderRadius.circular(4),
                                child: LinearProgressIndicator(
                                  value: _downloadProgress > 0
                                      ? _downloadProgress
                                      : null,
                                  backgroundColor: AppTheme.surface,
                                  color: AppTheme.accent,
                                  minHeight: 4,
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                      const SizedBox(width: 16),
                      OutlinedButton(
                        onPressed: _checkingUpdate || _downloading
                            ? null
                            : _updateInfo != null
                                ? _installUpdate
                                : _checkForUpdate,
                        child: Text(
                          _downloading
                              ? 'Installing...'
                              : _checkingUpdate
                                  ? 'Checking...'
                                  : _updateInfo != null
                                      ? 'Install'
                                      : 'Check for Updates',
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _checkForUpdate() async {
    setState(() {
      _checkingUpdate = true;
      _updateError = null;
      _updateInfo = null;
    });
    try {
      final info = await UpdateService.checkForUpdate();
      if (!mounted) return;
      setState(() {
        _checkingUpdate = false;
        _updateInfo = info;
        if (info == null) _updateError = 'Already on the latest version.';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _checkingUpdate = false;
        _updateError = 'Failed to check for updates.';
      });
    }
  }

  Future<void> _installUpdate() async {
    if (_updateInfo == null) return;
    setState(() {
      _downloading = true;
      _downloadProgress = 0;
    });
    try {
      await UpdateService.downloadAndInstall(
        _updateInfo!,
        (progress) {
          if (mounted) setState(() => _downloadProgress = progress);
        },
      );
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _downloading = false;
        _updateError = 'Update failed. Try downloading manually.';
        _updateInfo = null;
      });
    }
  }
}

class _SectionCard extends StatelessWidget {
  final String label;
  final List<Widget> children;

  const _SectionCard({required this.label, required this.children});

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              label,
              style: const TextStyle(
                color: AppTheme.textSecondary,
                fontSize: 11,
                fontWeight: FontWeight.w600,
                letterSpacing: 0.8,
              ),
            ),
            const SizedBox(height: 12),
            ...children,
          ],
        ),
      ),
    );
  }
}

class _SwitchRow extends StatelessWidget {
  final String label;
  final bool value;
  final ValueChanged<bool> onChanged;

  const _SwitchRow({required this.label, required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: Text(label,
              style: const TextStyle(color: AppTheme.textPrimary, fontSize: 13)),
        ),
        Switch(value: value, onChanged: onChanged),
      ],
    );
  }
}

class _FieldLabel extends StatelessWidget {
  final String text;
  const _FieldLabel(this.text);

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: AppTheme.textSecondary,
        fontSize: 11,
        fontWeight: FontWeight.w600,
        letterSpacing: 0.8,
      ),
    );
  }
}
