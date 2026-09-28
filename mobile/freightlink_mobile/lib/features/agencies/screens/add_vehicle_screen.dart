import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/utils/app_validators.dart';
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
  bool _hasAttemptedSubmit = false;
  Map<String, String> _fieldErrors = {};

  @override
  void dispose() {
    _registrationNoController.dispose();
    _capacityKgController.dispose();
    _volumeM3Controller.dispose();
    super.dispose();
  }

  String? _errorFor(String field) => _hasAttemptedSubmit ? _fieldErrors[field] : null;

  /// Mirrors `VehicleCreateDto`'s DataAnnotations — see
  /// `backend/DTOs/Agency/VehicleCreateDto.cs`.
  Map<String, String> _validate() {
    final errors = <String, String>{};

    final regNoError = AppValidators.length(_registrationNoController.text, 'Registration number', max: 50);
    if (regNoError != null) errors['registrationNo'] = regNoError;

    final capacityError = AppValidators.number(_capacityKgController.text, 'Capacity', min: 0, max: 100000);
    if (capacityError != null) errors['capacityKg'] = capacityError;

    final volumeError = AppValidators.number(_volumeM3Controller.text, 'Volume', min: 0, max: 1000);
    if (volumeError != null) errors['volumeM3'] = volumeError;

    return errors;
  }

  void _revalidateIfDirty() {
    if (!_hasAttemptedSubmit) return;
    setState(() => _fieldErrors = _validate());
  }

  Future<void> _submit(BuildContext context) async {
    setState(() {
      _hasAttemptedSubmit = true;
      _fieldErrors = _validate();
    });
    if (_fieldErrors.isNotEmpty) return;

    final repository = context.read<AgenciesRepository>();

    setState(() => _isSubmitting = true);

    try {
      await repository.addVehicle({
        'registrationNo': _registrationNoController.text.trim(),
        'vehicleType': _selectedVehicleType,
        'capacityKg': double.parse(_capacityKgController.text.trim()),
        'volumeM3': double.parse(_volumeM3Controller.text.trim()),
      });

      if (!context.mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vehicle added successfully!')),
      );
      context.go('/dashboard/fleet');
    } on ApiException catch (e) {
      if (!context.mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message)),
      );
    } catch (e) {
      if (!context.mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Failed to add vehicle. Please try again.')),
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
                maxLength: 50,
                errorText: _errorFor('registrationNo'),
                onChanged: (_) => _revalidateIfDirty(),
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
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: decimalInputFormatters,
                errorText: _errorFor('capacityKg'),
                onChanged: (_) => _revalidateIfDirty(),
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'Volume (m³)',
                controller: _volumeM3Controller,
                prefixIcon: Icons.view_in_ar_outlined,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: decimalInputFormatters,
                errorText: _errorFor('volumeM3'),
                onChanged: (_) => _revalidateIfDirty(),
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
