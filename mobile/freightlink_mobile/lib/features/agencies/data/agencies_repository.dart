import '../../../core/network/api_client.dart';

class AgenciesRepository {
  AgenciesRepository(this._client, this.agencyId);

  final ApiClient _client;
  final String? agencyId;
  
  // Build dashboard metrics from the same persisted resources shown by the
  // Agency screens; never surface demo-only numbers in an operational view.
  Future<Map<String, dynamic>> getDashboardStats() async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    final results = await Future.wait([getFleet(), getDrivers(), getComplianceDocs()]);
    final vehicles = results[0];
    final drivers = results[1];
    final complianceDocs = results[2];
    
    return {
      'totalVehicles': vehicles.length,
      'activeDrivers': drivers.length,
      'availableVehicles': vehicles.where((v) => v['status'] == 'Available').length,
      'onTripVehicles': vehicles.where((v) => v['status'] == 'OnTrip').length,
      'maintenanceVehicles': vehicles.where((v) => v['status'] == 'Maintenance').length,
      'pendingCompliance': complianceDocs.where((d) => d['status'] == 'Pending').length,
    };
  }

  // Fetch actual fleet
  Future<List<Map<String, dynamic>>> getFleet() async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    final response = await _client.get('/agencies/$agencyId/vehicles') as List<dynamic>;
    return response.cast<Map<String, dynamic>>();
  }

  // Fetch actual agency profile
  Future<Map<String, dynamic>> getProfile() async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    final response = await _client.get('/agencies/$agencyId') as Map<String, dynamic>;
    return response;
  }

  // Update actual agency profile
  Future<void> updateProfile(Map<String, dynamic> data) async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    await _client.put(
      '/agencies/$agencyId',
      body: data,
    );
  }

  // Add actual vehicle
  Future<void> addVehicle(Map<String, dynamic> data) async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    await _client.post(
      '/agencies/$agencyId/vehicles',
      body: data,
    );
  }

  Future<Map<String, dynamic>> updateVehicleStatus(
    String vehicleId,
    String status,
  ) async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }

    final response = await _client.patch(
      '/agencies/$agencyId/vehicles/$vehicleId/status',
      body: {'status': status},
    ) as Map<String, dynamic>;
    return response;
  }

  // Add actual driver
  Future<void> addDriver(Map<String, dynamic> data) async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    await _client.post(
      '/agencies/$agencyId/drivers',
      body: data,
    );
  }

  // Fetch actual drivers
  Future<List<Map<String, dynamic>>> getDrivers() async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    final response = await _client.get('/agencies/$agencyId/drivers') as List<dynamic>;
    return response.cast<Map<String, dynamic>>();
  }

  // Fetch compliance docs
  Future<List<Map<String, dynamic>>> getComplianceDocs() async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    final response = await _client.get('/agencies/$agencyId/compliance-docs') as List<dynamic>;
    return response.cast<Map<String, dynamic>>();
  }

  // Upload file
  Future<String> uploadFile(List<int> bytes, String fileName) async {
    final response = await _client.postMultipart(
      '/files/single',
      fileBytes: bytes,
      filename: fileName,
    ) as Map<String, dynamic>;
    return response['publicId'] as String;
  }

  // Add compliance doc
  Future<void> addComplianceDoc(Map<String, dynamic> data) async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    await _client.post(
      '/agencies/$agencyId/compliance-docs',
      body: data,
    );
  }
}
