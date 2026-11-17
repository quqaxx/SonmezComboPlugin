using JetBrains.Annotations;

namespace SonmezKomboPlugin;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SonmezKomboConfiguration
{
    // Bir makasin taban puani
    public int BasePoints { get; init; } = 100;
    // Iki arac arasi bosluk (metre); bundan yakinsa makas sayilir
    public float CloseGap { get; init; } = 1.5f;
    // Bu hizin (km/h) altinda puan yok
    public float MinSpeed { get; init; } = 60f;
    // Gecilen araca gore en az hiz farki (km/h)
    public float MinRelativeSpeed { get; init; } = 20f;
    // Kombo kac saniye makas yapilmazsa sifirlanir
    public float ComboTimeout { get; init; } = 5f;
    // Can sayisi (kazada 1 azalir)
    public int Lives { get; init; } = 2;
    // Canlar bitince oyuncunun puani sifirlansin mi
    public bool ResetOnDeath { get; init; } = true;
    // Kazanin sayilmasi icin en az carpisma hizi (km/h)
    public float MinCollisionSpeed { get; init; } = 15f;
    // Iki aracin yan yana genisligi toplami (merkezden merkeze bosluk hesabi)
    public float CarWidthSum { get; init; } = 1.85f;
    // Puan tablosunun kirmizi dolum siniri (sadece gorsel). Skor bunu asinca da artmaya devam eder.
    public int BarMaxScore { get; init; } = 200_000_000;
    // Coklu makas: bu sure (sn) icinde ust uste gecilen araclar ayni "coklu makas" sayilir
    public float MultiWindow { get; init; } = 0.8f;
    // Coklu makasta her ek arac icin eklenen carpan (2 arac = x1.75, 3 arac = x2.5 ...)
    public float MultiBonus { get; init; } = 0.75f;
    // Coklu makasta sayilacak en fazla arac
    public int MaxMulti { get; init; } = 6;
    // Kafedeki hub ile ayni gizli anahtar (hub-config.json -> hubKey). Bos birakirsan hub baglantisi kapali olur.
    public string HubKey { get; init; } = "";
    // Kalici skor dosyasi
    public string ScoreFile { get; init; } = "cfg/sonmez_kombo_scores.json";
}
