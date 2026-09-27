import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/primary_button.dart';
import '../providers/auth_provider.dart';

enum LoginRole {
  shipper('Shipper', 'Sign in to manage your loads', Icons.local_shipping_rounded),
  agencyStaff('Agency Staff', 'Sign in to dispatch and manage fleet loads', Icons.business_rounded),
  driver('Driver', 'Sign in to view your assigned trips & deliveries', Icons.drive_eta_rounded);

  const LoginRole(this.label, this.subtitle, this.icon);
  final String label;
  final String subtitle;
  final IconData icon;
}

/// Login screen supporting distinct roles (Shipper, Agency Staff, Driver)
/// per Y3S01-32 / Y3S01-76 with role-aware subtitles, branding, and testing helpers.
class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, this.initialRole = LoginRole.shipper});

  final LoginRole initialRole;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  late LoginRole _selectedRole;
  bool _obscurePassword = true;

  @override
  void initState() {
    super.initState();
    _selectedRole = widget.initialRole;
  }

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  void _fillCredentials(String email, String password) {
    setState(() {
      _emailController.text = email;
      _passwordController.text = password;
    });
  }

  Future<void> _submit() async {
    final auth = context.read<AuthProvider>();
    await auth.login(
      email: _emailController.text.trim(),
      password: _passwordController.text,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
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
                    Center(
                      child: Container(
                        width: 64,
                        height: 64,
                        alignment: Alignment.center,
                        decoration: BoxDecoration(
                          color: AppColors.primary,
                          borderRadius: BorderRadius.circular(
                            AppConstants.radiusMd,
                          ),
                        ),
                        child: Icon(
                          _selectedRole.icon,
                          color: AppColors.onPrimary,
                          size: 32,
                        ),
                      ),
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    const Text(
                      AppConstants.appName,
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontSize: 24,
                        fontWeight: FontWeight.w700,
                        color: AppColors.ink,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      _selectedRole.subtitle,
                      textAlign: TextAlign.center,
                      style: const TextStyle(fontSize: 14, color: AppColors.inkMuted),
                    ),
                    const SizedBox(height: AppConstants.spaceXl),
                    // Role Selector
                    Container(
                      padding: const EdgeInsets.all(3),
                      decoration: BoxDecoration(
                        color: AppColors.background,
                        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                        border: Border.all(color: AppColors.border),
                      ),
                      child: Row(
                        children: LoginRole.values.map((role) {
                          final isSelected = _selectedRole == role;
                          return Expanded(
                            child: GestureDetector(
                              key: Key('role_tab_${role.name}'),
                              onTap: () => setState(() => _selectedRole = role),
                              child: Container(
                                padding: const EdgeInsets.symmetric(vertical: 8),
                                decoration: BoxDecoration(
                                  color: isSelected ? AppColors.surface : Colors.transparent,
                                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                                  boxShadow: isSelected
                                      ? const [
                                          BoxShadow(
                                            color: Color(0x0A000000),
                                            blurRadius: 4,
                                            offset: Offset(0, 1),
                                          ),
                                        ]
                                      : null,
                                ),
                                child: Text(
                                  role.label,
                                  textAlign: TextAlign.center,
                                  style: TextStyle(
                                    fontSize: 12,
                                    fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
                                    color: isSelected ? AppColors.primary : AppColors.inkMuted,
                                  ),
                                ),
                              ),
                            ),
                          );
                        }).toList(),
                      ),
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    AppTextField(
                      label: 'Email',
                      controller: _emailController,
                      hintText: _selectedRole == LoginRole.driver
                          ? 'driver@freightlink.lk'
                          : 'you@company.com',
                      prefixIcon: Icons.mail_outline_rounded,
                      keyboardType: TextInputType.emailAddress,
                      textInputAction: TextInputAction.next,
                    ),
                    const SizedBox(height: AppConstants.spaceLg),
                    AppTextField(
                      label: 'Password',
                      controller: _passwordController,
                      hintText: '••••••••',
                      prefixIcon: Icons.lock_outline_rounded,
                      obscureText: _obscurePassword,
                      textInputAction: TextInputAction.done,
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
                    // Quick-fill demo helpers
                    const SizedBox(height: AppConstants.spaceSm),
                    Wrap(
                      spacing: 6,
                      runSpacing: 4,
                      children: [
                        if (_selectedRole == LoginRole.driver) ...[
                          _buildQuickFillChip(
                            key: 'fill_driver1_chip',
                            label: 'Demo: Sunil (driver1)',
                            onTap: () => _fillCredentials('driver1@freightlink.lk', 'Password123!'),
                          ),
                          _buildQuickFillChip(
                            key: 'fill_driver2_chip',
                            label: 'Demo: Kamal (driver2)',
                            onTap: () => _fillCredentials('driver2@freightlink.lk', 'Password123!'),
                          ),
                        ] else if (_selectedRole == LoginRole.agencyStaff) ...[
                          _buildQuickFillChip(
                            key: 'fill_agency_chip',
                            label: 'Demo: Kasun (agency)',
                            onTap: () => _fillCredentials('agency@freightlink.lk', 'Password123!'),
                          ),
                        ] else ...[
                          _buildQuickFillChip(
                            key: 'fill_shipper_chip',
                            label: 'Demo: Amara (shipper)',
                            onTap: () => _fillCredentials('user@example.com', 'Password123!'),
                          ),
                        ],
                      ],
                    ),
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
                      label: 'Sign in',
                      isLoading: auth.isSubmitting,
                      onPressed: _submit,
                    ),
                    if (_selectedRole == LoginRole.driver) ...[
                      const SizedBox(height: AppConstants.spaceLg),
                      const Text(
                        key: Key('driver_registration_notice'),
                        'Driver accounts are added by your agency. Contact your dispatcher if you need access.',
                        textAlign: TextAlign.center,
                        style: TextStyle(color: AppColors.inkMuted, fontSize: 13),
                      ),
                    ] else if (_selectedRole == LoginRole.agencyStaff) ...[
                      const SizedBox(height: AppConstants.spaceLg),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Text(
                            "Don't have an account?",
                            style: TextStyle(color: AppColors.inkMuted, fontSize: 13),
                          ),
                          TextButton(
                            onPressed: () => context.go('/register'),
                            child: const Text('Register agency'),
                          ),
                        ],
                      ),
                    ] else ...[
                      const SizedBox(height: AppConstants.spaceLg),
                      const Text(
                        'New Shipper accounts must be registered through the FreightLink web portal.',
                        textAlign: TextAlign.center,
                        style: TextStyle(color: AppColors.inkMuted, fontSize: 13),
                      ),
                    ],
                  ],
                );
              },
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildQuickFillChip({
    required String key,
    required String label,
    required VoidCallback onTap,
  }) {
    return InkWell(
      key: Key(key),
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: AppColors.background,
          borderRadius: BorderRadius.circular(AppConstants.radiusSm),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.flash_on_rounded, size: 12, color: AppColors.primary),
            const SizedBox(width: 4),
            Text(
              label,
              style: const TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w500,
                color: AppColors.inkMuted,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
