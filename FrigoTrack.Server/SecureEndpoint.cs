using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using FrigoTrack.Common;

namespace FrigoTrack.Server;

public record HandlerResult(int Status, JsonNode Body, bool CloseSession = false);

/// <summary>
/// La pipeline comune a telemetry, readings e close.
/// Regola di sicurezza: PRIMA si decifra, POI si valida; se decifratura/tag falliscono -> 400 e stop.
/// </summary>
public static class SecureEndpoint
{
    public static async Task<IResult> HandleAsync(
        HttpContext ctx, SessionStore sessions, string requestSchema, Func<JsonNode, HandlerResult> handler)
    {
        // 1) il corpo deve essere JSON ed essere una busta valida
        var raw = await ApiHelpers.ReadJsonAsync(ctx);
        if (raw is null)
            return ApiHelpers.Plain(400, "bad_request", "Il corpo non è un JSON valido");

        var envErrors = SchemaValidator.Validate("envelope", raw);
        if (envErrors.Count > 0)
            return ApiHelpers.Plain(400, "bad_request", "Busta non valida", envErrors);

        var env = raw.Deserialize<Envelope>(JsonOpts.Options)!;

        // 2) la sessione esiste? (se no, non abbiamo nessuna chiave: 401)
        if (!sessions.TryGet(env.SessionId, out var session))
            return ApiHelpers.Plain(401, "unauthorized", "Sessione inesistente o chiusa");

        // 3) DECIFRA (il tag GCM garantisce integrità e autenticità)
        string plain;
        try { plain = SecureChannel.Decrypt(env, session.Key); }
        catch (CryptographicException)
        {
            // volutamente generico: non diciamo all'attaccante cosa è andato storto
            return ApiHelpers.Plain(400, "bad_request", "Decifratura o tag non validi");
        }

        // 4) POI valida con lo schema del messaggio applicativo
        JsonNode? msg;
        try { msg = JsonNode.Parse(plain); }
        catch (JsonException)
        {
            return Encrypted(session, 400, ApiHelpers.ErrorBody("bad_request", "Il contenuto cifrato non è JSON"));
        }

        var errors = SchemaValidator.Validate(requestSchema, msg);
        if (errors.Count > 0)
            return Encrypted(session, 400, ApiHelpers.ErrorBody("bad_request", "Messaggio non valido", errors));

        // 5) elabora e rispondi (cifrato)
        var result = handler(msg!);
        var response = Encrypted(session, result.Status, result.Body);
        if (result.CloseSession) sessions.Remove(session.Id);   // dopo aver cifrato la risposta
        return response;
    }

    private static IResult Encrypted(Session s, int status, JsonNode body)
    {
        var env = SecureChannel.Encrypt(s.Id, s.Key, body.ToJsonString());
        return ApiHelpers.JsonResult(status, JsonSerializer.SerializeToNode(env, JsonOpts.Options)!);
    }
}
