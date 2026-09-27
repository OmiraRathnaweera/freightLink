import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_validators.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';
import '../providers/auth_provider.dart';
import '../../loads/screens/location_picker_screen.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  // Common fields
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _fullNameController = TextEditingController();
  final _phoneController = TextEditingController();
  bool _obscurePassword = true;
  
  // Agency fields
  final _agencyNameController = TextEditingController();
  final _businessRegNoController = TextEditingController();
  final _yardAddressController = TextEditingController();
  final _jobTitleController = TextEditingController();
  PickedLocation? _yardLocation;

  bool _hasAttemptedSubmit = false;
  Map<String, String> _fieldErrors = {};

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _fullNameController.dispose();
    _phoneController.dispose();
    _agencyNameController.dispose();
    _businessRegNoController.dispose();
    _yardAddressController.dispose();
    _jobTitleController.dispose();
    super.dispose();
  }

  String? _errorFor(String field) => _hasAttemptedSubmit ? _fieldErrors[field] : null;

  /// Mirrors every DataAnnotation on `RegisterAgencyRequestDto` — see
  /// `backend/DTOs/Auth/RegisterAgencyRequestDto.cs`.
  Map<String, String> _validate() {
    final errors = <String, String>{};

    final fullNameError = AppValidators.length(_fullNameController.text, 'Full name', min: 2, max: 200);
    if (fullNameError != null) errors['fullName'] = fullNameError;

    final emailError = AppValidators.email(_emailController.text);
    if (emailError != null) errors['email'] = emailError;

    final passwordError = AppValidators.strongPassword(_passwordController.text);
    if (passwordError != null) errors['password'] = passwordError;

    final phoneError = AppValidators.optionalPhone(_phoneController.text);
    if (phoneError != null) errors['phoneE164'] = phoneError;

    final agencyNameError = AppValidators.length(_agencyNameController.text, 'Agency name', min: 2, max: 200);
    if (agencyNameError != null) errors['agencyName'] = agencyNameError;

    final businessRegNoError = AppValidators.length(_businessRegNoController.text, 'Business registration no.', max: 100);
    if (businessRegNoError != null) errors['businessRegNo'] = businessRegNoError;

    final jobTitleError = AppValidators.length(
      _jobTitleController.text,
      'Job title',
      max: 150,
      isRequired: false,
    );
    if (jobTitleError != null) errors['jobTitle'] = jobTitleError;

    if (_yardLocation == null) {
      errors['yardAddress'] = 'Select your agency yard location.';
    } else {
      final yardError = AppValidators.length(_yardLocation!.address, 'Yard address', min: 5, max: 500);
      if (yardError != null) errors['yardAddress'] = yardError;
    }

    return errors;
  }

  void _revalidateIfDirty() {
    if (!_hasAttemptedSubmit) return;
    setState(() => _fieldErrors = _validate());
  }

  Future<void> _submit() async {
    setState(() {
      _hasAttemptedSubmit = true;
      _fieldErrors = _validate();
    });
    if (_fieldErrors.isNotEmpty) return;

    final auth = context.read<AuthProvider>();
    final yard = _yardLocation!;

    final registered = await auth.registerAgency(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      fullName: _fullNameController.text.trim(),
      phoneE164: _phoneController.text.trim(),
      jobTitle: _jobTitleController.text.trim(),
      agencyName: _agencyNameController.text.trim(),
      businessRegNo: _businessRegNoController.text.trim(),
      yardAddress: yard.address,
      yardLat: yard.point.latitude,
      yardLng: yard.point.longitude,
    );
    if (registered && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Agency registered. Verify your email, then sign in.')),
      );
      context.go('/login');
    }
  }

  Future<void> _pickYard() async {
    final picked = await Navigator.of(context).push<PickedLocation>(
      MaterialPageRoute(
        builder: (_) => LocationPickerScreen(
          title: 'Select agency yard',
          initialPoint: _yardLocation?.point,
        ),
      ),
    );
    if (picked == null || !mounted) return;
    setState(() {
      _yardLocation = picked;
      _yardAddressController.text = picked.address;
    });
    _revalidateIfDirty();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Register an Agency Account'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/login'),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(AppConstants.spaceXl),
            child: Consumer<AuthProvider>(
              builder: (context, auth, _) {
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text(
                      'Shipper registration is available through the FreightLink web portal.',
                      style: TextStyle(color: AppColors.inkMuted),
                    ),
                    const SizedBox(height: AppConstants.spaceXxl),
                    
                    // Common Fields
                    AppTextField(
                      label: 'Full Name',
                      controller: _fullNameController,
                      hintText: 'John Doe',
                      prefixIcon: Icons.person_outline,
                      textInputAction: TextInputAction.next,
                      maxLength: 200,
                      errorText: _errorFor('fullName'),
                      onChanged: (_) => _revalidateIfDirty(),
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    AppTextField(
                      label: 'Email',
                      controller: _emailController,
                      hintText: 'you@company.com',
                      prefixIcon: Icons.mail_outline_rounded,
                      keyboardType: TextInputType.emailAddress,
                      textInputAction: TextInputAction.next,
                      maxLength: 256,
                      errorText: _errorFor('email'),
                      onChanged: (_) => _revalidateIfDirty(),
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    AppTextField(
                      label: 'Password',
                      controller: _passwordController,
                      hintText: '••••••••',
                      prefixIcon: Icons.lock_outline_rounded,
                      obscureText: _obscurePassword,
                      textInputAction: TextInputAction.next,
                      errorText: _errorFor('password'),
                      onChanged: (_) => _revalidateIfDirty(),
                      suffixIcon: IconButton(
                        icon: Icon(
                          _obscurePassword
                              ? Icons.visibility_outlined
                              : Icons.visibility_off_outlined,
                          size: 20,
                        ),
                        onPressed: () => setState(
                          () => _obscurePassword = !_obscurePassword,
                        ),
                      ),
                    ),
                    const SizedBox(height: 4),
                    const Text(
                      'At least 8 characters, with an uppercase letter, a lowercase letter, a digit, and a special character.',
                      style: TextStyle(fontSize: 11, color: AppColors.inkMuted),
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    AppTextField(
                      label: 'Phone (E.164) — optional',
                      controller: _phoneController,
                      hintText: '+14155552671',
                      prefixIcon: Icons.phone_outlined,
                      keyboardType: TextInputType.phone,
                      textInputAction: TextInputAction.next,
                      errorText: _errorFor('phoneE164'),
                      onChanged: (_) => _revalidateIfDirty(),
                    ),
                    const SizedBox(height: AppConstants.spaceLg),

                    // Agency-specific fields. Shipper signup is intentionally web-only.
                    ...[
                      AppTextField(
                        label: 'Agency Name',
                        controller: _agencyNameController,
                        hintText: 'Fast Freight Co.',
                        prefixIcon: Icons.business_outlined,
                        textInputAction: TextInputAction.next,
                        maxLength: 200,
                        errorText: _errorFor('agencyName'),
                        onChanged: (_) => _revalidateIfDirty(),
                      ),
                      const SizedBox(height: AppConstants.spaceLg),
                      AppTextField(
                        label: 'Business Registration No.',
                        controller: _businessRegNoController,
                        hintText: 'REG-12345',
                        prefixIcon: Icons.numbers_outlined,
                        textInputAction: TextInputAction.next,
                        maxLength: 100,
                        errorText: _errorFor('businessRegNo'),
                        onChanged: (_) => _revalidateIfDirty(),
                      ),
                      const SizedBox(height: AppConstants.spaceLg),
                      AppTextField(
                        label: 'Job Title — optional',
                        controller: _jobTitleController,
                        hintText: 'Fleet Manager',
                        prefixIcon: Icons.work_outline,
                        textInputAction: TextInputAction.next,
                        maxLength: 150,
                        errorText: _errorFor('jobTitle'),
                        onChanged: (_) => _revalidateIfDirty(),
                      ),
                      const SizedBox(height: AppConstants.spaceLg),
                      AppTextField(
                        label: 'Yard Address',
                        controller: _yardAddressController,
                        hintText: 'Tap to select on map',
                        prefixIcon: Icons.location_on_outlined,
                        readOnly: true,
                        onTap: _pickYard,
                        textInputAction: TextInputAction.done,
                        errorText: _errorFor('yardAddress'),
                      ),
                    ],

                    if (auth.errorMessage != null) ...[
                      const SizedBox(height: AppConstants.spaceMd),
                      Container(
                        padding: const EdgeInsets.all(AppConstants.spaceMd),
                        decoration: BoxDecoration(
                          color: AppColors.statusErrorBg,
                          borderRadius: BorderRadius.circular(
                            AppConstants.radiusSm,
                          ),
                        ),
                        child: Text(
                          auth.errorMessage!,
                          style: const TextStyle(
                            color: AppColors.statusErrorFg,
                            fontSize: 13,
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: AppConstants.spaceXl),
                    PrimaryButton(
                      label: 'Create Account',
                      isLoading: auth.isSubmitting,
                      onPressed: _submit,
                    ),
                  ],
                );
              },
            ),
          ),
        ),
      ),
    );
  }
}
