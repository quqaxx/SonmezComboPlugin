-- Sönmez Kombo Puan (sunucu scripti)
-- Puanı sunucu plugin'i hesaplar, bu script sadece gösterir.

local SCALE = 0.75
-- Yerlesim: ana panel 'center' veya 'right'; PB paneli 'right' veya 'left'
local MAIN_POS = 'center'
local PB_POS = 'right'
local WHITE = rgbm(1, 1, 1, 1)
-- Yazi tipi (oyuncunun Windows'unda kurulu olmali, yoksa varsayilana doner).
-- Alternatifler: 'Segoe UI Semibold', 'Trebuchet MS', 'Verdana'
local FONT = 'Bahnschrift;Weight=SemiBold'
local FONT_LABEL = 'Bahnschrift;Weight=Medium'

local TARGET_DEFAULT = 200000000
local st = { score = 0, best = 0, rank = 0, prox = 1, combo = 0, lives = 2, time = 0, speedMult = 1, totalMult = 1, comboLeft = 0, target = TARGET_DEFAULT }
local dispScore = 0
local function fmt(n)
  local s = string.format('%.0f', n)
  local r = s:reverse():gsub('(%d%d%d)', '%1,'):reverse()
  return (r:gsub('^,', ''))
end
local multiNames = { [2] = "2'Lİ", [3] = "3'LÜ", [4] = "4'LÜ", [5] = "5'Lİ", [6] = "6'LI" }
local popups = {}

ac.OnlineEvent({
  key = ac.StructItem.key('SonmezKomboState'),
  scoreHi = ac.StructItem.int32(),
  scoreLo = ac.StructItem.int32(),
  bestHi = ac.StructItem.int32(),
  bestLo = ac.StructItem.int32(),
  combo = ac.StructItem.int32(),
  lives = ac.StructItem.int32(),
  time = ac.StructItem.int32(),
  speedMult = ac.StructItem.float(),
  totalMult = ac.StructItem.float(),
  comboLeft = ac.StructItem.float(),
  barMax = ac.StructItem.int32(),
  rank = ac.StructItem.int32(),
  prox = ac.StructItem.float(),
}, function(sender, m)
  st.score = m.scoreHi * 1000000000 + m.scoreLo
  st.best = m.bestHi * 1000000000 + m.bestLo
  st.combo, st.lives, st.time = m.combo, m.lives, m.time
  st.speedMult, st.totalMult, st.comboLeft = m.speedMult, m.totalMult, m.comboLeft
  st.target = m.barMax > 0 and m.barMax or TARGET_DEFAULT
  st.rank = m.rank
  st.prox = m.prox
end)

ac.OnlineEvent({
  key = ac.StructItem.key('SonmezKomboPopup'),
  points = ac.StructItem.int32(),
  kind = ac.StructItem.int32(),
  multi = ac.StructItem.int32(),
}, function(sender, m)
  local text
  if m.kind == 1 then
    text = 'KAZA'
  elseif m.multi >= 2 then
    text = '+' .. fmt(m.points) .. '  ' .. (multiNames[m.multi] or (m.multi .. 'X')) .. ' MAKAS'
  else
    text = '+' .. fmt(m.points) .. '  MAKAS'
  end
  table.insert(popups, 1, { text = text, t = 1.8, crash = m.kind == 1, multi = m.kind == 0 and m.multi >= 2 })
  if #popups > 3 then table.remove(popups) end
end)


local function txt(s, size, cx, cy, col, f)
  ui.pushDWriteFont(f or FONT)
  local sz = ui.measureDWriteText(s, size)
  ui.dwriteDrawText(s, size, vec2(cx - sz.x / 2, cy - sz.y / 2), col)
  ui.popDWriteFont()
end
local function txtL(s, size, x, cy, col, f)
  ui.pushDWriteFont(f or FONT)
  local sz = ui.measureDWriteText(s, size)
  ui.dwriteDrawText(s, size, vec2(x, cy - sz.y / 2), col)
  ui.popDWriteFont()
end
local function txtR(s, size, x, cy, col, f)
  ui.pushDWriteFont(f or FONT)
  local sz = ui.measureDWriteText(s, size)
  ui.dwriteDrawText(s, size, vec2(x - sz.x, cy - sz.y / 2), col)
  ui.popDWriteFont()
end

-- Yuvarlatilmis koseli konveks cokgen (pts: {x,y} listesi, radii: kose yaricaplari)
local function fillRounded(pts, radii, col)
  local n = #pts
  ui.pathClear()
  for i = 1, n do
    local p0 = pts[(i - 2) % n + 1]
    local p1 = pts[i]
    local p2 = pts[i % n + 1]
    local r = radii[i]
    if r <= 0.5 then
      ui.pathLineTo(vec2(p1[1], p1[2]))
    else
      local d1x, d1y = p0[1] - p1[1], p0[2] - p1[2]
      local l1 = math.sqrt(d1x * d1x + d1y * d1y)
      local d2x, d2y = p2[1] - p1[1], p2[2] - p1[2]
      local l2 = math.sqrt(d2x * d2x + d2y * d2y)
      local rr = math.min(r, l1 / 2, l2 / 2)
      local ax, ay = p1[1] + d1x / l1 * rr, p1[2] + d1y / l1 * rr
      local bx, by = p1[1] + d2x / l2 * rr, p1[2] + d2y / l2 * rr
      for k = 0, 6 do
        local t = k / 6
        local u = 1 - t
        ui.pathLineTo(vec2(u * u * ax + 2 * u * t * p1[1] + t * t * bx,
                           u * u * ay + 2 * u * t * p1[2] + t * t * by))
      end
    end
  end
  ui.pathFillConvex(col)
end

-- Egik dikdortgen. x = sol alt kose, sl = ust kenarin saga kaymasi,
-- r = sol koseler, rr = sag koseler yaricapi (yoksa r ile ayni; 0 = keskin)
local function para(S, x, y, w, h, sl, col, r, rr)
  r = (r or 0) * S
  rr = rr and (rr * S) or r
  local tl = { (x + sl) * S, y * S }
  local tr = { (x + w + sl) * S, y * S }
  local br = { (x + w) * S, (y + h) * S }
  local bl = { x * S, (y + h) * S }
  if (r > 0.5 or rr > 0.5) and ui.pathFillConvex then
    fillRounded({ tl, tr, br, bl }, { r, rr, rr, r }, col)
  else
    ui.drawQuadFilled(vec2(tl[1], tl[2]), vec2(tr[1], tr[2]), vec2(br[1], br[2]), vec2(bl[1], bl[2]), col)
  end
end
local function drawMain(S)
  local DARK = rgbm(0.07, 0.07, 0.09, 0.90)
  local ORANGE = rgbm(1, 0.52, 0.10, 1)
  local GRAY = rgbm(1, 1, 1, 0.65)
  local GREEN = rgbm(0.2, 0.9, 0.3, 1)
  local OFF = rgbm(1, 1, 1, 0.14)

  -- ust satir: Speed / Proximity / Combo + turuncu toplam carpan
  local tabs = {
    { x = 0,   v = string.format('%.1fX', st.speedMult), l = 'Speed' },
    { x = 122, v = string.format('%.1fX', st.prox),      l = 'Proximity' },
    { x = 244, v = fmt(st.combo) .. 'X',                 l = 'Combo' },
  }
  for _, t in ipairs(tabs) do
    para(S, t.x, 0, 118, 52, 16, DARK, 9)
    txt(t.v, 22 * S, (t.x + 67) * S, 20 * S, WHITE)
    txt(t.l, 13 * S, (t.x + 67) * S, 41 * S, GRAY, FONT_LABEL)
  end

  para(S, 366, 0, 140, 52, 16, ORANGE, 9)
  local hx = 366 + 16 * 6 / 52
  para(S, hx + 9, 6, 5, 40, 16 * 40 / 52, rgbm(1, 0.82, 0.55, 0.95), 1.5)
  para(S, hx + 19, 6, 2.5, 40, 16 * 40 / 52, rgbm(1, 0.82, 0.55, 0.95), 1)
  txt(string.format('%.1fX', st.totalMult), 28 * S, 444 * S, 24 * S, WHITE)
  if st.combo > 0 then
    para(S, 388, 44, 112 * math.saturate(st.comboLeft), 5, 1.5, rgbm(1, 1, 1, 0.9), 2)
  end

  -- puan seridi: 200M'a kadar soldan kirmiziya dolar, ucu silik
  local px, py, pw, ph, psl = 8, 60, 500, 52, 18
  para(S, px, py, pw, ph, psl, WHITE, 12)
  local p = math.saturate(dispScore / (st.target > 0 and st.target or TARGET_DEFAULT))
  if p > 0.0005 then
    local rr, rg, rb = 0.93, 0.13, 0.15
    if p >= 0.999 then
      para(S, px, py, pw, ph, psl, rgbm(rr, rg, rb, 1), 12)
    else
      local w = pw * p
      local fade = math.min(w, 90) * math.saturate((1 - p) / 0.05)
      local solid = w - fade
      if solid > 0 then para(S, px, py, solid, ph, psl, rgbm(rr, rg, rb, 1), 12, 0) end
      local N = 28
      for k = 0, N - 1 do
        local a = 1 - (k + 0.5) / N
        para(S, px + solid + fade * k / N, py, fade / N + 0.6, ph, psl, rgbm(rr, rg, rb, a))
      end
    end
  end
  local ptsText = fmt(dispScore) .. ' PTS'
  txt(ptsText, (#ptsText > 20 and 26 or 34) * S, (px + pw / 2 + psl / 2) * S, (py + ph / 2) * S, rgbm(0.05, 0.05, 0.05, 1))

  -- canlar
  for i = 1, 2 do
    para(S, 534 + (i - 1) * 16, 66, 10, 40, 5, (st.lives >= (3 - i)) and GREEN or OFF, 4)
  end

  -- makas / kaza yazilari
  for i, pp in ipairs(popups) do
    local a = math.saturate(pp.t)
    local col = pp.crash and rgbm(1, 0.3, 0.3, a) or (pp.multi and rgbm(1, 0.82, 0.2, a) or rgbm(1, 1, 1, a))
    txt(pp.text, (pp.multi and 26 or 22) * S, 268 * S, (140 + (i - 1) * 26 - (1.8 - pp.t) * 8) * S, col)
  end
end

local logoUrl = nil
local function getLogoUrl()
  if not logoUrl then
    local ip, port = ac.getServerIP(), ac.getServerHTTPPort()
    if ip and ip ~= '' and port and port > 0 then
      logoUrl = string.format('http://%s:%d/sonmezkombo/logo.png', ip, port)
    end
  end
  return logoUrl
end

local function drawPB(S)
  local P = function(x, y) return vec2(x * S, y * S) end
  local DARK = rgbm(0.06, 0.06, 0.08, 0.92)
  local LIGHT = rgbm(0.93, 0.93, 0.93, 1)

  -- ust satir: egik koyu serit + beyaz "PB" sekmesi
  para(S, 74, 0, 276, 46, 22, DARK, 8)
  para(S, 12, 0, 78, 46, 22, LIGHT, 8)
  txt('PB', 26 * S, 62 * S, 24 * S, rgbm(0.05, 0.05, 0.05, 1))
  local numText = fmt(st.best)
  txt(numText, (#numText > 14 and 22 or 28) * S, 236 * S, 24 * S, WHITE)

  -- alt satir: logo + siralama
  local rankText = (st.rank > 0 and ('#' .. fmt(st.rank)) or '#-') .. ' SIRALAMA'
  ui.pushDWriteFont(FONT)
  local sz = ui.measureDWriteText(rankText, 18 * S)
  ui.popDWriteFont()
  local w = 52 + sz.x / S + 14
  ui.drawRectFilled(P(6, 54), P(6 + w, 92), DARK, 10 * S)
  local url = getLogoUrl()
  if url then ui.drawImage(url, P(12, 56), P(48, 92)) end
  txtL(rankText, 18 * S, 56 * S, 73 * S, WHITE)
end

function script.drawUI()
  local dt = ac.getDeltaT()
  dispScore = dispScore + (st.score - dispScore) * math.min(1, dt * 8)
  for i = #popups, 1, -1 do
    popups[i].t = popups[i].t - dt
    if popups[i].t <= 0 then table.remove(popups, i) end
  end

  local scr = ac.getUI().windowSize
  local k0 = scr.y / 1080
  local S = SCALE * k0

  local mw, mh = 570 * S, 215 * S
  local mx = MAIN_POS == 'right' and (scr.x - mw - 30 * k0) or ((scr.x - mw) / 2)
  ui.transparentWindow('SonmezKomboMain', vec2(mx, 20 * k0), vec2(mw, mh), function() drawMain(S) end)

  local pw, ph = 390 * S, 100 * S
  local pbx = PB_POS == 'left' and (30 * k0) or (scr.x - pw - 30 * k0)
  ui.transparentWindow('SonmezKomboPB', vec2(pbx, 20 * k0), vec2(pw, ph), function() drawPB(S) end)
end
