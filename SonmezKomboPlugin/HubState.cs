using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;

namespace SonmezKomboPlugin;

// Kafedeki hub'in gonderdigi birlesik siralama (tum sunucularin rekorlari).
// Hub, POST /sonmezkombo/sync ile gonderir; plugin disariya baglanmaz.
// Hub 30 sn'den uzun suredir yazmadiysa Available=false olur ve plugin kendi dosyasina doner.
public class HubState
{
    private class Snapshot
    {
        public Dictionary<string, long> Bests = new();
        public DateTime At = DateTime.UtcNow;
    }

    private readonly string _key;
    private volatile Snapshot? _snap;
    private bool _loggedFirst;

    public HubState(string hubKey) { _key = hubKey ?? ""; }

    public bool Enabled => !string.IsNullOrWhiteSpace(_key);
    public bool Available => _snap != null && (DateTime.UtcNow - _snap.At).TotalSeconds < 30;

    public bool CheckKey(string? given)
    {
        if (!Enabled || string.IsNullOrEmpty(given)) return false;
        var a = Encoding.UTF8.GetBytes(_key);
        var b = Encoding.UTF8.GetBytes(given);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    // Govde: {"v":12,"total":34,"players":[{"n":"Ali","b":12345}, ...]}
    public int Apply(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var snap = new Snapshot();
        if (doc.RootElement.TryGetProperty("players", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in arr.EnumerateArray())
            {
                if (!p.TryGetProperty("n", out var n) || !p.TryGetProperty("b", out var b)) continue;
                var name = n.GetString();
                if (string.IsNullOrWhiteSpace(name) || !b.TryGetInt64(out var best) || best <= 0) continue;
                var k = ScoreStore.NormalizeKey(name);
                if (!snap.Bests.TryGetValue(k, out var old) || best > old) snap.Bests[k] = best;
            }
        }
        _snap = snap;
        if (!_loggedFirst)
        {
            _loggedFirst = true;
            Log.Information("SonmezKombo: hub baglandi, {Count} oyuncu rekoru alindi", snap.Bests.Count);
        }
        return snap.Bests.Count;
    }

    public long GetBest(string key)
    {
        var s = _snap;
        return s != null && s.Bests.TryGetValue(key, out var v) ? v : 0;
    }

    // Kafe geneli siralama (kendi anlik rekorum dahil)
    public int Rank(string key, long myBest)
    {
        var s = _snap;
        if (s == null || myBest <= 0) return 0;
        var n = 1;
        foreach (var kv in s.Bests)
            if (kv.Key != key && kv.Value > myBest) n++;
        return n;
    }
}
