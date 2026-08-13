namespace FreightLink.Api.Common.Validation;

/// <summary>
/// Shared numeric bounds for <c>Load</c> request DTOs, kept as a single source of truth so DTO-layer
/// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/> validation stays in sync with
/// the equivalent Postgres CHECK constraints in <c>Data/Configurations/LoadConfiguration.cs</c>.
/// </summary>
public static class LoadRanges
{
    /// <summary>Minimum <c>WeightKg</c>, mirroring <c>ck_load_weight</c> (<c>WeightKg &gt; 0</c>).</summary>
    public const double MinWeightKg = 0.01;

    /// <summary>Maximum <c>WeightKg</c> representable under the column's <c>precision(10,2)</c>.</summary>
    public const double MaxWeightKg = 99999999.99;

    /// <summary>Minimum <c>VolumeM3</c>, mirroring <c>ck_load_volume</c> (<c>VolumeM3 &gt; 0</c>).</summary>
    public const double MinVolumeM3 = 0.001;

    /// <summary>Maximum <c>VolumeM3</c> representable under the column's <c>precision(10,3)</c>.</summary>
    public const double MaxVolumeM3 = 9999999.999;

    /// <summary>Minimum latitude, mirroring <c>ck_load_pickup_lat</c>/<c>ck_load_dropoff_lat</c>.</summary>
    public const double MinLatitude = -90;

    /// <summary>Maximum latitude, mirroring <c>ck_load_pickup_lat</c>/<c>ck_load_dropoff_lat</c>.</summary>
    public const double MaxLatitude = 90;

    /// <summary>Minimum longitude, mirroring <c>ck_load_pickup_lng</c>/<c>ck_load_dropoff_lng</c>.</summary>
    public const double MinLongitude = -180;

    /// <summary>Maximum longitude, mirroring <c>ck_load_pickup_lng</c>/<c>ck_load_dropoff_lng</c>.</summary>
    public const double MaxLongitude = 180;
}
