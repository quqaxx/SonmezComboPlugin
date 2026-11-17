// Sonmez launcher icin ornek: kombo siralamasini sunuculardan okur.
// servers: [{ ip: '1.2.3.4', httpPort: 8081 }, ...]
async function fetchKomboLeaderboard(servers, playerName, top = 50) {
  const results = await Promise.allSettled(servers.map(async (s) => {
    const ctrl = new AbortController();
    const timer = setTimeout(() => ctrl.abort(), 2500);
    try {
      const url = `http://${s.ip}:${s.httpPort}/sonmezkombo/leaderboard?top=500`;
      const res = await fetch(url, { signal: ctrl.signal });
      return await res.json();
    } finally {
      clearTimeout(timer);
    }
  }));

  // Ayni isim farkli sunucularda varsa en yuksek puan alinir
  const best = new Map();
  for (const r of results) {
    if (r.status !== 'fulfilled') continue;
    for (const p of r.value.players || []) {
      const k = String(p.name).trim().toLowerCase();
      const cur = best.get(k);
      if (!cur || p.best > cur.best) best.set(k, { name: p.name, best: p.best });
    }
  }

  const list = [...best.values()]
    .sort((a, b) => b.best - a.best)
    .map((p, i) => ({ rank: i + 1, name: p.name, best: p.best }));

  const key = String(playerName || '').trim().toLowerCase();
  const me = list.find((p) => String(p.name).trim().toLowerCase() === key) || null;
  return { top: list.slice(0, top), me };
}

module.exports = { fetchKomboLeaderboard };
