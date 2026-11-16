using System.Text.Json.Nodes;

namespace FrigoTrack.Common;

/// <summary>Ciò che viaggia in rete dopo l'handshake: tutto in Base64.</summary>
public record Envelope(string SessionId, string Ciphertext, string Tag);

/// <summary>Misura di un sensore (stessa forma di schemas/telemetry-request.json).</summary>
public record Reading(
    string SensorId,
    string Timestamp,
    string CargoType,
    double TemperatureC,
    double? Humidity,
    int? Battery);

/// <summary>Un errore di validazione: quale campo (JSON Pointer, es. /temperature_c) e perché.</summary>
public record FieldError(string Field, string Message);

/// <summary>Risposta del server già decifrata (se era cifrata).</summary>
public record ApiResponse(int Status, JsonNode? Body, bool Encrypted);

public class SchemaValidationException : Exception
{
    public IReadOnlyList<FieldError> Errors { get; }

    public SchemaValidationException(string schema, IReadOnlyList<FieldError> errors)
        : base($"Messaggio non valido per lo schema '{schema}': " +
               string.Join("; ", errors.Select(e => $"{e.Field} -> {e.Message}")))
        => Errors = errors;
}
