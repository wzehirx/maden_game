using UnityEngine;

/// <summary>
/// Android eklentisinden (MadenActivity) UnitySendMessage ile gelen iletileri alır.
/// GameObject adı TAM OLARAK "MadenBridge" olmalıdır.
/// </summary>
public class MadenBridge : MonoBehaviour
{
    public InputRouter Router;

    public void OnNativeTouch(string msg) { if (Router != null) Router.OnNativeTouch(msg); }
    public void OnVolumeKey(string msg) { if (Router != null) Router.OnVolumeKey(msg); }
    public void OnTouchExploration(string msg) { if (Router != null) Router.SetTouchExploration(msg == "1"); }

    /// <summary>TalkBack gibi dokunarak keşfet açık mı? (Eklenti yoksa false.)</summary>
    public static bool QueryTouchExploration()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var cls = new AndroidJavaClass("com.maden.oyun.MadenActivity"))
                return cls.CallStatic<bool>("isTouchExplorationEnabled");
        }
        catch (System.Exception e) { Debug.LogWarning("Dokunarak keşfet sorgusu: " + e.Message); }
#endif
        return false;
    }
}
