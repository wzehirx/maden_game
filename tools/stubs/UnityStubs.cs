// Unity API'sinin Maden'in kullandığı alt kümesini taklit eden BOŞ sınıflar.
// Amaç yalnızca sözdizimi / tip kontrolüdür; davranış yoktur. Unity'ye girmez (Assets dışında).
#pragma warning disable CS0067, CS0169, CS0649, CS8618
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void DontDestroyOnLoad(Object o) { }
        public static implicit operator bool(Object o) => o != null;
    }
    public class Component : Object
    {
        public GameObject gameObject; public Transform transform;
        public T GetComponent<T>() => default;
        public Coroutine StartCoroutine(IEnumerator e) => null;
    }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component { public void SetParent(Transform p, bool worldPositionStays) { } }
    public class GameObject : Object
    {
        public string tag; public Transform transform;
        public GameObject() { } public GameObject(string name) { }
        public T AddComponent<T>() where T : Component, new() => new T();
        public static GameObject Find(string n) => null;
    }
    public class Coroutine { }
    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }
    public static class Debug
    {
        public static void Log(object m) { } public static void LogWarning(object m) { } public static void LogError(object m) { }
        public static void LogException(Exception e) { }
    }
    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Abs(float f) => Math.Abs(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Sign(float f) => f >= 0 ? 1 : -1;
        public static float Clamp(float v, float a, float b) => Math.Min(Math.Max(v, a), b);
        public static int Clamp(int v, int a, int b) => Math.Min(Math.Max(v, a), b);
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 1e-6f;
        public static int RoundToInt(float f) => (int)Math.Round(f);
    }
    public struct Vector3 { public float x, y, z; }
    public struct Vector2 { public float x, y; }
    public struct Color
    {
        public float r, g, b, a; public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; }
        public static Color black, white;
    }
    public struct Rect { public Rect(float x, float y, float w, float h) { } }
    public static class Random
    {
        public static float Range(float a, float b) => a;
        public static int Range(int a, int b) => a;
    }
    public static class Time { public static float unscaledTime, unscaledDeltaTime; }
    public static class Application
    {
        public static int targetFrameRate; public static bool runInBackground;
        public static string streamingAssetsPath, persistentDataPath;
    }
    public static class QualitySettings { public static int vSyncCount; }
    public static class SleepTimeout { public const int NeverSleep = -1; }
    public enum ScreenOrientation { Portrait }
    public static class Screen
    {
        public static int sleepTimeout; public static ScreenOrientation orientation;
        public static int width, height; public static float dpi;
        public static void SetResolution(int w, int h, bool fs) { }
    }
    public class Display { public static Display main; public int systemWidth; }
    public enum CameraClearFlags { SolidColor }
    public class Camera : Behaviour { public CameraClearFlags clearFlags; public Color backgroundColor; public int cullingMask; public bool orthographic; }
    public class AudioListener : Behaviour { }
    public class AudioClip : Object
    {
        public float length;
        public static AudioClip Create(string n, int len, int ch, int sr, bool stream) => new AudioClip();
        public bool SetData(float[] d, int off) => true;
    }
    public class AudioSource : Behaviour
    {
        public AudioClip clip; public bool loop, playOnAwake; public float spatialBlend, pitch, volume, time; public bool isPlaying;
        public void Play() { } public void Stop() { } public void PlayOneShot(AudioClip c, float v) { }
    }
    public class TextAsset : Object { public string text; }
    public static class Resources
    {
        public static T Load<T>(string p) where T : Object => null;
        public static T[] LoadAll<T>(string p) where T : Object => null;
    }
    public static class PlayerPrefs
    {
        public static int GetInt(string k, int d) => d; public static void SetInt(string k, int v) { }
        public static string GetString(string k, string d) => d; public static void SetString(string k, string v) { }
        public static void Save() { }
    }
    public static class Handheld { public static void Vibrate() { } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad }
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    [AttributeUsage(AttributeTargets.Field)]
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }

    // Giriş (eski Input Manager)
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public TouchPhase phase; public Vector2 position; }
    public enum KeyCode { Space, Return, UpArrow, DownArrow, LeftArrow, RightArrow, Alpha2, Alpha3, PageUp, PageDown }
    public static class Input
    {
        public static int touchCount; public static bool touchSupported; public static Vector3 mousePosition;
        public static Touch GetTouch(int i) => default;
        public static bool GetMouseButtonDown(int b) => false; public static bool GetMouseButton(int b) => false;
        public static bool GetKeyDown(KeyCode k) => false; public static bool GetKeyUp(KeyCode k) => false;
    }

    // IMGUI
    public enum TextAnchor { UpperCenter, MiddleCenter }
    public enum FontStyle { Bold }
    public enum EventType { Repaint }
    public class Event { public static Event current; public EventType type; }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle
    {
        public GUIStyle() { } public GUIStyle(GUIStyle o) { }
        public TextAnchor alignment; public int fontSize; public FontStyle fontStyle; public bool wordWrap;
        public GUIStyleState normal = new GUIStyleState();
    }
    public class GUISkin { public GUIStyle label; }
    public static class GUI
    {
        public static GUISkin skin;
        public static void Label(Rect r, string t, GUIStyle s) { }
    }

    // Android JNI
    public class AndroidJavaObject : IDisposable
    {
        public AndroidJavaObject(string cls, params object[] args) { }
        public void Call(string m, params object[] a) { }
        public T Call<T>(string m, params object[] a) => default;
        public T GetStatic<T>(string f) => default;
        public void Dispose() { }
    }
    public class AndroidJavaClass : AndroidJavaObject
    {
        public AndroidJavaClass(string cls) : base(cls) { }
        public T CallStatic<T>(string m, params object[] a) => default;
        public void CallStatic(string m, params object[] a) { }
    }
    public class AndroidJavaProxy { public AndroidJavaProxy(string iface) { } }
    public delegate void AndroidJavaRunnable();
}

namespace UnityEngine.Networking
{
    public class DownloadHandler { public string text; }
    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public Result result; public string error; public DownloadHandler downloadHandler;
        public static UnityWebRequest Get(string url) => new UnityWebRequest();
        public YieldInstruction SendWebRequest() => null;
        public void Dispose() { }
    }
}

// Not: gerçek Unity'de "yield return req.SendWebRequest()" AsyncOperation döndürür; stub'ta YieldInstruction yeterli.
