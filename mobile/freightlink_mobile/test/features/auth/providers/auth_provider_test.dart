import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import '../../../helpers/fakes.dart';

void main() {
  setUpAll(registerFallbackValues);

  group('AuthProvider driver registration & agencies', () {
    late MockTokenStorage tokenStorage;

    setUp(() {
      tokenStorage = MockTokenStorage();
    });

    test('fetchAgencies parses list of agencies successfully', () async {
      final client = MockClient((request) async {
        if (request.url.path.endsWith('/auth/agencies')) {
          return http.Response(
            jsonEncode([
              {'agencyId': 'ag-1', 'name': 'Alpha Transport'},
              {'agencyId': 'ag-2', 'name': 'Bravo Logistics'},
            ]),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not found', 404);
      });

      final provider = AuthProvider(
        tokenStorage: tokenStorage,
        httpClient: client,
      );

      final agencies = await provider.fetchAgencies();

      expect(agencies.length, 2);
      expect(agencies[0].agencyId, 'ag-1');
      expect(agencies[0].name, 'Alpha Transport');
      expect(agencies[1].agencyId, 'ag-2');
      expect(agencies[1].name, 'Bravo Logistics');
    });

    test('registerDriver succeeds on 201 response', () async {
      Map<String, dynamic>? receivedBody;
      final client = MockClient((request) async {
        if (request.url.path.endsWith('/auth/register/driver')) {
          receivedBody = jsonDecode(request.body) as Map<String, dynamic>;
          return http.Response(
            jsonEncode({
              'message': 'Driver registered successfully.',
              'userId': 'usr-123',
              'email': 'driver@test.com',
            }),
            201,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not found', 404);
      });

      final provider = AuthProvider(
        tokenStorage: tokenStorage,
        httpClient: client,
      );

      final success = await provider.registerDriver(
        email: 'driver@test.com',
        password: 'Password123!',
        fullName: 'Test Driver',
        phoneE164: '+94771234567',
        licenceNo: 'DL-987654',
        licenceExpiry: '2028-12-31',
        agencyId: 'ag-1',
      );

      expect(success, isTrue);
      expect(provider.errorMessage, isNull);
      expect(receivedBody?['email'], 'driver@test.com');
      expect(receivedBody?['licenceNo'], 'DL-987654');
      expect(receivedBody?['agencyId'], 'ag-1');
    });

    test('registerDriver sets errorMessage on API conflict error', () async {
      final client = MockClient((request) async {
        if (request.url.path.endsWith('/auth/register/driver')) {
          return http.Response(
            jsonEncode({
              'error': {
                'code': 'DRIVER_LICENCE_ALREADY_REGISTERED',
                'message': 'A driver with this driving licence number already exists.',
                'details': [],
              }
            }),
            409,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not found', 404);
      });

      final provider = AuthProvider(
        tokenStorage: tokenStorage,
        httpClient: client,
      );

      final success = await provider.registerDriver(
        email: 'driver@test.com',
        password: 'Password123!',
        fullName: 'Test Driver',
        phoneE164: null,
        licenceNo: 'DL-DUPLICATE',
        licenceExpiry: '2028-12-31',
        agencyId: 'ag-1',
      );

      expect(success, isFalse);
      expect(
        provider.errorMessage,
        'A driver with this driving licence number already exists.',
      );
    });
  });
}
