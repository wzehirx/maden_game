using System;
using Kazi.Core;

namespace Kazi.Game
{
    /// <summary>
    /// Sesli ayarlar menüsü. 3 parmak dokunuşuyla açılır/kapanır (kapama GameController'da).
    /// Sola/sağa: ayar seç. Yukarı/aşağı: değeri değiştir. Her adım seslendirilir; ekranda büyük yazıyla da görünür.
    /// </summary>
    public sealed class SettingsMenu
    {
        static readonly string[] Names =
        {
            "Müzik sesi", "Efekt sesi", "Konuşma hızı", "Konuşma ayrıntısı", "Konuşma motoru", "Konuşma sesi",
            "Titreşim", "Kazıma modu", "Tuzakta çöpe at", "Yükseltme bildirimi", "Öğreticiyi tekrar dinle"
        };
        public const int ItemCount = 11;

        readonly Speech _speech;
        readonly SoundPlayer _sound;
        readonly Func<GameSession> _session;
        readonly Action _replayTutorial;
        readonly Action _changed;
        int _index;

        public bool IsOpen { get; private set; }
        public int Index => _index;
        public string ItemName => Names[_index];

        public SettingsMenu(Speech speech, SoundPlayer sound, Func<GameSession> session, Action replayTutorial, Action changed)
        {
            _speech = speech; _sound = sound; _session = session; _replayTutorial = replayTutorial; _changed = changed;
        }

        public void Open()
        {
            IsOpen = true;
            _index = 0;
            _sound.PlaySfx("menu_whoosh");
            _speech.Say("Ayarlar. Sola ve sağa kaydır: ayar seç. Yukarı ve aşağı kaydır: değiştir. Üç parmakla dokunursan kapanır. " + Describe(), SpeechPriority.Critical);
        }

        public void Close()
        {
            IsOpen = false;
            Settings.Save();
            _sound.PlaySfx("menu_whoosh_2");
            _speech.Say("Ayarlar kapandı.", SpeechPriority.Critical);
        }

        public void Handle(Gesture g)
        {
            switch (g)
            {
                case Gesture.SwipeRight: Move(1); break;
                case Gesture.SwipeLeft: Move(-1); break;
                case Gesture.SwipeUp: Change(1); break;
                case Gesture.SwipeDown: Change(-1); break;
                case Gesture.Tap: case Gesture.TwoFingerTap: _speech.Say(Describe(), SpeechPriority.Critical); break;
            }
        }

        void Move(int d)
        {
            _index = (_index + d + ItemCount) % ItemCount;
            _sound.PlaySfx("page_slide", 0.7f);
            _speech.Say(Describe(), SpeechPriority.Critical);
        }

        static string Rate(double r) => NumberFormat.Dec(r);

        void Change(int d)
        {
            var s = _session();
            switch (_index)
            {
                case 0: Settings.MusicStep += d; break;
                case 1: Settings.SfxStep += d; _sound.PlaySfx("coin_single", 0.8f); break;
                case 2:
                    if (s != null) { s.SpeechRate = s.SpeechRate + d * GameState.RateStep; Settings.SpeechRate = (float)s.SpeechRate; }
                    break;
                case 3:
                    if (s != null) { int v = ((int)s.Detail + d + 3) % 3; s.Detail = (SpeechDetail)v; }
                    break;
                case 4: _speech.SelectEngineOption(_speech.CurrentEngineOption() + d); return; // yeni motorla örnek cümle okunur
                case 5: _speech.SelectVoiceOption(_speech.CurrentVoiceOption() + d); return;
                case 6:
                    Settings.HapticsOn = d > 0;
                    if (d > 0) Haptics.Play(HapticKind.Hard);
                    break;
                case 7: if (s != null) s.Mode = d > 0 ? ScratchMode.Drag : ScratchMode.Sequential; break;
                case 8: if (s != null) s.State.AutoDiscardTrap = d > 0; break;
                case 9: if (s != null) s.State.NotifyAffordable = d > 0; break;
                case 10:
                    IsOpen = false;
                    Settings.Save();
                    _replayTutorial?.Invoke();
                    return;
            }
            _changed?.Invoke();
            _speech.Say(Describe(), SpeechPriority.Critical);
        }

        /// <summary>Ekranda gösterilen kısa değer metni.</summary>
        public string Value()
        {
            var s = _session();
            switch (_index)
            {
                case 0: return "Yüzde " + Settings.MusicStep * 10;
                case 1: return "Yüzde " + Settings.SfxStep * 10;
                case 2: return s == null ? "" : Rate(s.SpeechRate) + " kat";
                case 3: return s == null ? "" : s.Detail == SpeechDetail.Full ? "Tam" : s.Detail == SpeechDetail.Short ? "Kısa" : "Sessiz";
                case 4: return _speech.EngineOptionName(_speech.CurrentEngineOption());
                case 5: return _speech.VoiceOptionName(_speech.CurrentVoiceOption());
                case 6: return Settings.HapticsOn ? "Açık" : "Kapalı";
                case 7: return s == null ? "" : s.Mode == ScratchMode.Drag ? "Sürükleme" : "Sıralı";
                case 8: return s != null && s.State.AutoDiscardTrap ? "Açık" : "Kapalı";
                case 9: return s != null && s.State.NotifyAffordable ? "Açık" : "Kapalı";
                default: return "Yukarı veya aşağı kaydır";
            }
        }

        public string Describe()
        {
            var s = _session();
            switch (_index)
            {
                case 0: return "Müzik sesi yüzde " + Settings.MusicStep * 10;
                case 1: return "Efekt sesi yüzde " + Settings.SfxStep * 10;
                case 2: return "Konuşma hızı " + Value();
                case 3:
                    return "Konuşma ayrıntısı: " + Value() + ". " +
                        (s != null && s.Detail == SpeechDetail.Full ? "Her kutu okunur." : s != null && s.Detail == SpeechDetail.Short ? "Yalnızca sonuçlar okunur." : "Yalnızca sesler çalar.");
                case 4:
                    return "Konuşma motoru: " + Value() + ". " +
                        (_speech.EngineChoiceAvailable
                            ? (_speech.CurrentEngineOption() + 1) + " / " + _speech.EngineOptionCount + ". Değiştirmek için yukarı veya aşağı kaydır."
                            : "Bu cihazda motor seçimi yok.");
                case 5: return "Ses seçimi: " + Value() + ". " + (_speech.CurrentVoiceOption() + 1) + " / " + _speech.VoiceOptionCount + ". Değiştirmek için yukarı veya aşağı kaydır.";
                case 6: return "Titreşim " + Value().ToLowerInvariant();
                case 7:
                    return "Kazıma modu: " + Value() + ". " + (s != null && s.Mode == ScratchMode.Drag
                        ? "Parmağınla kutuların üstünde gez, sürterek kaz."
                        : "Her dokunuş bir kutu açar.");
                case 8: return "Tuzakta çöpe at " + Value().ToLowerInvariant() + ". Açıkken otomatik kazıyıcı tuzaklı kartı yarıda bırakır.";
                case 9: return "Yükseltme bildirimi " + Value().ToLowerInvariant();
                default: return "Öğreticiyi tekrar dinle. Başlatmak için yukarı veya aşağı kaydır.";
            }
        }
    }
}
