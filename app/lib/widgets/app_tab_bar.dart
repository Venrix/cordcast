import 'package:flutter/material.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../theme/app_theme.dart';

class AppTabBar extends StatefulWidget {
  final int selectedIndex;
  final ValueChanged<int> onTabChanged;

  const AppTabBar({
    super.key,
    required this.selectedIndex,
    required this.onTabChanged,
  });

  @override
  State<AppTabBar> createState() => _AppTabBarState();
}

class _AppTabBarState extends State<AppTabBar> {
  String _version = 'dev';

  @override
  void initState() {
    super.initState();
    PackageInfo.fromPlatform().then((info) {
      if (mounted && info.version != '0.0.0') {
        setState(() => _version = 'v${info.version}');
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      color: AppTheme.surface,
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: Row(
        children: [
          const Text(
            'cordcast',
            style: TextStyle(
              color: AppTheme.accent,
              fontSize: 15,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.3,
            ),
          ),
          const SizedBox(width: 20),
          _Tab(label: 'HOME', index: 0, selected: widget.selectedIndex == 0, onTap: widget.onTabChanged),
          _Tab(label: 'SETTINGS', index: 1, selected: widget.selectedIndex == 1, onTap: widget.onTabChanged),
          _Tab(label: 'GUILDS', index: 2, selected: widget.selectedIndex == 2, onTap: widget.onTabChanged),
          const Spacer(),
          Text(
            _version,
            style: const TextStyle(
              color: AppTheme.textSecondary,
              fontSize: 11,
            ),
          ),
        ],
      ),
    );
  }
}

class _Tab extends StatefulWidget {
  final String label;
  final int index;
  final bool selected;
  final ValueChanged<int> onTap;

  const _Tab({
    required this.label,
    required this.index,
    required this.selected,
    required this.onTap,
  });

  @override
  State<_Tab> createState() => _TabState();
}

class _TabState extends State<_Tab> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    final active = widget.selected || _hovered;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: GestureDetector(
        onTap: () => widget.onTap(widget.index),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 100),
          margin: const EdgeInsets.symmetric(vertical: 8, horizontal: 2),
          padding: const EdgeInsets.symmetric(horizontal: 10),
          decoration: BoxDecoration(
            color: widget.selected
                ? AppTheme.accent.withAlpha(30)
                : _hovered
                    ? AppTheme.surfaceVariant
                    : Colors.transparent,
            borderRadius: BorderRadius.circular(6),
          ),
          alignment: Alignment.center,
          child: Text(
            widget.label,
            style: TextStyle(
              color: active ? AppTheme.accent : AppTheme.textSecondary,
              fontSize: 12,
              fontWeight: widget.selected ? FontWeight.w600 : FontWeight.w400,
              letterSpacing: 0.5,
            ),
          ),
        ),
      ),
    );
  }
}
