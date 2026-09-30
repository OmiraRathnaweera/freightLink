import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_text_field.dart';
import '../../../shared/widgets/auto_resize_text.dart';
import '../../../shared/widgets/primary_button.dart';
import 'package:latlong2/latlong.dart';

import '../../../shared/widgets/section_card.dart';
import '../../../shared/widgets/status_pill.dart';
import '../../loads/widgets/route_map_preview.dart';
import '../data/trips_repository.dart';
import '../models/fleet_resources.dart';
import '../models/job_proposal.dart';
import '../models/trip_models.dart';

/// Screen where Agency Staff reviews an AI-proposed load and assigns a specific
/// vehicle and driver from their fleet using the concurrency-safe endpoint (Y3S01-54).
class AcceptLoadAssignScreen extends StatefulWidget {
  const AcceptLoadAssignScreen({
    super.key,
    this.proposal,
    this.assignmentId,
    this.initialVehicles,
    this.initialDrivers,
    this.onTripCreated,
  }) : assert(proposal != null || assignmentId != null,
            'Either proposal or assignmentId must be provided');

  final JobProposal? proposal;
  final String? assignmentId;
  final List<FleetVehicle>? initialVehicles;
  final List<FleetDriver>? initialDrivers;
  final ValueChanged<TripResponse>? onTripCreated;

  @override
  State<AcceptLoadAssignScreen> createState() => _AcceptLoadAssignScreenState();
}

class _AcceptLoadAssignScreenState extends State<AcceptLoadAssignScreen> {
  JobProposal? _proposal;
  List<FleetVehicle> _vehicles = [];
  List<FleetDriver> _drivers = [];

  FleetVehicle? _selectedVehicle;
  FleetDriver? _selectedDriver;
  final _notesController = TextEditingController();

  bool _isLoading = true;
  bool _isSubmitting = false;
  bool _isDeclining = false;
  String? _errorMessage;

  final _currencyFormat = NumberFormat.currency(
    symbol: 'LKR ',
    decimalDigits: 2,
  );

  @override
  void initState() {
    super.initState();
    _proposal = widget.proposal;
    _vehicles = widget.initialVehicles ?? [];
    _drivers = widget.initialDrivers ?? [];

    if (_proposal != null && _vehicles.isNotEmpty && _drivers.isNotEmpty) {
      _isLoading = false;
      _preselectDefaults();
    } else {
      WidgetsBinding.instance.addPostFrameCallback((_) => _loadData());
    }
  }

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  void _preselectDefaults() {
    if (_selectedVehicle == null && _vehicles.isNotEmpty) {
      final available = _vehicles.where((v) => v.isAvailable).toList();
      if (available.isNotEmpty) {
        _selectedVehicle = available.first;
      }
    }
    if (_selectedDriver == null && _drivers.isNotEmpty) {
      final active = _drivers.where((d) => d.isActive).toList();
      if (active.isNotEmpty) {
        _selectedDriver = active.first;
      }
    }
  }

  Future<void> _loadData() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    final repository = context.read<TripsRepository>();

    try {
      if (_proposal == null && widget.assignmentId != null) {
        _proposal = await repository.getProposalById(widget.assignmentId!);
      }

      if (_vehicles.isEmpty || _drivers.isEmpty) {
        final fleet = await repository.getFleet(agencyId: _proposal?.agencyId);
        _vehicles = fleet.vehicles;
        _drivers = fleet.drivers;
      }

      _preselectDefaults();

      setState(() => _isLoading = false);
    } catch (e) {
      setState(() {
        _isLoading = false;
        _errorMessage = e is ApiException ? e.message : 'Failed to load assignment data.';
      });
    }
  }

  Future<void> _submitAssignment() async {
    if (_proposal == null || _selectedVehicle == null || _selectedDriver == null) return;

    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });

    final repository = context.read<TripsRepository>();

    try {
      final request = CreateTripRequest(
        assignmentId: _proposal!.assignmentId,
        vehicleId: _selectedVehicle!.vehicleId,
        driverId: _selectedDriver!.driverId,
        notes: _notesController.text.trim().isNotEmpty ? _notesController.text.trim() : null,
      );

      final trip = await repository.createTrip(request);

      if (!mounted) return;

      setState(() => _isSubmitting = false);
      widget.onTripCreated?.call(trip);

      // Show success dialog
      await showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (ctx) => AlertDialog(
          icon: const Icon(
            Icons.check_circle_rounded,
            color: AppColors.statusSuccessFg,
            size: 48,
          ),
          title: const Text('Trip Dispatched!'),
          content: Text(
            'Load #${_proposal!.referenceCode ?? _proposal!.loadId.substring(0, 8)} has been assigned '
            'to ${_selectedDriver!.fullName} on vehicle ${_selectedVehicle!.registrationNo}. '
            '${_selectedDriver!.fullName} will start the trip and capture proof of pickup from their app.',
          ),
          actions: [
            FilledButton(
              onPressed: () {
                Navigator.of(ctx).pop();
                Navigator.of(context).pop(true);
              },
              child: const Text('Done'),
            ),
          ],
        ),
      );
    } on ApiException catch (e) {
      setState(() {
        _isSubmitting = false;
        _errorMessage = e.message;
      });
    } catch (e) {
      setState(() {
        _isSubmitting = false;
        _errorMessage = 'An unexpected error occurred while assigning the trip.';
      });
    }
  }

  Future<void> _confirmDecline() async {
    if (_proposal == null) return;

    final reasonController = TextEditingController();

    final shouldDecline = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Decline Job Proposal?'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Declining will inform the shipper and trigger an automatic re-match attempt (ADR-018).',
              style: TextStyle(fontSize: 13, color: AppColors.inkMuted),
            ),
            const SizedBox(height: AppConstants.spaceMd),
            TextField(
              controller: reasonController,
              decoration: const InputDecoration(
                labelText: 'Reason for decline (optional)',
                hintText: 'e.g. Fleet fully booked today',
                border: OutlineInputBorder(),
              ),
              maxLines: 2,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.statusErrorFg),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: const Text('Confirm Decline'),
          ),
        ],
      ),
    );

    if (shouldDecline != true) return;
    if (!mounted) return;

    setState(() {
      _isDeclining = true;
      _errorMessage = null;
    });

    final repository = context.read<TripsRepository>();

    try {
      await repository.declineProposal(
        _proposal!.loadId,
        reason: reasonController.text.trim().isNotEmpty ? reasonController.text.trim() : null,
      );

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Job proposal declined. The shipper has been notified.'),
        ),
      );

      Navigator.of(context).pop(false);
    } on ApiException catch (e) {
      setState(() {
        _isDeclining = false;
        _errorMessage = e.message;
      });
    } catch (e) {
      setState(() {
        _isDeclining = false;
        _errorMessage = 'Failed to decline proposal.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(
          _proposal?.isAccepted == true
              ? 'Job Proposal (Accepted)'
              : _proposal?.isDeclined == true
                  ? 'Job Proposal (Declined)'
                  : 'Accept & Assign Load',
        ),
        actions: [
          if (_proposal != null)
            Padding(
              padding: const EdgeInsets.only(right: AppConstants.spaceLg),
              child: Center(
                child: StatusPill(
                  label: _proposal!.status.displayName,
                  foreground: _proposal!.isProposed
                      ? AppColors.statusMatchedFg
                      : _proposal!.isAccepted
                          ? AppColors.statusSuccessFg
                          : AppColors.statusErrorFg,
                  background: _proposal!.isProposed
                      ? AppColors.statusMatchedBg
                      : _proposal!.isAccepted
                          ? AppColors.statusSuccessBg
                          : AppColors.statusErrorBg,
                ),
              ),
            ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _proposal == null
              ? Center(
                  child: Text(
                    _errorMessage ?? 'Proposal not found.',
                    style: const TextStyle(color: AppColors.inkMuted),
                  ),
                )
              : Column(
                  children: [
                    Expanded(
                      child: SingleChildScrollView(
                        padding: const EdgeInsets.all(AppConstants.spaceLg),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            if (_errorMessage != null) ...[
                              _buildErrorBanner(_errorMessage!),
                              const SizedBox(height: AppConstants.spaceLg),
                            ],

                            if (_proposal!.isProposed) ...[
                              _buildDecisionSupportCard(),
                              const SizedBox(height: AppConstants.spaceLg),
                            ],
                            if (_proposal!.isDeclined) ...[
                              _buildDeclinedInfoCard(),
                              const SizedBox(height: AppConstants.spaceLg),
                            ],
                            if (_proposal!.isAccepted) ...[
                              _buildAcceptedInfoCard(),
                              const SizedBox(height: AppConstants.spaceLg),
                            ],

                            _buildPricingHeroCard(),
                            const SizedBox(height: AppConstants.spaceLg),

                            _buildRouteMapPreview(),
                            const SizedBox(height: AppConstants.spaceLg),

                            _buildCargoAndRouteCard(),
                            const SizedBox(height: AppConstants.spaceLg),

                            if (_proposal!.isProposed) ...[
                              _buildFleetAssignmentCard(),
                              const SizedBox(height: AppConstants.spaceLg),

                              _buildNotesCard(),
                              const SizedBox(height: AppConstants.spaceXl),
                            ],
                          ],
                        ),
                      ),
                    ),
                    _buildBottomActionBar(),
                  ],
                ),
    );
  }

  Widget _buildErrorBanner(String message) {
    return Container(
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
          const SizedBox(width: AppConstants.spaceSm),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(
                color: AppColors.statusErrorFg,
                fontSize: 13,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDecisionSupportCard() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceMd),
      decoration: BoxDecoration(
        color: AppColors.statusMatchedBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        border: Border.all(
          color: AppColors.statusMatchedFg.withValues(alpha: 0.3),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.auto_awesome,
            color: AppColors.statusMatchedFg,
            size: 20,
          ),
          const SizedBox(width: AppConstants.spaceSm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Row(
                  children: [
                    Text(
                      'AI Match Confirmation',
                      style: TextStyle(
                        color: AppColors.statusMatchedFg,
                        fontSize: 13,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    SizedBox(width: 6),
                    Text(
                      '(ADR-017)',
                      style: TextStyle(
                        color: AppColors.inkMuted,
                        fontSize: 11,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 3),
                Text(
                  'FreightLink AI matched this load with your agency\'s operating route and verified capacity. Review the rate, assign an available driver and vehicle from your fleet, and confirm dispatch.',
                  style: TextStyle(
                    color: AppColors.ink.withValues(alpha: 0.85),
                    fontSize: 12,
                    height: 1.35,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDeclinedInfoCard() {
    final respondedStr = _proposal?.respondedAt != null
        ? DateFormat('MMM dd, yyyy • hh:mm a').format(_proposal!.respondedAt!)
        : null;

    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceMd),
      decoration: BoxDecoration(
        color: AppColors.statusErrorBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        border: Border.all(
          color: AppColors.statusErrorFg.withValues(alpha: 0.3),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(
                Icons.cancel_outlined,
                color: AppColors.statusErrorFg,
                size: 20,
              ),
              const SizedBox(width: AppConstants.spaceSm),
              const Expanded(
                child: Text(
                  'Job Proposal Declined',
                  style: TextStyle(
                    color: AppColors.statusErrorFg,
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              if (respondedStr != null)
                Text(
                  respondedStr,
                  style: const TextStyle(
                    color: AppColors.inkMuted,
                    fontSize: 11,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            _proposal?.declineReason != null && _proposal!.declineReason!.isNotEmpty
                ? 'Reason: "${_proposal!.declineReason}"'
                : 'No specific decline reason was provided.',
            style: const TextStyle(
              color: AppColors.ink,
              fontSize: 13,
              fontStyle: FontStyle.italic,
            ),
          ),
          const SizedBox(height: 8),
          const Text(
            'In accordance with ADR-018, this load was returned to the matching engine for automatic re-assignment to the next qualified agency.',
            style: TextStyle(
              color: AppColors.inkMuted,
              fontSize: 11,
              height: 1.3,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildAcceptedInfoCard() {
    final respondedStr = _proposal?.respondedAt != null
        ? DateFormat('MMM dd, yyyy • hh:mm a').format(_proposal!.respondedAt!)
        : null;

    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceMd),
      decoration: BoxDecoration(
        color: AppColors.statusSuccessBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        border: Border.all(
          color: AppColors.statusSuccessFg.withValues(alpha: 0.3),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(
                Icons.check_circle_outline,
                color: AppColors.statusSuccessFg,
                size: 20,
              ),
              const SizedBox(width: AppConstants.spaceSm),
              const Expanded(
                child: Text(
                  'Job Proposal Accepted',
                  style: TextStyle(
                    color: AppColors.statusSuccessFg,
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              if (respondedStr != null)
                Text(
                  respondedStr,
                  style: const TextStyle(
                    color: AppColors.inkMuted,
                    fontSize: 11,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 8),
          const Text(
            'This job proposal has already been accepted and assigned to your agency fleet. Driver dispatch and trip execution are currently active or completed.',
            style: TextStyle(
              color: AppColors.ink,
              fontSize: 12,
              height: 1.35,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPricingHeroCard() {
    final priceStr = _currencyFormat.format(_proposal!.proposedPrice);
    final distanceStr = _proposal!.routedDistanceKm != null
        ? '${_proposal!.routedDistanceKm!.toStringAsFixed(1)} km'
        : 'Routed';
    final durationStr = _proposal!.formattedDuration;

    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.ink,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'PROPOSED AGENCY PAYOUT',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.6,
              color: AppColors.inkFaint,
            ),
          ),
          const SizedBox(height: 6),
          AutoResizeText.kpi(
            priceStr,
            minFontSize: 16,
            style: const TextStyle(
              fontSize: 26,
              fontWeight: FontWeight.w800,
              color: AppColors.surface,
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              const Icon(Icons.navigation_rounded, size: 14, color: AppColors.statusMatchedBg),
              const SizedBox(width: 4),
              Flexible(
                child: AutoResizeText(
                  '$distanceStr • $durationStr transit',
                  maxLines: 1,
                  minFontSize: 10,
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.statusMatchedBg,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
              if (_proposal!.referenceCode != null) ...[
                const SizedBox(width: AppConstants.spaceSm),
                Flexible(
                  child: AutoResizeText(
                    _proposal!.referenceCode!,
                    maxLines: 1,
                    minFontSize: 10,
                    textAlign: TextAlign.end,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.inkFaint,
                    ),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildRouteMapPreview() {
    var pLat = _proposal!.pickupLat ?? 6.9271;
    var pLng = _proposal!.pickupLng ?? 79.8612;
    var dLat = _proposal!.dropoffLat ?? 7.2906;
    var dLng = _proposal!.dropoffLng ?? 80.6337;

    // Guard against identical coordinates so CameraFit bounds calculation never collapses.
    if ((pLat - dLat).abs() < 0.0001 && (pLng - dLng).abs() < 0.0001) {
      dLat += 0.01;
      dLng += 0.01;
    }

    return RouteMapPreview(
      pickup: LatLng(pLat, pLng),
      dropoff: LatLng(dLat, dLng),
      height: 190,
    );
  }

  Widget _buildCargoAndRouteCard() {
    final weightStr = _proposal!.weightKg != null
        ? '${_proposal!.weightKg!.toStringAsFixed(0)} kg'
        : 'N/A';
    final volumeStr = _proposal!.volumeM3 != null
        ? '${_proposal!.volumeM3!.toStringAsFixed(1)} m³'
        : 'N/A';

    return SectionCard(
      title: 'Cargo & Routing Summary',
      icon: Icons.inventory_2_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            _proposal!.cargoDescription,
            style: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: AppConstants.spaceSm),
          Wrap(
            spacing: AppConstants.spaceSm,
            runSpacing: AppConstants.spaceXs,
            children: [
              _buildMetricChip(Icons.scale_rounded, weightStr),
              _buildMetricChip(Icons.view_in_ar_rounded, volumeStr),
              if (_proposal!.shipperName != null)
                _buildMetricChip(Icons.business_rounded, _proposal!.shipperName!),
            ],
          ),
          const Divider(height: AppConstants.spaceXl, color: AppColors.border),
          // Route Steps
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Column(
                children: [
                  const Icon(Icons.radio_button_checked_rounded,
                      size: 16, color: AppColors.statusSuccessFg),
                  Container(
                    width: 2,
                    height: 36,
                    color: AppColors.border,
                  ),
                  const Icon(Icons.location_on_rounded,
                      size: 16, color: AppColors.statusErrorFg),
                ],
              ),
              const SizedBox(width: AppConstants.spaceMd),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      _proposal!.pickupAddress,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.ink,
                      ),
                    ),
                    if (_proposal!.pickupWindowStart != null)
                      Text(
                        'Pickup Window: ${DateFormat('MMM d, h:mm a').format(_proposal!.pickupWindowStart!)}',
                        style: const TextStyle(fontSize: 12, color: AppColors.inkMuted),
                      ),
                    const SizedBox(height: 18),
                    Text(
                      _proposal!.dropoffAddress,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.ink,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildMetricChip(IconData icon, String text) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 13, color: AppColors.inkMuted),
          const SizedBox(width: 4),
          Text(
            text,
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w500,
              color: AppColors.ink,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildFleetAssignmentCard() {
    final availableVehicles = _vehicles.where((v) => v.isAvailable).toList();
    final activeDrivers = _drivers.where((d) => d.isActive).toList();

    final hasInsufficientCapacity = _selectedVehicle != null &&
        _proposal?.weightKg != null &&
        _selectedVehicle!.capacityKg < _proposal!.weightKg!;

    final hasAdequateCapacity = _selectedVehicle != null &&
        _proposal?.weightKg != null &&
        _selectedVehicle!.capacityKg >= _proposal!.weightKg!;

    return SectionCard(
      title: 'Fleet Assignment',
      icon: Icons.local_shipping_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Vehicle selection dropdown
          const Text(
            'ASSIGN VEHICLE',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.4,
              color: AppColors.inkMuted,
            ),
          ),
          const SizedBox(height: 6),
          if (availableVehicles.isEmpty)
            const Text(
              'No available vehicles currently in fleet.',
              style: TextStyle(color: AppColors.statusErrorFg, fontSize: 13),
            )
          else
            DropdownButtonFormField<FleetVehicle>(
              key: const Key('vehicle_dropdown'),
              initialValue: _selectedVehicle,
              isExpanded: true,
              decoration: InputDecoration(
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                  borderSide: const BorderSide(color: AppColors.border),
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
              ),
              items: availableVehicles.map((vehicle) {
                return DropdownMenuItem<FleetVehicle>(
                  value: vehicle,
                  child: Text(
                    vehicle.summary,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 14, color: AppColors.ink),
                  ),
                );
              }).toList(),
              onChanged: (val) {
                setState(() => _selectedVehicle = val);
              },
            ),

          if (hasInsufficientCapacity) ...[
            const SizedBox(height: 6),
            Row(
              children: [
                const Icon(Icons.warning_amber_rounded, size: 14, color: AppColors.statusErrorFg),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    'Warning: Vehicle capacity (${_selectedVehicle!.capacityKg.toInt()} kg) '
                    'is below cargo weight (${_proposal!.weightKg!.toInt()} kg).',
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.statusErrorFg,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ),
              ],
            ),
          ] else if (hasAdequateCapacity) ...[
            const SizedBox(height: 6),
            Row(
              children: [
                const Icon(Icons.check_circle_outline_rounded,
                    size: 14, color: AppColors.statusSuccessFg),
                const SizedBox(width: 4),
                Expanded(
                  child: AutoResizeText(
                    'Capacity Verified (${_selectedVehicle!.capacityKg.toInt()} kg max)',
                    maxLines: 1,
                    minFontSize: 10,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.statusSuccessFg,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ),
              ],
            ),
          ],

          const SizedBox(height: AppConstants.spaceLg),

          // Driver selection dropdown
          const Text(
            'ASSIGN DRIVER',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.4,
              color: AppColors.inkMuted,
            ),
          ),
          const SizedBox(height: 6),
          if (activeDrivers.isEmpty)
            const Text(
              'No active drivers available in agency.',
              style: TextStyle(color: AppColors.statusErrorFg, fontSize: 13),
            )
          else
            DropdownButtonFormField<FleetDriver>(
              key: const Key('driver_dropdown'),
              initialValue: _selectedDriver,
              isExpanded: true,
              decoration: InputDecoration(
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                  borderSide: const BorderSide(color: AppColors.border),
                ),
                contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
              ),
              items: activeDrivers.map((driver) {
                return DropdownMenuItem<FleetDriver>(
                  value: driver,
                  child: Text(
                    driver.summary,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 14, color: AppColors.ink),
                  ),
                );
              }).toList(),
              onChanged: (val) {
                setState(() => _selectedDriver = val);
              },
            ),
        ],
      ),
    );
  }

  Widget _buildNotesCard() {
    return SectionCard(
      title: 'Dispatch Instructions',
      icon: Icons.note_alt_outlined,
      child: AppTextField(
        label: 'Driver Notes & Special Instructions (Optional)',
        controller: _notesController,
        hintText: 'e.g. Enter Gate 4 at Port, check temperature seal upon loading',
        maxLines: 2,
      ),
    );
  }

  Widget _buildBottomActionBar() {
    if (_proposal?.isAccepted == true) {
      return Container(
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        decoration: BoxDecoration(
          color: AppColors.surface,
          border: const Border(top: BorderSide(color: AppColors.border)),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.04),
              blurRadius: 10,
              offset: const Offset(0, -4),
            ),
          ],
        ),
        child: SafeArea(
          top: false,
          child: Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  key: const Key('back_button'),
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                    ),
                  ),
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('Back to Trips'),
                ),
              ),
            ],
          ),
        ),
      );
    }

    if (_proposal?.isDeclined == true) {
      return Container(
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        decoration: BoxDecoration(
          color: AppColors.surface,
          border: const Border(top: BorderSide(color: AppColors.border)),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.04),
              blurRadius: 10,
              offset: const Offset(0, -4),
            ),
          ],
        ),
        child: SafeArea(
          top: false,
          child: Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  key: const Key('back_button'),
                  style: OutlinedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                    ),
                  ),
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('Back to Proposals'),
                ),
              ),
            ],
          ),
        ),
      );
    }

    final canSubmit = _selectedVehicle != null &&
        _selectedDriver != null &&
        !_isSubmitting &&
        !_isDeclining;

    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: const Border(top: BorderSide(color: AppColors.border)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 10,
            offset: const Offset(0, -4),
          ),
        ],
      ),
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            Expanded(
              flex: 1,
              child: OutlinedButton(
                key: const Key('decline_button'),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.statusErrorFg,
                  side: const BorderSide(color: AppColors.statusErrorFg),
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                  ),
                ),
                onPressed: _isSubmitting || _isDeclining ? null : _confirmDecline,
                child: _isDeclining
                    ? const SizedBox(
                        height: 18,
                        width: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text(
                        'Decline',
                        style: TextStyle(fontWeight: FontWeight.w600),
                      ),
              ),
            ),
            const SizedBox(width: AppConstants.spaceMd),
            Expanded(
              flex: 2,
              child: PrimaryButton(
                key: const Key('accept_assign_button'),
                label: 'Accept & Assign Trip',
                isLoading: _isSubmitting,
                onPressed: canSubmit ? _submitAssignment : null,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
