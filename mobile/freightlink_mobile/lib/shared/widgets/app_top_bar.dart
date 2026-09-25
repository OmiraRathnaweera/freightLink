import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/theme/app_colors.dart';
import '../../features/auth/providers/auth_provider.dart';

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
      actions: [
        ...actions,
        const AppAvatar(),
      ],
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
/// Tapping it opens a small menu with the signed-in user's name/email and a
/// "Log out" action — there's no dedicated account screen yet, so this is
/// the one place logout is reachable from.
class AppAvatar extends StatelessWidget {
  const AppAvatar({super.key});

  static String _initialsOf(String fullName) {
    final parts = fullName
        .trim()
        .split(RegExp(r'\s+'))
        .where((p) => p.isNotEmpty);
    final letters = parts.take(2).map((p) => p[0].toUpperCase());
    final initials = letters.join();
    return initials.isEmpty ? '?' : initials;
  }

  Future<void> _confirmLogout(BuildContext context, AuthProvider auth) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Log out?'),
        content: const Text("You'll need to sign in again to continue."),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Log out'),
          ),
        ],
      ),
    );
    if (confirmed ?? false) {
      await auth.logout();
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final user = auth.user;
    final initials = user == null ? '?' : _initialsOf(user.fullName);

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: PopupMenuButton<String>(
        tooltip: 'Account',
        offset: const Offset(0, 44),
        itemBuilder: (menuContext) => [
          if (user != null)
            PopupMenuItem<String>(
              enabled: false,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    user.fullName,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  Text(
                    user.email,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.inkMuted,
                    ),
                  ),
                ],
              ),
            ),
          if (user != null) const PopupMenuDivider(),
          const PopupMenuItem<String>(
            value: _logoutMenuValue,
            child: Row(
              children: [
                Icon(
                  Icons.logout_rounded,
                  size: 18,
                  color: AppColors.statusErrorFg,
                ),
                SizedBox(width: 12),
                Text(
                  'Log out',
                  style: TextStyle(color: AppColors.statusErrorFg),
                ),
              ],
            ),
          ),
        ],
        onSelected: (_) => _confirmLogout(context, auth),
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
      ),
    );
  }
}

const _logoutMenuValue = 'logout';

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
