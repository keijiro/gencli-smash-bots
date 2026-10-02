using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SmashBots {

// Drives the global volume overrides for slow motion and impact pulses
public sealed class PostFxController : MonoBehaviour
{
    [field:SerializeField] public Volume Volume { get; set; }

    public void SetSlowMo(float amount) => _slowMoTarget = Mathf.Clamp01(amount);

    public void Impact(float strength) => _impact = Mathf.Max(_impact, strength);

    void Start()
    {
        var profile = Volume.profile; // runtime instance
        profile.TryGet(out _bloom);
        profile.TryGet(out _chroma);
        profile.TryGet(out _vignette);
        profile.TryGet(out _lens);
        profile.TryGet(out _color);
        if (_bloom != null) _bloomBase = _bloom.intensity.value;
        if (_vignette != null) _vignetteBase = _vignette.intensity.value;
    }

    void Update()
    {
        var dt = Time.unscaledDeltaTime;
        _slowMo = Mathf.MoveTowards(_slowMo, _slowMoTarget, dt * 3);
        _impact = Mathf.Max(0, _impact - dt * 2.5f);
        var i = _impact * _impact;

        if (_chroma != null) _chroma.intensity.value = Mathf.Clamp01(0.08f + _slowMo * 0.35f + i * 0.9f);
        if (_vignette != null) _vignette.intensity.value = _vignetteBase + _slowMo * 0.18f;
        if (_lens != null) _lens.intensity.value = -0.08f * _slowMo - 0.45f * i;
        if (_bloom != null) _bloom.intensity.value = _bloomBase + i * 1.2f;
        if (_color != null)
        {
            _color.saturation.value = 8 - _slowMo * 25;
            _color.postExposure.value = i * 0.3f;
        }
    }

    Bloom _bloom;
    ChromaticAberration _chroma;
    Vignette _vignette;
    LensDistortion _lens;
    ColorAdjustments _color;
    float _bloomBase, _vignetteBase, _slowMo, _slowMoTarget, _impact;
}

} // namespace SmashBots
