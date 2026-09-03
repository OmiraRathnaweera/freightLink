import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';

/// The persistent top bar from the mockups: a leading avatar/back button, a
/// bold title (with an optional subtitle line), and trailing actions (the
/// notification bell on list screens; a delete/overflow icon on detail
/// screens). Used as a [Scaffold.appBar] everywhere.
class AppTopBar extends StatelessWidget implements PreferredSizeWidget {
  const AppTopBar({
    super.key,
    required this.title,
    this.subtitle,
    this.leading,
    this.actions = const [],
    this.showBackButton = false,
  });

  final String title;
  final String? subtitle;
  final Widget? leading;
  final List<Widget> actions;
  final bool showBackButton;

  @override
  Widget build(BuildContext context) {
    return AppBar(
      leading: leading ?? (showBackButton ? const _BackButton() : null),
      leadingWidth: leading != null ? 64 : null,
      titleSpacing: leading == null && !showBackButton ? 20 : 0,
      title: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
          if (subtitle != null)
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Text(
                subtitle!,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w400,
                  color: AppColors.inkMuted,
                ),
              ),
            ),
        ],
      ),
      actions: actions,
    );
  }

  @override
  Size get preferredSize =>
      Size.fromHeight(subtitle == null ? kToolbarHeight : kToolbarHeight + 8);
}

class _BackButton extends StatelessWidget {
  const _BackButton();

  @override
  Widget build(BuildContext context) {
    return IconButton(
      icon: const Icon(Icons.arrow_back_rounded),
      onPressed: () => Navigator.of(context).maybePop(),
    );
  }
}

/// The circular avatar shown as the leading widget on top-level tab screens.
class AppAvatar extends StatelessWidget {
  const AppAvatar({super.key, this.initials = 'FL'});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: CircleAvatar(
        radius: 16,
        backgroundColor: AppColors.statusNeutralBg,
        child: Text(
          initials,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: AppColors.inkMuted,
          ),
        ),
      ),
    );
  }
}

/// The notification bell trailing action shown on top-level tab screens.
class NotificationBellButton extends StatelessWidget {
  const NotificationBellButton({super.key, this.onPressed});

  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      icon: const Icon(Icons.notifications_none_rounded),
      onPressed:
          onPressed ??
          () {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('No new notifications')),
            );
          },
    );
  }
}
