using AssettoServer.Network.ClientMessages;

namespace SonmezKomboPlugin;

// Skorlar 2 milyari asabilsin diye hi/lo olarak gonderilir: skor = Hi * 1_000_000_000 + Lo

// Her oyuncuya ozel durum (saniyede ~3 kez)
[OnlineEvent(Key = "SonmezKomboState")]
public class KomboState : OnlineEvent<KomboState>
{
    [OnlineEventField(Name = "scoreHi")] public int ScoreHi;
    [OnlineEventField(Name = "scoreLo")] public int ScoreLo;
    [OnlineEventField(Name = "bestHi")] public int BestHi;
    [OnlineEventField(Name = "bestLo")] public int BestLo;
    [OnlineEventField(Name = "combo")] public int Combo;
    [OnlineEventField(Name = "lives")] public int Lives;
    [OnlineEventField(Name = "time")] public int Time;
    [OnlineEventField(Name = "speedMult")] public float SpeedMult;
    [OnlineEventField(Name = "totalMult")] public float TotalMult;
    [OnlineEventField(Name = "comboLeft")] public float ComboLeft;
    [OnlineEventField(Name = "barMax")] public int BarMax;
    [OnlineEventField(Name = "rank")] public int Rank; // sunucudaki siralama (0 = henuz puan yok)
    [OnlineEventField(Name = "prox")] public float Prox; // yakinlik carpani (son makas)
}

// Makas / kaza yazisi
[OnlineEvent(Key = "SonmezKomboPopup")]
public class KomboPopup : OnlineEvent<KomboPopup>
{
    [OnlineEventField(Name = "points")] public int Points;
    [OnlineEventField(Name = "kind")] public int Kind; // 0 = makas, 1 = kaza
    [OnlineEventField(Name = "multi")] public int Multi; // kac arac birden gecildi (1 = tekli)
}
