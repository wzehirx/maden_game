using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Maden.Core;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Oyun denetleyicisi: ekonomi + kayıt yükleme, GameSession kurulumu, Tick, otomatik kayıt,
/// çevrimdışı kazanç, öğretici, hareket -> komut eşlemesi ve olay -> ses/konuşma/titreşim çevirisi.
/// </summary>
public class GameController : MonoBehaviour
{
    public InputRouter Router;
    public Speech Speech;
    public SoundPlayer Sound;

    const float AutoSaveSeconds = 30f;
    const double OfflineMinSeconds = 20.0;
    const double ResumeMinSeconds = 5.0;
    const string EconomyFile = "economy.json";
    const string SaveFile = "save.json";
    
    EconomyConfig _config;
    GameSession _session;
    SettingsMenu _settings;
    float _nextSave;
    bool _tutorialPlaying;
    float _tutorialEndsAt;
    bool _failed;

    readonly Dictionary<EventKind, float> _lastSpoken = new Dictionary<EventKind, float>();
    static readonly Dictionary<EventKind, float> Cooldown = new Dictionary<EventKind, float>
    {
        { EventKind.BagFull, 25f },
        { EventKind.NotEnoughMoney, 2f },
        { EventKind.MachineBlast, 6f },
        { EventKind.MachineDrill, 6f },
        { EventKind.Error, 4f },
    };

    string SavePath => Path.Combine(Application.persistentDataPath, SaveFile);

    void Start()
    {
        _settings = new SettingsMenu(Speech, Sound, StartTutorial);
        if (Router != null) Router.OnGesture += HandleGesture;
        StartCoroutine(Boot());
    }

    void OnDestroy()
    {
        if (Router != null) Router.OnGesture -= HandleGesture;
        if (_session != null) _session.OnEvent -= HandleEvent;
    }

    // ==================== Açılış ====================

    IEnumerator Boot()
    {
        string json = null;
        yield return ReadStreamingText(EconomyFile, s => json = s);
        if (string.IsNullOrEmpty(json))
        {
            var ta = Resources.Load<TextAsset>("economy"); // yedek (olmayabilir)
            if (ta != null) json = ta.text;
        }
        if (string.IsNullOrEmpty(json))
        {
            Fail("Ekonomi dosyası okunamadı. Oyun başlatılamıyor.");
            yield break;
        }

        EconomyConfig cfg = null;
        GameState state = null;
        try
        {
            cfg = EconomyConfig.FromJson(json);
            _config = cfg;
            state = LoadState();
            _session = new GameSession(cfg, state, new SystemRandom());
            _session.OnEvent += HandleEvent;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Fail("Oyun kurulamadı. Hata: " + e.Message);
            yield break;
        }

        Sound.PickaxeTier = state.PickaxeTier;
        Sound.SetLayer(state.LayerIndex);
        Sound.StartMusic();

        _nextSave = Time.unscaledTime + AutoSaveSeconds;

        // Yeni oyuncuda öğretici; değilse kısa karşılama + çevrimdışı kazanç.
        yield return null;
        string offlineSpeech = ApplyAwayTime(OfflineMinSeconds);
        if (!MadenSettings.TutorialDone) StartTutorial();
        else
        {
            string welcome = "Maden. Devam ediyorsun.";
            if (!string.IsNullOrEmpty(offlineSpeech)) welcome += " " + offlineSpeech;
            if (MadenBridge.QueryTouchExploration() || (Router != null && Router.TouchExploration))
                welcome += " " + Tutorial.TouchExplorationLine;
            Speech.Say(welcome, SpeechPriority.Critical);
        }
    }

    void Fail(string message)
    {
        _failed = true;
        Debug.LogError(message);
        Speech.Say(message, SpeechPriority.Critical);
    }

    IEnumerator ReadStreamingText(string fileName, Action<string> done)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (path.Contains("://") || path.StartsWith("jar:"))
        {
            // Android: dosya APK içinde (jar:file://...!/assets/...) -> UnityWebRequest şart.
            using (var req = UnityWebRequest.Get(path))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) done(req.downloadHandler.text);
                else Debug.LogError("StreamingAssets okunamadı: " + path + " -> " + req.error);
            }
        }
        else
        {
            string text = null;
            try { if (File.Exists(path)) text = File.ReadAllText(path); }
            catch (Exception e) { Debug.LogError("Dosya okunamadı: " + e.Message); }
            done(text);
        }
    }

    // ==================== Kayıt ====================

    GameState LoadState()
    {
        try
        {
            // LoadOrNew: bozuk/boş JSON'da asla istisna atmaz, yeni oyun döner.
            if (SaveSystem.TryDeserialize(File.Exists(SavePath) ? File.ReadAllText(SavePath) : null, _config, out GameState s))
                return s;
            string bak = SavePath + ".bak";
            if (File.Exists(bak) && SaveSystem.TryDeserialize(File.ReadAllText(bak), _config, out s))
                return s;
        }
        catch (Exception e) { Debug.LogWarning("Kayıt okunamadı: " + e.Message); }
        return new GameState(); // yeni oyun
    }

    void SaveNow()
    {
        if (_session == null) return;
        try
        {
            _session.State.LastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string json = SaveSystem.Serialize(_session.State);
            string tmp = SavePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(SavePath))
            {
                File.Copy(SavePath, SavePath + ".bak", true);
                File.Delete(SavePath);
            }
            File.Move(tmp, SavePath);
            PlayerPrefs.Save();
        }
        catch (Exception e) { Debug.LogWarning("Kayıt yazılamadı: " + e.Message); }
    }

    /// <summary>Son görülmeden bu yana geçen süreyi çevrimdışı kazanç olarak uygular; söylenecek metni döndürür.</summary>
    string ApplyAwayTime(double minSeconds)
    {
        string result = null;
        try
        {
            long last = _session != null ? _session.State.LastSaveUnix : 0;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (last > 0)
            {
                double secs = now - last; // saat geri alınırsa negatif: ApplyOffline 0 sayar
                if (secs >= minSeconds)
                {
                    var r = _session.ApplyOffline(secs);
                    if (r != null && !string.IsNullOrEmpty(r.Speech)) result = r.Speech;
                }
            }
            if (_session != null) _session.State.LastSaveUnix = now;
        }
        catch (Exception e) { Debug.LogWarning("Çevrimdışı kazanç: " + e.Message); }
        return result;
    }

    void OnApplicationPause(bool paused)
    {
        if (_session == null) return;
        if (paused) SaveNow();
        else
        {
            string s = ApplyAwayTime(ResumeMinSeconds);
            if (!string.IsNullOrEmpty(s)) Speech.Say(s, SpeechPriority.Critical);
        }
    }

    void OnApplicationFocus(bool focus) { if (!focus) SaveNow(); }
    void OnApplicationQuit() { SaveNow(); MadenSettings.Save(); }

    // ==================== Döngü ====================

    void Update()
    {
        if (_session == null || _failed) return;
        try { _session.Tick(Math.Min(Time.unscaledDeltaTime, 1.0f)); }
        catch (Exception e) { Debug.LogException(e); }

        if (Time.unscaledTime >= _nextSave)
        {
            _nextSave = Time.unscaledTime + AutoSaveSeconds;
            SaveNow();
        }
        if (_tutorialPlaying && Time.unscaledTime >= _tutorialEndsAt && !Speech.IsBusy) _tutorialPlaying = false;
    }

    // ==================== Öğretici ====================

    void StartTutorial()
    {
        MadenSettings.TutorialDone = true;
        string text = string.Join(" ", Tutorial.Lines);
        if (MadenBridge.QueryTouchExploration() || (Router != null && Router.TouchExploration))
            text += " " + Tutorial.TouchExplorationLine;
        _tutorialPlaying = true;
        Speech.Say(text, SpeechPriority.Critical);
        _tutorialEndsAt = Time.unscaledTime + 1f; // Speech'in süre tahmini dolunca IsBusy düşer ve öğretici biter
    }

    // ==================== Hareket -> komut ====================

    void HandleGesture(Gesture g)
    {
        if (_session == null || _failed) return;
        try
        {
            if (_tutorialPlaying)
            {
                // Herhangi bir hareket öğreticiyi atlar (o hareket tüketilir).
                _tutorialPlaying = false;
                Speech.Stop();
                Speech.Say(Tutorial.Skipped, SpeechPriority.Critical);
                return;
            }

            if (_settings.IsOpen)
            {
                if (g == Gesture.ThreeFingerTap) _settings.Close();
                else _settings.Handle(g);
                return;
            }

            bool shop = _session.ShopOpen;
            switch (g)
            {
                case Gesture.Tap:
                    if (shop) SpeakResult(_session.ReadShopItem());
                    else _session.Tap();
                    break;
                case Gesture.DoubleTap:
                    if (shop) _session.ShopBuy(); // dükkân dışında zaten 2 kaz sayıldı
                    break;
                case Gesture.SwipeDown: _session.SellAll(); break;
                case Gesture.SwipeUp: _session.ToggleShop(); break;
                case Gesture.SwipeLeft: if (shop) _session.ShopPrev(); break;
                case Gesture.SwipeRight: if (shop) _session.ShopNext(); break;
                case Gesture.TwoFingerTap: SpeakResult(_session.ReadStatus()); break;
                case Gesture.ThreeFingerTap: _settings.Open(); break;

                // Ses tuşları: dükkân dışında kaz/sat; dükkânda gezin; uzun basış = satın al.
                case Gesture.VolumeUp: if (shop) _session.ShopNext(); else _session.Tap(); break;
                case Gesture.VolumeDown: if (shop) _session.ShopPrev(); else _session.SellAll(); break;
                case Gesture.VolumeUpHold: if (shop) _session.ShopBuy(); break;
                case Gesture.VolumeBoth: _session.ToggleShop(); break;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Speech.Say("Bir hata oluştu.", SpeechPriority.Normal);
        }
    }

    void SpeakResult(string text)
    {
        if (!string.IsNullOrEmpty(text)) Speech.Say(text, SpeechPriority.Critical);
    }

    // ==================== Olay -> ses / konuşma / titreşim ====================

    void HandleEvent(GameEvent ev)
    {
        if (ev == null) return;
        try { Dispatch(ev); }
        catch (Exception e) { Debug.LogException(e); }
    }

    bool CooldownOk(EventKind kind)
    {
        if (!Cooldown.TryGetValue(kind, out float cd)) return true;
        float now = Time.unscaledTime;
        if (_lastSpoken.TryGetValue(kind, out float last) && now - last < cd) return false;
        _lastSpoken[kind] = now;
        return true;
    }

    void Dispatch(GameEvent ev)
    {
        switch (ev.Kind)
        {
            case EventKind.Hit:
                bool crit = ev.Rarity != Rarity.Common; // Core: kritik vuruş = Uncommon
                Sound.PlayHit(ev.Layer, crit ? 1.25f : 1f);
                Haptics.Play(crit ? HapticKind.Hard : Haptics.ForLayerGroup(Math.Min(2, SoundPlayer.HitGroup(ev.Layer))));
                return; // kazma vuruşlarında konuşma yok

            case EventKind.ItemFound:
                Haptics.Play(HapticKind.Tick);
                Speech.Say(ev.Speech, SpeechPriority.Normal);
                return;

            case EventKind.RareFound:
                Sound.PlaySfx("rare_find");
                Haptics.Play(HapticKind.Rare);
                Speech.Say(ev.Speech, SpeechPriority.High);
                return;

            case EventKind.AncientTreasureFound:
                Sound.PlaySfx("fanfare");
                Haptics.Play(HapticKind.Rare);
                Speech.Say(ev.Speech, SpeechPriority.Critical);
                return;

            case EventKind.Sold:
                Sound.PlaySfx("sell");
                Haptics.Play(HapticKind.Sell);
                Speech.Say(ev.Speech, SpeechPriority.High);
                return;

            case EventKind.BagFull:
                if (!CooldownOk(ev.Kind)) return;
                Sound.PlaySfx("error", 0.7f, 0.9f);
                Speech.Say(ev.Speech, SpeechPriority.Normal);
                return;

            case EventKind.LayerChanged:
                Sound.SetLayer(ev.Layer);
                Sound.PlaySfx("fanfare");
                Speech.Say(ev.Speech, SpeechPriority.Critical);
                return;

            case EventKind.UpgradeBought:
                Sound.PlaySfx("buy");
                Haptics.Play(HapticKind.Sell);
                Sound.PickaxeTier = _session.State.PickaxeTier;
                Speech.Say(ev.Speech, SpeechPriority.High);
                return;

            case EventKind.NotEnoughMoney:
                if (!CooldownOk(ev.Kind)) return;
                Sound.PlaySfx("error");
                Speech.Say(ev.Speech, SpeechPriority.High);
                return;

            case EventKind.MachineBlast:
                Sound.PlayDynamite();
                Haptics.Play(HapticKind.Blast);
                if (CooldownOk(ev.Kind)) Speech.Say(ev.Speech, SpeechPriority.Low);
                return;

            case EventKind.MachineDrill:
                Sound.PlayDrill();
                Haptics.Play(HapticKind.Metal);
                if (CooldownOk(ev.Kind)) Speech.Say(ev.Speech, SpeechPriority.Low);
                return;

            case EventKind.ShopOpened:
                Sound.PlaySfx("menu_open");
                Speech.Say(ev.Speech, SpeechPriority.Critical);
                return;

            case EventKind.ShopClosed:
                Sound.PlaySfx("menu_close");
                Speech.Say(ev.Speech, SpeechPriority.Critical);
                return;

            case EventKind.ShopItemChanged:
                Haptics.Play(HapticKind.Tick);
                Speech.Say(ev.Speech, SpeechPriority.Critical);
                return;

            case EventKind.Error:
                if (!CooldownOk(ev.Kind)) return;
                Sound.PlaySfx("error");
                Speech.Say(ev.Speech, SpeechPriority.Normal);
                return;

            default:
                Speech.Say(ev.Speech, SpeechPriority.Normal);
                return;
        }
    }
}
