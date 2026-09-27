import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_validators.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/primary_button.dart';
import '../data/agencies_repository.dart';

class AddDriverScreen extends StatefulWidget {
  const AddDriverScreen({super.key});

  @override
  State<AddDriverScreen> createState() => _AddDriverScreenState();
}

class _AddDriverScreenState extends State<AddDriverScreen> {
  final _nameController = TextEditingController();
  final _licenseController = TextEditingController();
  final _emailController = TextEditingController();
  final _expiryController = TextEditingController(); // YYYY-MM-DD
  DateTime? _licenceExpiry;

  bool _isSubmitting = false;
  bool _hasAttemptedSubmit = false;
  Map<String, String> _fieldErrors = {};

  @override
  void dispose() {
    _nameController.dispose();
    _licenseController.dispose();
    _emailController.dispose();
    _expiryController.dispose();
    super.dispose();
  }

  String? _errorFor(String field) => _hasAttemptedSubmit ? _fieldErrors[field] : null;

  /// Mirrors `CreateDriverRequestDto`'s DataAnnotations plus the
  /// future-date business rule `AgencyService.AddDriverAsync` enforces
  /// (licence expiry must be after today) — see
  /// `backend/DTOs/Agency/CreateDriverRequestDto.cs`. There is no Password
  /// field on that DTO: the server generates and emails a temporary
  /// password, so this form intentionally has no password field.
  Map<String, String> _validate() {
    final errors = <String, String>{};

    final nameError = AppValidators.length(_nameController.text, 'Full name', min: 2, max: 200);
    if (nameError != null) errors['fullName'] = nameError;

    final emailError = AppValidators.email(_emailController.text);
    if (emailError != null) errors['email'] = emailError;

    final licenceError = AppValidators.length(_licenseController.text, 'Licence number', min: 2, max: 100);
    if (licenceError != null) errors['licenceNo'] = licenceError;

    if (_licenceExpiry == null) {
      errors['licenceExpiry'] = 'Licence expiry date is required.';
    } else if (!_licenceExpiry!.isAfter(DateTime.now())) {
      errors['licenceExpiry'] = 'Licence expiry date must be in the future.';
    }

    return errors;
  }

  void _revalidateIfDirty() {
    if (!_hasAttemptedSubmit) return;
    setState(() => _fieldErrors = _validate());
  }

  Future<void> _pickExpiryDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime.now().add(const Duration(days: 365)),
      firstDate: DateTime.now().add(const Duration(days: 1)),
      lastDate: DateTime.now().add(const Duration(days: 3650)),
    );
    if (picked == null) return;
    setState(() {
      _licenceExpiry = picked;
      _expiryController.text = DateFormat('yyyy-MM-dd').format(picked);
    });
    _revalidateIfDirty();
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
      await repository.addDriver({
        'fullName': _nameController.text.trim(),
        'licenceNo': _licenseController.text.trim(),
        'email': _emailController.text.trim(),
        'licenceExpiry': _expiryController.text.trim(),
      });

      if (!context.mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Driver registered successfully!')),
      );
      context.go('/dashboard/driver-onboarding');
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
        title: 'Add Driver',
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/dashboard/driver-onboarding'),
        ),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spaceXl),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AppTextField(
                label: 'Driver Full Name',
                controller: _nameController,
                prefixIcon: Icons.person_outline,
                maxLength: 200,
                errorText: _errorFor('fullName'),
                onChanged: (_) => _revalidateIfDirty(),
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'Email Address (for login)',
                controller: _emailController,
                prefixIcon: Icons.email_outlined,
                keyboardType: TextInputType.emailAddress,
                maxLength: 256,
                errorText: _errorFor('email'),
                onChanged: (_) => _revalidateIfDirty(),
              ),
              const SizedBox(height: AppConstants.spaceLg),
              const Text(
                'A temporary password is generated automatically and emailed to the driver — there is no password to set here.',
                style: TextStyle(fontSize: 12, color: AppColors.inkMuted),
              ),
              const SizedBox(height: AppConstants.spaceLg),
              AppTextField(
                label: 'License Number',
                controller: _licenseController,
                prefixIcon: Icons.badge_outlined,
                maxLength: 100,
                errorText: _errorFor('licenceNo'),
                onChanged: (_) => _revalidateIfDirty(),
              ),
              const SizedBox(height: AppConstants.spaceLg),
              InkWell(
                onTap: _pickExpiryDate,
                child: IgnorePointer(
                  child: AppTextField(
                    label: 'License Expiry',
                    controller: _expiryController,
                    prefixIcon: Icons.calendar_today_outlined,
                    hintText: 'Tap to select a date',
                    errorText: _errorFor('licenceExpiry'),
                  ),
                ),
              ),
              const SizedBox(height: AppConstants.spaceXxl),
              PrimaryButton(
                label: 'Register Driver',
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
