using UnityEngine;

/// <summary>
/// Sahne olmadan oyunu kurar: siyah kamera, DontDestroyOnLoad nesneler, bileşenler ve tek ekran görünümü.
/// </summary>
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (GameObject.Find("MadenRoot") != null) return;

        Application.targetFrameRate = 24;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.orientation = ScreenOrientation.Portrait;
        Application.runInBackground = true;
#if UNITY_ANDROID && !UNITY_EDITOR
        // Ekranı çizmeye ihtiyaç yok: pil için düşük çözünürlük.
        Screen.SetResolution(Screen.width / 2, Screen.height / 2, true);
#endif

        // Siyah kamera (AudioListener de burada)
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 0;
        cam.orthographic = true;
        camGo.AddComponent<AudioListener>();
        Object.DontDestroyOnLoad(camGo);

        // Ana nesne ve bileşenler
        var root = new GameObject("MadenRoot");
        Object.DontDestroyOnLoad(root);
        var speech = root.AddComponent<Speech>();
        var sound = root.AddComponent<SoundPlayer>();
        var router = root.AddComponent<InputRouter>();
        var view = root.AddComponent<MadenView>();
        var controller = root.AddComponent<GameController>();
        controller.Speech = speech;
        controller.Sound = sound;
        controller.Router = router;
        view.Speech = speech;

        // Android eklentisinin UnitySendMessage hedefi (ad değişmemeli)
        var bridgeGo = new GameObject("MadenBridge");
        Object.DontDestroyOnLoad(bridgeGo);
        var bridge = bridgeGo.AddComponent<MadenBridge>();
        bridge.Router = router;
    }
}

/// <summary>Tek büyük "Maden" yazısı ve altında son söylenen kısa durum metni (yüksek karşıtlık).</summary>
public class MadenView : MonoBehaviour
{
    public Speech Speech;
    GUIStyle _title, _status;
    int _lastH;

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (_title == null || _lastH != Screen.height)
        {
            _lastH = Screen.height;
            _title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Screen.height * 0.12f),
                fontStyle = FontStyle.Bold
            };
            _title.normal.textColor = Color.white;
            _status = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Mathf.RoundToInt(Screen.height * 0.035f),
                wordWrap = true
            };
            _status.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        }
        float w = Screen.width, h = Screen.height;
        GUI.Label(new Rect(0, h * 0.25f, w, h * 0.2f), "Maden", _title);
        string text = Speech != null ? Speech.LastText : "";
        if (!string.IsNullOrEmpty(text))
        {
            if (text.Length > 140) text = text.Substring(0, 140) + "…";
            GUI.Label(new Rect(w * 0.08f, h * 0.5f, w * 0.84f, h * 0.3f), text, _status);
        }
    }
}
