import '../../../core/network/api_client.dart';

class AgenciesRepository {
  AgenciesRepository(this._client, this.agencyId);

  final ApiClient _client;
  final String? agencyId;
  
  final List<Map<String, dynamic>> _mockVehicles = [];
  final List<Map<String, dynamic>> _mockDrivers = [];

  // Fetch dashboard stats
  Future<Map<String, dynamic>> getDashboardStats() async {
    if (agencyId == null) {
      throw Exception('Agency ID is missing.');
    }
    
    final vehicles = await getFleet();
    
    return {
      'totalVehicles': vehicles.length,
      'activeDrivers': 8 + _mockDrivers.length,
      'pendingCompliance': 3,
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

  // Placeholder for adding a driver
  Future<void> addDriver(Map<String, dynamic> data) async {
    await Future.delayed(const Duration(milliseconds: 800));
    _mockDrivers.add(data);
  }
}
