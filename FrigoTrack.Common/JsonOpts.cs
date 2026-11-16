using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrigoTrack.Common;

public static class JsonOpts
{
    // SessionId -> session_id, TemperatureC -> temperature_c ...
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions Pretty = new(Options) { WriteIndented = true };
}
