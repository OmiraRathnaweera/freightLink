import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';

import 'core/constants/app_constants.dart';
import 'core/theme/app_theme.dart';
import 'core/routing/app_router.dart';
import 'features/auth/providers/auth_provider.dart';
import 'features/loads/data/loads_repository.dart';
import 'features/agencies/data/agencies_repository.dart';

void main() {
  final authProvider = AuthProvider()..bootstrap();
  final appRouter = createAppRouter(authProvider);

  runApp(FreightLinkApp(authProvider: authProvider, appRouter: appRouter));
}

class FreightLinkApp extends StatelessWidget {
  const FreightLinkApp({
    super.key,
    required this.authProvider,
    required this.appRouter,
  });

  final AuthProvider authProvider;
  final GoRouter appRouter;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: authProvider),
        // LoadsRepository wraps AuthProvider's single ApiClient instance, so
        // it always sees the current auth token without being rebuilt on
        // every request.
        ProxyProvider<AuthProvider, LoadsRepository>(
          update: (_, auth, _) => LoadsRepository(auth.apiClient),
        ),
        ProxyProvider<AuthProvider, AgenciesRepository>(
          update: (_, auth, _) => AgenciesRepository(auth.apiClient),
        ),
      ],
      child: MaterialApp.router(
        title: AppConstants.appName,
        debugShowCheckedModeBanner: false,
        theme: AppTheme.light,
        routerConfig: appRouter,
      ),
    );
  }
}
