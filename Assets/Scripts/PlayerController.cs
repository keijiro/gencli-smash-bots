using UnityEngine;

namespace SmashBots {

// Click-to-hit judgement for normal shots and serves
public sealed class PlayerController : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public GameManager Game { get; set; }
    [field:SerializeField] public Robot Robot { get; set; }
    [field:SerializeField] public Ball Ball { get; set; }
    [field:SerializeField] public HudController Hud { get; set; }
    [field:SerializeField] public Camera Camera { get; set; }
    [field:SerializeField] public AudioController Audio { get; set; }

    public bool IsExpecting => _active;

    // Attract mode: clicks automatically with some timing variation
    public bool Demo { get; set; }

    // Debug: automatic clicking for unattended testing
    public static bool AutoPlay { get; set; }
    public static float AutoPlayTiming { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDebugFlags() => (AutoPlay, AutoPlayTiming) = (false, 0);

    // Public methods

    public void Expect(Vector3 point, float ideal, float open, float close, SwingType type, float showTime)
    {
        (_point, _ideal, _open, _close, _type, _showTime) = (point, ideal, open, close, type, showTime);
        _active = true;
        _autoOffset = Demo ? Random.Range(-0.1f, 0.2f) : AutoPlayTiming;
    }

    public void Cancel()
    {
        _active = false;
        Hud.HideHitIndicator();
        Robot.SetReady(0, _type);
    }

    // Quality ratings

    public static (string label, Color color) Rate(float q)
    {
        if (q >= 0.85f) return ("PERFECT!", new Color(1.0f, 0.85f, 0.3f));
        if (q >= 0.6f) return ("GREAT!", new Color(1.0f, 0.6f, 0.25f));
        if (q >= 0.3f) return ("GOOD", new Color(0.55f, 0.85f, 1.0f));
        return ("OK", new Color(0.8f, 0.85f, 0.9f));
    }

    // MonoBehaviour implementation

    void Update()
    {
        if (!_active) return;

        var now = Time.time;
        var toIdeal = _ideal - now;

        // Anticipation pose as the ball approaches
        Robot.SetReady(1 - Mathf.Clamp01((toIdeal - 0.08f) / 0.55f), _type);

        if (now >= _showTime && !Demo)
        {
            var approach = Mathf.Clamp01(toIdeal / 0.9f);
            var tint = Color.Lerp(new Color(1, 0.55f, 0.15f), new Color(1, 0.8f, 0.4f), approach);
            Hud.ShowHitIndicator(Camera.WorldToScreenPoint(_point), approach, tint);
        }

        if (now > _close)
        {
            Cancel();
            Robot.Swing(SwingType.Whiff);
            if (!Demo) Hud.ShowRating("MISS", Camera.WorldToScreenPoint(Ball.Position), new Color(0.6f, 0.7f, 0.8f));
            Game.PlayerMissedWindow(_type == SwingType.Serve);
            return;
        }

        var auto = (AutoPlay || Demo) && now >= _ideal + _autoOffset;
        if (!auto && (!PointerInput.Pressed || Time.unscaledTime < _lockUntil)) return;

        var click = auto ? (Vector2)Camera.WorldToScreenPoint(Ball.Position) : PointerInput.Position;
        if (now < _open)
        {
            // Too early: whiff and a short lockout to discourage spamming
            Robot.Swing(SwingType.Whiff);
            Audio.Play(Audio.Whiff, 0.7f);
            Hud.ShowRating("TOO EARLY", click, new Color(0.7f, 0.75f, 0.8f));
            _lockUntil = Time.unscaledTime + 0.3f;
            return;
        }

        var dt = now - _ideal;
        var timing = dt < 0 ? 1 - Smooth(0.03f, 0.45f, -dt) : 1 - Smooth(0.02f, 0.3f, dt);
        var ballScreen = (Vector2)Camera.WorldToScreenPoint(Ball.Position);
        var dist = Vector2.Distance(click, ballScreen) / Screen.height;
        var aim = 1 - Smooth(0.03f, 0.3f, dist);
        var q = Mathf.Clamp01(timing * (0.3f + 0.7f * aim));

        Cancel();
        Game.PlayerHit(q, click, _type == SwingType.Serve);
    }

    // Private members

    Vector3 _point;
    float _ideal, _open, _close, _showTime, _lockUntil, _autoOffset;
    SwingType _type;
    bool _active;

    static float Smooth(float a, float b, float x)
    {
        var t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }
}

} // namespace SmashBots
