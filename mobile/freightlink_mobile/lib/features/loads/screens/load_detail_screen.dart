import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/error_state.dart';
import '../../../shared/widgets/primary_button.dart';
import '../../../shared/widgets/section_card.dart';
import '../data/loads_repository.dart';
import '../models/load.dart';
import '../providers/load_detail_provider.dart';
import '../widgets/load_status_badge.dart';
import '../widgets/load_timeline.dart';
import '../widgets/route_map_preview.dart';
import 'edit_load_screen.dart';

/// **Load Detail** — one screen that adapts to the load's real
/// [LoadStatus] (composed from shared sections) rather than one bespoke
/// layout per status, per the implementation plan.
class LoadDetailScreen extends StatelessWidget {
  const LoadDetailScreen({super.key, required this.loadId});

  final String loadId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          LoadDetailProvider(context.read<LoadsRepository>(), loadId)..fetch(),
      child: const _LoadDetailBody(),
    );
  }
}

class _LoadDetailBody extends StatelessWidget {
  const _LoadDetailBody();

  Future<void> _postLoad(
    BuildContext context,
    LoadDetailProvider provider,
  ) async {
    final success = await provider.postLoad();
    if (!success && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(provider.error?.message ?? 'Failed to post load.'),
        ),
      );
    }
  }

  Future<void> _cancelLoad(
    BuildContext context,
    LoadDetailProvider provider,
  ) async {
    final reasonController = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Cancel this load?'),
        content: TextField(
          controller: reasonController,
          autofocus: true,
          decoration: const InputDecoration(
            labelText: 'Reason',
            hintText: 'Why is this load being cancelled?',
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Keep load'),
          ),
          TextButton(
            onPressed: () =>
                Navigator.of(dialogContext).pop(reasonController.text.trim()),
            child: const Text('Cancel load'),
          ),
        ],
      ),
    );
    reasonController.dispose();
    if (reason == null || reason.isEmpty || !context.mounted) return;

    final success = await provider.cancelLoad(reason);
    if (!success && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(provider.error?.message ?? 'Failed to cancel load.'),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<LoadDetailProvider>();
    final load = provider.load;

    return Scaffold(
      appBar: AppTopBar(
        title: load == null ? 'Load Detail' : load.referenceCode,
        subtitle: 'Load Detail',
        showBackButton: true,
        actions: [
          if (load != null && load.status.isEditable)
            IconButton(
              tooltip: 'Edit load',
              icon: const Icon(Icons.edit_outlined),
              onPressed: () async {
                final updated = await Navigator.of(context).push<Load>(
                  MaterialPageRoute(builder: (_) => EditLoadScreen(load: load)),
                );
                if (updated != null) provider.fetch();
              },
            ),
        ],
      ),
      body: SafeArea(child: _buildBody(context, provider)),
      bottomNavigationBar: load == null
          ? null
          : _buildActionBar(context, provider, load),
    );
  }

  Widget _buildBody(BuildContext context, LoadDetailProvider provider) {
    switch (provider.state) {
      case LoadDetailState.loading:
        return const Center(child: CircularProgressIndicator());
      case LoadDetailState.error:
        return ErrorState(
          title: 'Unable to load this load',
          message: provider.error?.message ?? 'Something went wrong.',
          errorCode: provider.error?.code,
          onRetry: provider.fetch,
        );
      case LoadDetailState.loaded:
        return _LoadDetailContent(load: provider.load!);
    }
  }

  Widget? _buildActionBar(
    BuildContext context,
    LoadDetailProvider provider,
    Load load,
  ) {
    final String label;
    final VoidCallback onPressed;
    if (load.status.canPost) {
      label = 'Post Load';
      onPressed = () => _postLoad(context, provider);
    } else if (load.status.isCancellable) {
      label = 'Cancel Load';
      onPressed = () => _cancelLoad(context, provider);
    } else {
      return null;
    }

    return SafeArea(
      child: Container(
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        decoration: const BoxDecoration(
          color: AppColors.surface,
          border: Border(top: BorderSide(color: AppColors.border)),
        ),
        child: PrimaryButton(
          label: label,
          isLoading: provider.isUpdatingStatus,
          onPressed: onPressed,
        ),
      ),
    );
  }
}

class _LoadDetailContent extends StatelessWidget {
  const _LoadDetailContent({required this.load});

  final Load load;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      children: [
        SectionCard(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'LOAD ID',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.inkMuted,
                      ),
                    ),
                    Text(
                      load.referenceCode,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w700,
                        color: AppColors.ink,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: AppConstants.spaceSm),
              LoadStatusBadge(status: load.status),
            ],
          ),
        ),
        const SizedBox(height: AppConstants.spaceLg),
        RouteMapPreview(
          pickup: LatLng(load.pickupLat, load.pickupLng),
          dropoff: LatLng(load.dropoffLat, load.dropoffLng),
        ),
        const SizedBox(height: AppConstants.spaceLg),
        SectionCard(
          title: 'Workflow Status',
          icon: Icons.timeline_outlined,
          child: LoadTimeline(
            history: load.statusHistory,
            currentStatus: load.status,
          ),
        ),
        const SizedBox(height: AppConstants.spaceLg),
        SectionCard(
          title: 'Route & Cargo',
          icon: Icons.local_shipping_outlined,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _RouteRow(
                label: 'Origin',
                address: load.pickupAddress,
                icon: Icons.trip_origin_rounded,
                iconColor: AppColors.primary,
              ),
              const Padding(
                padding: EdgeInsets.only(left: 7),
                child: SizedBox(
                  height: 16,
                  child: VerticalDivider(color: AppColors.border, thickness: 2),
                ),
              ),
              _RouteRow(
                label: 'Destination',
                address: load.dropoffAddress,
                icon: Icons.location_on_rounded,
                iconColor: AppColors.statusErrorFg,
              ),
              const Divider(height: AppConstants.spaceXl),
              Row(
                children: [
                  Expanded(
                    child: _KeyValue(
                      label: 'Weight',
                      value: AppFormatters.weightKg(load.weightKg),
                    ),
                  ),
                  Expanded(
                    child: _KeyValue(
                      label: 'Volume',
                      value: AppFormatters.volumeM3(load.volumeM3),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppConstants.spaceMd),
              _KeyValue(
                label: 'Pickup Window',
                value:
                    '${AppFormatters.dateTime(load.pickupWindowStart)} – '
                    '${AppFormatters.time(load.pickupWindowEnd)}',
              ),
              const SizedBox(height: AppConstants.spaceMd),
              _KeyValue(
                label: 'Cargo Type',
                value: load.cargoDescription,
                valueMaxLines: 3,
              ),
            ],
          ),
        ),
        const SizedBox(height: AppConstants.spaceLg),
        SectionCard(
          title: 'Estimated Revenue',
          icon: Icons.payments_outlined,
          child: Text(
            AppFormatters.currency(load.estimatedPrice),
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
        ),
      ],
    );
  }
}

class _RouteRow extends StatelessWidget {
  const _RouteRow({
    required this.label,
    required this.address,
    required this.icon,
    required this.iconColor,
  });

  final String label;
  final String address;
  final IconData icon;
  final Color iconColor;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 16, color: iconColor),
        const SizedBox(width: AppConstants.spaceSm),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label.toUpperCase(),
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: AppColors.inkMuted,
                ),
              ),
              Text(
                address,
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  color: AppColors.ink,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _KeyValue extends StatelessWidget {
  const _KeyValue({
    required this.label,
    required this.value,
    this.valueMaxLines = 1,
  });

  final String label;
  final String value;
  final int valueMaxLines;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label.toUpperCase(),
          style: const TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w700,
            color: AppColors.inkMuted,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          maxLines: valueMaxLines,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(
            fontWeight: FontWeight.w600,
            color: AppColors.ink,
          ),
        ),
      ],
    );
  }
}
