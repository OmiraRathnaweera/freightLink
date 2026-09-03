/// Every numeric/length bound `LoadFormProvider` validates against, mirrored
/// 1:1 from the backend's `CreateLoadDto`/`UpdateLoadDto` DataAnnotations and
/// `Common/Validation/LoadRanges.cs`, so a client-side rejection always
/// matches what the API would also reject — and nothing the API validates is
/// left unchecked here.
class LoadValidationRules {
  LoadValidationRules._();

  // CargoDescription: [StringLength(1000, MinimumLength = 3)]
  static const cargoDescriptionMinLength = 3;
  static const cargoDescriptionMaxLength = 1000;

  // PickupAddress/DropoffAddress: [StringLength(500, MinimumLength = 5)]
  static const addressMinLength = 5;
  static const addressMaxLength = 500;

  // WeightKg: [Range(LoadRanges.MinWeightKg, LoadRanges.MaxWeightKg)]
  static const minWeightKg = 0.01;
  static const maxWeightKg = 99999999.99;

  // VolumeM3: [Range(LoadRanges.MinVolumeM3, LoadRanges.MaxVolumeM3)]
  static const minVolumeM3 = 0.001;
  static const maxVolumeM3 = 9999999.999;

  // PickupLat/DropoffLat: [Range(LoadRanges.MinLatitude, LoadRanges.MaxLatitude)]
  static const minLatitude = -90.0;
  static const maxLatitude = 90.0;

  // PickupLng/DropoffLng: [Range(LoadRanges.MinLongitude, LoadRanges.MaxLongitude)]
  static const minLongitude = -180.0;
  static const maxLongitude = 180.0;

  static bool isValidLatitude(double value) =>
      value >= minLatitude && value <= maxLatitude;

  static bool isValidLongitude(double value) =>
      value >= minLongitude && value <= maxLongitude;
}
