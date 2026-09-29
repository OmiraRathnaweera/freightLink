import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/models/paged_result.dart';
import 'package:freightlink_mobile/features/loads/models/load.dart';
import 'package:freightlink_mobile/features/loads/models/load_status.dart';
import 'package:freightlink_mobile/features/loads/screens/load_detail_screen.dart';
import 'package:freightlink_mobile/features/loads/screens/my_loads_screen.dart';
import 'package:freightlink_mobile/features/loads/widgets/load_card.dart';
import 'package:freightlink_mobile/features/loads/widgets/load_list_skeleton.dart';
import 'package:mocktail/mocktail.dart';

import '../../../../helpers/fakes.dart';
import '../../../../helpers/fixtures.dart';
import '../../../../helpers/pump_app.dart';

void main() {
  setUpAll(registerFallbackValues);

  late MockLoadsRepository repository;

  setUp(() {
    repository = MockLoadsRepository();
  });

  group('My Loads screen', () {
    testWidgets('shows a loading skeleton while the list is fetching', (
      tester,
    ) async {
      final completer = Completer<PagedResult<LoadListItem>>();
      when(
        () => repository.getList(
          search: any(named: 'search'),
          status: any(named: 'status'),
        ),
      ).thenAnswer((_) => completer.future);

      await pumpApp(tester, const MyLoadsScreen(), repository: repository);
      // Bounded pump — LoadListSkeleton's shimmer animation repeats forever,
      // so pumpAndSettle would never complete while it's on screen.
      await tester.pump();

      expect(find.byType(LoadListSkeleton), findsOneWidget);
    });

    testWidgets('shows the empty state when there are no loads', (
      tester,
    ) async {
      when(
        () => repository.getList(
          search: any(named: 'search'),
          status: any(named: 'status'),
        ),
      ).thenAnswer((_) async => buildPagedResult(const []));

      await pumpApp(tester, const MyLoadsScreen(), repository: repository);
      await tester.pumpAndSettle();

      expect(find.text('No loads found'), findsOneWidget);
      expect(find.text('Post your first load to get started'), findsOneWidget);
    });

    testWidgets('renders a card per load with its reference code and status', (
      tester,
    ) async {
      final items = [
        buildLoadListItem(
          loadId: 'load-1',
          referenceCode: 'FM-1001',
          status: LoadStatus.posted,
        ),
        buildLoadListItem(
          loadId: 'load-2',
          referenceCode: 'FM-1002',
          status: LoadStatus.matched,
        ),
      ];
      when(
        () => repository.getList(
          search: any(named: 'search'),
          status: any(named: 'status'),
        ),
      ).thenAnswer((_) async => buildPagedResult(items));

      await pumpApp(tester, const MyLoadsScreen(), repository: repository);
      await tester.pumpAndSettle();

      expect(find.byType(LoadCard), findsNWidgets(2));
      expect(find.text('FM-1001'), findsOneWidget);
      expect(find.text('FM-1002'), findsOneWidget);
      expect(find.text(LoadStatus.posted.label), findsOneWidget);
      expect(find.text(LoadStatus.matched.label), findsOneWidget);
    });

    testWidgets('tapping a load card opens its detail screen', (tester) async {
      final item = buildLoadListItem(
        loadId: 'load-1',
        referenceCode: 'FM-1001',
      );
      when(
        () => repository.getList(
          search: any(named: 'search'),
          status: any(named: 'status'),
        ),
      ).thenAnswer((_) async => buildPagedResult([item]));
      when(() => repository.getById('load-1')).thenAnswer(
        (_) async => buildLoad(loadId: 'load-1', referenceCode: 'FM-1001'),
      );

      await pumpApp(tester, const MyLoadsScreen(), repository: repository);
      await tester.pumpAndSettle();

      await tester.tap(find.byType(LoadCard));
      await tester.pumpAndSettle();

      expect(find.byType(LoadDetailScreen), findsOneWidget);
      verify(() => repository.getById('load-1')).called(1);
    });
  });
}
