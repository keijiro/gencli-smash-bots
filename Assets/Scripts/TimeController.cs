using UnityEngine;

namespace SmashBots {

// Owns Time.timeScale: slow motion level plus short hit-stop freezes
public sealed class TimeController : MonoBehaviour
{
    public float SlowMo { get; set; } = 1;

    public bool InHitstop => Time.unscaledTime < _hitstopUntil;

    public void Hitstop(float duration)
      => _hitstopUntil = Mathf.Max(_hitstopUntil, Time.unscaledTime + duration);

    public void ResetTime()
    {
        SlowMo = 1;
        _hitstopUntil = 0;
        Time.timeScale = 1;
    }

    void Update() => Time.timeScale = InHitstop ? 0 : Mathf.Clamp(SlowMo, 0.01f, 1);

    void OnDisable() => Time.timeScale = 1;

    float _hitstopUntil;
}

} // namespace SmashBots
