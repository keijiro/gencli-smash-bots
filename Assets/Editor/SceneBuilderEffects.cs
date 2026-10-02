using UnityEditor;
using UnityEngine;

namespace SmashBots.Editor {

// Particle effect prefabs
public static partial class SceneBuilder
{
    static Effects BuildEffects(GameObject host)
    {
        var fx = host.AddComponent<Effects>();
        fx.HitSparkPlayer = SavePrefab(HitSpark("HitSparkPlayer", new Color(1, 0.6f, 0.2f)));
        fx.HitSparkCpu = SavePrefab(HitSpark("HitSparkCpu", new Color(0.35f, 0.65f, 1)));
        fx.PerfectBurst = SavePrefab(PerfectBurst());
        fx.SmashBurst = SavePrefab(SmashBurst());
        fx.BounceRing = SavePrefab(BounceRing());
        fx.GroundExplosion = SavePrefab(GroundExplosion());
        return fx;
    }

    static GameObject SavePrefab(GameObject go)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + go.name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static ParticleSystem Emitter(Transform parent, string name, Material mat, ParticleSystemRenderMode mode,
                                  int burst, Vector2 lifetime, Vector2 speed, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = 1;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 500;
        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0, burst) });
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = mode;
        if (mode == ParticleSystemRenderMode.Stretch)
        {
            r.velocityScale = 0.035f;
            r.lengthScale = 1.5f;
        }
        return ps;
    }

    static void Shrink(ParticleSystem ps)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
    }

    static void Grow(ParticleSystem ps, float from = 0.1f)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, from, 0, 3), new Keyframe(1, 1, 0, 0)));
        var shape = ps.shape;
        shape.enabled = false;
    }

    static void Drag(ParticleSystem ps, float drag, float gravity)
    {
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = drag;
        limit.limit = 1000;
        var main = ps.main;
        main.gravityModifier = gravity;
    }

    static GameObject HitSpark(string name, Color color)
    {
        var root = Emitter(null, name, Mats.FxSpark, ParticleSystemRenderMode.Stretch, 26,
                           new Vector2(0.18f, 0.4f), new Vector2(3, 9), new Vector2(0.03f, 0.07f), color);
        Drag(root, 3, 0.6f);
        Shrink(root);
        var flash = Emitter(root.transform, "Flash", Mats.FxGlow, ParticleSystemRenderMode.Billboard, 1,
                            new Vector2(0.1f, 0.1f), Vector2.zero, new Vector2(0.9f, 0.9f), color);
        Grow(flash, 0.6f);
        var ring = Emitter(root.transform, "Ring", Mats.FxRing, ParticleSystemRenderMode.Billboard, 1,
                           new Vector2(0.18f, 0.18f), Vector2.zero, new Vector2(1.0f, 1.0f), color);
        Grow(ring);
        return root.gameObject;
    }

    static GameObject PerfectBurst()
    {
        var root = Emitter(null, "PerfectBurst", Mats.FxBurst, ParticleSystemRenderMode.Billboard, 1,
                           new Vector2(0.22f, 0.22f), Vector2.zero, new Vector2(1.8f, 1.8f), Color.white);
        Grow(root, 0.4f);
        var main = root.main;
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        var sparks = Emitter(root.transform, "Sparks", Mats.FxSpark, ParticleSystemRenderMode.Stretch, 40,
                             new Vector2(0.2f, 0.5f), new Vector2(6, 14), new Vector2(0.03f, 0.08f), new Color(1, 0.85f, 0.4f));
        Drag(sparks, 4, 0.5f);
        Shrink(sparks);
        return root.gameObject;
    }

    static GameObject SmashBurst()
    {
        var root = Emitter(null, "SmashBurst", Mats.FxBurst, ParticleSystemRenderMode.Billboard, 1,
                           new Vector2(0.3f, 0.3f), Vector2.zero, new Vector2(2.6f, 2.6f), new Color(1, 0.85f, 0.6f, 0.85f));
        Grow(root, 0.3f);
        var main = root.main;
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        var shock = Emitter(root.transform, "Shockwave", Mats.FxRing, ParticleSystemRenderMode.Billboard, 1,
                            new Vector2(0.45f, 0.45f), Vector2.zero, new Vector2(8, 8), new Color(1, 0.7f, 0.3f));
        Grow(shock, 0.05f);
        var glow = Emitter(root.transform, "Glow", Mats.FxGlow, ParticleSystemRenderMode.Billboard, 1,
                           new Vector2(0.3f, 0.4f), Vector2.zero, new Vector2(2, 2.5f), new Color(1, 0.55f, 0.15f, 0.7f));
        Shrink(glow);
        var sparks = Emitter(root.transform, "Sparks", Mats.FxSpark, ParticleSystemRenderMode.Stretch, 120,
                             new Vector2(0.3f, 0.8f), new Vector2(8, 22), new Vector2(0.04f, 0.1f), new Color(1, 0.75f, 0.3f));
        Drag(sparks, 3, 0.8f);
        Shrink(sparks);
        return root.gameObject;
    }

    static GameObject BounceRing()
    {
        var root = Emitter(null, "BounceRing", Mats.FxRingFloor, ParticleSystemRenderMode.HorizontalBillboard, 1,
                           new Vector2(0.35f, 0.35f), Vector2.zero, new Vector2(1.1f, 1.1f), new Color(0.6f, 0.9f, 1));
        Grow(root, 0.1f);
        var dust = Emitter(root.transform, "Sparks", Mats.FxSpark, ParticleSystemRenderMode.Stretch, 10,
                           new Vector2(0.15f, 0.3f), new Vector2(1.5f, 4), new Vector2(0.02f, 0.04f), new Color(0.8f, 1, 0.5f));
        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.rotation = new Vector3(-90, 0, 0);
        Drag(dust, 4, 0.8f);
        return root.gameObject;
    }

    static GameObject GroundExplosion()
    {
        var root = Emitter(null, "GroundExplosion", Mats.FxRingFloor, ParticleSystemRenderMode.HorizontalBillboard, 1,
                           new Vector2(0.6f, 0.6f), Vector2.zero, new Vector2(9, 9), new Color(1, 0.65f, 0.25f));
        Grow(root, 0.05f);
        var ring2 = Emitter(root.transform, "Ring2", Mats.FxRingFloor, ParticleSystemRenderMode.HorizontalBillboard, 1,
                            new Vector2(0.4f, 0.4f), Vector2.zero, new Vector2(5, 5), new Color(1, 0.9f, 0.6f));
        Grow(ring2, 0.05f);
        var burst = Emitter(root.transform, "Burst", Mats.FxBurst, ParticleSystemRenderMode.Billboard, 1,
                            new Vector2(0.22f, 0.22f), Vector2.zero, new Vector2(1.5f, 1.5f), new Color(1, 0.7f, 0.4f, 0.7f));
        Grow(burst, 0.4f);
        burst.transform.localPosition = new Vector3(0, 0.4f, 0);
        var floorGlow = Emitter(root.transform, "FloorGlow", Mats.FxGlow, ParticleSystemRenderMode.HorizontalBillboard, 1,
                                new Vector2(0.7f, 0.7f), Vector2.zero, new Vector2(4, 4), new Color(1, 0.55f, 0.2f));
        Shrink(floorGlow);
        var sparks = Emitter(root.transform, "Sparks", Mats.FxSpark, ParticleSystemRenderMode.Stretch, 140,
                             new Vector2(0.4f, 1.0f), new Vector2(6, 16), new Vector2(0.04f, 0.1f), new Color(1, 0.8f, 0.35f));
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 50;
        shape.rotation = new Vector3(-90, 0, 0);
        Drag(sparks, 2, 1.2f);
        Shrink(sparks);
        return root.gameObject;
    }
}

} // namespace SmashBots.Editor
