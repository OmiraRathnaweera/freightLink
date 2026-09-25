import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class AgencyDashboardScreen extends StatelessWidget {
  const AgencyDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Dashboard')),
      body: ListView(
        padding: const EdgeInsets.all(16.0),
        children: [
          const Text('Welcome, Agency Staff!', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
          const SizedBox(height: 20),
          _buildDashboardCard(context, 'Fleet Management', 'Manage your vehicles and availability', Icons.directions_car, '/fleet'),
          _buildDashboardCard(context, 'Driver Onboarding', 'Add and manage drivers', Icons.person_add, '/driver-onboarding'),
          _buildDashboardCard(context, 'Compliance Documents', 'Upload and verify documents', Icons.verified_user, '/compliance-docs'),
        ],
      ),
    );
  }

  Widget _buildDashboardCard(BuildContext context, String title, String subtitle, IconData icon, String route) {
    return Card(
      margin: const EdgeInsets.only(bottom: 16.0),
      child: ListTile(
        leading: Icon(icon, size: 40),
        title: Text(title, style: const TextStyle(fontWeight: FontWeight.bold)),
        subtitle: Text(subtitle),
        trailing: const Icon(Icons.arrow_forward_ios),
        onTap: () => context.go(route),
      ),
    );
  }
}
