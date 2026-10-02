using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmashBots.Editor {

// Textures and materials used by the scene builder
public static partial class SceneBuilder
{
    const string GenTexDir = "Assets/Generated/Textures/";
    const string GenUiDir = "Assets/Generated/UI/";
    const string ProcTexDir = "Assets/Textures/Procedural/";
    const string MaterialDir = "Assets/Materials/";
    const string MeshDir = "Assets/Meshes/";
    const string PrefabDir = "Assets/Prefabs/";
    const string AudioDir = "Assets/Generated/Audio/";

    static class Mats
    {
        public static Material Floor, Wall, WallDark, Ceiling, BackWall, Reflection;
        public static Material LightWhite, LightCool, CourtLine, Laser, NetMesh;
        public static Material Ball, Trail, Shadow;
        public static Material FxGlow, FxSpark, FxRing, FxBurst, FxRingFloor;
        public static Material StringsPlayer, StringsCpu, ThrusterPlayer, ThrusterCpu;
    }

    static class Tex
    {
        public static Texture2D SoftDot, Ring, StringsGrid, NetGrid, TrailProfile;
    }

    static void PrepareAssets()
    {
        AssetDatabase.DeleteAsset(MeshDir.TrimEnd('/'));
        foreach (var dir in new[] { ProcTexDir, MaterialDir, MeshDir, PrefabDir, "Assets/Settings/" }) EnsureFolder(dir);

        // Import settings of the generated images
        foreach (var n in new[] { "FloorTile", "WallPanel", "CeilingPanel", "BallTexture" })
            ConfigureTexture(GenTexDir + n + ".png", true, true, false, 2048);
        ConfigureTexture(GenTexDir + "BackWall.png", false, true, false, 2048);
        ConfigureTexture(GenTexDir + "ImpactBurst.png", false, true, true, 1024);
        foreach (var n in new[] { "Logo", "SmashText", "ReticleRing", "Chevron", "PanelOrange", "PanelBlue", "SpeedLines" })
            ConfigureTexture(GenUiDir + n + ".png", false, false, true, 2048);

        // Procedural textures
        Tex.SoftDot = SaveTexture(MakeSoftDot(128), "SoftDot", false);
        Tex.Ring = SaveTexture(MakeRing(256), "Ring", false);
        Tex.StringsGrid = SaveTexture(MakeStringsGrid(512), "StringsGrid", false);
        Tex.NetGrid = SaveTexture(MakeNetGrid(128), "NetGrid", true);
        Tex.TrailProfile = SaveTexture(MakeTrailProfile(64), "TrailProfile", false);
        var floorEm = SaveTexture(DeriveMask(Load<Texture2D>(GenTexDir + "FloorTile.png"), 1024,
                                             c => Smooth(0.62f, 0.85f, c.b) * Smooth(0.15f, 0.3f, c.b - c.r)), "FloorTile_Emission", true);
        var backEm = SaveTexture(DeriveMask(Load<Texture2D>(GenTexDir + "BackWall.png"), 1024,
                                            c => Smooth(0.93f, 0.99f, c.b) * Smooth(0.03f, 0.1f, c.b - c.r)), "BackWall_Emission", false);

        // Environment materials
        Mats.Floor = LitMaterial("Floor", Load<Texture2D>(GenTexDir + "FloorTile.png"), 0.72f, 0.15f, floorEm, new Color(0.8f, 1.2f, 2.2f) * 1.1f);
        Mats.Reflection = GetMaterial("FloorReflection", Shader.Find("SmashBots/PlanarReflection"));
        Mats.Reflection.SetFloat("_Strength", 0.3f);
        Mats.Reflection.SetFloat("_Blur", 4.0f);
        Mats.Reflection.SetFloat("_Fresnel", 2.5f);
        Mats.Wall = LitMaterial("Wall", Load<Texture2D>(GenTexDir + "WallPanel.png"), 0.45f, 0.0f);
        Mats.WallDark = LitMaterial("WallDark", Load<Texture2D>(GenTexDir + "WallPanel.png"), 0.5f, 0.1f);
        Mats.WallDark.SetColor("_BaseColor", new Color(0.62f, 0.66f, 0.74f));
        Mats.Ceiling = LitMaterial("Ceiling", Load<Texture2D>(GenTexDir + "CeilingPanel.png"), 0.35f, 0.0f);
        Mats.BackWall = LitMaterial("BackWall", Load<Texture2D>(GenTexDir + "BackWall.png"), 0.45f, 0.0f, backEm, new Color(2.5f, 2.8f, 3.4f));

        Mats.LightWhite = UnlitMaterial("LightWhite", new Color(3.2f, 3.3f, 3.6f));
        Mats.LightCool = UnlitMaterial("LightCool", new Color(1.6f, 2.6f, 5.0f));
        Mats.CourtLine = UnlitMaterial("CourtLine", new Color(1.6f, 2.8f, 5.5f));
        Mats.Laser = UnlitMaterial("Laser", new Color(6.0f, 1.2f, 0.15f));
        Mats.NetMesh = EffectMaterial("NetMesh", Tex.NetGrid, new Color(1.6f, 0.45f, 0.08f, 0.3f), true);
        Mats.NetMesh.SetTextureScale("_MainTex", new Vector2(40, 4));

        // Ball
        var ballTex = Load<Texture2D>(GenTexDir + "BallTexture.png");
        Mats.Ball = LitMaterial("Ball", ballTex, 0.3f, 0.0f, ballTex, new Color(0.55f, 0.65f, 0.25f));
        Mats.Trail = EffectMaterial("Trail", Tex.TrailProfile, new Color(1.6f, 1.6f, 1.6f), true);
        Mats.Shadow = EffectMaterial("Shadow", Tex.SoftDot, new Color(0, 0, 0, 0.5f), false);

        // Effects
        Mats.FxGlow = EffectMaterial("FxGlow", Tex.SoftDot, Color.white * 3, true);
        Mats.FxSpark = EffectMaterial("FxSpark", Tex.SoftDot, Color.white * 5, true);
        Mats.FxRing = EffectMaterial("FxRing", Tex.Ring, Color.white * 3, true);
        Mats.FxRingFloor = EffectMaterial("FxRingFloor", Tex.Ring, Color.white * 2.5f, true);
        Mats.FxBurst = EffectMaterial("FxBurst", Load<Texture2D>(GenTexDir + "ImpactBurst.png"), Color.white * 1.4f, true);
        Mats.StringsPlayer = EffectMaterial("StringsPlayer", Tex.StringsGrid, new Color(4.0f, 1.6f, 0.4f, 0.9f), true);
        Mats.StringsCpu = EffectMaterial("StringsCpu", Tex.StringsGrid, new Color(0.6f, 1.6f, 5.0f, 0.9f), true);
        Mats.ThrusterPlayer = EffectMaterial("ThrusterPlayer", Tex.SoftDot, new Color(4.0f, 1.5f, 0.4f), true);
        Mats.ThrusterCpu = EffectMaterial("ThrusterCpu", Tex.SoftDot, new Color(0.6f, 1.5f, 5.0f), true);

        AssetDatabase.SaveAssets();
    }

    // Asset helpers

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    static void EnsureFolder(string path)
    {
        path = path.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void ConfigureTexture(string path, bool repeat, bool mips, bool alpha, int maxSize, bool srgb = true)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) { Debug.LogWarning($"Texture not found: {path}"); return; }
        imp.textureType = TextureImporterType.Default;
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.mipmapEnabled = mips;
        imp.alphaIsTransparency = alpha;
        imp.maxTextureSize = maxSize;
        imp.sRGBTexture = srgb;
        imp.anisoLevel = repeat ? 8 : 1;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
    }

    static Texture2D SaveTexture(Texture2D tex, string name, bool repeat)
    {
        var path = ProcTexDir + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        ConfigureTexture(path, repeat, true, true, 2048);
        return Load<Texture2D>(path);
    }

    static Material GetMaterial(string name, Shader shader)
    {
        var path = MaterialDir + name + ".mat";
        var m = Load<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = shader;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LitMaterial(string name, Texture baseMap, float smoothness, float metallic,
                                Texture emissionMap = null, Color emission = default)
    {
        var m = GetMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", baseMap);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emissionMap != null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", emissionMap);
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
        }
        return m;
    }

    static Material UnlitMaterial(string name, Color color)
    {
        var m = GetMaterial(name, Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", color);
        return m;
    }

    static Material EffectMaterial(string name, Texture tex, Color color, bool additive)
    {
        var m = GetMaterial(name, Shader.Find("SmashBots/Effect"));
        m.SetTexture("_MainTex", tex);
        m.SetColor("_Color", color);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.renderQueue = additive ? 3100 : 3000;
        return m;
    }

    // Procedural textures

    static float Smooth(float a, float b, float x)
    {
        var t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }

    static Texture2D NewTexture(int w, int h, System.Func<float, float, Color> f)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Texture2D MakeSoftDot(int size) => NewTexture(size, size, (u, v) =>
    {
        var r = Mathf.Min(1, new Vector2(u - 0.5f, v - 0.5f).magnitude * 2);
        var a = Mathf.Pow(1 - r, 2.2f);
        return new Color(1, 1, 1, a);
    });

    static Texture2D MakeRing(int size) => NewTexture(size, size, (u, v) =>
    {
        var r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2;
        var a = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.05f, 2)) + 0.25f * Mathf.Exp(-Mathf.Pow((r - 0.75f) / 0.15f, 2));
        a *= Smooth(1.0f, 0.95f, r);
        return new Color(1, 1, 1, Mathf.Clamp01(a));
    });

    static Texture2D MakeStringsGrid(int size) => NewTexture(size, size, (u, v) =>
    {
        var r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2;
        var inside = Smooth(1.0f, 0.94f, r);
        const float n = 15;
        var gx = Mathf.Abs(Mathf.Repeat(u * n, 1) - 0.5f) * 2;
        var gy = Mathf.Abs(Mathf.Repeat(v * n, 1) - 0.5f) * 2;
        var line = Mathf.Max(Smooth(0.82f, 0.97f, gx), Smooth(0.82f, 0.97f, gy));
        var a = (0.12f + 0.75f * line) * inside;
        return new Color(1, 1, 1, a);
    });

    static Texture2D MakeNetGrid(int size) => NewTexture(size, size, (u, v) =>
    {
        var gx = Mathf.Abs(u - 0.5f) * 2;
        var gy = Mathf.Abs(v - 0.5f) * 2;
        var line = Mathf.Max(Smooth(0.8f, 0.97f, gx), Smooth(0.8f, 0.97f, gy));
        return new Color(1, 1, 1, line * 0.8f + 0.03f);
    });

    static Texture2D MakeTrailProfile(int size) => NewTexture(size, size, (u, v) =>
    {
        var a = Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.22f, 2));
        return new Color(1, 1, 1, a);
    });

    // Emission mask derived from a color texture
    static Texture2D DeriveMask(Texture source, int size, System.Func<Color, float> mask)
    {
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels();
        for (var i = 0; i < px.Length; i++)
        {
            var c = px[i];
            var m = mask(c);
            px[i] = new Color(c.r * m, c.g * m, c.b * m, 1);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}

} // namespace SmashBots.Editor
