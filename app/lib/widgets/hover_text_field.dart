import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

class HoverTextField extends StatefulWidget {
  final TextEditingController controller;
  final String label;
  final bool obscureText;
  final ValueChanged<String>? onChanged;

  const HoverTextField({
    super.key,
    required this.controller,
    required this.label,
    this.obscureText = false,
    this.onChanged,
  });

  @override
  State<HoverTextField> createState() => _HoverTextFieldState();
}

class _HoverTextFieldState extends State<HoverTextField> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 100),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(6),
          border: Border.all(
            color: _hovered ? AppTheme.surfaceVariant : Colors.transparent,
            width: 1,
          ),
        ),
        child: TextField(
          controller: widget.controller,
          obscureText: widget.obscureText,
          onChanged: widget.onChanged,
          style: const TextStyle(color: AppTheme.textPrimary, fontSize: 13),
          decoration: InputDecoration(
            labelText: widget.label,
          ),
        ),
      ),
    );
  }
}
