using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Komut satırı derlemesi:
/// Unity -batchmode -nographics -quit -projectPath . -buildTarget Android -executeMethod MadenBuild.BuildAndroid
/// Çıktı: Builds/Maden.apk (ya da -customBuildPath ile verilen yer).
/// </summary>
public static class MadenBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string DefaultOutput = "Builds/Maden.apk";

    /// <summary>Yalnızca ayarları uygular (Input handling gibi bazıları sonraki açılışta etkili olur).</summary>
    public static void Configure()
    {
        try { ApplySettings(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogError("Maden ayar hatası: " + e); EditorApplication.Exit(1); }
    }

    public static void BuildAndroid()
    {
        try
        {
            ApplySettings();
            EnsureScene();

            string output = ResolveOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log("Maden derleme sonucu: " + summary.result + ", boyut: " + summary.totalSize + " bayt, süre: " + summary.totalTime);
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Maden derlemesi BAŞARISIZ: " + summary.result + " (hata sayısı " + summary.totalErrors + ")");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("Maden APK hazır: " + Path.GetFullPath(output));
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("Maden derleme istisnası: " + e);
            EditorApplication.Exit(1);
        }
    }

    static string ResolveOutputPath()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-customBuildPath" || args[i] == "-outputPath")
            {
                string p = args[i + 1];
                return p.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) ? p : Path.Combine(p, "Maden.apk");
            }
        }
        return DefaultOutput;
    }

    static void ApplySettings()
    {
        PlayerSettings.companyName = "Maden";
        PlayerSettings.productName = "Maden";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.maden.oyun");
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.Android.bundleVersionCode = 1;

        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto; // yüklü en yüksek API
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);

        // Unity 6: GameActivity yerine Activity (MadenActivity, UnityPlayerActivity'yi genişletir).
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.runInBackground = true;

        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        SetActiveInputHandlerBoth();
    }

    /// <summary>Active Input Handling = Both (2). Genel API yok; ProjectSettings.asset üzerinden yazılır.</summary>
    static void SetActiveInputHandlerBoth()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) { Debug.LogWarning("ProjectSettings.asset bulunamadı; Input handling atlandı."); return; }
        var so = new SerializedObject(assets[0]);
        var prop = so.FindProperty("activeInputHandler");
        if (prop == null) { Debug.LogWarning("activeInputHandler özelliği bulunamadı."); return; }
        if (prop.intValue != 2)
        {
            prop.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Active Input Handling = Both olarak ayarlandı (tam etki için düzenleyicinin yeniden açılması gerekebilir).");
        }
    }

    static void EnsureScene()
    {
        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}

/// <summary>Editör her açıldığında Input handling'i Both yapar (tek seferlik ayar).</summary>
[InitializeOnLoad]
static class MadenProjectSetup
{
    static MadenProjectSetup()
    {
        EditorApplication.delayCall += () =>
        {
            try
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0) return;
                var so = new SerializedObject(assets[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 2)
                {
                    prop.intValue = 2;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception e) { Debug.LogWarning("Maden proje ayarı: " + e.Message); }
        };
    }
}
