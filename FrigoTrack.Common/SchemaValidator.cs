using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Json.Schema;

namespace FrigoTrack.Common;

/// <summary>Carica gli schemi da schemas/*.json e valida i messaggi.</summary>
public static class SchemaValidator
{
    private static readonly ConcurrentDictionary<string, JsonSchema> Cache = new();

    public static JsonSchema Load(string name) => Cache.GetOrAdd(name, n =>
        JsonSchema.FromFile(Path.Combine(AppContext.BaseDirectory, "schemas", n + ".json")));

    /// <summary>Lista vuota = messaggio valido.</summary>
    public static List<FieldError> Validate(string schemaName, JsonNode? instance)
    {
        var errors = new List<FieldError>();
        if (instance is null)
        {
            errors.Add(new FieldError("$", "Messaggio vuoto"));
            return errors;
        }

        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.Hierarchical,
            RequireFormatValidation = true   // senza questo "format": "date-time" verrebbe ignorato
        };

        var result = Load(schemaName).Evaluate(instance, options);
        if (!result.IsValid)
        {
            Collect(result, errors);
            if (errors.Count == 0) errors.Add(new FieldError("$", "Messaggio non conforme allo schema"));
        }
        return errors.Distinct().ToList();
    }

    public static void ValidateOrThrow(string schemaName, JsonNode? instance)
    {
        var errors = Validate(schemaName, instance);
        if (errors.Count > 0) throw new SchemaValidationException(schemaName, errors);
    }

    private static void Collect(EvaluationResults r, List<FieldError> acc)
    {
        if (r.Errors is not null)
        {
            var loc = r.InstanceLocation.ToString();
            foreach (var e in r.Errors)
                acc.Add(new FieldError(loc.Length == 0 ? "$" : loc, e.Value));
        }
        foreach (var d in r.Details) Collect(d, acc);
    }
}
