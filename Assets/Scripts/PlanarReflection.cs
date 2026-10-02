using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SmashBots {

// Renders the scene mirrored across the y = 0 floor plane into a global
// texture sampled by the floor reflection overlay.
[DefaultExecutionOrder(100)]
public sealed class PlanarReflection : MonoBehaviour
{
    [field:SerializeField] public Camera Source { get; set; }
    [field:SerializeField] public LayerMask CullingMask { get; set; } = ~0;
    [field:SerializeField, Range(0.25f, 1)] public float Resolution { get; set; } = 0.5f;

    static readonly int TextureID = Shader.PropertyToID("_PlanarReflectionTex");

    void OnEnable()
    {
        var go = new GameObject("Reflection Camera") { hideFlags = HideFlags.HideAndDontSave };
        _camera = go.AddComponent<Camera>();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.renderShadows = true;
        data.requiresDepthTexture = false;
        data.requiresColorTexture = false;
    }

    void OnDisable()
    {
        if (_camera != null) Destroy(_camera.gameObject);
        if (_rt != null) Destroy(_rt);
        _camera = null;
        _rt = null;
    }

    void LateUpdate()
    {
        var w = Mathf.Max(16, (int)(Screen.width * Resolution));
        var h = Mathf.Max(16, (int)(Screen.height * Resolution));
        if (_rt == null || _rt.width != w || _rt.height != h)
        {
            if (_rt != null) Destroy(_rt);
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "Planar Reflection" };
            Shader.SetGlobalTexture(TextureID, _rt);
        }

        // Proper (non-mirrored) camera below the floor; the image comes out
        // flipped horizontally, which the overlay shader compensates for.
        var t = Source.transform;
        var p = t.position;
        var f = t.forward;
        var u = t.up;
        _camera.transform.SetPositionAndRotation(new Vector3(p.x, -p.y, p.z),
            Quaternion.LookRotation(new Vector3(f.x, -f.y, f.z), new Vector3(u.x, -u.y, u.z)));
        _camera.fieldOfView = Source.fieldOfView;
        _camera.nearClipPlane = Source.nearClipPlane;
        _camera.farClipPlane = Source.farClipPlane;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Source.backgroundColor;
        _camera.cullingMask = CullingMask;
        _camera.depth = Source.depth - 10;
        _camera.targetTexture = _rt;
        _camera.allowMSAA = false;
    }

    Camera _camera;
    RenderTexture _rt;
}

} // namespace SmashBots
