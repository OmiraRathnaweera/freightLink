import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';
import '../models/agency_lookup.dart';
import '../providers/auth_provider.dart';

/// Screen for driver self-registration from the mobile application.
class DriverRegisterScreen extends StatefulWidget {
  const DriverRegisterScreen({super.key});

  @override
  State<DriverRegisterScreen> createState() => _DriverRegisterScreenState();
}

class _DriverRegisterScreenState extends State<DriverRegisterScreen> {
  final _fullNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  final _phoneController = TextEditingController();
  final _licenceNoController = TextEditingController();
  final _expiryDateController = TextEditingController();

  DateTime? _selectedExpiryDate;
  AgencyLookup? _selectedAgency;
  List<AgencyLookup> _agencies = [];
  bool _isLoadingAgencies = true;
  String? _agenciesError;

  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;
  String? _clientError;

  @override
  void initState() {
    super.initState();
    _loadAgencies();
  }

  @override
  void dispose() {
    _fullNameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    _phoneController.dispose();
    _licenceNoController.dispose();
    _expiryDateController.dispose();
    super.dispose();
  }

  Future<void> _loadAgencies() async {
    setState(() {
      _isLoadingAgencies = true;
      _agenciesError = null;
    });

    try {
      final auth = context.read<AuthProvider>();
      final list = await auth.fetchAgencies();
      if (mounted) {
        setState(() {
          _agencies = list;
          _isLoadingAgencies = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _agenciesError = 'Failed to load employing agencies.';
          _isLoadingAgencies = false;
        });
      }
    }
  }

  Future<void> _pickExpiryDate() async {
    final now = DateTime.now();
    final tomorrow = DateTime(now.year, now.month, now.day + 1);
    final initial = _selectedExpiryDate ?? now.add(const Duration(days: 365));

    final picked = await showDatePicker(
      context: context,
      initialDate: initial.isBefore(tomorrow) ? tomorrow : initial,
      firstDate: tomorrow,
      lastDate: now.add(const Duration(days: 365 * 15)),
    );

    if (picked != null) {
      setState(() {
        _selectedExpiryDate = picked;
        _expiryDateController.text = DateFormat('yyyy-MM-dd').format(picked);
        _clientError = null;
      });
    }
  }

  bool _validateInput() {
    final name = _fullNameController.text.trim();
    final email = _emailController.text.trim();
    final password = _passwordController.text;
    final confirmPassword = _confirmPasswordController.text;
    final phone = _phoneController.text.trim();
    final licence = _licenceNoController.text.trim();

    if (name.length < 2) {
      _clientError = 'Full name must be at least 2 characters.';
      return false;
    }

    final emailRegex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    if (!emailRegex.hasMatch(email)) {
      _clientError = 'Please enter a valid email address.';
      return false;
    }

    if (password.length < 8) {
      _clientError = 'Password must be at least 8 characters long.';
      return false;
    }
    if (!RegExp(r'[A-Z]').hasMatch(password)) {
      _clientError = 'Password must contain at least one uppercase letter.';
      return false;
    }
    if (!RegExp(r'[a-z]').hasMatch(password)) {
      _clientError = 'Password must contain at least one lowercase letter.';
      return false;
    }
    if (!RegExp(r'[0-9]').hasMatch(password)) {
      _clientError = 'Password must contain at least one digit.';
      return false;
    }
    if (!RegExp(r'[^a-zA-Z0-9]').hasMatch(password)) {
      _clientError = 'Password must contain at least one special character.';
      return false;
    }

    if (password != confirmPassword) {
      _clientError = 'Passwords do not match.';
      return false;
    }

    if (phone.isNotEmpty && !phone.startsWith('+')) {
      _clientError = 'Phone number must start with + and include country code (e.g. +94771234567).';
      return false;
    }

    if (licence.length < 2) {
      _clientError = 'Driving licence number is required.';
      return false;
    }

    if (_selectedExpiryDate == null) {
      _clientError = 'Driving licence expiry date is required.';
      return false;
    }

    if (_selectedAgency == null) {
      _clientError = 'Please select your employing transport agency.';
      return false;
    }

    _clientError = null;
    return true;
  }

  Future<void> _submit() async {
    setState(() => _clientError = null);

    if (!_validateInput()) {
      setState(() {});
      return;
    }

    final auth = context.read<AuthProvider>();
    final success = await auth.registerDriver(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      fullName: _fullNameController.text.trim(),
      phoneE164: _phoneController.text.trim().isEmpty ? null : _phoneController.text.trim(),
      licenceNo: _licenceNoController.text.trim(),
      licenceExpiry: DateFormat('yyyy-MM-dd').format(_selectedExpiryDate!),
      agencyId: _selectedAgency!.agencyId,
    );

    if (!mounted) return;

    if (success) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Registration successful! Please sign in with your credentials.'),
          backgroundColor: AppColors.statusSuccessFg,
        ),
      );
      if (Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_rounded, color: AppColors.ink),
          onPressed: () => Navigator.of(context).pop(),
        ),
        title: const Text(
          'Driver Registration',
          style: TextStyle(
            color: AppColors.ink,
            fontWeight: FontWeight.w700,
            fontSize: 18,
          ),
        ),
        centerTitle: true,
      ),
      body: SafeArea(
        child: Consumer<AuthProvider>(
          builder: (context, auth, _) {
            final activeError = _clientError ?? auth.errorMessage;

            return SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppConstants.spaceXl,
                vertical: AppConstants.spaceLg,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Center(
                    child: Container(
                      width: 56,
                      height: 56,
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: AppColors.primary,
                        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                      ),
                      child: const Icon(
                        Icons.drive_eta_rounded,
                        color: AppColors.onPrimary,
                        size: 28,
                      ),
                    ),
                  ),
                  const SizedBox(height: AppConstants.spaceMd),
                  const Text(
                    'Join FreightLink as Driver',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.w700,
                      color: AppColors.ink,
                    ),
                  ),
                  const SizedBox(height: AppConstants.spaceXs),
                  const Text(
                    'Create your driver account to access assigned loads, live trip navigation, and delivery proofs.',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: 13,
                      color: AppColors.inkMuted,
                    ),
                  ),
                  const SizedBox(height: AppConstants.spaceXl),

                  // Agency Selector
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'EMPLOYING AGENCY',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          letterSpacing: 0.3,
                          color: AppColors.inkMuted,
                        ),
                      ),
                      const SizedBox(height: 6),
                      if (_isLoadingAgencies)
                        Container(
                          padding: const EdgeInsets.all(AppConstants.spaceMd),
                          decoration: BoxDecoration(
                            color: AppColors.surface,
                            borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                            border: Border.all(color: AppColors.border),
                          ),
                          child: const Row(
                            children: [
                              SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              ),
                              SizedBox(width: 12),
                              Text(
                                'Loading available agencies...',
                                style: TextStyle(color: AppColors.inkMuted, fontSize: 14),
                              ),
                            ],
                          ),
                        )
                      else if (_agenciesError != null)
                        InkWell(
                          onTap: _loadAgencies,
                          borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                          child: Container(
                            padding: const EdgeInsets.all(AppConstants.spaceMd),
                            decoration: BoxDecoration(
                              color: AppColors.statusErrorBg,
                              borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                              border: Border.all(color: AppColors.statusErrorFg),
                            ),
                            child: Row(
                              children: [
                                const Icon(Icons.refresh_rounded, size: 18, color: AppColors.statusErrorFg),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(
                                    '${_agenciesError!} Tap to retry.',
                                    style: const TextStyle(color: AppColors.statusErrorFg, fontSize: 13),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        )
                      else
                        DropdownButtonFormField<AgencyLookup>(
                          key: const Key('register_agency_dropdown'),
                          value: _selectedAgency,
                          decoration: InputDecoration(
                            hintText: 'Select your agency',
                            prefixIcon: const Icon(Icons.business_rounded, size: 20, color: AppColors.inkMuted),
                            filled: true,
                            fillColor: AppColors.surface,
                            contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                              borderSide: const BorderSide(color: AppColors.border),
                            ),
                            enabledBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                              borderSide: const BorderSide(color: AppColors.border),
                            ),
                          ),
                          items: _agencies.map((agency) {
                            return DropdownMenuItem<AgencyLookup>(
                              value: agency,
                              child: Text(
                                agency.name,
                                style: const TextStyle(fontSize: 14, color: AppColors.ink),
                              ),
                            );
                          }).toList(),
                          onChanged: (val) {
                            setState(() {
                              _selectedAgency = val;
                              _clientError = null;
                            });
                          },
                        ),
                    ],
                  ),
                  const SizedBox(height: AppConstants.spaceLg),

                  // Full Name
                  AppTextField(
                    key: const Key('register_name_field'),
                    label: 'FULL NAME',
                    controller: _fullNameController,
                    hintText: 'e.g. John Silva',
                    prefixIcon: Icons.person_outline_rounded,
                    textInputAction: TextInputAction.next,
                  ),
                  const SizedBox(height: AppConstants.spaceLg),

                  // Email
                  AppTextField(
                    key: const Key('register_email_field'),
                    label: 'EMAIL ADDRESS',
                    controller: _emailController,
                    hintText: 'driver@example.com',
                    prefixIcon: Icons.email_outlined,
                    keyboardType: TextInputType.emailAddress,
                    textInputAction: TextInputAction.next,
                  ),
                  const SizedBox(height: AppConstants.spaceLg),

                  // Phone
                  AppTextField(
                    key: const Key('register_phone_field'),
                    label: 'PHONE NUMBER (OPTIONAL)',
                    controller: _phoneController,
                    hintText: '+94771234567',
                    prefixIcon: Icons.phone_outlined,
                    keyboardType: TextInputType.phone,
                    textInputAction: TextInputAction.next,
                  ),
                  const SizedBox(height: AppConstants.spaceLg),

                  // Driving Licence No
                  AppTextField(
                    key: const Key('register_licence_field'),
                    label: 'DRIVING LICENCE NUMBER',
                    controller: _licenceNoController,
                    hintText: 'e.g. B1234567',
                    prefixIcon: Icons.badge_outlined,
                    textInputAction: TextInputAction.next,
                  ),
                  const SizedBox(height: AppConstants.spaceLg),

                  // Licence Expiry Date
                  AppTextField(
                    key: const Key('register_expiry_field'),
                    label: 'LICENCE EXPIRY DATE',
                    controller: _expiryDateController,
                    hintText: 'YYYY-MM-DD',
                    prefixIcon: Icons.calendar_today_outlined,
                    readOnly: true,
                    onTap: _pickExpiryDate,
                  ),
                  const SizedBox(height: AppConstants.spaceLg),

                  // Password
                  AppTextField(
                    key: const Key('register_password_field'),
                    label: 'PASSWORD',
                    controller: _passwordController,
                    hintText: 'At least 8 chars (upper, lower, digit, symbol)',
                    prefixIcon: Icons.lock_outline_rounded,
                    obscureText: _obscurePassword,
                    textInputAction: TextInputAction.next,
                    suffixIcon: IconButton(
                      icon: Icon(
                        _obscurePassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                        size: 20,
                        color: AppColors.inkMuted,
                      ),
                      onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                    ),
                  ),
                  const SizedBox(height: AppConstants.spaceLg),

                  // Confirm Password
                  AppTextField(
                    key: const Key('register_confirm_password_field'),
                    label: 'CONFIRM PASSWORD',
                    controller: _confirmPasswordController,
                    hintText: 'Re-enter your password',
                    prefixIcon: Icons.lock_outline_rounded,
                    obscureText: _obscureConfirmPassword,
                    textInputAction: TextInputAction.done,
                    suffixIcon: IconButton(
                      icon: Icon(
                        _obscureConfirmPassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                        size: 20,
                        color: AppColors.inkMuted,
                      ),
                      onPressed: () => setState(() => _obscureConfirmPassword = !_obscureConfirmPassword),
                    ),
                  ),

                  // Error Banner
                  if (activeError != null) ...[
                    const SizedBox(height: AppConstants.spaceLg),
                    Container(
                      key: const Key('register_error_banner'),
                      padding: const EdgeInsets.all(AppConstants.spaceMd),
                      decoration: BoxDecoration(
                        color: AppColors.statusErrorBg,
                        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                        border: Border.all(color: AppColors.statusErrorFg.withValues(alpha: 0.3)),
                      ),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Icon(Icons.error_outline_rounded, color: AppColors.statusErrorFg, size: 20),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              activeError,
                              style: const TextStyle(
                                color: AppColors.statusErrorFg,
                                fontSize: 13,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],

                  const SizedBox(height: AppConstants.spaceXl),

                  // Submit Button
                  PrimaryButton(
                    key: const Key('register_submit_button'),
                    label: 'Register as Driver',
                    isLoading: auth.isSubmitting,
                    onPressed: _submit,
                  ),

                  const SizedBox(height: AppConstants.spaceLg),

                  // Back to Login link
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Text(
                        'Already have an account? ',
                        style: TextStyle(color: AppColors.inkMuted, fontSize: 13),
                      ),
                      GestureDetector(
                        onTap: () => Navigator.of(context).pop(),
                        child: const Text(
                          'Sign in',
                          style: TextStyle(
                            color: AppColors.primary,
                            fontSize: 13,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppConstants.spaceLg),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}
