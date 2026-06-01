import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/guild_config.dart';
import '../models/guild_info.dart';
import '../providers/bot_provider.dart';
import '../providers/settings_provider.dart';
import '../theme/app_theme.dart';

class GuildsScreen extends StatelessWidget {
  const GuildsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final bot = context.watch<BotProvider>();

    if (bot.guilds.isEmpty) {
      return const Scaffold(
        backgroundColor: AppTheme.background,
        body: Center(
          child: Text(
            'No guilds. Start the bot to see connected servers.',
            style: TextStyle(color: AppTheme.textSecondary, fontSize: 13),
            textAlign: TextAlign.center,
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppTheme.background,
      body: ListView.separated(
        padding: const EdgeInsets.all(20),
        itemCount: bot.guilds.length,
        separatorBuilder: (context, index) => const SizedBox(height: 10),
        itemBuilder: (context, i) => _GuildCard(guild: bot.guilds[i]),
      ),
    );
  }
}

class _GuildCard extends StatefulWidget {
  final GuildInfo guild;
  const _GuildCard({required this.guild});

  @override
  State<_GuildCard> createState() => _GuildCardState();
}

class _GuildCardState extends State<_GuildCard> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final settings = context.read<SettingsProvider>();
    final bot = context.read<BotProvider>();
    final cfg = settings.config.guildConfig(widget.guild.id);

    return Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Header row
          InkWell(
            borderRadius: BorderRadius.circular(8),
            onTap: () => setState(() => _expanded = !_expanded),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              child: Row(
                children: [
                  Container(
                    width: 8,
                    height: 8,
                    decoration: BoxDecoration(
                      color: widget.guild.channelId != null
                          ? AppTheme.statusActive
                          : AppTheme.textSecondary,
                      shape: BoxShape.circle,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(widget.guild.name,
                        style:
                            const TextStyle(color: AppTheme.textPrimary, fontSize: 13)),
                  ),
                  if (widget.guild.channelName != null)
                    Text(widget.guild.channelName!,
                        style: const TextStyle(
                            color: AppTheme.textSecondary, fontSize: 12)),
                  const SizedBox(width: 8),
                  Icon(
                    _expanded ? Icons.expand_less : Icons.expand_more,
                    color: AppTheme.textSecondary,
                    size: 16,
                  ),
                ],
              ),
            ),
          ),

          // Expanded config
          if (_expanded) ...[
            const Divider(color: AppTheme.surfaceVariant, height: 1),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Command channels
                  const _FieldLabel('COMMAND CHANNELS'),
                  const SizedBox(height: 6),
                  _CommandChannelsEditor(cfg: cfg, onChanged: () {
                    bot.updateGuildConfig(cfg);
                  }),
                  const SizedBox(height: 14),

                  // Auto-join channel
                  const _FieldLabel('AUTO-JOIN CHANNEL ID'),
                  const SizedBox(height: 4),
                  _InlineTextField(
                    value: cfg.autoJoinAudioChannelId ?? '',
                    hint: 'Channel ID',
                    onChanged: (v) {
                      cfg.autoJoinAudioChannelId = v.isEmpty ? null : v;
                      bot.updateGuildConfig(cfg);
                    },
                  ),
                  const SizedBox(height: 14),

                  // Follow user
                  const _FieldLabel('FOLLOW USER ID'),
                  const SizedBox(height: 4),
                  _InlineTextField(
                    value: cfg.followedUserId ?? '',
                    hint: 'User ID',
                    onChanged: (v) {
                      cfg.followedUserId = v.isEmpty ? null : v;
                      bot.updateGuildConfig(cfg);
                    },
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _CommandChannelsEditor extends StatefulWidget {
  final GuildConfig cfg;
  final VoidCallback onChanged;

  const _CommandChannelsEditor({required this.cfg, required this.onChanged});

  @override
  State<_CommandChannelsEditor> createState() => _CommandChannelsEditorState();
}

class _CommandChannelsEditorState extends State<_CommandChannelsEditor> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (widget.cfg.commandChannelIds.isNotEmpty)
          Wrap(
            spacing: 6,
            runSpacing: 4,
            children: widget.cfg.commandChannelIds.map((id) {
              return Chip(
                label: Text(id,
                    style: const TextStyle(
                        color: AppTheme.textPrimary, fontSize: 12)),
                backgroundColor: AppTheme.surfaceVariant,
                deleteIcon:
                    const Icon(Icons.close, size: 14, color: AppTheme.textSecondary),
                onDeleted: () {
                  setState(() => widget.cfg.commandChannelIds.remove(id));
                  widget.onChanged();
                },
                side: BorderSide.none,
              );
            }).toList(),
          ),
        const SizedBox(height: 6),
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _controller,
                style: const TextStyle(color: AppTheme.textPrimary, fontSize: 13),
                decoration: const InputDecoration(hintText: 'Add channel ID'),
              ),
            ),
            const SizedBox(width: 8),
            FilledButton(
              onPressed: () {
                final v = _controller.text.trim();
                if (v.isEmpty) return;
                setState(() => widget.cfg.commandChannelIds.add(v));
                _controller.clear();
                widget.onChanged();
              },
              style: FilledButton.styleFrom(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                minimumSize: Size.zero,
                tapTargetSize: MaterialTapTargetSize.shrinkWrap,
              ),
              child: const Text('ADD', style: TextStyle(fontSize: 12)),
            ),
          ],
        ),
        if (widget.cfg.commandChannelIds.isEmpty)
          const Padding(
            padding: EdgeInsets.only(top: 4),
            child: Text('All channels (no restriction)',
                style: TextStyle(color: AppTheme.textSecondary, fontSize: 12)),
          ),
      ],
    );
  }
}

class _InlineTextField extends StatefulWidget {
  final String value;
  final String hint;
  final ValueChanged<String> onChanged;

  const _InlineTextField({
    required this.value,
    required this.hint,
    required this.onChanged,
  });

  @override
  State<_InlineTextField> createState() => _InlineTextFieldState();
}

class _InlineTextFieldState extends State<_InlineTextField> {
  late TextEditingController _ctrl;

  @override
  void initState() {
    super.initState();
    _ctrl = TextEditingController(text: widget.value);
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: _ctrl,
      style: const TextStyle(color: AppTheme.textPrimary, fontSize: 13),
      decoration: InputDecoration(hintText: widget.hint),
      onChanged: widget.onChanged,
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
