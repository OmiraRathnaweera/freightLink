import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/primary_button.dart';
import '../../../shared/widgets/status_pill.dart';
import '../data/trips_repository.dart';
import '../models/trip_models.dart';
import '../services/image_capture_service.dart';

/// Screen for Agency Staff to capture a photo at dispatch/pickup using the device camera.
/// Creates a `TripEvidence` row of type `PickupProof` that satisfies the evidence
/// hard-block from Y3S01-74 before the trip can transition to `PickedUp`.
class ProofOfPickupScreen extends StatefulWidget {
  const ProofOfPickupScreen({
    super.key,
    required this.trip,
    this.imageCaptureService,
    this.onPickupConfirmed,
  });

  final TripResponse trip;
  final ImageCaptureService? imageCaptureService;
  final void Function(TripResponse updatedTrip)? onPickupConfirmed;

  @override
  State<ProofOfPickupScreen> createState() => _ProofOfPickupScreenState();
}

class _ProofOfPickupScreenState extends State<ProofOfPickupScreen> {
  late final ImageCaptureService _captureService;
  late TripResponse _currentTrip;

  CapturedImage? _capturedImage;
  bool _isSubmitting = false;
  String? _statusMessage;
  String? _errorMessage;

  final _notesController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _captureService = widget.imageCaptureService ?? DefaultImageCaptureService();
    _currentTrip = widget.trip;
  }

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _capturePhoto() async {
    setState(() => _errorMessage = null);
    try {
      final image = await _captureService.capturePhoto();
      if (image != null && mounted) {
        setState(() => _capturedImage = image);
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _errorMessage = 'Failed to capture photo from camera.');
    }
  }

  Future<void> _pickFromGallery() async {
    setState(() => _errorMessage = null);
    try {
      final image = await _captureService.pickFromGallery();
      if (image != null && mounted) {
        setState(() => _capturedImage = image);
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _errorMessage = 'Failed to select photo from gallery.');
    }
  }

  void _clearCapturedImage() {
    setState(() {
      _capturedImage = null;
      _errorMessage = null;
    });
  }

  Future<void> _submitProofAndConfirmPickup() async {
    if (_capturedImage == null) {
      setState(() => _errorMessage = 'Please capture or select a proof-of-pickup photo first.');
      return;
    }

    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
      _statusMessage = 'Uploading photo...';
    });

    final repository = context.read<TripsRepository>();

    try {
      // Step 1: Upload file to POST /api/v1/files/single
      final uploadResult = await repository.uploadFile(
        bytes: _capturedImage!.bytes,
        filename: _capturedImage!.name,
      );

      if (!mounted) return;
      setState(() => _statusMessage = 'Registering proof of pickup...');

      // Step 2: Link evidence to trip via POST /api/v1/trips/{id}/evidence
      await repository.submitEvidence(
        tripId: _currentTrip.tripId,
        publicId: uploadResult.publicId,
        evidenceType: 'PickupProof',
      );

      if (!mounted) return;
      setState(() => _statusMessage = 'Advancing trip status to PickedUp...');

      // Step 3: Advance status to PickedUp (satisfies Y3S01-74 hard-block)
      final notes = _notesController.text.trim();
      final updatedTrip = await repository.changeTripStatus(
        tripId: _currentTrip.tripId,
        targetStatus: 'PickedUp',
        notes: notes.isNotEmpty ? notes : null,
      );

      if (!mounted) return;
      setState(() {
        _currentTrip = updatedTrip;
        _isSubmitting = false;
        _statusMessage = null;
      });

      widget.onPickupConfirmed?.call(updatedTrip);

      // Show confirmation dialog
      await showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (ctx) => AlertDialog(
          icon: const Icon(
            Icons.check_circle_rounded,
            color: AppColors.statusSuccessFg,
            size: 48,
          ),
          title: const Text('Pickup Confirmed!'),
          content: Text(
            'Proof of pickup was uploaded and verified. Trip #${_currentTrip.tripId.substring(0, 8).toUpperCase()} '
            'has transitioned to PickedUp status.',
          ),
          actions: [
            TextButton(
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
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _statusMessage = null;
        _errorMessage = e.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _statusMessage = null;
        _errorMessage = 'An unexpected error occurred while confirming pickup.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final tripCode = _currentTrip.tripId.length >= 8
        ? _currentTrip.tripId.substring(0, 8).toUpperCase()
        : _currentTrip.tripId;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Proof of Pickup'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: AppConstants.spaceLg),
            child: Center(
              child: StatusPill(
                label: _currentTrip.isPickedUp ? 'PICKED UP' : _currentTrip.status.toUpperCase(),
                foreground: _currentTrip.isPickedUp
                    ? AppColors.statusSuccessFg
                    : AppColors.statusMatchedFg,
                background: _currentTrip.isPickedUp
                    ? AppColors.statusSuccessBg
                    : AppColors.statusMatchedBg,
              ),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spaceLg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (_errorMessage != null) ...[
                _buildErrorBanner(_errorMessage!),
                const SizedBox(height: AppConstants.spaceLg),
              ],

              _buildTripSummaryCard(tripCode),
              const SizedBox(height: AppConstants.spaceLg),

              _buildPolicyNoticeCard(),
              const SizedBox(height: AppConstants.spaceLg),

              _buildCameraCaptureCard(),
              const SizedBox(height: AppConstants.spaceLg),

              _buildNotesCard(),
              const SizedBox(height: AppConstants.spaceXl),

              _buildSubmitButton(),
            ],
          ),
        ),
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
        children: [
          const Icon(Icons.error_outline_rounded, color: AppColors.statusErrorFg, size: 20),
          const SizedBox(width: AppConstants.spaceMd),
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

  Widget _buildTripSummaryCard(String tripCode) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Flexible(
                child: Text(
                  'TRIP #$tripCode',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 0.5,
                    color: AppColors.inkMuted,
                  ),
                ),
              ),
              const SizedBox(width: AppConstants.spaceSm),
              Flexible(
                child: Text(
                  DateFormat('dd MMM yyyy, HH:mm').format(_currentTrip.createdAt),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  textAlign: TextAlign.end,
                  style: const TextStyle(fontSize: 12, color: AppColors.inkMuted),
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spaceMd),

          // Route row
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Icon(Icons.trip_origin_rounded, size: 16, color: AppColors.primary),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  _currentTrip.pickupAddress ?? 'Origin Pickup Yard',
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: AppColors.ink,
                  ),
                ),
              ),
            ],
          ),
          const Padding(
            padding: EdgeInsets.only(left: 7),
            child: SizedBox(
              height: 14,
              child: VerticalDivider(thickness: 2, color: AppColors.border),
            ),
          ),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Icon(Icons.location_on_rounded, size: 16, color: AppColors.statusSuccessFg),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  _currentTrip.dropoffAddress ?? 'Destination Dropoff Point',
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: AppColors.ink,
                  ),
                ),
              ),
            ],
          ),
          const Divider(height: AppConstants.spaceLg, color: AppColors.border),

          // Vehicle & Driver Details
          Row(
            children: [
              Expanded(
                child: _buildResourceBadge(
                  icon: Icons.local_shipping_outlined,
                  label: 'Vehicle',
                  value: _currentTrip.vehicleRegistrationNo ?? _currentTrip.vehicleId,
                ),
              ),
              const SizedBox(width: AppConstants.spaceMd),
              Expanded(
                child: _buildResourceBadge(
                  icon: Icons.person_outline_rounded,
                  label: 'Driver',
                  value: _currentTrip.driverName ?? _currentTrip.driverId,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildResourceBadge({
    required IconData icon,
    required String label,
    required String value,
  }) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppConstants.spaceMd,
        vertical: AppConstants.spaceSm,
      ),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
      ),
      child: Row(
        children: [
          Icon(icon, size: 16, color: AppColors.primary),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label.toUpperCase(),
                  style: const TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w600,
                    color: AppColors.inkMuted,
                  ),
                ),
                Text(
                  value,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: AppColors.ink,
                  ),
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPolicyNoticeCard() {
    final hasProof = _currentTrip.hasPickupProof || _currentTrip.isPickedUp;

    return Container(
      padding: const EdgeInsets.all(AppConstants.spaceMd),
      decoration: BoxDecoration(
        color: hasProof ? AppColors.statusSuccessBg : AppColors.statusMatchedBg,
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        border: Border.all(
          color: (hasProof ? AppColors.statusSuccessFg : AppColors.statusMatchedFg)
              .withValues(alpha: 0.3),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            hasProof ? Icons.verified_rounded : Icons.shield_outlined,
            color: hasProof ? AppColors.statusSuccessFg : AppColors.statusMatchedFg,
            size: 20,
          ),
          const SizedBox(width: AppConstants.spaceMd),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  hasProof ? 'Proof of Pickup Verified' : 'Evidence Hard-Block Policy (Y3S01-74)',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: hasProof ? AppColors.statusSuccessFg : AppColors.statusMatchedFg,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  hasProof
                      ? 'Proof of pickup was captured and linked. The trip is now in PickedUp state.'
                      : 'Per dispatch verification rules, photo evidence must be captured at the pickup site before this trip can advance to PickedUp.',
                  style: TextStyle(
                    fontSize: 12,
                    color: hasProof ? AppColors.statusSuccessFg : AppColors.statusMatchedFg,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCameraCaptureCard() {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Proof-of-Pickup Photo',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Capture a clear photo of the loaded cargo, shipping manifest, or signed waybill.',
            style: TextStyle(fontSize: 13, color: AppColors.inkMuted),
          ),
          const SizedBox(height: AppConstants.spaceLg),

          if (_capturedImage != null) ...[
            // Photo Preview
            ClipRRect(
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
              child: Image.memory(
                Uint8List.fromList(_capturedImage!.bytes),
                height: 220,
                width: double.infinity,
                fit: BoxFit.cover,
                key: const Key('captured_image_preview'),
              ),
            ),
            const SizedBox(height: AppConstants.spaceMd),

            // Metadata Chips
            Wrap(
              spacing: 8,
              runSpacing: 6,
              children: [
                Chip(
                  avatar: const Icon(Icons.image_outlined, size: 14),
                  label: Text(_capturedImage!.formattedSize),
                  visualDensity: VisualDensity.compact,
                ),
                Chip(
                  avatar: const Icon(Icons.access_time_rounded, size: 14),
                  label: Text(DateFormat('HH:mm:ss').format(DateTime.now())),
                  visualDensity: VisualDensity.compact,
                ),
                Chip(
                  avatar: const Icon(Icons.check_circle_outline_rounded, size: 14, color: AppColors.statusSuccessFg),
                  label: const Text('Ready for upload'),
                  visualDensity: VisualDensity.compact,
                ),
              ],
            ),
            const SizedBox(height: AppConstants.spaceMd),

            OutlinedButton.icon(
              key: const Key('retake_photo_button'),
              icon: const Icon(Icons.refresh_rounded, size: 16),
              label: const Text('Retake Photo'),
              onPressed: _isSubmitting ? null : _clearCapturedImage,
            ),
          ] else ...[
            // Empty / Prompt State
            Container(
              height: 180,
              decoration: BoxDecoration(
                color: AppColors.background,
                borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                border: Border.all(
                  color: AppColors.border,
                  style: BorderStyle.solid,
                ),
              ),
              child: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(
                      Icons.camera_alt_outlined,
                      size: 48,
                      color: AppColors.inkMuted.withValues(alpha: 0.6),
                    ),
                    const SizedBox(height: AppConstants.spaceSm),
                    const Text(
                      'No photo captured yet',
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.inkMuted,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: AppConstants.spaceLg),

            Row(
              children: [
                Expanded(
                  flex: 3,
                  child: FilledButton.icon(
                    key: const Key('take_photo_button'),
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                      ),
                    ),
                    icon: const Icon(Icons.camera_alt_rounded, size: 18),
                    label: const Text(
                      'Take Photo',
                      style: TextStyle(fontWeight: FontWeight.w700),
                    ),
                    onPressed: _isSubmitting ? null : _capturePhoto,
                  ),
                ),
                const SizedBox(width: AppConstants.spaceMd),
                Expanded(
                  flex: 2,
                  child: OutlinedButton.icon(
                    key: const Key('pick_gallery_button'),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                      ),
                    ),
                    icon: const Icon(Icons.photo_library_outlined, size: 18),
                    label: const Text('Gallery'),
                    onPressed: _isSubmitting ? null : _pickFromGallery,
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildNotesCard() {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Pickup Notes (Optional)',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: 6),
          TextField(
            key: const Key('pickup_notes_field'),
            controller: _notesController,
            enabled: !_isSubmitting,
            decoration: const InputDecoration(
              hintText: 'e.g. 500 crates loaded, seal #FL-8821 verified intact.',
              border: OutlineInputBorder(),
            ),
            maxLines: 2,
          ),
        ],
      ),
    );
  }

  Widget _buildSubmitButton() {
    return PrimaryButton(
      key: const Key('submit_pickup_proof_button'),
      label: _statusMessage ?? 'Upload Evidence & Confirm Pickup',
      isLoading: _isSubmitting,
      onPressed: (_isSubmitting || _currentTrip.isPickedUp)
          ? null
          : _submitProofAndConfirmPickup,
    );
  }
}
