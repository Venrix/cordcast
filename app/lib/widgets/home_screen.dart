import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/bot_provider.dart';
import '../theme/app_theme.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final bot = context.watch<BotProvider>();
    final isConnected = bot.status == BotStatus.connected;
    final isConnecting = bot.status == BotStatus.connecting;
    final isError = bot.status == BotStatus.error;

    return Scaffold(
      backgroundColor: AppTheme.background,
      body: Column(
        children: [
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(20, 20, 20, 0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Status card
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Row(
                        children: [
                          _StatusDot(status: bot.status),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              _statusLabel(bot.status, bot.errorMessage),
                              style: TextStyle(
                                color: isError ? AppTheme.statusError : AppTheme.textPrimary,
                                fontSize: 13,
                              ),
                            ),
                          ),
                          if (isConnected) ...[
                            _PingChip(label: 'GW', value: bot.gatewayPing),
                            const SizedBox(width: 8),
                            _PingChip(label: 'AUDIO', value: bot.audioPing),
                          ],
                          if (isConnected) ...[
                            const SizedBox(width: 8),
                            MouseRegion(
                              cursor: SystemMouseCursors.click,
                              child: GestureDetector(
                                onTap: bot.refreshDevices,
                                child: const Icon(Icons.refresh,
                                    color: AppTheme.textSecondary, size: 16),
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),

                  // Start / Stop button
                  SizedBox(
                    height: 48,
                    child: isConnected
                        ? OutlinedButton(
                            onPressed: bot.stopBot,
                            style: OutlinedButton.styleFrom(
                              foregroundColor: AppTheme.statusError,
                              backgroundColor: AppTheme.statusError.withAlpha(20),
                              side: BorderSide.none,
                              shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(6)),
                            ),
                            child: const Text(
                              'STOP BOT',
                              style: TextStyle(fontWeight: FontWeight.w600, letterSpacing: 0.5),
                            ),
                          )
                        : FilledButton(
                            onPressed: isConnecting ? null : bot.startBot,
                            child: isConnecting
                                ? const SizedBox(
                                    width: 16,
                                    height: 16,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                      valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
                                    ),
                                  )
                                : const Text(
                                    'START BOT',
                                    style: TextStyle(fontWeight: FontWeight.w600, letterSpacing: 0.5),
                                  ),
                          ),
                  ),
                  const SizedBox(height: 20),

                  // Connected guilds
                  if (bot.guilds.isNotEmpty) ...[
                    const Text(
                      'CONNECTED GUILDS',
                      style: TextStyle(
                        color: AppTheme.textSecondary,
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        letterSpacing: 0.8,
                      ),
                    ),
                    const SizedBox(height: 8),
                    ...bot.guilds.map((g) => Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: Card(
                            child: Padding(
                              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                              child: Row(
                                children: [
                                  Container(
                                    width: 8,
                                    height: 8,
                                    decoration: BoxDecoration(
                                      color: g.channelId != null
                                          ? AppTheme.statusActive
                                          : AppTheme.textSecondary,
                                      shape: BoxShape.circle,
                                    ),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: Text(
                                      g.name,
                                      style: const TextStyle(
                                          color: AppTheme.textPrimary, fontSize: 13),
                                    ),
                                  ),
                                  if (g.channelName != null)
                                    Text(
                                      g.channelName!,
                                      style: const TextStyle(
                                          color: AppTheme.textSecondary, fontSize: 12),
                                    ),
                                ],
                              ),
                            ),
                          ),
                        )),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  String _statusLabel(BotStatus status, String? error) {
    return switch (status) {
      BotStatus.disconnected => 'Not connected. Enter token in Settings and press Start.',
      BotStatus.connecting => 'Connecting to Discord…',
      BotStatus.connected => 'Connected',
      BotStatus.error => error ?? 'Error',
    };
  }
}

class _StatusDot extends StatelessWidget {
  final BotStatus status;
  const _StatusDot({required this.status});

  @override
  Widget build(BuildContext context) {
    final color = switch (status) {
      BotStatus.connected => AppTheme.statusActive,
      BotStatus.connecting => AppTheme.statusWarning,
      BotStatus.error => AppTheme.statusError,
      BotStatus.disconnected => AppTheme.textSecondary,
    };
    return Container(
      width: 8,
      height: 8,
      decoration: BoxDecoration(color: color, shape: BoxShape.circle),
    );
  }
}

class _PingChip extends StatelessWidget {
  final String label;
  final int value;
  const _PingChip({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: AppTheme.surfaceVariant,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        '$label ${value}ms',
        style: const TextStyle(color: AppTheme.textSecondary, fontSize: 11),
      ),
    );
  }
}
