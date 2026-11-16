using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FrigoTrack.Common;

namespace FrigoTrack.Server;

public class Session
{
    public Session(string id, byte[] key) { Id = id; Key = key; }
    public string Id { get; }
    public byte[] Key { get; }
}

/// <summary>Una sessione per client, ciascuna con la propria chiave (ConcurrentDictionary = thread-safe).</summary>
public class SessionStore
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public string Create(byte[] key)
    {
        while (true)
        {
            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant(); // 8 caratteri hex
            if (_sessions.TryAdd(id, new Session(id, key))) return id;
        }
    }

    public bool TryGet(string id, out Session session) => _sessions.TryGetValue(id, out session!);

    /// <summary>Chiusura: la sessione sparisce e la chiave viene azzerata in memoria.</summary>
    public void Remove(string id)
    {
        if (_sessions.TryRemove(id, out var s)) Array.Clear(s.Key);
    }
}

/// <summary>Salva le misure in un file JSON (data/readings.json) e le rende consultabili.</summary>
public class ReadingStore
{
    private readonly object _lock = new();
    private readonly string _path;
    private readonly List<Reading> _items;

    public ReadingStore(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _items = File.Exists(_path)
            ? JsonSerializer.Deserialize<List<Reading>>(File.ReadAllText(_path), JsonOpts.Options) ?? new()
            : new();
    }

    public void Add(Reading r)
    {
        lock (_lock)
        {
            _items.Add(r);
            File.WriteAllText(_path, JsonSerializer.Serialize(_items, JsonOpts.Pretty));
        }
    }

    public bool SensorExists(string sensorId)
    {
        lock (_lock) return _items.Any(r => r.SensorId == sensorId);
    }

    public List<Reading> Query(string sensorId, DateTimeOffset? from, DateTimeOffset? to)
    {
        lock (_lock)
        {
            return _items.Where(r => r.SensorId == sensorId && Within(r, from, to)).ToList();
        }
    }

    private static bool Within(Reading r, DateTimeOffset? from, DateTimeOffset? to)
    {
        var t = DateTimeOffset.Parse(r.Timestamp, CultureInfo.InvariantCulture);
        return (from is null || t >= from) && (to is null || t <= to);
    }
}
