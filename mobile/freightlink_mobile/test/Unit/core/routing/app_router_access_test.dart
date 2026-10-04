import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/routing/app_router.dart';

void main() {
  group('mobile role route policy', () {
    test(
      'Shipper owns Loads and Billing but not Agency workspace or driver execution',
      () {
        expect(mobileRoleHome('Shipper'), '/loads');
        expect(isMobileRouteAllowed('Shipper', '/loads'), isTrue);
        expect(isMobileRouteAllowed('Shipper', '/payments/dispute'), isTrue);
        expect(isMobileRouteAllowed('Shipper', '/payments/disputes'), isTrue);
        expect(
          isMobileRouteAllowed('Shipper', '/payments/disputes/dispute-1'),
          isTrue,
        );
        expect(isMobileRouteAllowed('Shipper', '/dashboard'), isTrue);
        expect(isMobileRouteAllowed('Shipper', '/dashboard/fleet'), isFalse);
        expect(
          isMobileRouteAllowed('Shipper', '/dashboard/compliance-docs'),
          isFalse,
        );
        expect(isMobileRouteAllowed('Shipper', '/reports'), isFalse);
      },
    );

    test(
      'Agency Staff owns Agency workspace, proposals, trips, and Billing',
      () {
        expect(mobileRoleHome('AgencyStaff'), '/dashboard');
        expect(
          isMobileRouteAllowed('AgencyStaff', '/dashboard/fleet/add'),
          isTrue,
        );
        expect(
          isMobileRouteAllowed('AgencyStaff', '/dashboard/compliance-docs/add'),
          isTrue,
        );
        expect(isMobileRouteAllowed('AgencyStaff', '/loads'), isTrue);
        expect(isMobileRouteAllowed('AgencyStaff', '/payments'), isTrue);
        expect(
          isMobileRouteAllowed('AgencyStaff', '/payments/disputes'),
          isTrue,
        );
        expect(isMobileRouteAllowed('AgencyStaff', '/dashboard/trips'), isTrue);
        expect(
          isMobileRouteAllowed('AgencyStaff', '/dashboard/trips/trip-1'),
          isTrue,
        );
        expect(isMobileRouteAllowed('AgencyStaff', '/reports'), isFalse);
      },
    );

    test(
      'Driver is restricted to operational trip tab and placeholder dashboard',
      () {
        expect(mobileRoleHome('Driver'), '/loads');
        expect(isMobileRouteAllowed('Driver', '/loads'), isTrue);
        expect(isMobileRouteAllowed('Driver', '/dashboard'), isTrue);
        expect(isMobileRouteAllowed('Driver', '/dashboard/profile'), isFalse);
        // Trip history is Driver-only; the Agency trips list stays Agency-only.
        expect(isMobileRouteAllowed('Driver', '/dashboard/trip-history'), isTrue);
        expect(isMobileRouteAllowed('Driver', '/dashboard/trips'), isFalse);
        expect(isMobileRouteAllowed('AgencyStaff', '/dashboard/trip-history'), isFalse);
        expect(isMobileRouteAllowed('Shipper', '/dashboard/trip-history'), isFalse);
        expect(isMobileRouteAllowed('Driver', '/payments'), isFalse);
        expect(isMobileRouteAllowed('Driver', '/payments/disputes'), isFalse);
        expect(isMobileRouteAllowed('Driver', '/reports'), isFalse);
      },
    );

    test('unknown and Admin roles are not granted a mobile route', () {
      expect(mobileRoleHome('Admin'), '/login');
      expect(isMobileRouteAllowed('Admin', '/loads'), isFalse);
      expect(isMobileRouteAllowed(null, '/dashboard'), isFalse);
    });
  });
}
