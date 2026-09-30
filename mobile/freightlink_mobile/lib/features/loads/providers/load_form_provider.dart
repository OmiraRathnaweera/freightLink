import 'package:flutter/foundation.dart';
import 'package:latlong2/latlong.dart';

import '../../../core/network/api_exception.dart';
import '../data/loads_repository.dart';
import '../models/load.dart';
import '../models/load_validation_rules.dart';

/// Shared by **Post a Load** and **Edit Load** — same field set
/// (`CreateLoadDto`/`UpdateLoadDto` only differ by `postImmediately`, which
/// this provider doesn't need since publishing is a separate status-change
/// action from Load Detail). Drives the mockups' default / validation-error
/// / submitting states.
class LoadFormProvider extends ChangeNotifier {
  LoadFormProvider(this._repository, {Load? initial}) : editing = initial {
    if (initial != null) {
      cargoDescription = initial.cargoDescription;
      weightKgText = _trimZeros(initial.weightKg);
      volumeM3Text = _trimZeros(initial.volumeM3);
      pickupAddress = initial.pickupAddress;
      pickupPoint = LatLng(initial.pickupLat, initial.pickupLng);
      dropoffAddress = initial.dropoffAddress;
      dropoffPoint = LatLng(initial.dropoffLat, initial.dropoffLng);
      pickupWindowStart = initial.pickupWindowStart;
      pickupWindowEnd = initial.pickupWindowEnd;
    }
  }

  final LoadsRepository _repository;

  /// Non-null when editing an existing load (PUT); null when creating (POST).
  final Load? editing;

  bool get isEditing => editing != null;

  String cargoDescription = '';
  String weightKgText = '';
  String volumeM3Text = '';
  String pickupAddress = '';
  LatLng? pickupPoint;
  String dropoffAddress = '';
  LatLng? dropoffPoint;
  DateTime? pickupWindowStart;
  DateTime? pickupWindowEnd;

  bool _isSubmitting = false;
  bool _hasAttemptedSubmit = false;
  Map<String, String> _fieldErrors = {};
  String? _formError;
  Load? _result;
  LoadAttachment? _attachment;
  String? _attachmentWarning;

  bool get isSubmitting => _isSubmitting;
  String? get formError => _formError;
  Load? get result => _result;
  LoadAttachment? get attachment => _attachment;
  String? get attachmentWarning => _attachmentWarning;
  String? errorFor(String field) =>
      _hasAttemptedSubmit ? _fieldErrors[field] : null;

  static String _trimZeros(double value) {
    final text = value.toString();
    return text.endsWith('.0') ? text.substring(0, text.length - 2) : text;
  }

  void setCargoDescription(String value) {
    cargoDescription = value;
    _revalidateIfDirty();
    notifyListeners();
  }

  void setWeightKgText(String value) {
    weightKgText = value;
    _revalidateIfDirty();
    notifyListeners();
  }

  void setVolumeM3Text(String value) {
    volumeM3Text = value;
    _revalidateIfDirty();
    notifyListeners();
  }

  void setPickup(String address, LatLng point) {
    pickupAddress = address;
    pickupPoint = point;
    _revalidateIfDirty();
    notifyListeners();
  }

  void setDropoff(String address, LatLng point) {
    dropoffAddress = address;
    dropoffPoint = point;
    _revalidateIfDirty();
    notifyListeners();
  }

  void setPickupWindow(DateTime start, DateTime end) {
    pickupWindowStart = start;
    pickupWindowEnd = end;
    _revalidateIfDirty();
    notifyListeners();
  }

  void setAttachment(LoadAttachment? value) {
    _attachment = value;
    _attachmentWarning = null;
    notifyListeners();
  }

  /// Once the user has attempted a submit, re-validate on every field change
  /// too — so a field's error clears (or updates) as soon as it's fixed,
  /// instead of only re-checking on the next submit attempt.
  void _revalidateIfDirty() {
    if (!_hasAttemptedSubmit) return;
    _fieldErrors = _validate();
    // Clear the top banner once every field checks out; leave it alone
    // otherwise so it doesn't flicker between the generic message and a
    // stale server-side one while the user is still fixing fields.
    if (_fieldErrors.isEmpty) _formError = null;
  }

  /// Client-side validation mirroring **every** rule the backend enforces on
  /// `CreateLoadDto`/`UpdateLoadDto` — both the DataAnnotations
  /// (`[Required]`, `[StringLength]`, `[Range]`) and the two cross-field
  /// checks `LoadService` enforces itself (`ValidatePickupWindow`,
  /// `ValidateDistinctPoints`) — so nothing the API would reject can slip
  /// past this form only to fail on submit.
  Map<String, String> _validate() {
    final errors = <String, String>{};

    // CargoDescription: [Required], [StringLength(1000, MinimumLength = 3)]
    final cargo = cargoDescription.trim();
    if (cargo.isEmpty) {
      errors['cargoDescription'] = 'Cargo description is required.';
    } else if (cargo.length < LoadValidationRules.cargoDescriptionMinLength) {
      errors['cargoDescription'] =
          'Enter at least '
          '${LoadValidationRules.cargoDescriptionMinLength} characters.';
    } else if (cargo.length > LoadValidationRules.cargoDescriptionMaxLength) {
      errors['cargoDescription'] =
          'Must be ${LoadValidationRules.cargoDescriptionMaxLength} '
          'characters or fewer.';
    }

    errors.addEntries(
      _validateNumber(
        field: 'weightKg',
        label: 'Weight',
        text: weightKgText,
        min: LoadValidationRules.minWeightKg,
        max: LoadValidationRules.maxWeightKg,
      ).entries,
    );

    errors.addEntries(
      _validateNumber(
        field: 'volumeM3',
        label: 'Volume',
        text: volumeM3Text,
        min: LoadValidationRules.minVolumeM3,
        max: LoadValidationRules.maxVolumeM3,
      ).entries,
    );

    final pickupError = _validateLocation(
      label: 'pickup',
      address: pickupAddress,
      point: pickupPoint,
    );
    if (pickupError != null) errors['pickupAddress'] = pickupError;

    final dropoffError = _validateLocation(
      label: 'delivery',
      address: dropoffAddress,
      point: dropoffPoint,
    );
    if (dropoffError != null) errors['dropoffAddress'] = dropoffError;

    // Cross-field: LoadService.ValidateDistinctPoints
    // (LOAD_PICKUP_DROPOFF_IDENTICAL) — only meaningful once both points are
    // individually valid.
    if (pickupError == null &&
        dropoffError == null &&
        pickupPoint!.latitude == dropoffPoint!.latitude &&
        pickupPoint!.longitude == dropoffPoint!.longitude) {
      errors['dropoffAddress'] =
          'Pickup and delivery locations must be different.';
    }

    // PickupWindowStart/End: [Required] each, plus LoadService's
    // ValidatePickupWindow (INVALID_PICKUP_WINDOW): end must be after start.
    if (pickupWindowStart == null) {
      errors['pickupWindowStart'] = 'Pickup window start is required.';
    }
    if (pickupWindowEnd == null) {
      errors['pickupWindowEnd'] = 'Pickup window end is required.';
    }
    if (pickupWindowStart != null &&
        pickupWindowEnd != null &&
        !pickupWindowEnd!.isAfter(pickupWindowStart!)) {
      errors['pickupWindowEnd'] = 'Pickup window end must be after the start.';
    }

    return errors;
  }

  /// Validates one numeric field: required, parses as a number, and falls
  /// within [min]/[max] — mirrors a `[Required]` + `[Range]` pair.
  Map<String, String> _validateNumber({
    required String field,
    required String label,
    required String text,
    required double min,
    required double max,
  }) {
    final trimmed = text.trim();
    if (trimmed.isEmpty) {
      return {field: '$label is required.'};
    }
    final value = double.tryParse(trimmed);
    if (value == null) {
      return {field: 'Enter a valid number.'};
    }
    if (value < min) {
      return {field: '$label must be at least $min.'};
    }
    if (value > max) {
      return {field: '$label must be $max or less.'};
    }
    return {};
  }

  /// Validates one address+coordinate pair: required, within
  /// [LoadValidationRules.addressMinLength]/`addressMaxLength`, and its
  /// coordinates within the valid lat/lng range — mirrors a `[Required]` +
  /// `[StringLength]` + two `[Range]`s (lat, lng). Returns the error
  /// message, or null if valid.
  String? _validateLocation({
    required String label,
    required String address,
    required LatLng? point,
  }) {
    final trimmed = address.trim();
    if (trimmed.isEmpty || point == null) {
      return 'Choose a $label location.';
    }
    if (trimmed.length < LoadValidationRules.addressMinLength) {
      return 'Enter at least '
          '${LoadValidationRules.addressMinLength} characters.';
    }
    if (trimmed.length > LoadValidationRules.addressMaxLength) {
      return 'Must be ${LoadValidationRules.addressMaxLength} characters '
          'or fewer.';
    }
    if (!LoadValidationRules.isValidLatitude(point.latitude) ||
        !LoadValidationRules.isValidLongitude(point.longitude)) {
      return 'That location has invalid coordinates — choose another point.';
    }
    return null;
  }

  Future<bool> submit() async {
    _hasAttemptedSubmit = true;
    _fieldErrors = _validate();
    _formError = _fieldErrors.isEmpty
        ? null
        : 'Please fill in all required fields.';
    notifyListeners();

    if (_fieldErrors.isNotEmpty) return false;

    _isSubmitting = true;
    notifyListeners();

    final data = LoadFormData(
      cargoDescription: cargoDescription.trim(),
      weightKg: double.parse(weightKgText),
      volumeM3: double.parse(volumeM3Text),
      pickupAddress: pickupAddress.trim(),
      pickupLat: pickupPoint!.latitude,
      pickupLng: pickupPoint!.longitude,
      dropoffAddress: dropoffAddress.trim(),
      dropoffLat: dropoffPoint!.latitude,
      dropoffLng: dropoffPoint!.longitude,
      pickupWindowStart: pickupWindowStart!,
      pickupWindowEnd: pickupWindowEnd!,
    );

    try {
      _result = isEditing
          ? await _repository.update(editing!.loadId, data)
          : await _repository.create(data);
      if (!isEditing && _attachment != null) {
        try {
          final publicId = await _repository.uploadFile(_attachment!.bytes, _attachment!.filename);
          await _repository.attachFile(_result!.loadId, publicId, _attachment!.fileType);
        } catch (error) {
          _attachmentWarning = 'Load created, but ${_attachment!.filename} could not be attached: $error';
        }
      }
      return true;
    } on ApiException catch (error) {
      _formError = error.message;
      if (error.isValidationError) {
        for (final fieldError in error.fieldErrors) {
          _fieldErrors[_camelCase(fieldError.field)] = fieldError.issue;
        }
      }
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  /// The API echoes DTO field names as PascalCase (e.g. `"WeightKg"`); our
  /// error map keys are camelCase to match this provider's field ids.
  String _camelCase(String field) {
    if (field.isEmpty) return field;
    return field[0].toLowerCase() + field.substring(1);
  }
}

class LoadAttachment {
  const LoadAttachment({
    required this.filename,
    required this.bytes,
    required this.fileType,
  });

  final String filename;
  final List<int> bytes;
  final String fileType;
}
