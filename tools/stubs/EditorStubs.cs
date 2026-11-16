// UnityEditor API'sinin Assets/Editor betiklerinin kullandığı alt kümesi (yalnızca sözdizimi kontrolü içindir).
#pragma warning disable CS0649, CS8618
using System;

namespace UnityEngine { public class Object { } public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) { } } }

namespace UnityEditor
{
    public enum BuildTarget { Android } public enum BuildTargetGroup { Android } [Flags] public enum BuildOptions { None = 0 }
    public enum AndroidBuildSystem { Gradle } public enum UIOrientation { Portrait }
    public enum AndroidSdkVersions { AndroidApiLevel26, AndroidApiLevelAuto }
    public enum ScriptingImplementation { IL2CPP } public enum ManagedStrippingLevel { Minimal }
    [Flags] public enum AndroidArchitecture { ARMv7 = 1, ARM64 = 2 }
    [Flags] public enum AndroidApplicationEntry { Activity = 1, GameActivity = 2 }
    public class InitializeOnLoadAttribute : Attribute { }
    public static class EditorApplication { public static Action delayCall; public static void Exit(int c) { } }
    public static class EditorUserBuildSettings { public static bool buildAppBundle; public static AndroidBuildSystem androidBuildSystem; }
    public static class AssetDatabase
    {
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string p) => null; public static void SaveAssets() { } public static void Refresh() { }
    }
    public class SerializedProperty { public int intValue; }
    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object o) { } public SerializedProperty FindProperty(string n) => null;
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
    }
    public class BuildPlayerOptions { public string[] scenes; public string locationPathName; public BuildTarget target; public BuildTargetGroup targetGroup; public BuildOptions options; }
    public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e) { } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
    public static class PlayerSettings
    {
        public static string companyName, productName, bundleVersion;
        public static bool allowedAutorotateToPortrait, allowedAutorotateToPortraitUpsideDown, allowedAutorotateToLandscapeLeft, allowedAutorotateToLandscapeRight, runInBackground;
        public static UIOrientation defaultInterfaceOrientation;
        public static void SetApplicationIdentifier(Build.NamedBuildTarget t, string id) { }
        public static void SetScriptingBackend(Build.NamedBuildTarget t, ScriptingImplementation s) { }
        public static void SetManagedStrippingLevel(Build.NamedBuildTarget t, ManagedStrippingLevel l) { }
        public static class Android
        {
            public static int bundleVersionCode; public static AndroidSdkVersions minSdkVersion, targetSdkVersion;
            public static AndroidArchitecture targetArchitectures; public static AndroidApplicationEntry applicationEntry;
        }
        public static class SplashScreen { public static bool show, showUnityLogo; }
    }
}
namespace UnityEditor.Build
{
    public struct NamedBuildTarget { public static NamedBuildTarget Android; }
}
namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Succeeded, Failed }
    public class BuildSummary { public BuildResult result; public ulong totalSize; public TimeSpan totalTime; public int totalErrors; }
    public class BuildReport { public BuildSummary summary; }
}
namespace UnityEditor
{
    public static class BuildPipeline { public static Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o) => null; }
}
namespace UnityEditor.SceneManagement
{
    public struct Scene { }
    public enum NewSceneSetup { EmptyScene } public enum NewSceneMode { Single }
    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup s, NewSceneMode m) => default;
        public static bool SaveScene(Scene s, string p) => true;
    }
}
namespace UnityEditor.Android
{
    public interface IPostGenerateGradleAndroidProject { int callbackOrder { get; } void OnPostGenerateGradleAndroidProject(string path); }
}
