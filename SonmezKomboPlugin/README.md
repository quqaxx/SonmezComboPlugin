# SonmezKomboPlugin kurulum

## GitHub'da derleme
1. Bu klasoru (icindeki `.github` ve `SonmezKomboPlugin` ile) bir GitHub reposuna yukle.
2. Actions sekmesinde "Build SonmezKomboPlugin" calisir (ya da "Run workflow").
   Workflow AssettoServer 0.0.55 preview 44 kaynagini kendisi indirir ve
   framework'u (net8 / net9 ...) AssettoServer'dan okuyup eslestirir.
   Derleme icin .NET 11 SDK kurulur (eski SDK'lar da yuklenir).
3. Biten isin altindaki `SonmezKomboPlugin` artifact'ini indir.

## Sunucuya kurulum
1. Artifact klasorunu sunucudaki `plugins/` icine kopyala.
2. `cfg/extra_cfg.yml`:
   EnablePlugins:
     - SonmezKomboPlugin
3. Ayarlar `cfg/plugin_sonmez_kombo_cfg.yml` (hepsi istege bagli):
   BasePoints: 100
   CloseGap: 1.5
   MinSpeed: 60
   MinRelativeSpeed: 20
   ComboTimeout: 5
   Lives: 2
   ResetOnDeath: true
   BarMaxScore: 200000000   # sadece puan tablosunun kirmizi dolum siniri; skor bunu asinca da artar
   MultiWindow: 0.8         # sn: bu sure icinde gecilen araclar coklu makas sayilir
   MultiBonus: 0.75         # her ek arac icin ek carpan (2 arac x1.75, 3 arac x2.5)
   MaxMulti: 6
4. Sunucuyu yeniden baslat. Kalici skorlar `cfg/sonmez_kombo_scores.json` dosyasinda.

## Rekor ve launcher siralamasi
- Rekor, oyuncunun launcher'da yazdigi isme baglidir (buyuk/kucuk harf fark etmez).
  Ayni isimle girince en iyi puani otomatik gelir; oyunda "Rekorun" satirinda gorunur.
- Siralama adresi (JSON): http://SUNUCU_IP:HTTP_PORT/sonmezkombo/leaderboard?top=50&name=OyuncuAdi
- Launcher ornegi: `launcher/kombo-leaderboard.js` (birden fazla sunucuyu sorgulayip her isim icin en yuksek puani alir).

## Sag ust panel (PB + siralama)
- Sag ustte "PB <rekor>" ve altinda logo + "#<sira> SIRALAMA" gorunur.
- Logo, sunucunun HTTP portundan gelir: http://SUNUCU_IP:HTTP_PORT/sonmezkombo/logo.png
  (eklentinin icinde gomulu; ayri dosya kopyalamak gerekmez). HTTP port disariya acik olmali.

## Gorunum ayarlari (lua/sonmezkombo.lua basinda)
- `MAIN_POS` ('center' / 'right') ve `PB_POS` ('right' / 'left'): panel yerleri.
- `FONT` / `FONT_LABEL`: yazi tipi (varsayilan Bahnschrift; alternatif: 'Segoe UI Semibold', 'Trebuchet MS').

## Ortak skor dosyasi (birden fazla sunucu)
- Tum sunucularda `ScoreFile` ayni yola ayarlanirsa (ornek: `ScoreFile: "C:/SonmezKombo/scores.json"`) rekorlar sunucular arasi ortaklasir.
- Dosya her kayitta ve oyuncu baglanirken diskten okunup birlestirilir (isim basina en yuksek puan korunur), gecici dosya ile yazilir.
- Ayni dosyayi kullanmayan sunucularda her sunucu kendi rekorunu tutar; launcher siralamasi hepsinin en yuksegini alir.

## Merkezi HUB (kafe geneli siralama + canli skorlar)
Hub, kafedeki server PC'de calisan kucuk bir Node programidir (SonmezKomboHub klasoru). Baglantilar hep hub -> sunucu yonundedir; plugin disariya baglanmaz.
- Hub her sunucunun `/sonmezkombo/live` ve `/sonmezkombo/leaderboard` adreslerini sorgular.
- Hub birlesik siralamayi `POST /sonmezkombo/sync` ile sunuculara geri gonderir: oyunda PB ve #SIRA kafe geneli gorunur.
- Plugin ayari: `cfg/plugin_sonmez_kombo_cfg.yml` icine `HubKey: "<hub-config.json'daki hubKey>"` (AYNI olmali).
- HubKey bos ise `/sync` kapali kalir, plugin eskisi gibi calisir. Hub 30 sn yazmazsa plugin kendi dosyasina doner.
- Sunucunun HTTP portu hub'in PC'sinden erisilebilir olmali.
