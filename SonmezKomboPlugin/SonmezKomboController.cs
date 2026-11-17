using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace SonmezKomboPlugin;

// Launcher icin: http://SUNUCU_IP:HTTP_PORT/sonmezkombo/leaderboard?top=50&name=OyuncuAdi
public class SonmezKomboController : ControllerBase
{
    private static readonly Lazy<byte[]> Logo = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("sonmezlogo.png");
        if (stream == null) return Array.Empty<byte>();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    });

    private readonly ScoreStore _store;
    private readonly HubState _hub;
    private readonly SonmezKombo _kombo;

    public SonmezKomboController(ScoreStore store, HubState hub, SonmezKombo kombo)
    {
        _store = store;
        _hub = hub;
        _kombo = kombo;
    }

    // Hub icin: sunucuda su an oynayanlar
    [HttpGet("/sonmezkombo/live")]
    public IActionResult Live()
    {
        Response.Headers["Access-Control-Allow-Origin"] = "*";
        return Ok(new { updated = DateTime.UtcNow, players = _kombo.GetLive() });
    }

    // Hub, birlesik siralamayi buraya gonderir (X-Hub-Key basligi plugin'deki HubKey ile ayni olmali)
    [HttpPost("/sonmezkombo/sync")]
    public async Task<IActionResult> Sync()
    {
        if (!_hub.Enabled) return NotFound();
        if (!_hub.CheckKey(Request.Headers["X-Hub-Key"].ToString())) return Unauthorized();
        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync();
        try
        {
            var count = _hub.Apply(body);
            return Ok(new { ok = true, count });
        }
        catch
        {
            return BadRequest();
        }
    }

    [HttpGet("/sonmezkombo/leaderboard")]
    public IActionResult Leaderboard([FromQuery] int top = 50, [FromQuery] string? name = null)
    {
        Response.Headers["Access-Control-Allow-Origin"] = "*";
        top = Math.Clamp(top, 1, 500);
        var players = _store.Top(top).Select((e, i) => new { rank = i + 1, name = e.Name, best = e.Best }).ToList();

        object? me = null;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var key = ScoreStore.NormalizeKey(name);
            var best = _store.GetBest(key);
            me = new { name = name.Trim(), best, rank = _store.Rank(key) };
        }

        return Ok(new { updated = DateTime.UtcNow, players, me });
    }

    // HUD'daki logo (Lua script bunu sunucunun HTTP portundan ceker)
    [HttpGet("/sonmezkombo/logo.png")]
    public IActionResult LogoImage()
    {
        Response.Headers["Access-Control-Allow-Origin"] = "*";
        Response.Headers["Cache-Control"] = "public, max-age=86400";
        var bytes = Logo.Value;
        return bytes.Length == 0 ? NotFound() : File(bytes, "image/png");
    }
}
