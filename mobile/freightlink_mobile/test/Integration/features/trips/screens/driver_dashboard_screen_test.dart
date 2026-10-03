import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:freightlink_mobile/core/models/paged_result.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';
import 'package:freightlink_mobile/features/trips/data/trips_repository.dart';
import 'package:freightlink_mobile/features/trips/models/trip_models.dart';
import 'package:freightlink_mobile/features/trips/screens/driver_dashboard_screen.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../../helpers/fakes.dart';

class MockAuthProvider extends Mock implements AuthProvider {}

void main() {
  late MockTripsRepository mockTripsRepo;
  late MockAuthProvider mockAuth;

  const sampleUser = AuthUser(
    userId: 'user-1',
    email: 'driver1@freightlink.lk',
    fullName: 'Sunil Perera',
    role: 'Driver',
    isActive: true,
  );

  PagedResult<TripResponse> paged(int totalItems) => PagedResult<TripResponse>(
        items: const [],
        page: 1,
        pageSize: 1,
        totalItems: totalItems,
        totalPages: totalItems == 0 ? 0 : 1,
      );

  setUp(() {
    mockTripsRepo = MockTripsRepository();
    mockAuth = MockAuthProvider();

    when(() => mockAuth.user).thenReturn(sampleUser);
    when(() => mockAuth.status).thenReturn(AuthStatus.authenticated);
  });

  void stubCounts({
    int total = 0,
    int assigned = 0,
    int pickedUp = 0,
    int inTransit = 0,
    int delivered = 0,
    int cancelled = 0,
  }) {
    when(() => mockTripsRepo.getTrips(pageSize: 1)).thenAnswer((_) async => paged(total));
    when(() => mockTripsRepo.getTrips(pageSize: 1, status: 'Assigned')).thenAnswer((_) async => paged(assigned));
    when(() => mockTripsRepo.getTrips(pageSize: 1, status: 'PickedUp')).thenAnswer((_) async => paged(pickedUp));
    when(() => mockTripsRepo.getTrips(pageSize: 1, status: 'InTransit')).thenAnswer((_) async => paged(inTransit));
    when(() => mockTripsRepo.getTrips(pageSize: 1, status: 'Delivered')).thenAnswer((_) async => paged(delivered));
    when(() => mockTripsRepo.getTrips(pageSize: 1, status: 'Cancelled')).thenAnswer((_) async => paged(cancelled));
  }

  Future<void> pumpDashboard(WidgetTester tester) async {
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<TripsRepository>.value(value: mockTripsRepo),
          ChangeNotifierProvider<AuthProvider>.value(value: mockAuth),
          ChangeNotifierProvider<NotificationProvider>(create: (_) => NotificationProvider()),
        ],
        child: MaterialApp(
          theme: AppTheme.light,
          home: const DriverDashboardScreen(),
        ),
      ),
    );
  }

  /// Pumps the dashboard inside a real GoRouter and records the trip-history locations it pushes.
  Future<List<String>> pumpDashboardWithRouter(WidgetTester tester) async {
    // Tall enough that the whole 2x2 KPI grid and quick actions are on screen to tap.
    tester.view.physicalSize = const Size(800, 1800);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final visited = <String>[];
    final router = GoRouter(
      routes: [
        GoRoute(path: '/', builder: (_, _) => const DriverDashboardScreen()),
        GoRoute(
          path: '/dashboard/trip-history',
          builder: (_, state) {
            visited.add(state.uri.toString());
            return const Scaffold(body: Text('history screen'));
          },
        ),
        GoRoute(
          path: '/loads',
          builder: (_, _) {
            visited.add('/loads');
            return const Scaffold(body: Text('my trip screen'));
          },
        ),
      ],
    );
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<TripsRepository>.value(value: mockTripsRepo),
          ChangeNotifierProvider<AuthProvider>.value(value: mockAuth),
          ChangeNotifierProvider<NotificationProvider>(create: (_) => NotificationProvider()),
        ],
        child: MaterialApp.router(theme: AppTheme.light, routerConfig: router),
      ),
    );
    await tester.pumpAndSettle();
    return visited;
  }

  group('DriverDashboardScreen navigation', () {
    for (final (label, expected) in [
      ('Rides Completed', '/dashboard/trip-history?filter=completed'),
      ('Cancelled', '/dashboard/trip-history?filter=cancelled'),
      ('Total Trips', '/dashboard/trip-history'),
    ]) {
      testWidgets('"$label" card opens trip history ($expected)', (tester) async {
        stubCounts(total: 4, delivered: 3, inTransit: 1, cancelled: 0);
        final visited = await pumpDashboardWithRouter(tester);

        await tester.tap(find.text(label));
        await tester.pumpAndSettle();

        expect(find.text('history screen'), findsOneWidget);
        expect(visited, [expected]);
      });
    }

    testWidgets('"Active Trip" card still opens My Trip, the live trip screen', (tester) async {
      stubCounts(total: 4, delivered: 3, inTransit: 1);
      final visited = await pumpDashboardWithRouter(tester);

      await tester.tap(find.text('Active Trip'));
      await tester.pumpAndSettle();

      expect(find.text('my trip screen'), findsOneWidget);
      expect(visited, ['/loads']);
    });

    testWidgets('Trip History quick action opens the unfiltered history', (tester) async {
      stubCounts(total: 4, delivered: 3, inTransit: 1);
      final visited = await pumpDashboardWithRouter(tester);

      await tester.tap(find.text('Trip History'));
      await tester.pumpAndSettle();

      expect(visited, ['/dashboard/trip-history']);
    });
  });

  group('DriverDashboardScreen', () {
    testWidgets('renders KPIs and quick actions when loaded', (tester) async {
      stubCounts(total: 12, delivered: 9, cancelled: 1, inTransit: 1);

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      // Hero banner
      expect(find.text('DRIVER PORTAL'), findsOneWidget);
      expect(find.text('Sunil Perera'), findsOneWidget);
      expect(find.text('driver1@freightlink.lk'), findsOneWidget);

      // Section headers
      expect(find.text('TRIP PERFORMANCE'), findsOneWidget);
      expect(find.text('QUICK ACTIONS'), findsOneWidget);

      // KPI values
      expect(find.text('9'), findsOneWidget); // Rides Completed
      expect(find.text('Rides Completed'), findsOneWidget);
      expect(find.text('1'), findsNWidgets(2)); // Active Trip + Cancelled both 1
      expect(find.text('Active Trip'), findsOneWidget);
      expect(find.text('Cancelled'), findsOneWidget);
      expect(find.text('12'), findsOneWidget); // Total Trips
      expect(find.text('Total Trips'), findsOneWidget);

      // Quick action
      expect(find.text('My Trip'), findsOneWidget);
      expect(find.text('ACTIVE'), findsOneWidget); // StatusPill uppercases the badge label
    });

    testWidgets('renders ErrorState with retry button when error occurs', (tester) async {
      when(() => mockTripsRepo.getTrips(pageSize: 1)).thenThrow(Exception('Network timeout'));

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      expect(find.text('Unable to load dashboard'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);

      stubCounts(total: 3, delivered: 3);

      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(find.text('Rides Completed'), findsOneWidget);
      expect(find.text('3'), findsNWidgets(2)); // Rides Completed + Total Trips both 3
    });

    testWidgets('shows no "Active" badge and greys out Cancelled when there are none', (tester) async {
      stubCounts(total: 5, delivered: 5);

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      expect(find.text('ACTIVE'), findsNothing);
      expect(find.text('None right now'), findsOneWidget);
      expect(find.text('None cancelled'), findsOneWidget);
    });
  });
}
