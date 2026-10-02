using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmashBots.Editor {

// Post-processes the generated glTF models: bakes emission maps from the
// base color textures (the generator outputs no emission) and creates
// editable material variants that use them.
public static class ModelProcessor
{
    public enum GlowMode { Warm, Cool, WarmWide, White }

    public static readonly (string name, GlowMode mode, float intensity)[] Models =
    {
        ("RobotPlayer", GlowMode.Warm, 4.0f),
        ("RobotEnemy", GlowMode.Cool, 4.0f),
        ("RacketPlayer", GlowMode.Warm, 6.0f),
        ("RacketEnemy", GlowMode.Cool, 6.0f),
        ("NetPost", GlowMode.WarmWide, 5.0f),
        ("WallPillar", GlowMode.White, 3.0f),
    };

    const string ModelDir = "Assets/Generated/Models/";
    const string EmissionDir = "Assets/Textures/Emission/";
    const string MaterialDir = "Assets/Materials/Models/";

    public static string MaterialPath(string name) => MaterialDir + name + ".mat";
    public static string ModelPath(string name) => ModelDir + name + ".glb";

    [MenuItem("Smash Bots/Process Models")]
    public static void ProcessAll()
    {
        Directory.CreateDirectory(EmissionDir);
        Directory.CreateDirectory(MaterialDir);
        foreach (var (name, mode, intensity) in Models) Process(name, mode, intensity);
        AssetDatabase.SaveAssets();
    }

    static void Process(string name, GlowMode mode, float intensity)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name));
        if (model == null) { Debug.LogWarning($"Model not found: {name}"); return; }

        var src = model.GetComponentInChildren<Renderer>().sharedMaterial;
        var baseTex = src.GetTexture("baseColorTexture");

        // Emission map
        var texPath = EmissionDir + name + "_Emission.png";
        File.WriteAllBytes(texPath, BakeEmission(baseTex, mode, 1024).EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.sRGBTexture = true;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // Material variant
        var matPath = MaterialPath(name);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(src);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.CopyPropertiesFromMaterial(src);
        }
        mat.SetTexture("emissiveTexture", emission);
        mat.SetColor("emissiveFactor", Color.white * intensity);
        EditorUtility.SetDirty(mat);
    }

    static Texture2D BakeEmission(Texture source, GlowMode mode, int size)
    {
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var pixels = tex.GetPixels();
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            var m = Mask(c, mode);
            pixels[i] = new Color(c.r * m, c.g * m, c.b * m);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static float Mask(Color c, GlowMode mode)
    {
        switch (mode)
        {
            case GlowMode.Warm:
                // Bright yellowish glow, excluding orange paint and white panels
                return Smooth(0.72f, 0.88f, c.g) * Smooth(0.10f, 0.22f, c.r - c.b);
            case GlowMode.Cool:
                // Light blue glow, excluding blue paint and white panels
                return Smooth(0.62f, 0.80f, c.g) * Smooth(0.08f, 0.20f, c.b - c.r) * Smooth(0.85f, 0.95f, c.b);
            case GlowMode.WarmWide:
                // Any bright orange or yellow
                return Smooth(0.55f, 0.75f, c.r) * Smooth(0.25f, 0.40f, c.r - c.b);
            default:
                // Near-white light strips only
                return Smooth(0.93f, 0.98f, Mathf.Min(c.r, Mathf.Min(c.g, c.b)));
        }
    }

    static float Smooth(float a, float b, float x)
    {
        var t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }
}

} // namespace SmashBots.Editor
