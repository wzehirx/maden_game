using System.IO;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Gradle projesi üretildikten sonra başlatıcı etkinliği com.maden.oyun.MadenActivity ile değiştirir.
/// (Kütüphane manifestleri ana manifestin başlatıcı girdisini ezemediği için derleme sonrası yama gerekir.)
/// </summary>
public class AndroidManifestPatcher : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 100;

    const string Custom = "com.maden.oyun.MadenActivity";
    static readonly string[] Originals =
    {
        "com.unity3d.player.UnityPlayerActivity",
        "com.unity3d.player.UnityPlayerGameActivity"
    };

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        int patched = 0;
        string root = Directory.GetParent(path)?.FullName ?? path;
        foreach (var file in Directory.GetFiles(root, "AndroidManifest.xml", SearchOption.AllDirectories))
        {
            // Yalnızca kaynak manifestleri (build çıktılarını değil).
            if (file.Contains(Path.DirectorySeparatorChar + "build" + Path.DirectorySeparatorChar)) continue;
            string text = File.ReadAllText(file);
            string updated = text;
            foreach (var o in Originals)
                updated = updated.Replace("android:name=\"" + o + "\"", "android:name=\"" + Custom + "\"");
            if (updated != text)
            {
                File.WriteAllText(file, updated);
                patched++;
                Debug.Log("MadenActivity yaması: " + file);
            }
        }
        if (patched == 0)
            Debug.LogWarning("UYARI: Hiçbir manifestte başlatıcı etkinlik bulunamadı; MadenActivity kullanılmayabilir (dokunma yalnız Unity Input ile çalışır).");
    }
}
