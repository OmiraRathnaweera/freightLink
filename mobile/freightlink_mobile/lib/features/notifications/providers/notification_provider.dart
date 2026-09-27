import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/in_app_notification.dart';

/// Manages local push notifications, in-app banner presentation, and unread counts
/// across all 3 Flutter roles (Shipper, Driver, Agency).
class NotificationProvider extends ChangeNotifier {
  NotificationProvider({this.bannerDisplayDuration = const Duration(seconds: 4)});

  final Duration bannerDisplayDuration;

  final List<InAppNotification> _notifications = [];
  InAppNotification? _currentPushBanner;
  Timer? _bannerDismissTimer;

  List<InAppNotification> get notifications => List.unmodifiable(_notifications);

  InAppNotification? get currentPushBanner => _currentPushBanner;

  int get unreadCount => _notifications.where((n) => !n.isRead).length;

  bool get hasUnread => unreadCount > 0;

  /// Pushes a new status change notification and displays a temporary top banner.
  void pushNotification({
    required String title,
    required String message,
    required NotificationCategory category,
    String? referenceId,
    bool showPushBanner = true,
  }) {
    final notification = InAppNotification(
      id: 'notif_${DateTime.now().millisecondsSinceEpoch}_${_notifications.length}',
      title: title,
      message: message,
      category: category,
      timestamp: DateTime.now(),
      referenceId: referenceId,
    );

    // Add to front of history
    _notifications.insert(0, notification);

    if (showPushBanner) {
      _currentPushBanner = notification;
      _bannerDismissTimer?.cancel();
      _bannerDismissTimer = Timer(bannerDisplayDuration, dismissPushBanner);
    }

    notifyListeners();
  }

  /// Dismisses the currently visible top push banner.
  void dismissPushBanner() {
    if (_currentPushBanner != null) {
      _bannerDismissTimer?.cancel();
      _bannerDismissTimer = null;
      _currentPushBanner = null;
      notifyListeners();
    }
  }

  /// Marks a specific notification as read.
  void markAsRead(String id) {
    final index = _notifications.indexWhere((n) => n.id == id);
    if (index != -1 && !_notifications[index].isRead) {
      _notifications[index] = _notifications[index].copyWith(isRead: true);
      notifyListeners();
    }
  }

  /// Marks all stored notifications as read.
  void markAllAsRead() {
    bool changed = false;
    for (int i = 0; i < _notifications.length; i++) {
      if (!_notifications[i].isRead) {
        _notifications[i] = _notifications[i].copyWith(isRead: true);
        changed = true;
      }
    }
    if (changed) {
      notifyListeners();
    }
  }

  /// Clears the notification history.
  void clearAll() {
    _notifications.clear();
    _currentPushBanner = null;
    _bannerDismissTimer?.cancel();
    notifyListeners();
  }

  @override
  void dispose() {
    _bannerDismissTimer?.cancel();
    super.dispose();
  }
}
