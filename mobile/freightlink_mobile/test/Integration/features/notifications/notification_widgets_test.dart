import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/notifications/models/in_app_notification.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';
import 'package:freightlink_mobile/features/notifications/widgets/in_app_push_banner.dart';
import 'package:freightlink_mobile/shared/widgets/app_top_bar.dart';
import 'package:provider/provider.dart';

Widget _wrapWithProvider(
  Widget child, {
  NotificationProvider? provider,
  bool includePushBanner = false,
}) {
  final notifProvider = provider ?? NotificationProvider();
  return ChangeNotifierProvider<NotificationProvider>.value(
    value: notifProvider,
    child: MaterialApp(
      theme: AppTheme.light,
      home: includePushBanner
          ? Scaffold(
              body: Stack(
                children: [
                  child,
                  const InAppPushBanner(),
                ],
              ),
            )
          : child,
    ),
  );
}

void main() {
  group('InAppPushBanner', () {
    testWidgets('renders push banner when notification is active and dismisses on close button', (tester) async {
      final provider = NotificationProvider();

      await tester.pumpWidget(_wrapWithProvider(
        const Center(child: Text('Content')),
        provider: provider,
        includePushBanner: true,
      ));

      // Initially no banner
      expect(find.byKey(const Key('in_app_push_banner')), findsNothing);

      // Trigger status-change push notification
      provider.pushNotification(
        title: 'Load Matched!',
        message: 'Your load LD-TEST has been approved.',
        category: NotificationCategory.loadMatched,
      );

      await tester.pumpAndSettle();

      // Banner is now visible
      expect(find.byKey(const Key('in_app_push_banner')), findsOneWidget);
      expect(find.text('Load Matched!'), findsOneWidget);
      expect(find.text('Your load LD-TEST has been approved.'), findsOneWidget);

      // Tap close button
      await tester.tap(find.byKey(const Key('push_banner_dismiss_button')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('in_app_push_banner')), findsNothing);
    });
  });

  group('NotificationBellButton and NotificationCenterSheet', () {
    testWidgets('NotificationBellButton shows unread badge when notifications exist', (tester) async {
      final provider = NotificationProvider();

      await tester.pumpWidget(_wrapWithProvider(
        const Scaffold(
          appBar: AppTopBar(
            title: 'Test Bar',
            actions: [NotificationBellButton()],
          ),
        ),
        provider: provider,
      ));

      // No badge initially
      expect(find.byKey(const Key('notification_bell_badge')), findsNothing);

      // Add 2 notifications
      provider.pushNotification(
        title: 'New Trip Assigned!',
        message: 'Trip #TRP-99 assigned.',
        category: NotificationCategory.tripAssigned,
        showPushBanner: false,
      );
      provider.pushNotification(
        title: 'Proposal Update',
        message: 'Proposal #PR-01 updated.',
        category: NotificationCategory.proposalUpdate,
        showPushBanner: false,
      );

      await tester.pumpAndSettle();

      // Badge now displays '2'
      expect(find.byKey(const Key('notification_bell_badge')), findsOneWidget);
      expect(find.text('2'), findsOneWidget);
    });

    testWidgets('tapping NotificationBellButton opens NotificationCenterSheet with items', (tester) async {
      final provider = NotificationProvider();
      provider.pushNotification(
        title: 'Load Matched!',
        message: 'Your load LD-8812 was matched.',
        category: NotificationCategory.loadMatched,
        showPushBanner: false,
      );

      await tester.pumpWidget(_wrapWithProvider(
        const Scaffold(
          appBar: AppTopBar(
            title: 'Test Bar',
            actions: [NotificationBellButton()],
          ),
        ),
        provider: provider,
      ));

      await tester.pumpAndSettle();

      // Tap bell
      await tester.tap(find.byKey(const Key('notification_bell_button')));
      await tester.pumpAndSettle();

      // Notification Center is displayed
      expect(find.byKey(const Key('notification_center_sheet')), findsOneWidget);
      expect(find.text('Notifications'), findsOneWidget);
      expect(find.text('Load Matched!'), findsWidgets);
      expect(find.text('Your load LD-8812 was matched.'), findsOneWidget);
      expect(find.byKey(const Key('mark_all_read_button')), findsOneWidget);

      // Tap "Mark all read"
      await tester.tap(find.byKey(const Key('mark_all_read_button')));
      await tester.pumpAndSettle();

      expect(provider.unreadCount, 0);
    });

    testWidgets('NotificationCenterSheet displays empty state when no notifications', (tester) async {
      final provider = NotificationProvider();

      await tester.pumpWidget(_wrapWithProvider(
        const Scaffold(
          appBar: AppTopBar(
            title: 'Test Bar',
            actions: [NotificationBellButton()],
          ),
        ),
        provider: provider,
      ));

      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('notification_bell_button')));
      await tester.pumpAndSettle();

      expect(find.text('No Notifications Yet'), findsOneWidget);
      expect(
        find.text('You will receive in-app push alerts when your loads are matched, trips assigned, or proposals updated.'),
        findsOneWidget,
      );
    });
  });
}
