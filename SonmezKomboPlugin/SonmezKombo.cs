using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SonmezKomboPlugin;

public class SonmezKombo : BackgroundService
{
    private class PlayerState
    {
        public double Score;
        public long Best;
        public int Combo;
        public double ComboLeft;
        public int Lives;
        public double Time;
        public double HitCooldown;
        public int MultiCount;
        public float Prox = 1f;
        public double ProxLeft;
        public double MultiLeft;
        public string Key = "";
        public Dictionary<int, (float fwd, float lat)> Prev = new();
        public Dictionary<int, double> LastPass = new();
    }

    private readonly SonmezKomboConfiguration _config;
    private readonly EntryCarManager _entryCarManager;
    private readonly ScoreStore _store;
    private readonly HubState _hub;
    private readonly ConcurrentDictionary<int, PlayerState> _states = new();

    public SonmezKombo(SonmezKomboConfiguration config,
        EntryCarManager entryCarManager,
        ScoreStore store,
        HubState hub,
        CSPServerScriptProvider scriptProvider,
        IHostApplicationLifetime applicationLifetime)
    {
        _config = config;
        _entryCarManager = entryCarManager;
        _store = store;
        _hub = hub;

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("sonmezkombo.lua")
                           ?? throw new InvalidOperationException("sonmezkombo.lua bulunamadi");
        using var reader = new StreamReader(stream);
        scriptProvider.AddScript(reader.ReadToEnd(), "sonmezkombo.lua");

        _entryCarManager.ClientConnected += OnClientConnected;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;
    }

    private PlayerState GetState(ACTcpClient client)
    {
        return _states.GetOrAdd(client.SessionId, _ =>
        {
            // Rekor, oyuncunun launcher'da yazdigi isme baglidir
            var key = ScoreStore.NormalizeKey(client.Name);
            return new PlayerState
            {
                Lives = _config.Lives,
                Key = key,
                Best = _store.GetBest(key)
            };
        });
    }

    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        _states.TryRemove(client.SessionId, out _);
        _store.Refresh(); // baska sunucularda yapilan rekoru da al
        var st = GetState(client);
        _store.Update(st.Key, client.Name ?? "?", st.Best);
        client.Collision += OnCollision;
    }

    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        _states.TryRemove(client.SessionId, out _);
        _store.Save();
    }

    private void OnCollision(ACTcpClient client, CollisionEventArgs args)
    {
        if (args.Speed < _config.MinCollisionSpeed) return;
        var st = GetState(client);
        lock (st)
        {
            if (st.HitCooldown > 0) return;
            st.HitCooldown = 1.2;
            st.Prox = 1f;
            st.ProxLeft = 0;
            st.Combo = 0;
            st.ComboLeft = 0;
            st.MultiCount = 0;
            st.MultiLeft = 0;
            st.Lives--;
            if (st.Lives <= 0)
            {
                st.Lives = _config.Lives;
                if (_config.ResetOnDeath)
                {
                    st.Score = 0;
                    st.Time = 0;
                }
            }
        }
        client.SendPacket(new KomboPopup { Points = 0, Kind = 1 });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const double dt = 0.1;
        int tick = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                Update(dt);
                tick++;
                if (tick % 3 == 0) SendStates();
                if (tick % 300 == 0) _store.Save();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SonmezKombo guncelleme hatasi");
            }
        }
        _store.Save();
    }

    // Hub icin: su an sunucudaki oyuncular ve anlik puanlari
    public List<object> GetLive()
    {
        var list = new List<object>();
        foreach (var car in _entryCarManager.EntryCars)
        {
            var client = car.Client;
            if (client == null || !client.HasSentFirstUpdate) continue;
            var st = GetState(client);
            lock (st)
            {
                list.Add(new
                {
                    name = client.Name ?? "?",
                    score = (long)Math.Min(st.Score, MaxScore),
                    best = st.Best,
                    combo = st.Combo,
                    lives = st.Lives,
                    time = (int)st.Time
                });
            }
        }
        return list;
    }

    private static bool IsActive(EntryCar car) => car.AiControlled || (car.Client?.HasSentFirstUpdate ?? false);

    private void Update(double dt)
    {
        var cars = _entryCarManager.EntryCars;
        foreach (var car in cars)
        {
            var client = car.Client;
            if (client == null || !client.HasSentFirstUpdate) continue;
            var st = GetState(client);

            Vector3 v = car.Status.Velocity;
            float speed = v.Length() * 3.6f;
            var vh = new Vector2(v.X, v.Z);
            bool moving = vh.Length() > 1f;
            var fwd2 = moving ? Vector2.Normalize(vh) : Vector2.Zero;

            lock (st)
            {
                st.Time += dt;
                if (st.HitCooldown > 0) st.HitCooldown -= dt;
                if (st.ProxLeft > 0)
                {
                    st.ProxLeft -= dt;
                    if (st.ProxLeft <= 0) st.Prox = 1f;
                }
                if (st.MultiLeft > 0)
                {
                    st.MultiLeft -= dt;
                    if (st.MultiLeft <= 0) st.MultiCount = 0;
                }
                if (st.ComboLeft > 0)
                {
                    st.ComboLeft -= dt;
                    if (st.ComboLeft <= 0) st.Combo = 0;
                }

                foreach (var other in cars)
                {
                    if (other == car || !IsActive(other)) continue;
                    var d3 = other.Status.Position - car.Status.Position;
                    var d = new Vector2(d3.X, d3.Z);
                    float fwd = moving ? Vector2.Dot(d, fwd2) : 0f;
                    float lat = moving ? d.X * fwd2.Y - d.Y * fwd2.X : 0f;
                    int idx = other.SessionId;

                    if (moving && st.Prev.TryGetValue(idx, out var prev)
                        && prev.fwd > 0 && fwd <= 0 && prev.fwd < 15 && fwd > -15)
                    {
                        float t = prev.fwd / (prev.fwd - fwd);
                        float latCross = prev.lat + (lat - prev.lat) * t;
                        float gap = MathF.Abs(latCross) - _config.CarWidthSum;

                        var ov = other.Status.Velocity;
                        float oSpeed = ov.Length() * 3.6f;
                        bool sameDir = oSpeed < 5f ||
                            Vector2.Dot(Vector2.Normalize(new Vector2(ov.X, ov.Z)), fwd2) > 0.6f;
                        float rel = speed - oSpeed;
                        bool cooled = !st.LastPass.TryGetValue(idx, out var last) || (st.Time - last) > 2;

                        if (sameDir && cooled && speed >= _config.MinSpeed && rel >= _config.MinRelativeSpeed
                            && gap > -0.3f && gap <= _config.CloseGap)
                        {
                            st.LastPass[idx] = st.Time;
                            st.Combo++;
                            st.ComboLeft = _config.ComboTimeout;
                            float closeness = 1f + Math.Clamp((_config.CloseGap - MathF.Max(gap, 0f)) / _config.CloseGap, 0f, 1f) * 2f;

                            // Coklu makas: kisa surede birden fazla arac (farkli seritler) gecildiyse bonus
                            st.MultiCount = st.MultiLeft > 0 ? st.MultiCount + 1 : 1;
                            st.MultiLeft = _config.MultiWindow;
                            int multi = Math.Clamp(st.MultiCount, 1, Math.Max(1, _config.MaxMulti));
                            float multiMult = 1f + (multi - 1) * _config.MultiBonus;

                            st.Prox = closeness;
                            st.ProxLeft = 2.0;
                            double pts = _config.BasePoints * closeness * TotalMult(speed, st.Combo) * multiMult;
                            st.Score += pts;
                            long cur = (long)Math.Min(st.Score, MaxScore);
                            if (cur > st.Best)
                            {
                                st.Best = cur;
                                _store.Update(st.Key, client.Name ?? "?", st.Best);
                            }
                            client.SendPacket(new KomboPopup { Points = (int)pts, Kind = 0, Multi = multi });
                        }
                    }
                    st.Prev[idx] = (fwd, lat);
                }
            }
        }
    }

    private const double MaxScore = 9e15; // Lua double hassasiyet siniri

    private static (int hi, int lo) Split(long v)
    {
        if (v < 0) v = 0;
        return ((int)(v / 1_000_000_000L), (int)(v % 1_000_000_000L));
    }

    private static float SpeedMult(float speed) => MathF.Max(1f, speed / 25f);
    private static float ComboMult(int combo) => 1f + MathF.Min(combo, 500) * 0.004f;
    private static float TotalMult(float speed, int combo) => SpeedMult(speed) * ComboMult(combo);

    private void SendStates()
    {
        foreach (var car in _entryCarManager.EntryCars)
        {
            var client = car.Client;
            if (client == null || !client.HasSentFirstUpdate) continue;
            var st = GetState(client);
            float speed = car.Status.Velocity.Length() * 3.6f;
            KomboState pkt;
            lock (st)
            {
                // Kafe geneli rekor (hub) bu sunucudakinden yuksekse onu goster
                var hubBest = _hub.Available ? _hub.GetBest(st.Key) : 0;
                if (hubBest > st.Best) st.Best = hubBest;
                var (sh, sl) = Split((long)Math.Min(st.Score, MaxScore));
                var (bh, bl) = Split(st.Best);
                pkt = new KomboState
                {
                    ScoreHi = sh,
                    ScoreLo = sl,
                    BestHi = bh,
                    BestLo = bl,
                    Combo = st.Combo,
                    Lives = st.Lives,
                    Time = (int)st.Time,
                    SpeedMult = SpeedMult(speed),
                    TotalMult = TotalMult(speed, st.Combo) * st.Prox,
                    ComboLeft = (float)Math.Clamp(st.ComboLeft / _config.ComboTimeout, 0, 1),
                    BarMax = _config.BarMaxScore,
                    Rank = _hub.Available ? _hub.Rank(st.Key, st.Best) : _store.Rank(st.Key),
                    Prox = st.Prox
                };
            }
            client.SendPacket(pkt);
        }
    }
}
