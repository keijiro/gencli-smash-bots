using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SmashBots.Editor {

// Builds the whole game scene (environment, actors, systems) from the
// generated assets and saves it as Assets/Main.unity.
public static partial class SceneBuilder
{
    const string ScenePath = "Assets/Main.unity";
    public const int ActorLayer = 6;
    public const int NoReflectLayer = 7;

    // Hall dimensions
    const float HallHalfWidth = 9;
    const float HallHalfLength = 22;
    const float WallTop = 6;
    const float CeilingY = 8.5f;
    const float CeilingHalfWidth = 6.5f;
    const float SkirtTop = 0.8f;
    const float SkirtInset = 0.8f;

    [MenuItem("Smash Bots/Build Scene")]
    public static void Build()
    {
        PrepareAssets();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildEnvironment();
        var player = BuildRobot(Side.Player);
        var cpu = BuildRobot(Side.Cpu);
        var ball = BuildBall();
        BuildSystems(player, cpu, ball);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
    }

    // Environment

    static Transform _envRoot;

    static void BuildEnvironment()
    {
        _envRoot = new GameObject("Environment").transform;

        var w = HallHalfWidth;
        var l = HallHalfLength;

        // Floor (2 m tiles) and its planar reflection overlay
        var floor = Surface("Floor", Mats.Floor, Vector3.up, 2,
                            new Vector3(-w, 0, -l), new Vector3(w, 0, -l), new Vector3(w, 0, l), new Vector3(-w, 0, l));
        floor.layer = NoReflectLayer;
        var refl = Surface("Floor Reflection", Mats.Reflection, Vector3.up, 0,
                           new Vector3(-w, 0.004f, -l), new Vector3(w, 0.004f, -l), new Vector3(w, 0.004f, l), new Vector3(-w, 0.004f, l), null, false);
        refl.layer = NoReflectLayer;

        foreach (var s in new[] { -1, 1 })
        {
            var name = s < 0 ? "Left" : "Right";
            var inward = new Vector3(-s, 0, 0);

            // Angled skirt, vertical wall, upper slant
            Surface(name + " Skirt", Mats.WallDark, (inward + Vector3.up).normalized, 1.13f,
                    new Vector3(s * (w - SkirtInset), 0, -l), new Vector3(s * (w - SkirtInset), 0, l),
                    new Vector3(s * w, SkirtTop, l), new Vector3(s * w, SkirtTop, -l));
            Surface(name + " Wall", Mats.Wall, inward, 2.6f,
                    new Vector3(s * w, SkirtTop, -l), new Vector3(s * w, SkirtTop, l),
                    new Vector3(s * w, WallTop, l), new Vector3(s * w, WallTop, -l));
            Surface(name + " Slant", Mats.Wall, (inward - Vector3.up).normalized, 3.54f,
                    new Vector3(s * w, WallTop, -l), new Vector3(s * w, WallTop, l),
                    new Vector3(s * CeilingHalfWidth, CeilingY, l), new Vector3(s * CeilingHalfWidth, CeilingY, -l));

            // Light bar along the top of the skirt
            Box(name + " Skirt Light", Mats.LightCool, new Vector3(s * (w - 0.03f), SkirtTop + 0.06f, 0), new Vector3(0.05f, 0.07f, 2 * l));
            // Light bar along the top of the wall
            Box(name + " Wall Light", Mats.LightWhite, new Vector3(s * (w - 0.04f), WallTop - 0.2f, 0), new Vector3(0.06f, 0.1f, 2 * l));
        }

        // Ceiling and ceiling lights
        Surface("Ceiling", Mats.Ceiling, Vector3.down, 3.25f,
                new Vector3(-CeilingHalfWidth, CeilingY, -l), new Vector3(CeilingHalfWidth, CeilingY, -l),
                new Vector3(CeilingHalfWidth, CeilingY, l), new Vector3(-CeilingHalfWidth, CeilingY, l));
        foreach (var x in new[] { -2.4f, 2.4f })
            Box("Ceiling Light", Mats.LightWhite, new Vector3(x, CeilingY - 0.05f, 0), new Vector3(0.4f, 0.08f, 2 * l - 1));
        Box("Skylight", Mats.LightCool, new Vector3(0, CeilingY - 0.04f, 0), new Vector3(1.6f, 0.05f, 12));

        // End walls
        foreach (var s in new[] { -1, 1 })
        {
            var z = s * l;
            Surface(s < 0 ? "Front Wall" : "Back Wall", Mats.Wall, new Vector3(0, 0, -s), 2.6f,
                    new Vector3(-w, 0, z), new Vector3(w, 0, z), new Vector3(w, CeilingY, z), new Vector3(-w, CeilingY, z));
            Surface(s < 0 ? "Front Door" : "Back Door", Mats.BackWall, new Vector3(0, 0, -s), 0,
                    new Vector3(-6.4f, 0, z - s * 0.02f), new Vector3(6.4f, 0, z - s * 0.02f),
                    new Vector3(6.4f, 8.5f, z - s * 0.02f), new Vector3(-6.4f, 8.5f, z - s * 0.02f));
        }

        // Structural pillars with ceiling ribs
        var pillar = Load<GameObject>(ModelProcessor.ModelPath("WallPillar"));
        var pillarMat = Load<Material>(ModelProcessor.MaterialPath("WallPillar"));
        for (var i = 0; i < 8; i++)
        {
            var z = -19.25f + i * 5.5f;
            foreach (var s in new[] { -1, 1 })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pillar, _envRoot);
                go.name = "Pillar";
                go.transform.localScale = Vector3.one * 6.2f;
                go.transform.position = new Vector3(s * (w - 1.3f + 0.05f), 3.1f, z);
                go.transform.rotation = Quaternion.Euler(0, s < 0 ? 180 : 0, 0);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = pillarMat;
            }
            Box("Ceiling Rib Light", Mats.LightWhite, new Vector3(0, CeilingY - 0.06f, z), new Vector3(2 * CeilingHalfWidth - 0.4f, 0.06f, 0.18f));

            // Wall light panels between pillars
            if (i < 7)
            {
                foreach (var s in new[] { -1, 1 })
                    Box("Wall Panel Light", Mats.LightWhite, new Vector3(s * (w - 0.03f), 1.55f, z + 2.75f), new Vector3(0.05f, 0.45f, 1.3f));
            }
        }

        BuildCourt();
        BuildNet();
    }

    static void BuildCourt()
    {
        var root = new GameObject("Court Lines").transform;
        root.SetParent(_envRoot);
        const float y = 0.006f, t = 0.07f;
        var hw = Court.HalfWidth;
        var hl = Court.HalfLength;
        Box("Sideline", Mats.CourtLine, new Vector3(-hw, y, 0), new Vector3(t, 0.01f, 2 * hl + t), root);
        Box("Sideline", Mats.CourtLine, new Vector3(hw, y, 0), new Vector3(t, 0.01f, 2 * hl + t), root);
        Box("Baseline", Mats.CourtLine, new Vector3(0, y, -hl), new Vector3(2 * hw, 0.01f, t), root);
        Box("Baseline", Mats.CourtLine, new Vector3(0, y, hl), new Vector3(2 * hw, 0.01f, t), root);
        Box("Service Line", Mats.CourtLine, new Vector3(0, y, -3.0f), new Vector3(2 * hw, 0.01f, t * 0.8f), root);
        Box("Service Line", Mats.CourtLine, new Vector3(0, y, 3.0f), new Vector3(2 * hw, 0.01f, t * 0.8f), root);
        Box("Center Line", Mats.CourtLine, new Vector3(0, y, 0), new Vector3(t * 0.8f, 0.01f, 6.0f), root);
        Box("Center Mark", Mats.CourtLine, new Vector3(0, y, -hl + 0.15f), new Vector3(t, 0.01f, 0.3f), root);
        Box("Center Mark", Mats.CourtLine, new Vector3(0, y, hl - 0.15f), new Vector3(t, 0.01f, 0.3f), root);
        SetLayerRecursive(root.gameObject, NoReflectLayer);
    }

    static void BuildNet()
    {
        var root = new GameObject("Net").transform;
        root.SetParent(_envRoot);
        var post = Load<GameObject>(ModelProcessor.ModelPath("NetPost"));
        var postMat = Load<Material>(ModelProcessor.MaterialPath("NetPost"));
        foreach (var s in new[] { -1, 1 })
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(post, root);
            go.name = "Net Post";
            go.transform.localScale = Vector3.one * 1.2f;
            go.transform.position = new Vector3(s * (Court.NetHalfWidth + 0.1f), 0.6f, 0);
            // Light panel (local +X) faces the court sides toward the camera
            go.transform.rotation = Quaternion.Euler(0, 90, 0);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = postMat;
            var light = new GameObject("Post Light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0, 0, 0);
            light.type = LightType.Point;
            light.color = new Color(1, 0.55f, 0.2f);
            light.range = 3;
            light.intensity = 2;
        }
        var span = 2 * Court.NetHalfWidth;
        Cylinder("Laser Top", Mats.Laser, new Vector3(0, Court.NetHeight, 0), span, 0.022f, root);
        Cylinder("Laser Bottom", Mats.Laser, new Vector3(0, 0.12f, 0), span, 0.016f, root);
        Surface("Net Mesh", Mats.NetMesh, new Vector3(0, 0, -1), 0,
                new Vector3(-Court.NetHalfWidth, 0.12f, 0), new Vector3(Court.NetHalfWidth, 0.12f, 0),
                new Vector3(Court.NetHalfWidth, Court.NetHeight, 0), new Vector3(-Court.NetHalfWidth, Court.NetHeight, 0), root, false);
    }

    // Geometry helpers

    static int _meshCount;

    // Quad from four corners (bl, br, tr, tl); tile <= 0 means 0..1 UVs
    static GameObject Surface(string name, Material mat, Vector3 normal, float tile,
                              Vector3 a, Vector3 b, Vector3 c, Vector3 d, Transform parent = null, bool isStatic = true)
    {
        var mesh = new Mesh { name = name };
        mesh.vertices = new[] { a, b, c, d };
        var uMax = tile > 0 ? (b - a).magnitude / tile : 1;
        var vMax = tile > 0 ? (d - a).magnitude / tile : 1;
        mesh.uv = new[] { Vector2.zero, new Vector2(uMax, 0), new Vector2(uMax, vMax), new Vector2(0, vMax) };
        var n = Vector3.Cross(d - a, c - a);
        mesh.triangles = Vector3.Dot(n, normal) >= 0 ? new[] { 0, 3, 2, 0, 2, 1 } : new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, $"{MeshDir}{name.Replace(' ', '_')}_{_meshCount++}.asset");

        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : _envRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        if (!isStatic) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
        return go;
    }

    static GameObject Box(string name, Material mat, Vector3 position, Vector3 size, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent != null ? parent : _envRoot, false);
        go.transform.position = position;
        go.transform.localScale = size;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
        return go;
    }

    static GameObject Cylinder(string name, Material mat, Vector3 center, float length, float radius, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0, 0, 90);
        go.transform.localScale = new Vector3(radius * 2, length / 2, radius * 2);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }
}

} // namespace SmashBots.Editor
