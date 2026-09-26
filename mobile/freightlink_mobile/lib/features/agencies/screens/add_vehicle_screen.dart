import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/primary_button.dart';
import '../data/agencies_repository.dart';

class AddVehicleScreen extends StatefulWidget {
  const AddVehicleScreen({super.key});

  @override
  State<AddVehicleScreen> createState() => _AddVehicleScreenState();
}

class _AddVehicleScreenState extends State<AddVehicleScreen> {
  final _registrationNoController = TextEditingController();
  final _capacityKgController = TextEditingController();
  final _volumeM3Controller = TextEditingController();
  String _selectedVehicleType = 'Lorry';
  
  bool _isSubmitting = false;

  @override
  void dispose() {
    _registrationNoController.dispose();
    _capacityKgController.dispose();
    _volumeM3Controller.dispose();
    super.dispose();
  }

  Future<void> _submit(BuildContext context) async {
    final repository = context.read<AgenciesRepository>();

    setState(() => _isSubmitting = true);

    try {
      await repository.addVehicle({
        'registrationNo': _registrationNoController.text.trim(),
        'vehicleType': _selectedVehicleType,
        'capacityKg': double.tryParse(_capacityKgController.text.trim()) ?? 0,
        'volumeM3': double.tryParse(_volumeM3Controller.text.trim()) ?? 0,
      });
      
      if (!context.mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vehicle added successfully!')),
      );
      context.go('/dashboard/fleet');
    } catch (e) {
      if (!context.mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString())),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppTopBar(
        title: 'Add Vehicle',
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/dashboard/fleet'),
        ),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spaceXl),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AppTextField(
                label: 'Registration Number',
                controller: _registrationNoController,
                prefixIcon: Icons.pin_outlined,
              ),
              const SizedBox(height: AppConstants.spaceLg),
              DropdownButtonFormField<String>(
                initialValue: _selectedVehicleType,
                decoration: InputDecoration(
                  labelText: 'Vehicle Type',
                  prefixIcon: const Icon(Icons.local_shipping_outlined),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  ),
                ),
                items: const [
                  DropdownMenuItem(value: 'Lorry', child: Text('Lorry')),
                  DropdownMenuItem(value: 'Container', child: Text('Container')),
                  DropdownMenuItem(value: 'Refrigerated', child: Text('Refrigerated')),
                  DropdownMenuItem(value: 'FlatBed', child: Text('FlatBed')),
                  DropdownMenuItem(value: 'Tipper', child: Text('Tipper')),
                ],
                onChanged: (val) {
                  if (val != null) setState(() => _selectedVehicleType = val);
                },
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'Capacity (kg)',
                controller: _capacityKgController,
                prefixIcon: Icons.monitor_weight_outlined,
                keyboardType: TextInputType.number,
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'Volume (m³)',
                controller: _volumeM3Controller,
                prefixIcon: Icons.view_in_ar_outlined,
                keyboardType: TextInputType.number,
              ),
              const SizedBox(height: AppConstants.spaceXxl),
              PrimaryButton(
                label: 'Add Vehicle',
                isLoading: _isSubmitting,
                onPressed: () => _submit(context),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
