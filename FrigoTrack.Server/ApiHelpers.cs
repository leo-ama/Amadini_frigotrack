using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FrigoTrack.Common;

namespace FrigoTrack.Server;

public static class ApiHelpers
{
    /// <summary>Legge il corpo come JSON; null se vuoto o malformato.</summary>
    public static async Task<JsonNode?> ReadJsonAsync(HttpContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonNode.Parse(text); }
        catch (JsonException) { return null; }
    }

    public static IResult JsonResult(int status, JsonNode body)
        => Results.Text(body.ToJsonString(), "application/json", Encoding.UTF8, status);

    /// <summary>Corpo conforme a schemas/error.json.</summary>
    public static JsonObject ErrorBody(string error, string message, IEnumerable<FieldError>? details = null)
    {
        var o = new JsonObject { ["error"] = error, ["message"] = message };
        if (details is not null)
        {
            var arr = new JsonArray();
            foreach (var d in details)
                arr.Add(new JsonObject { ["field"] = d.Field, ["message"] = d.Message });
            o["details"] = arr;
        }
        return o;
    }

    /// <summary>Errore in chiaro: solo quando non abbiamo (ancora) una chiave con cui cifrare.</summary>
    public static IResult Plain(int status, string error, string message, IEnumerable<FieldError>? details = null)
        => JsonResult(status, ErrorBody(error, message, details));

    public static DateTimeOffset? GetDate(JsonNode msg, string key)
        => msg[key] is JsonNode n
            ? DateTimeOffset.Parse(n.GetValue<string>(), CultureInfo.InvariantCulture)
            : null;
}
