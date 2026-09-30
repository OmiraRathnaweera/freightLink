import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/notifications/models/in_app_notification.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';

void main() {
  group('NotificationProvider', () {
    test('initial state has empty notifications and zero unread count', () {
      final provider = NotificationProvider();
      expect(provider.notifications, isEmpty);
      expect(provider.unreadCount, 0);
      expect(provider.hasUnread, isFalse);
      expect(provider.currentPushBanner, isNull);
    });

    test('pushNotification adds notification, increments unread, and sets push banner', () {
      final provider = NotificationProvider();

      provider.pushNotification(
        title: 'Load Matched!',
        message: 'Your load LD-TEST-01 has been matched.',
        category: NotificationCategory.loadMatched,
        referenceId: 'load_123',
      );

      expect(provider.notifications.length, 1);
      expect(provider.unreadCount, 1);
      expect(provider.hasUnread, isTrue);
      expect(provider.currentPushBanner, isNotNull);
      expect(provider.currentPushBanner!.title, 'Load Matched!');
      expect(provider.currentPushBanner!.category, NotificationCategory.loadMatched);
      expect(provider.currentPushBanner!.referenceId, 'load_123');
    });

    test('dismissPushBanner clears currentPushBanner without clearing notifications list', () {
      final provider = NotificationProvider();

      provider.pushNotification(
        title: 'New Trip Assigned!',
        message: 'Trip #TRP-001 assigned.',
        category: NotificationCategory.tripAssigned,
      );

      expect(provider.currentPushBanner, isNotNull);
      provider.dismissPushBanner();
      expect(provider.currentPushBanner, isNull);
      expect(provider.notifications.length, 1);
      expect(provider.unreadCount, 1);
    });

    test('markAsRead marks specific notification as read and decrements unreadCount', () {
      final provider = NotificationProvider();

      provider.pushNotification(
        title: 'Note 1',
        message: 'Message 1',
        category: NotificationCategory.general,
      );
      provider.pushNotification(
        title: 'Note 2',
        message: 'Message 2',
        category: NotificationCategory.proposalUpdate,
      );

      expect(provider.unreadCount, 2);

      final firstId = provider.notifications.first.id;
      provider.markAsRead(firstId);

      expect(provider.unreadCount, 1);
      expect(provider.notifications.first.isRead, isTrue);
      expect(provider.notifications.last.isRead, isFalse);
    });

    test('markAllAsRead marks all notifications as read', () {
      final provider = NotificationProvider();

      provider.pushNotification(
        title: 'Note 1',
        message: 'Message 1',
        category: NotificationCategory.loadMatched,
      );
      provider.pushNotification(
        title: 'Note 2',
        message: 'Message 2',
        category: NotificationCategory.tripAssigned,
      );

      expect(provider.unreadCount, 2);
      provider.markAllAsRead();
      expect(provider.unreadCount, 0);
      expect(provider.hasUnread, isFalse);
    });

    test('clearAll removes all notifications and dismisses banner', () {
      final provider = NotificationProvider();

      provider.pushNotification(
        title: 'Note 1',
        message: 'Message 1',
        category: NotificationCategory.loadMatched,
      );

      expect(provider.notifications, isNotEmpty);
      provider.clearAll();
      expect(provider.notifications, isEmpty);
      expect(provider.currentPushBanner, isNull);
      expect(provider.unreadCount, 0);
    });

  });
}
