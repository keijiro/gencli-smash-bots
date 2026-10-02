using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace SmashBots.Editor {

// Camera, lighting, post-processing, UI and game systems
public static partial class SceneBuilder
{
    static void BuildSystems(Robot player, Robot cpu, Ball ball)
    {
        ConfigurePipeline();

        // Lighting
        var sun = new GameObject("Key Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.5f;
        sun.color = new Color(1, 0.98f, 0.95f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.7f;
        sun.transform.rotation = Quaternion.Euler(62, -28, 0);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.82f, 0.86f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.62f, 0.67f, 0.78f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.35f, 0.42f);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;

        var probe = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
        probe.transform.position = new Vector3(0, 3, 0);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(2 * HallHalfWidth, CeilingY + 0.5f, 2 * HallHalfLength);
        probe.center = new Vector3(0, CeilingY / 2 - 3, 0);
        probe.boxProjection = true;
        probe.resolution = 512;
        probe.hdr = true;
        probe.cullingMask = ~(1 << ActorLayer);
        probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = new Color(0.8f, 0.83f, 0.88f);

        // Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 120;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.8f, 0.83f, 0.88f);
        camGo.transform.position = new Vector3(0, 2.4f, -11.6f);
        camGo.transform.LookAt(new Vector3(0, 1, 3));
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.None;
        camData.dithering = true;
        camGo.AddComponent<AudioListener>();
        var director = camGo.AddComponent<CameraDirector>();
        director.Camera = cam;
        director.Player = player;
        director.Cpu = cpu;
        director.Ball = ball;

        // Planar floor reflection
        var refl = GameObject.Find("Floor Reflection").AddComponent<PlanarReflection>();
        refl.Source = cam;
        refl.CullingMask = ~((1 << NoReflectLayer) | (1 << 5));
        refl.Resolution = 0.6f;

        // Post-processing volume
        var volume = new GameObject("Global Volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = BuildVolumeProfile();

        // UI
        var uiGo = new GameObject("UI");
        var doc = uiGo.AddComponent<UIDocument>();
        var panel = Load<PanelSettings>("Assets/UI/DefaultSettings.asset");
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1920, 1080);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match = 0.5f;
        EditorUtility.SetDirty(panel);
        doc.panelSettings = panel;
        doc.visualTreeAsset = Load<VisualTreeAsset>("Assets/UI/Main.uxml");
        var hud = uiGo.AddComponent<HudController>();
        hud.Document = doc;

        // Game systems
        var game = new GameObject("Game");
        var gm = game.AddComponent<GameManager>();
        var pc = game.AddComponent<PlayerController>();
        var ai = game.AddComponent<CpuController>();
        var smash = game.AddComponent<SmashDirector>();
        var time = game.AddComponent<TimeController>();
        var post = game.AddComponent<PostFxController>();
        var fx = BuildEffects(game);
        var audio = BuildAudio(game);
        post.Volume = volume;

        // Landing marker for incoming balls
        var markerGo = ShadowQuad("Landing Marker");
        var markerRenderer = markerGo.GetComponent<MeshRenderer>();
        markerRenderer.sharedMaterial = Mats.FxRingFloor;
        var marker = markerGo.AddComponent<LandingMarker>();
        marker.Renderer = markerRenderer;
        marker.Color = new Color(1.0f, 0.5f, 0.15f, 1);
        gm.Marker = marker;

        gm.Ball = ball;
        gm.PlayerRobot = player;
        gm.CpuRobot = cpu;
        gm.Player = pc;
        gm.Cpu = ai;
        gm.Smash = smash;
        gm.Hud = hud;
        gm.CameraDirector = director;
        gm.Audio = audio;
        gm.Effects = fx;
        gm.TimeControl = time;

        pc.Game = gm;
        pc.Robot = player;
        pc.Ball = ball;
        pc.Hud = hud;
        pc.Camera = cam;
        pc.Audio = audio;

        ai.Game = gm;
        ai.Robot = cpu;
        ai.Opponent = player;
        ai.Ball = ball;

        smash.Game = gm;
        smash.Player = player;
        smash.Cpu = cpu;
        smash.CpuAi = ai;
        smash.Ball = ball;
        smash.Camera = cam;
        smash.CameraDirector = director;
        smash.Hud = hud;
        smash.TimeControl = time;
        smash.PostFx = post;
        smash.Audio = audio;
        smash.Effects = fx;
    }

    static AudioController BuildAudio(GameObject host)
    {
        var audio = host.AddComponent<AudioController>();
        AudioClip Clip(string n) => Load<AudioClip>(AudioDir + n);
        audio.HitNormal = Clip("HitNormal.wav");
        audio.HitStrong = Clip("HitStrong.wav");
        audio.Bounce = Clip("Bounce.wav");
        audio.SmashImpact = Clip("SmashImpact.wav");
        audio.SlowMoIn = Clip("SlowMoIn.wav");
        audio.QteLock = Clip("QteLock.wav");
        audio.PointWin = Clip("PointWin.wav");
        audio.PointLose = Clip("PointLose.wav");
        audio.Whiff = Clip("Whiff.wav");
        audio.GroundExplode = Clip("BallGroundExplode.wav");
        audio.UiClick = Clip("UiClick.wav");
        audio.Jingle = Clip("Jingle.wav");

        var music = new GameObject("Music");
        music.transform.SetParent(host.transform, false);
        var src = music.AddComponent<AudioSource>();
        src.clip = Clip("BGM.mp3");
        src.loop = true;
        src.playOnAwake = false;
        src.volume = 0.4f;
        src.spatialBlend = 0;
        audio.Music = src;
        audio.MusicFilter = music.AddComponent<AudioLowPassFilter>();
        audio.MusicFilter.cutoffFrequency = 22000;
        return audio;
    }

    static VolumeProfile BuildVolumeProfile()
    {
        const string path = "Assets/Settings/PostFx.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        T Add<T>() where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        var bloom = Add<Bloom>();
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.85f);
        bloom.scatter.Override(0.68f);
        bloom.highQualityFiltering.Override(true);

        var tone = Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);

        var color = Add<ColorAdjustments>();
        color.postExposure.Override(0);
        color.contrast.Override(14);
        color.saturation.Override(8);

        var wb = Add<WhiteBalance>();
        wb.temperature.Override(-6);

        var vignette = Add<Vignette>();
        vignette.intensity.Override(0.24f);
        vignette.smoothness.Override(0.45f);

        var chroma = Add<ChromaticAberration>();
        chroma.intensity.Override(0.08f);

        var lens = Add<LensDistortion>();
        lens.intensity.Override(0);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static void ConfigurePipeline()
    {
        var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return;
        urp.msaaSampleCount = 4;
        urp.shadowDistance = 40;
        urp.shadowCascadeCount = 2;
        urp.supportsHDR = true;
        var so = new SerializedObject(urp);
        var bp = so.FindProperty("m_ReflectionProbeBoxProjection");
        if (bp != null) bp.boolValue = true;
        var blend = so.FindProperty("m_ReflectionProbeBlending");
        if (blend != null) blend.boolValue = true;
        var lights = so.FindProperty("m_AdditionalLightsPerObjectLimit");
        if (lights != null) lights.intValue = 8;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(urp);

        PlayerSettings.productName = "Smash Bots";
    }
}

} // namespace SmashBots.Editor
