import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/models/paged_result.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/trips/data/trips_repository.dart';
import 'package:freightlink_mobile/features/trips/models/trip_models.dart';
import 'package:freightlink_mobile/features/trips/screens/driver_trip_history_screen.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../../helpers/fakes.dart';

TripResponse trip(String id, String status, {String? ref, String createdAt = '2026-09-01T10:00:00Z'}) =>
    TripResponse.fromJson({
      'tripId': id,
      'status': status,
      'referenceCode': ref ?? 'TRP-$id',
      'pickupAddress': 'Colombo',
      'dropoffAddress': 'Kandy',
      'cargoDescription': 'Tea chests',
      'vehicleRegistrationNo': 'WP-CAD-1234',
      'createdAt': createdAt,
      'updatedAt': createdAt,
    });

PagedResult<TripResponse> pageOf(List<TripResponse> items) => PagedResult<TripResponse>(
      items: items,
      page: 1,
      pageSize: 100,
      totalItems: items.length,
      totalPages: 1,
    );

void main() {
  late MockTripsRepository repo;

  setUp(() {
    repo = MockTripsRepository();
  });

  void stubGetTrips({
    List<TripResponse> all = const [],
    List<TripResponse> assigned = const [],
    List<TripResponse> pickedUp = const [],
    List<TripResponse> inTransit = const [],
    List<TripResponse> delivered = const [],
    List<TripResponse> cancelled = const [],
  }) {
    Future<PagedResult<TripResponse>> answer(Invocation i) async {
      final status = i.namedArguments[#status] as String?;
      return pageOf(switch (status) {
        null => all,
        'Assigned' => assigned,
        'PickedUp' => pickedUp,
        'InTransit' => inTransit,
        'Delivered' => delivered,
        'Cancelled' => cancelled,
        _ => const [],
      });
    }

    when(() => repo.getTrips(
          status: any(named: 'status'),
          page: any(named: 'page'),
          pageSize: any(named: 'pageSize'),
          sortBy: any(named: 'sortBy'),
          sortDir: any(named: 'sortDir'),
        )).thenAnswer(answer);
  }

  Future<void> pumpScreen(WidgetTester tester, {DriverTripFilter filter = DriverTripFilter.all}) async {
    await tester.pumpWidget(
      Provider<TripsRepository>.value(
        value: repo,
        child: MaterialApp(
          theme: AppTheme.light,
          home: DriverTripHistoryScreen(initialFilter: filter),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  group('DriverTripFilter.fromQuery', () {
    test('parses known values and falls back to all', () {
      expect(DriverTripFilter.fromQuery('completed'), DriverTripFilter.completed);
      expect(DriverTripFilter.fromQuery('cancelled'), DriverTripFilter.cancelled);
      expect(DriverTripFilter.fromQuery('active'), DriverTripFilter.active);
      expect(DriverTripFilter.fromQuery('nonsense'), DriverTripFilter.all);
      expect(DriverTripFilter.fromQuery(null), DriverTripFilter.all);
    });
  });

  group('DriverTripHistoryScreen', () {
    testWidgets('lists every trip, past and present, by default', (tester) async {
      stubGetTrips(all: [
        trip('1', 'Delivered', ref: 'TRP-DONE'),
        trip('2', 'InTransit', ref: 'TRP-LIVE'),
        trip('3', 'Cancelled', ref: 'TRP-OFF'),
      ]);

      await pumpScreen(tester);

      expect(find.text('Trip History'), findsOneWidget);
      expect(find.text('TRP-DONE'), findsOneWidget);
      expect(find.text('TRP-LIVE'), findsOneWidget);
      expect(find.text('TRP-OFF'), findsOneWidget);
    });

    testWidgets('opens on the requested filter and asks the API for that status only', (tester) async {
      stubGetTrips(delivered: [trip('1', 'Delivered', ref: 'TRP-DONE')], all: [trip('9', 'Cancelled', ref: 'TRP-OTHER')]);

      await pumpScreen(tester, filter: DriverTripFilter.completed);

      expect(find.text('TRP-DONE'), findsOneWidget);
      expect(find.text('TRP-OTHER'), findsNothing);
      verify(() => repo.getTrips(
            status: 'Delivered',
            page: any(named: 'page'),
            pageSize: any(named: 'pageSize'),
            sortBy: any(named: 'sortBy'),
            sortDir: any(named: 'sortDir'),
          )).called(1);
    });

    testWidgets('switching filter chips reloads with the matching status', (tester) async {
      stubGetTrips(
        all: [trip('1', 'Delivered', ref: 'TRP-DONE'), trip('3', 'Cancelled', ref: 'TRP-OFF')],
        cancelled: [trip('3', 'Cancelled', ref: 'TRP-OFF')],
      );

      await pumpScreen(tester);
      expect(find.text('TRP-DONE'), findsOneWidget);

      await tester.tap(find.byKey(const Key('trip_filter_cancelled')));
      await tester.pumpAndSettle();

      expect(find.text('TRP-OFF'), findsOneWidget);
      expect(find.text('TRP-DONE'), findsNothing);
    });

    testWidgets('Active merges Assigned, PickedUp and InTransit trips, newest first', (tester) async {
      stubGetTrips(
        assigned: [trip('1', 'Assigned', ref: 'TRP-A', createdAt: '2026-09-01T10:00:00Z')],
        inTransit: [trip('2', 'InTransit', ref: 'TRP-B', createdAt: '2026-09-03T10:00:00Z')],
        delivered: [trip('3', 'Delivered', ref: 'TRP-DONE')],
      );

      await pumpScreen(tester, filter: DriverTripFilter.active);

      expect(find.text('TRP-A'), findsOneWidget);
      expect(find.text('TRP-B'), findsOneWidget);
      expect(find.text('TRP-DONE'), findsNothing);
      expect(tester.getTopLeft(find.text('TRP-B')).dy, lessThan(tester.getTopLeft(find.text('TRP-A')).dy));
    });

    testWidgets('shows a filter-specific empty state', (tester) async {
      stubGetTrips();

      await pumpScreen(tester, filter: DriverTripFilter.completed);

      expect(find.text('No Trips Found'), findsOneWidget);
      expect(find.text("You haven't completed any trips yet."), findsOneWidget);
    });

    testWidgets('shows an error with retry that recovers', (tester) async {
      var failing = true;
      when(() => repo.getTrips(
            status: any(named: 'status'),
            page: any(named: 'page'),
            pageSize: any(named: 'pageSize'),
            sortBy: any(named: 'sortBy'),
            sortDir: any(named: 'sortDir'),
          )).thenAnswer((_) async {
        if (failing) throw Exception('offline');
        return pageOf([trip('1', 'Delivered', ref: 'TRP-DONE')]);
      });

      await pumpScreen(tester);
      expect(find.text('Failed to Load Trips'), findsOneWidget);

      failing = false;
      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(find.text('TRP-DONE'), findsOneWidget);
    });
  });
}
