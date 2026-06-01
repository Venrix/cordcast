import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

class FullWidthDropdown<T> extends StatefulWidget {
  final T value;
  final List<T> items;
  final String Function(T) labelOf;
  final ValueChanged<T?> onChanged;

  const FullWidthDropdown({
    super.key,
    required this.value,
    required this.items,
    required this.labelOf,
    required this.onChanged,
  });

  @override
  State<FullWidthDropdown<T>> createState() => _FullWidthDropdownState<T>();
}

class _FullWidthDropdownState<T> extends State<FullWidthDropdown<T>> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    // Deduplicate items (a device name can be enumerated more than once) and
    // guard the selected value: DropdownButton asserts exactly one item matches
    // value, so fall back to null when the value isn't (yet) in the list.
    final uniqueItems = <T>[];
    for (final item in widget.items) {
      if (!uniqueItems.contains(item)) uniqueItems.add(item);
    }
    final safeValue = uniqueItems.contains(widget.value) ? widget.value : null;

    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 100),
        decoration: BoxDecoration(
          color: AppTheme.background,
          borderRadius: BorderRadius.circular(6),
          border: Border.all(
            color: _hovered ? AppTheme.surfaceVariant : Colors.transparent,
            width: 1,
          ),
        ),
        padding: const EdgeInsets.symmetric(horizontal: 12),
        child: DropdownButtonHideUnderline(
          child: DropdownButton<T>(
            value: safeValue,
            isExpanded: true,
            dropdownColor: AppTheme.background,
            borderRadius: BorderRadius.circular(8),
            mouseCursor: SystemMouseCursors.click,
            style: const TextStyle(color: AppTheme.textPrimary, fontSize: 13),
            icon: const Icon(Icons.keyboard_arrow_down, color: AppTheme.textSecondary),
            items: uniqueItems
                .map((v) => DropdownMenuItem<T>(
                      value: v,
                      child: MouseRegion(
                        cursor: SystemMouseCursors.click,
                        child: SizedBox(
                          width: double.infinity,
                          child: Text(widget.labelOf(v)),
                        ),
                      ),
                    ))
                .toList(),
            onChanged: widget.onChanged,
          ),
        ),
      ),
    );
  }
}
