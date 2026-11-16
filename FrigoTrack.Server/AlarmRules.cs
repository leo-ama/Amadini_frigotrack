using System.Text.Json.Nodes;
using FrigoTrack.Common;

namespace FrigoTrack.Server;

/// <summary>Intervalli di sicurezza per tipo di merce (gradi C).</summary>
public static class AlarmRules
{
    private static readonly Dictionary<string, (double Min, double Max)> Ranges = new()
    {
        ["pharma"] = (2, 8),
        ["food"] = (0, 5),
        ["frozen"] = (-30, -18)
    };

    /// <summary>null = temperatura nei limiti, nessun allarme.</summary>
    public static JsonObject? Check(Reading r)
    {
        if (!Ranges.TryGetValue(r.CargoType, out var range)) return null;

        string? type = r.TemperatureC > range.Max ? "temperature_high"
                     : r.TemperatureC < range.Min ? "temperature_low"
                     : null;
        if (type is null) return null;

        return new JsonObject
        {
            ["type"] = type,
            ["temperature_c"] = r.TemperatureC,
            ["min_c"] = range.Min,
            ["max_c"] = range.Max
        };
    }
}
