using System.Globalization;
using System.Text.Json.Nodes;
using FrigoTrack.Common;

// Uso:  FrigoTrack.Client [http://localhost:5000] [--invalid]
//   --invalid  il primo sensore invia anche una misura sbagliata SALTANDO il controllo lato client,
//              così si vede il server che la rifiuta (utile per la demo).
var baseUrl = args.FirstOrDefault(a => a.StartsWith("http")) ?? "http://localhost:5000";
var demoInvalid = args.Contains("--invalid");
const int Measures = 6;       // misure per sensore
const int IntervalMs = 2000;  // intervallo regolare tra una misura e l'altra

var sensors = new[]
{
    new SensorDef("CELLA-BG-007", "pharma", 5.0),
    new SensorDef("CAMION-MI-012", "food", 3.0),
    new SensorDef("CELLA-BS-003", "frozen", -20.0)
};

Console.WriteLine($"Avvio {sensors.Length} sensori verso {baseUrl}\n");
await Task.WhenAll(sensors.Select((s, i) => RunSensorAsync(s, demoInvalid && i == 0)));
Console.WriteLine("\nFatto.");

async Task RunSensorAsync(SensorDef s, bool sendInvalid)
{
    void Log(string m) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {s.Id,-14} {m}");

    using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    var client = new SecureClient(http);   // ogni sensore ha la SUA sessione e la SUA chiave
    var rnd = new Random();

    try
    {
        // 1) handshake PRIMA di inviare qualsiasi dato
        await client.HandshakeAsync();
        Log($"handshake ok, sessione {client.SessionId}");

        int battery = 100;
        for (int i = 0; i < Measures; i++)
        {
            // ogni tanto (1 volta su 5) una "escursione termica" che farà scattare l'allarme
            var temp = Math.Round(s.BaseTemp + (rnd.NextDouble() - 0.5) * 3 + (rnd.Next(5) == 0 ? 6 : 0), 1);
            battery -= rnd.Next(0, 3);

            var msg = new JsonObject
            {
                ["sensor_id"] = s.Id,
                ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["cargo_type"] = s.Cargo,
                ["temperature_c"] = temp,
                ["humidity"] = rnd.Next(40, 71),
                ["battery"] = battery
            };

            // valida con lo schema, cifra, invia
            var r = await client.SendAsync("/api/telemetry", msg, "telemetry-request");

            if (r.Status == 200)
            {
                SchemaValidator.ValidateOrThrow("telemetry-response", r.Body);
                var alarm = r.Body!["alarm"];
                Log(alarm is null
                    ? $"{temp,5} °C  ->  200 ok"
                    : $"{temp,5} °C  ->  200 ok  *** ALLARME {alarm["type"]} (limiti {alarm["min_c"]}..{alarm["max_c"]}) ***");
            }
            else
            {
                Log($"HTTP {r.Status}: {r.Body?.ToJsonString()}");
            }

            await Task.Delay(IntervalMs);
        }

        if (sendInvalid)
        {
            var bad = new JsonObject
            {
                ["sensor_id"] = s.Id,
                ["timestamp"] = "ieri sera",        // non è date-time
                ["cargo_type"] = "carne",           // non è nell'enum
                ["temperature_c"] = 99              // maximum 40
            };
            // validateFirst:false = salto il controllo del client per provare quello del server
            var r = await client.SendAsync("/api/telemetry", bad, "telemetry-request", validateFirst: false);
            Log($"misura NON valida -> HTTP {r.Status}: {r.Body?.ToJsonString()}");
        }

        // consultazione delle misure salvate
        var q = await client.SendAsync("/api/readings", new JsonObject { ["sensor_id"] = s.Id }, "readings-request");
        if (q.Status == 200)
        {
            SchemaValidator.ValidateOrThrow("readings-response", q.Body);
            Log($"il server ha salvato {q.Body!["count"]} misure di questo sensore");
        }

        // chiusura sessione: il server cancella la chiave
        var c = await client.SendAsync("/api/close", new JsonObject { ["action"] = "close" }, "close-request");
        Log($"sessione chiusa (HTTP {c.Status})");
    }
    catch (SchemaValidationException e)
    {
        Log($"messaggio bloccato dal client: {e.Message}");
    }
    catch (Exception e)
    {
        Log($"errore: {e.Message}");
    }
}

record SensorDef(string Id, string Cargo, double BaseTemp);
