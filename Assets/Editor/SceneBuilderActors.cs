using UnityEditor;
using UnityEngine;

namespace SmashBots.Editor {

// Robots, rackets and the ball
public static partial class SceneBuilder
{
    // Robots

    static Robot BuildRobot(Side side)
    {
        var isPlayer = side == Side.Player;
        var modelName = isPlayer ? "RobotPlayer" : "RobotEnemy";
        var thrusterColor = isPlayer ? new Color(1, 0.55f, 0.2f) : new Color(0.3f, 0.6f, 1);

        var root = new GameObject(isPlayer ? "Player Robot" : "CPU Robot");
        root.transform.position = new Vector3(0, Court.HoverHeight, side.Sign() * 7.0f);
        root.transform.rotation = Quaternion.Euler(0, isPlayer ? 0 : 180, 0);

        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);

        // Body model, turned so that the face looks down local +Z
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ModelProcessor.ModelPath(modelName)), body);
        model.name = "Model";
        model.transform.localRotation = Quaternion.Euler(0, isPlayer ? 90 : -90, 0);
        model.transform.localScale = Vector3.one * 1.1f;
        var mat = Load<Material>(ModelProcessor.MaterialPath(modelName));
        foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

        // Racket on the right side disc
        var pivot = new GameObject("Racket Pivot").transform;
        pivot.SetParent(body, false);
        pivot.localPosition = new Vector3(0.42f, -0.02f, 0.05f);
        pivot.localRotation = Quaternion.Euler(0, 15, 35);
        var racketPrefab = BuildRacketPrefab(isPlayer ? "RacketPlayer" : "RacketEnemy",
                                             isPlayer ? Mats.StringsPlayer : Mats.StringsCpu);
        var racket = (GameObject)PrefabUtility.InstantiatePrefab(racketPrefab, pivot);
        racket.name = "Racket";
        racket.transform.localScale = Vector3.one * 1.12f;

        // Thruster glow
        var thruster = new GameObject("Thruster");
        thruster.transform.SetParent(body, false);
        thruster.transform.localPosition = new Vector3(0, -0.5f, 0);
        var ps = BuildThrusterParticles(thruster, isPlayer ? Mats.ThrusterPlayer : Mats.ThrusterCpu);
        var light = new GameObject("Thruster Light").AddComponent<Light>();
        light.transform.SetParent(thruster.transform, false);
        light.transform.localPosition = new Vector3(0, -0.15f, 0);
        light.type = LightType.Point;
        light.color = thrusterColor;
        light.range = 2.6f;
        light.intensity = 1.8f;

        // Hover hum
        var hum = root.AddComponent<AudioSource>();
        hum.clip = Load<AudioClip>(AudioDir + "HoverLoop.wav");
        hum.loop = true;
        hum.playOnAwake = true;
        hum.volume = 0.12f;
        hum.spatialBlend = 0.8f;
        hum.minDistance = 2;
        hum.maxDistance = 30;

        var robot = root.AddComponent<Robot>();
        robot.Side = side;
        robot.Body = body;
        robot.RacketPivot = pivot;
        robot.RacketHead = racket.transform.Find("Head");
        robot.Thruster = ps;
        robot.ThrusterLight = light;

        SetLayerRecursive(root, ActorLayer);

        // Contact shadow
        var shadow = ShadowQuad(isPlayer ? "Player Shadow" : "CPU Shadow");
        var blob = shadow.AddComponent<BlobShadow>();
        blob.Target = root.transform;
        blob.Size = 1.3f;
        blob.Opacity = 0.5f;

        return robot;
    }

    static ParticleSystem BuildThrusterParticles(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var em = ps.emission;
        em.rateOverTime = 25;
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(90, 0, 0);
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    // Rackets: align the generated mesh with PCA so that the grip sits at
    // the origin, the head points down +X and the strings face +Z.

    static GameObject BuildRacketPrefab(string modelName, Material stringsMat)
    {
        var path = PrefabDir + modelName + ".prefab";
        var root = new GameObject(modelName);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ModelProcessor.ModelPath(modelName)), root.transform);
        model.name = "Model";
        var mat = Load<Material>(ModelProcessor.MaterialPath(modelName));
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // Vertices in model space
        var mf = model.GetComponentInChildren<MeshFilter>();
        var src = mf.sharedMesh.vertices;
        var verts = new Vector3[src.Length];
        for (var i = 0; i < src.Length; i++)
            verts[i] = model.transform.InverseTransformPoint(mf.transform.TransformPoint(src[i]));

        // Principal axes
        var centroid = Vector3.zero;
        foreach (var v in verts) centroid += v;
        centroid /= verts.Length;
        var cov = new float[3, 3];
        foreach (var v in verts)
        {
            var d = v - centroid;
            for (var a = 0; a < 3; a++)
                for (var b = 0; b < 3; b++)
                    cov[a, b] += d[a] * d[b];
        }
        var (axes, _) = Eigen3(cov);
        var a1 = axes[0]; // longest: handle -> head
        var a2 = axes[1]; // head width
        var a3 = axes[2]; // head normal

        // Head end has the wider spread along a2
        float SpreadAlongA2(bool positive)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts)
            {
                var d = v - centroid;
                if ((Vector3.Dot(d, a1) > 0) != positive) continue;
                var p = Vector3.Dot(d, a2);
                min = Mathf.Min(min, p);
                max = Mathf.Max(max, p);
            }
            return max - min;
        }
        if (SpreadAlongA2(false) > SpreadAlongA2(true)) a1 = -a1;
        a3 = Vector3.Cross(a1, a2).normalized;
        a2 = Vector3.Cross(a3, a1).normalized;

        float tMin = float.MaxValue, tMax = float.MinValue;
        foreach (var v in verts)
        {
            var t = Vector3.Dot(v - centroid, a1);
            tMin = Mathf.Min(tMin, t);
            tMax = Mathf.Max(tMax, t);
        }
        var headWidth = SpreadAlongA2(true);
        var length = tMax - tMin;

        // Head center offsets (mean of the head region in a2/a3)
        var headLength = Mathf.Min(headWidth * 1.3f, length * 0.6f);
        float o2 = 0, o3 = 0;
        var count = 0;
        foreach (var v in verts)
        {
            var d = v - centroid;
            if (Vector3.Dot(d, a1) < tMax - headLength) continue;
            o2 += Vector3.Dot(d, a2);
            o3 += Vector3.Dot(d, a3);
            count++;
        }
        if (count > 0) { o2 /= count; o3 /= count; }

        // Transform: model axes -> racket axes, scaled to 0.85 m
        const float targetLength = 0.85f;
        var scale = targetLength / length;
        var q = Quaternion.LookRotation(a3, Vector3.Cross(a3, a1));
        var rot = Quaternion.Inverse(q);
        var grip = centroid + a1 * (tMin + length * 0.04f);
        model.transform.localRotation = rot;
        model.transform.localScale = Vector3.one * scale;
        model.transform.localPosition = -(rot * grip) * scale;

        // Head marker and string face
        var headCenter = new Vector3((tMax - tMin - headLength * 0.5f - length * 0.04f) * scale, o2 * scale, o3 * scale);
        var head = new GameObject("Head").transform;
        head.SetParent(root.transform, false);
        head.localPosition = headCenter;

        var strings = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(strings.GetComponent<Collider>());
        strings.name = "Strings";
        strings.transform.SetParent(head, false);
        strings.transform.localScale = new Vector3(headLength * scale * 0.86f, headWidth * scale * 0.84f, 1);
        var sr = strings.GetComponent<MeshRenderer>();
        sr.sharedMaterial = stringsMat;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // Jacobi eigen decomposition of a symmetric 3x3 matrix (descending)
    static (Vector3[] vectors, float[] values) Eigen3(float[,] m)
    {
        var a = (float[,])m.Clone();
        var v = new float[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (var sweep = 0; sweep < 50; sweep++)
        {
            for (var p = 0; p < 2; p++)
            {
                for (var q = p + 1; q < 3; q++)
                {
                    if (Mathf.Abs(a[p, q]) < 1e-9f) continue;
                    var theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    var t = Mathf.Sign(theta) / (Mathf.Abs(theta) + Mathf.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    var c = 1 / Mathf.Sqrt(t * t + 1);
                    var s = t * c;
                    for (var k = 0; k < 3; k++)
                    {
                        var akp = a[k, p];
                        var akq = a[k, q];
                        a[k, p] = c * akp - s * akq;
                        a[k, q] = s * akp + c * akq;
                    }
                    for (var k = 0; k < 3; k++)
                    {
                        var apk = a[p, k];
                        var aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk;
                        a[q, k] = s * apk + c * aqk;
                    }
                    for (var k = 0; k < 3; k++)
                    {
                        var vkp = v[k, p];
                        var vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq;
                        v[k, q] = s * vkp + c * vkq;
                    }
                }
            }
        }
        var idx = new[] { 0, 1, 2 };
        System.Array.Sort(idx, (i, j) => a[j, j].CompareTo(a[i, i]));
        var vectors = new Vector3[3];
        var values = new float[3];
        for (var i = 0; i < 3; i++)
        {
            var k = idx[i];
            vectors[i] = new Vector3(v[0, k], v[1, k], v[2, k]).normalized;
            values[i] = a[k, k];
        }
        return (vectors, values);
    }

    // Ball

    static Ball BuildBall()
    {
        var root = new GameObject("Ball");
        root.transform.position = new Vector3(0, 1.5f, 0);

        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * Court.BallRadius * 2.3f;
        visual.GetComponent<MeshRenderer>().sharedMaterial = Mats.Ball;

        var trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Mats.Trail;
        trail.time = 0.2f;
        trail.minVertexDistance = 0.04f;
        trail.widthMultiplier = 0.2f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.numCapVertices = 0;

        var light = new GameObject("Glow").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.type = LightType.Point;
        light.range = 3;
        light.intensity = 1.5f;
        light.color = new Color(0.8f, 1, 0.3f);

        SetLayerRecursive(root, ActorLayer);

        var shadow = ShadowQuad("Ball Shadow");
        var ball = root.AddComponent<Ball>();
        ball.Visual = visual.transform;
        ball.Trail = trail;
        ball.Glow = light;
        ball.Shadow = shadow.transform;
        ball.ShadowRenderer = shadow.GetComponent<Renderer>();
        ball.NormalTrailColor = new Color(0.75f, 1.0f, 0.2f);
        ball.PowerTrailColor = new Color(1.0f, 0.55f, 0.15f);
        return ball;
    }

    static GameObject ShadowQuad(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.layer = NoReflectLayer;
        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Mats.Shadow;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    static Gradient FadeGradient(Color c, float peak = 1)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                  new[] { new GradientAlphaKey(peak, 0), new GradientAlphaKey(peak, 0.4f), new GradientAlphaKey(0, 1) });
        return g;
    }
}

} // namespace SmashBots.Editor
