using UnityEngine;

namespace SmashBots {

public sealed class AudioController : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public AudioClip HitNormal { get; set; }
    [field:SerializeField] public AudioClip HitStrong { get; set; }
    [field:SerializeField] public AudioClip Bounce { get; set; }
    [field:SerializeField] public AudioClip SmashImpact { get; set; }
    [field:SerializeField] public AudioClip SlowMoIn { get; set; }
    [field:SerializeField] public AudioClip QteLock { get; set; }
    [field:SerializeField] public AudioClip PointWin { get; set; }
    [field:SerializeField] public AudioClip PointLose { get; set; }
    [field:SerializeField] public AudioClip Whiff { get; set; }
    [field:SerializeField] public AudioClip GroundExplode { get; set; }
    [field:SerializeField] public AudioClip UiClick { get; set; }
    [field:SerializeField] public AudioClip Jingle { get; set; }
    [field:SerializeField] public AudioSource Music { get; set; }
    [field:SerializeField] public AudioLowPassFilter MusicFilter { get; set; }

    // Public methods

    public void Play(AudioClip clip, float volume = 1, float pitch = 1)
    {
        if (clip == null) return;
        var src = _pool[_next];
        _next = (_next + 1) % _pool.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, volume);
    }

    public void PlayHit(float power)
      => Play(power > 0.7f ? HitStrong : HitNormal, Mathf.Lerp(0.6f, 1, power), Random.Range(0.95f, 1.08f));

    public void PlayBounce(float speed)
      => Play(Bounce, Mathf.Clamp01(speed / 12) * 0.8f, Random.Range(0.9f, 1.1f));

    public void SetSlowMo(float amount) => _slowMoTarget = Mathf.Clamp01(amount);

    // MonoBehaviour implementation

    void Awake()
    {
        _pool = new AudioSource[12];
        for (var i = 0; i < _pool.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0;
            _pool[i] = src;
        }
    }

    void Update()
    {
        _slowMo = Mathf.MoveTowards(_slowMo, _slowMoTarget, Time.unscaledDeltaTime * 4);
        if (Music != null) Music.pitch = Mathf.Lerp(1, 0.75f, _slowMo);
        if (MusicFilter != null) MusicFilter.cutoffFrequency = Mathf.Lerp(22000, 700, Mathf.Sqrt(_slowMo));
    }

    // Private members

    AudioSource[] _pool;
    int _next;
    float _slowMo, _slowMoTarget;
}

} // namespace SmashBots
