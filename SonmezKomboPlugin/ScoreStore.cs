using System.Text.Json;
using Serilog;

namespace SonmezKomboPlugin;

public class ScoreEntry
{
    public string Name { get; set; } = "";
    public long Best { get; set; }
}

// Rekorlar oyuncunun girdigi ISIMLE tutulur (buyuk/kucuk harf fark etmez).
public class ScoreStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly Dictionary<string, ScoreEntry> _data = new();
    private bool _dirty;

    public static string NormalizeKey(string? name)
    {
        var n = (name ?? "").Trim().ToLowerInvariant();
        return n.Length == 0 ? "?" : n;
    }

    public ScoreStore(string path)
    {
        _path = path;
        lock (_lock)
        {
            if (MergeFromDisk()) _dirty = true;
        }
    }

    // Diskteki dosyayi okuyup bellekle birlestirir (isim basina en yuksek puan kalir).
    // Birden fazla sunucu ayni ScoreFile'i paylasirsa rekorlar sunucular arasinda ortaklasir.
    // Bellekte olmayan / bellekten yuksek bir kayit varsa true doner.
    private bool MergeFromDisk()
    {
        var changed = false;
        try
        {
            if (!File.Exists(_path)) return false;
            var json = File.ReadAllText(_path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, ScoreEntry>>(json) ?? new();
            foreach (var e in raw.Values)
            {
                if (e == null || e.Best <= 0) continue;
                var k = NormalizeKey(e.Name);
                if (!_data.TryGetValue(k, out var ex))
                {
                    _data[k] = new ScoreEntry { Name = e.Name, Best = e.Best };
                    changed = true;
                }
                else if (e.Best > ex.Best)
                {
                    ex.Best = e.Best;
                    changed = true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SonmezKombo: skor dosyasi okunamadi");
        }
        return changed;
    }

    // Oyuncu baglanirken / periyodik olarak diger sunucularin yazdigi rekorlari al
    public void Refresh()
    {
        lock (_lock) MergeFromDisk();
    }

    public void Update(string key, string name, long best)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(key, out var e))
            {
                if (best <= 0) return;
                e = new ScoreEntry { Name = name };
                _data[key] = e;
                _dirty = true;
            }
            if (e.Name != name && !string.IsNullOrWhiteSpace(name))
            {
                e.Name = name;
                _dirty = true;
            }
            if (best > e.Best)
            {
                e.Best = best;
                _dirty = true;
            }
        }
    }

    public long GetBest(string key)
    {
        lock (_lock) return _data.TryGetValue(key, out var e) ? e.Best : 0;
    }

    public int Rank(string key)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(key, out var e) || e.Best <= 0) return 0;
            return _data.Values.Count(x => x.Best > e.Best) + 1;
        }
    }

    public List<ScoreEntry> Top(int n)
    {
        lock (_lock)
            return _data.Values.Where(x => x.Best > 0)
                .OrderByDescending(x => x.Best)
                .Take(n)
                .Select(x => new ScoreEntry { Name = x.Name, Best = x.Best })
                .ToList();
    }

    public void Save()
    {
        lock (_lock)
        {
            // Baska bir sunucu daha yuksek puan yazmis olabilir: once birlestir
            if (MergeFromDisk()) _dirty = true;
            if (!_dirty) return;
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = _path + "." + Environment.ProcessId + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, _path, true);
                _dirty = false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SonmezKombo: skor dosyasi yazilamadi");
            }
        }
    }
}
