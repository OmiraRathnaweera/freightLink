import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/loads/screens/load_detail_screen.dart';
import 'package:freightlink_mobile/features/loads/screens/my_loads_screen.dart';
import 'package:freightlink_mobile/features/loads/widgets/load_card.dart';
import 'package:mocktail/mocktail.dart';

import '../helpers/fakes.dart';
import '../helpers/fixtures.dart';
import '../helpers/pump_app.dart';

void main() {
  setUpAll(registerFallbackValues);

  late MockLoadsRepository repository;

  setUp(() {
    repository = MockLoadsRepository();
  });

  testWidgets('My Loads -> Load Detail -> back returns to the list', (
    tester,
  ) async {
    final item = buildLoadListItem(loadId: 'load-1', referenceCode: 'FM-2001');
    when(
      () => repository.getList(
        search: any(named: 'search'),
        status: any(named: 'status'),
      ),
    ).thenAnswer((_) async => buildPagedResult([item]));
    when(() => repository.getById('load-1')).thenAnswer(
      (_) async => buildLoad(loadId: 'load-1', referenceCode: 'FM-2001'),
    );

    await pumpApp(tester, const MyLoadsScreen(), repository: repository);
    await tester.pumpAndSettle();

    expect(find.byType(MyLoadsScreen), findsOneWidget);
    expect(find.text('FM-2001'), findsOneWidget);

    await tester.tap(find.byType(LoadCard));
    await tester.pumpAndSettle();

    expect(find.byType(LoadDetailScreen), findsOneWidget);
    expect(find.byType(MyLoadsScreen), findsNothing);

    await tester.tap(find.byIcon(Icons.arrow_back_rounded));
    await tester.pumpAndSettle();

    expect(find.byType(LoadDetailScreen), findsNothing);
    expect(find.byType(MyLoadsScreen), findsOneWidget);
    expect(find.text('FM-2001'), findsOneWidget);
  });
}
