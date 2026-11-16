using System;
using System.Globalization;
using UnityEngine;

public enum Gesture
{
    Tap, DoubleTap,
    SwipeUp, SwipeDown, SwipeLeft, SwipeRight,
    TwoFingerTap, ThreeFingerTap,
    VolumeUp, VolumeUpHold, VolumeDown, VolumeBoth
}

/// <summary>
/// Hareket tanıma. Üç giriş kaynağı:
/// (1) Unity eski Input.touches (+ editörde fare/klavye), (2) Android eklentisinden gelen olaylar (MadenBridge),
/// (3) ses tuşları. Eklentiden olay gelmeye başlayınca Unity dokunuşları yok sayılır (çift sayımı önlemek için).
/// Tüm eşikler dp cinsindendir.
/// </summary>
public class InputRouter : MonoBehaviour
{
    public event Action<Gesture> OnGesture;

    public bool NativeActive { get; private set; }
    public bool TouchExploration { get; private set; }

    [Header("Eşikler (dp / saniye)")]
    public float TapSlopDp = 18f;
    public float SwipeMinDp = 56f;
    public float TapMaxSeconds = 0.45f;
    public float DoubleTapGapSeconds = 0.35f;
    public float MultiTapMaxSeconds = 0.6f;
    public float VolumeBothWindow = 0.12f;

    // --- Hareket durumu ---
    bool _active;
    float _startT, _startX, _startY, _lastX, _lastY, _lastT;
    int _maxCount;
    bool _lastTapValid;
    float _lastTapT;

    // --- Unity girişi ---
    int _prevUnityCount;
    float _pxToDp = 1f;

    // --- Ses tuşları ---
    bool _upHeld, _downHeld, _bothFired;
    int _pendingKey; // 0 yok, 1 yukarı, 2 aşağı
    float _pendingAt;

    void Update()
    {
        ComputeScale();
        PollUnityInput();
        UpdateVolumePending();
    }

    void ComputeScale()
    {
        float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
        float sysW = Display.main != null ? Display.main.systemWidth : Screen.width;
        float scale = Screen.width > 0 && sysW > 0 ? sysW / Screen.width : 1f; // çözünürlük ölçeklemesine karşı
        _pxToDp = scale * 160f / dpi;
    }

    // ================= Unity girişi =================

    void PollUnityInput()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (NativeActive) return;
        float now = Time.unscaledTime;
        int n = Input.touchCount;
        if (n > 0)
        {
            int eff = 0; float sx = 0, sy = 0;
            for (int i = 0; i < n; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                eff++; sx += t.position.x; sy += t.position.y;
            }
            if (eff > 0)
            {
                float x = sx / eff * _pxToDp;
                float y = (Screen.height - sy / eff) * _pxToDp; // Unity y yukarı doğru; ekran koordinatına çevir
                Feed(!_active || eff > _prevUnityCount ? "down" : "move", eff, x, y, now);
            }
            else if (_active) Feed("up", 0, _lastX, _lastY, now);
            _prevUnityCount = eff;
        }
        else
        {
            if (_active && _prevUnityCount > 0) Feed("up", 0, _lastX, _lastY, now);
            _prevUnityCount = 0;
#if UNITY_EDITOR || UNITY_STANDALONE
            PollEditorInput(now);
#endif
        }
#endif
    }

#if ENABLE_LEGACY_INPUT_MANAGER && (UNITY_EDITOR || UNITY_STANDALONE)
    int _mouseCount;
    void PollEditorInput(float now)
    {
        if (Input.touchSupported && Input.touchCount > 0) return;
        int btn = Input.GetMouseButtonDown(0) ? 1 : Input.GetMouseButtonDown(1) ? 2 : Input.GetMouseButtonDown(2) ? 3 : 0;
        float x = Input.mousePosition.x * _pxToDp;
        float y = (Screen.height - Input.mousePosition.y) * _pxToDp;
        if (btn != 0) { _mouseCount = btn; Feed("down", btn, x, y, now); }
        else if (_active && _mouseCount > 0)
        {
            bool still = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (still) Feed("move", _mouseCount, x, y, now);
            else { Feed("up", 0, x, y, now); _mouseCount = 0; }
        }
        // Klavye yedeği (editör denemesi)
        if (Input.GetKeyDown(KeyCode.Space)) Emit(Gesture.Tap);
        if (Input.GetKeyDown(KeyCode.Return)) Emit(Gesture.DoubleTap);
        if (Input.GetKeyDown(KeyCode.UpArrow)) Emit(Gesture.SwipeUp);
        if (Input.GetKeyDown(KeyCode.DownArrow)) Emit(Gesture.SwipeDown);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Emit(Gesture.SwipeLeft);
        if (Input.GetKeyDown(KeyCode.RightArrow)) Emit(Gesture.SwipeRight);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Emit(Gesture.TwoFingerTap);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Emit(Gesture.ThreeFingerTap);
        if (Input.GetKeyDown(KeyCode.PageUp)) OnVolumeKey("UP|1");
        if (Input.GetKeyUp(KeyCode.PageUp)) OnVolumeKey("UP|0");
        if (Input.GetKeyDown(KeyCode.PageDown)) OnVolumeKey("DOWN|1");
        if (Input.GetKeyUp(KeyCode.PageDown)) OnVolumeKey("DOWN|0");
    }
#endif

    // ================= Android eklentisi =================

    /// <summary>Biçim: "down|move|up;parmakSayısı;x_dp;y_dp;olayZamanıMs"</summary>
    public void OnNativeTouch(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        var p = msg.Split(';');
        if (p.Length < 4) return;
        if (!int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)) return;
        if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return;
        if (!float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return;
        float t = Time.unscaledTime;
        if (p.Length > 4 && long.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms))
            t = ms / 1000f;
        if (!NativeActive)
        {
            NativeActive = true;
            _active = false; _lastTapValid = false; // kaynak değişti: durumu sıfırla
        }
        Feed(p[0], count, x, y, t);
    }

    public void SetTouchExploration(bool on) { TouchExploration = on; }

    // ================= Tanıyıcı =================

    void Feed(string phase, int count, float x, float y, float t)
    {
        switch (phase)
        {
            case "down":
                if (!_active)
                {
                    _active = true; _startT = t; _startX = x; _startY = y; _maxCount = count;
                }
                else if (count > _maxCount) _maxCount = count;
                _lastX = x; _lastY = y; _lastT = t;
                break;
            case "move":
                if (!_active) return;
                if (count > _maxCount) _maxCount = count;
                _lastX = x; _lastY = y; _lastT = t;
                break;
            case "up":
                if (!_active) return;
                if (count > 0) { _lastT = t; return; }
                _lastT = t; _lastX = x; _lastY = y; // ACTION_UP son konumu taşır
                _active = false;
                Recognize();
                break;
        }
    }

    void Recognize()
    {
        float dur = _lastT - _startT;
        if (_maxCount >= 3)
        {
            if (dur <= MultiTapMaxSeconds) Emit(Gesture.ThreeFingerTap);
            return;
        }
        if (_maxCount == 2)
        {
            if (dur <= MultiTapMaxSeconds) Emit(Gesture.TwoFingerTap);
            return;
        }
        float dx = _lastX - _startX, dy = _lastY - _startY;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        if (dist <= TapSlopDp)
        {
            if (dur > TapMaxSeconds) return; // uzun basış: yok say
            bool dbl = _lastTapValid && (_lastT - _lastTapT) <= DoubleTapGapSeconds;
            Emit(Gesture.Tap);
            if (dbl) { Emit(Gesture.DoubleTap); _lastTapValid = false; }
            else { _lastTapValid = true; _lastTapT = _lastT; }
            return;
        }
        _lastTapValid = false;
        if (dist < SwipeMinDp) return; // ölü bölge
        if (Mathf.Abs(dy) > Mathf.Abs(dx)) Emit(dy < 0f ? Gesture.SwipeUp : Gesture.SwipeDown);
        else Emit(dx < 0f ? Gesture.SwipeLeft : Gesture.SwipeRight);
    }

    void Emit(Gesture g)
    {
        try { OnGesture?.Invoke(g); }
        catch (Exception e) { Debug.LogException(e); }
    }

    // ================= Ses tuşları =================

    /// <summary>Biçim: "UP|1" (basıldı), "UP|2" (uzun basış), "UP|0" (bırakıldı); "DOWN|..." aynı.</summary>
    public void OnVolumeKey(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        var p = msg.Split('|');
        if (p.Length < 2) return;
        bool up = p[0] == "UP";
        string state = p[1];
        float now = Time.unscaledTime;

        if (state == "1")
        {
            if (up) _upHeld = true; else _downHeld = true;
            if (_upHeld && _downHeld)
            {
                _pendingKey = 0;
                if (!_bothFired) { _bothFired = true; Emit(Gesture.VolumeBoth); }
            }
            else if (!_bothFired)
            {
                _pendingKey = up ? 1 : 2;
                _pendingAt = now + VolumeBothWindow;
            }
        }
        else if (state == "2")
        {
            if (up && !_bothFired) Emit(Gesture.VolumeUpHold);
        }
        else
        {
            if (up) _upHeld = false; else _downHeld = false;
            if (!_upHeld && !_downHeld) _bothFired = false;
        }
    }

    void UpdateVolumePending()
    {
        if (_pendingKey != 0 && Time.unscaledTime >= _pendingAt)
        {
            int k = _pendingKey;
            _pendingKey = 0;
            Emit(k == 1 ? Gesture.VolumeUp : Gesture.VolumeDown);
        }
    }
}
