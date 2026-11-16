using System;

/// <summary>
/// Sesli ayarlar menüsü. 3 parmak dokunuşuyla açılır/kapanır (kapama GameController'da).
/// Sol/sağ: ayar seç. Yukarı/aşağı: değeri değiştir. Her adım seslendirilir.
/// </summary>
public class SettingsMenu
{
    enum Item { Music, Sfx, SpeechRate, Haptics, Tutorial }
    const int ItemCount = 5;

    readonly Speech _speech;
    readonly SoundPlayer _sound;
    readonly Action _replayTutorial;
    int _index;

    public bool IsOpen { get; private set; }

    public SettingsMenu(Speech speech, SoundPlayer sound, Action replayTutorial)
    {
        _speech = speech; _sound = sound; _replayTutorial = replayTutorial;
    }

    public void Open()
    {
        IsOpen = true;
        _index = 0;
        _sound.PlaySfx("menu_open");
        _speech.Say("Ayarlar. Sola ve sağa kaydır: ayar seç. Yukarı ve aşağı kaydır: değiştir. Üç parmakla dokunursan kapanır. " + Describe(), SpeechPriority.Critical);
    }

    public void Close()
    {
        IsOpen = false;
        MadenSettings.Save();
        _sound.PlaySfx("menu_close");
        _speech.Say("Ayarlar kapandı.", SpeechPriority.Critical);
    }

    public void Handle(Gesture g)
    {
        switch (g)
        {
            case Gesture.SwipeRight: case Gesture.VolumeBoth: Move(1); break;
            case Gesture.SwipeLeft: Move(-1); break;
            case Gesture.SwipeUp: case Gesture.VolumeUp: Change(1); break;
            case Gesture.SwipeDown: case Gesture.VolumeDown: Change(-1); break;
            case Gesture.Tap: case Gesture.TwoFingerTap: _speech.Say(Describe(), SpeechPriority.Critical); break;
        }
    }

    void Move(int d)
    {
        _index = (_index + d + ItemCount) % ItemCount;
        _sound.PlaySfx("menu_open", 0.5f, 1.4f);
        _speech.Say(Describe(), SpeechPriority.Critical);
    }

    void Change(int d)
    {
        switch ((Item)_index)
        {
            case Item.Music: MadenSettings.MusicStep += d; break;
            case Item.Sfx: MadenSettings.SfxStep += d; _sound.PlayHit(0); break;
            case Item.SpeechRate: MadenSettings.SpeechStep += d; break;
            case Item.Haptics:
                MadenSettings.HapticsOn = d > 0;
                if (d > 0) Haptics.Play(HapticKind.Hard);
                break;
            case Item.Tutorial:
                IsOpen = false;
                MadenSettings.Save();
                _replayTutorial?.Invoke();
                return;
        }
        _speech.Say(Describe(), SpeechPriority.Critical);
    }

    string Describe()
    {
        switch ((Item)_index)
        {
            case Item.Music: return "Müzik sesi yüzde " + MadenSettings.MusicStep * 10;
            case Item.Sfx: return "Efekt sesi yüzde " + MadenSettings.SfxStep * 10;
            case Item.SpeechRate:
                return "Konuşma hızı " + (MadenSettings.SpeechStep + 1) + " üzerinden " + MadenSettings.SpeechStepCount;
            case Item.Haptics: return "Titreşim " + (MadenSettings.HapticsOn ? "açık" : "kapalı");
            default: return "Öğreticiyi tekrar dinle. Başlatmak için yukarı veya aşağı kaydır.";
        }
    }
}
