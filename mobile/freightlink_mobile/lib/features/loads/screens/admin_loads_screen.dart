import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/empty_state.dart';
import '../../../shared/widgets/error_state.dart';
import '../data/loads_repository.dart';
import '../providers/loads_list_provider.dart';
import '../widgets/load_card.dart';
import '../widgets/load_list_skeleton.dart';
import '../widgets/load_search_bar.dart';
import '../widgets/load_status_filter_sheet.dart';
import 'load_detail_screen.dart';

/// **All Loads (Admin)** — every shipper's loads
/// (`LoadsController.GetList` returns everything for an Admin caller, no
/// separate query param needed). Read-only for now: `PATCH .../status` is
/// Shipper-only in the backend, so there's no cancel/flag action here — see
/// the implementation plan's scope note.
///
/// Reachable via a temporary dev entry point (a menu item on the Dashboard
/// placeholder tab) rather than real role-based routing, since login/roles
/// have no dedicated Admin flow yet.
class AdminLoadsScreen extends StatelessWidget {
  const AdminLoadsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          LoadsListProvider(context.read<LoadsRepository>())..load(),
      child: const _AdminLoadsBody(),
    );
  }
}

class _AdminLoadsBody extends StatelessWidget {
  const _AdminLoadsBody();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<LoadsListProvider>();

    return Scaffold(
      appBar: AppTopBar(
        title: 'All Loads',
        showBackButton: true,
        actions: const [NotificationBellButton()],
      ),
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppConstants.spaceLg,
                AppConstants.spaceLg,
                AppConstants.spaceLg,
                0,
              ),
              child: LoadSearchBar(
                hintText: 'Search load ID, route...',
                onChanged: provider.onSearchChanged,
                isFilterActive: provider.statusFilter != null,
                onFilterTap: () => showLoadStatusFilterSheet(context, provider),
              ),
            ),
            Expanded(child: _buildBody(context, provider)),
          ],
        ),
      ),
    );
  }

  Widget _buildBody(BuildContext context, LoadsListProvider provider) {
    switch (provider.state) {
      case LoadsListState.loading:
        return const LoadListSkeleton();
      case LoadsListState.error:
        return ErrorState(
          title: 'Unable to load data',
          message: provider.error?.message ?? 'Something went wrong.',
          errorCode: provider.error?.code,
          onRetry: provider.load,
        );
      case LoadsListState.empty:
        return const EmptyState(
          icon: Icons.local_shipping_outlined,
          title: 'No loads found',
        );
      case LoadsListState.loaded:
        return RefreshIndicator(
          onRefresh: provider.load,
          child: ListView.separated(
            padding: const EdgeInsets.all(AppConstants.spaceLg),
            itemCount: provider.items.length,
            separatorBuilder: (_, _) =>
                const SizedBox(height: AppConstants.spaceLg),
            itemBuilder: (context, index) {
              final load = provider.items[index];
              return LoadCard(
                load: load,
                showShipper: true,
                onTap: () => Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => LoadDetailScreen(loadId: load.loadId),
                  ),
                ),
              );
            },
          ),
        );
    }
  }
}
