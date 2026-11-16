using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FrigoTrack.Common;
using FrigoTrack.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");   // canale HTTP, niente HTTPS: cifra l'applicazione

builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton(_ => new ReadingStore(
    builder.Configuration["DataFile"] ?? Path.Combine(AppContext.BaseDirectory, "data", "readings.json")));

var app = builder.Build();

// ---------------------------------------------------------------- /api/handshake
app.MapPost("/api/handshake", async (HttpContext ctx, SessionStore sessions) =>
{
    var body = await ApiHelpers.ReadJsonAsync(ctx);
    if (body is null)
        return ApiHelpers.Plain(400, "bad_request", "Il corpo non è un JSON valido");

    var errors = SchemaValidator.Validate("handshake-request", body);
    if (errors.Count > 0)
        return ApiHelpers.Plain(400, "bad_request", "Handshake non valido", errors);

    var p = DiffieHellman.FromHex(body["p"]!.GetValue<string>());
    var g = DiffieHellman.FromHex(body["g"]!.GetValue<string>());
    var bigA = DiffieHellman.FromHex(body["A"]!.GetValue<string>());

    // Accettiamo solo il gruppo noto RFC 3526: un client non può imporci parametri deboli.
    if (p != DiffieHellman.P || g != DiffieHellman.G)
        return ApiHelpers.Plain(400, "bad_request", "Parametri non accettati",
            new[] { new FieldError("/p", "Gruppo non supportato: usare RFC 3526 gruppo 14 con g = 2") });

    if (!DiffieHellman.IsValidPublic(bigA, p))
        return ApiHelpers.Plain(400, "bad_request", "Chiave pubblica non valida",
            new[] { new FieldError("/A", "Deve essere compresa tra 2 e p-2") });

    var b = DiffieHellman.GeneratePrivate();                          // segreto del server
    var bigB = DiffieHellman.ComputePublic(g, b, p);                  // B = g^b mod p
    var k = DiffieHellman.ComputeShared(bigA, b, p);                  // K = A^b mod p
    var sessionId = sessions.Create(DiffieHellman.DeriveKey(k));      // b e K non vengono salvati

    return ApiHelpers.JsonResult(200, new JsonObject
    {
        ["session_id"] = sessionId,
        ["B"] = DiffieHellman.ToHex(bigB)
    });
});

// ---------------------------------------------------------------- /api/telemetry
app.MapPost("/api/telemetry", (HttpContext ctx, SessionStore sessions, ReadingStore store) =>
    SecureEndpoint.HandleAsync(ctx, sessions, "telemetry-request", msg =>
    {
        var reading = msg.Deserialize<Reading>(JsonOpts.Options)!;
        store.Add(reading);

        var ack = new JsonObject { ["status"] = "ok", ["sensor_id"] = reading.SensorId };
        var alarm = AlarmRules.Check(reading);
        if (alarm is not null) ack["alarm"] = alarm;
        return new HandlerResult(200, ack);
    }));

// ---------------------------------------------------------------- /api/readings
app.MapPost("/api/readings", (HttpContext ctx, SessionStore sessions, ReadingStore store) =>
    SecureEndpoint.HandleAsync(ctx, sessions, "readings-request", msg =>
    {
        var sensorId = msg["sensor_id"]!.GetValue<string>();
        var from = ApiHelpers.GetDate(msg, "from");
        var to = ApiHelpers.GetDate(msg, "to");

        if (!store.SensorExists(sensorId))
            return new HandlerResult(404,
                ApiHelpers.ErrorBody("not_found", $"Nessuna misura per il sensore {sensorId}"));

        var list = store.Query(sensorId, from, to);
        return new HandlerResult(200, new JsonObject
        {
            ["sensor_id"] = sensorId,
            ["count"] = list.Count,
            ["readings"] = JsonSerializer.SerializeToNode(list, JsonOpts.Options)
        });
    }));

// ---------------------------------------------------------------- /api/close
app.MapPost("/api/close", (HttpContext ctx, SessionStore sessions) =>
    SecureEndpoint.HandleAsync(ctx, sessions, "close-request", _ =>
        new HandlerResult(200, new JsonObject { ["status"] = "closed" }, CloseSession: true)));

// ---------------------------------------------------------------- tutto il resto: 404
app.MapFallback(() => ApiHelpers.Plain(404, "not_found", "Endpoint inesistente"));

app.Run();

// serve a WebApplicationFactory<Program> nei test
public partial class Program { }
