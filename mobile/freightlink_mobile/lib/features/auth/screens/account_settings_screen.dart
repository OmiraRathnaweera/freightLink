import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/app_validators.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/primary_button.dart';
import '../../../shared/widgets/section_card.dart';
import '../providers/auth_provider.dart';

/// **Account Settings** — lets any signed-in user (Shipper, Agency Staff,
/// Driver) update their own name/email/phone, or change their password.
/// Reached from [AppAvatar]'s menu, not a go_router route, so it needs no
/// entry in any role's allowed-path map.
class AccountSettingsScreen extends StatelessWidget {
  const AccountSettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: const AppTopBar(title: 'Account Settings', showBackButton: true),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spaceLg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: const [
              _ProfileSection(),
              SizedBox(height: AppConstants.spaceXl),
              _PasswordSection(),
            ],
          ),
        ),
      ),
    );
  }
}

class _ProfileSection extends StatefulWidget {
  const _ProfileSection();

  @override
  State<_ProfileSection> createState() => _ProfileSectionState();
}

class _ProfileSectionState extends State<_ProfileSection> {
  final _fullNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();

  bool _hasAttemptedSubmit = false;
  bool _isPopulated = false;
  Map<String, String> _fieldErrors = {};

  @override
  void dispose() {
    _fullNameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    super.dispose();
  }

  String? _errorFor(String field) => _hasAttemptedSubmit ? _fieldErrors[field] : null;

  /// Mirrors `UpdateProfileRequestDto`'s DataAnnotations — see
  /// `backend/DTOs/Auth/UpdateProfileRequestDto.cs`.
  Map<String, String> _validate() {
    final errors = <String, String>{};

    final fullNameError = AppValidators.length(_fullNameController.text, 'Full name', min: 2, max: 200);
    if (fullNameError != null) errors['fullName'] = fullNameError;

    final emailError = AppValidators.email(_emailController.text);
    if (emailError != null) errors['email'] = emailError;

    final phoneError = AppValidators.optionalPhone(_phoneController.text);
    if (phoneError != null) errors['phoneE164'] = phoneError;

    return errors;
  }

  void _revalidateIfDirty() {
    if (!_hasAttemptedSubmit) return;
    setState(() => _fieldErrors = _validate());
  }

  Future<void> _save(AuthProvider auth) async {
    setState(() {
      _hasAttemptedSubmit = true;
      _fieldErrors = _validate();
    });
    if (_fieldErrors.isNotEmpty) return;

    final success = await auth.updateProfile(
      fullName: _fullNameController.text.trim(),
      email: _emailController.text.trim(),
      phoneE164: _phoneController.text.trim(),
    );

    if (!mounted) return;
    if (success) {
      setState(() => _hasAttemptedSubmit = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Profile updated successfully.')),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(auth.errorMessage ?? 'Failed to update profile. Please try again.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final user = auth.user;

    if (!_isPopulated && user != null) {
      _fullNameController.text = user.fullName;
      _emailController.text = user.email;
      _phoneController.text = user.phoneE164 ?? '';
      _isPopulated = true;
    }

    return SectionCard(
      title: 'Profile',
      icon: Icons.person_outline,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          AppTextField(
            label: 'Full Name',
            controller: _fullNameController,
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
            prefixIcon: Icons.mail_outline_rounded,
            keyboardType: TextInputType.emailAddress,
            textInputAction: TextInputAction.next,
            maxLength: 256,
            errorText: _errorFor('email'),
            onChanged: (_) => _revalidateIfDirty(),
          ),
          const SizedBox(height: 4),
          const Text(
            'Changing your email will require you to verify it again.',
            style: TextStyle(fontSize: 11, color: AppColors.inkMuted),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          AppTextField(
            label: 'Phone (E.164) — optional',
            controller: _phoneController,
            hintText: '+14155552671',
            prefixIcon: Icons.phone_outlined,
            keyboardType: TextInputType.phone,
            textInputAction: TextInputAction.done,
            errorText: _errorFor('phoneE164'),
            onChanged: (_) => _revalidateIfDirty(),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          PrimaryButton(
            label: 'Save Changes',
            isLoading: auth.isSubmitting,
            onPressed: () => _save(auth),
          ),
        ],
      ),
    );
  }
}

class _PasswordSection extends StatefulWidget {
  const _PasswordSection();

  @override
  State<_PasswordSection> createState() => _PasswordSectionState();
}

class _PasswordSectionState extends State<_PasswordSection> {
  final _currentPasswordController = TextEditingController();
  final _newPasswordController = TextEditingController();
  bool _obscureCurrent = true;
  bool _obscureNew = true;

  bool _hasAttemptedSubmit = false;
  Map<String, String> _fieldErrors = {};

  @override
  void dispose() {
    _currentPasswordController.dispose();
    _newPasswordController.dispose();
    super.dispose();
  }

  String? _errorFor(String field) => _hasAttemptedSubmit ? _fieldErrors[field] : null;

  Map<String, String> _validate() {
    final errors = <String, String>{};

    final currentError = AppValidators.password(_currentPasswordController.text);
    if (currentError != null) errors['currentPassword'] = currentError;

    final newError = AppValidators.strongPassword(_newPasswordController.text);
    if (newError != null) errors['newPassword'] = newError;

    return errors;
  }

  void _revalidateIfDirty() {
    if (!_hasAttemptedSubmit) return;
    setState(() => _fieldErrors = _validate());
  }

  Future<void> _submit(AuthProvider auth) async {
    setState(() {
      _hasAttemptedSubmit = true;
      _fieldErrors = _validate();
    });
    if (_fieldErrors.isNotEmpty) return;

    final success = await auth.changePassword(
      currentPassword: _currentPasswordController.text,
      newPassword: _newPasswordController.text,
    );

    if (!mounted) return;
    if (success) {
      // changePassword() ends the local session (the backend revokes every
      // active session on a password change), so the router's own redirect
      // logic bounces the whole app back to /login once AuthProvider notifies
      // — this screen doesn't need to navigate itself.
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Password changed. Please sign in again.')),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(auth.errorMessage ?? 'Failed to change password. Please try again.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();

    return SectionCard(
      title: 'Change Password',
      icon: Icons.lock_outline_rounded,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          AppTextField(
            label: 'Current Password',
            controller: _currentPasswordController,
            prefixIcon: Icons.lock_outline_rounded,
            obscureText: _obscureCurrent,
            textInputAction: TextInputAction.next,
            errorText: _errorFor('currentPassword'),
            onChanged: (_) => _revalidateIfDirty(),
            suffixIcon: IconButton(
              icon: Icon(_obscureCurrent ? Icons.visibility_outlined : Icons.visibility_off_outlined, size: 20),
              onPressed: () => setState(() => _obscureCurrent = !_obscureCurrent),
            ),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          AppTextField(
            label: 'New Password',
            controller: _newPasswordController,
            prefixIcon: Icons.lock_outline_rounded,
            obscureText: _obscureNew,
            textInputAction: TextInputAction.done,
            errorText: _errorFor('newPassword'),
            onChanged: (_) => _revalidateIfDirty(),
            suffixIcon: IconButton(
              icon: Icon(_obscureNew ? Icons.visibility_outlined : Icons.visibility_off_outlined, size: 20),
              onPressed: () => setState(() => _obscureNew = !_obscureNew),
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'At least 8 characters, with an uppercase letter, a lowercase letter, a digit, and a special character.',
            style: TextStyle(fontSize: 11, color: AppColors.inkMuted),
          ),
          const SizedBox(height: AppConstants.spaceLg),
          PrimaryButton(
            label: 'Change Password',
            isLoading: auth.isSubmitting,
            onPressed: () => _submit(auth),
          ),
        ],
      ),
    );
  }
}
