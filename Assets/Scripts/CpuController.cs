using UnityEngine;

namespace SmashBots {

public struct CpuShot
{
    public Vector3 Velocity;
    public float Quality;
    public bool IsLob;
    public bool IsServe;
}

// Opponent AI: reachability check, positioning and shot selection
public sealed class CpuController : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public GameManager Game { get; set; }
    [field:SerializeField] public Robot Robot { get; set; }
    [field:SerializeField] public Robot Opponent { get; set; }
    [field:SerializeField] public Ball Ball { get; set; }

    // Public methods

    public void OnIncoming(Trajectory traj, float incomingQuality)
    {
        var plan = ShotPlanner.PredictReceive(traj, Side.Cpu);
        var now = Time.time;
        var skill = Game.CpuSkill;

        var reaction = Random.Range(0.16f, 0.28f) - skill * 0.06f;
        var speed = Mathf.Lerp(4.6f, 6.0f, skill) * Random.Range(0.9f, 1.1f);
        var contact = Robot.PositionForContact(plan.Point, SwingType.Forehand);
        var pos = Robot.transform.position;
        var dist = Vector2.Distance(new Vector2(contact.x, contact.z), new Vector2(pos.x, pos.z));
        var avail = plan.Time - now - reaction;
        var need = Mathf.Max(0, dist - 0.9f);
        var ratio = avail > 0 ? need / (speed * avail) : 10;

        _pressure = Mathf.Clamp01(ratio);
        _incomingQuality = incomingQuality;
        var missChance = Smooth(0.82f, 1.0f, incomingQuality) * 0.45f + Smooth(0.75f, 1.0f, ratio) * 0.3f;
        _willMiss = ratio > 1 || Random.value < missChance;

        if (_willMiss)
        {
            // Run for it, but fall short and dive
            var reach = Mathf.Clamp01(speed * Mathf.Max(0, avail) / Mathf.Max(0.01f, dist)) * Random.Range(0.55f, 0.8f);
            Robot.MaxSpeed = speed;
            Robot.MoveTo(Vector3.Lerp(pos, contact, reach), plan.Time + 0.1f);
        }
        else
        {
            Robot.MaxSpeed = 14;
            Robot.MoveTo(contact, plan.Time - 0.06f);
        }

        _hitTime = plan.Time;
        _scheduled = true;
        _swung = false;
        _serve = false;
    }

    public void ScheduleServe(float time)
    {
        _hitTime = time;
        _scheduled = true;
        _swung = false;
        _willMiss = false;
        _serve = true;
        _pressure = 0;
    }

    // Smash incoming: dive toward it and get blown away
    public void ForceMiss(Trajectory traj)
    {
        var bounceTime = traj.TimeAtHeightDescending(Court.BallRadius);
        var bounce = traj.PositionAt(bounceTime);
        var pos = Robot.transform.position;
        Robot.MaxSpeed = 7;
        Robot.MoveTo(Vector3.Lerp(pos, new Vector3(bounce.x, pos.y, pos.z), 0.45f), bounceTime + 0.25f);
        _scheduled = false;
        _knockAt = traj.TimeAtZ(pos.z - 1.5f);
        if (float.IsNaN(_knockAt)) _knockAt = bounceTime + 0.15f;
        _lungeAt = bounceTime - 0.02f;
        _smashed = true;
    }

    public void Cancel()
    {
        _scheduled = false;
        Robot.MaxSpeed = 14;
        Robot.SetReady(0, SwingType.Forehand);
    }

    public void ResetRally() => (_shotsSinceLob, _smashed) = (0, false);

    // Debug: force the next return to be a lob
    public static bool ForceLob { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDebugFlags() => ForceLob = false;

    // MonoBehaviour implementation

    void Update()
    {
        var now = Time.time;

        if (_smashed)
        {
            if (_lungeAt > 0 && now >= _lungeAt) { Robot.Swing(SwingType.Lunge); _lungeAt = -1; }
            if (now >= _knockAt) { Robot.Knockback(1.2f); _smashed = false; }
        }

        if (!_scheduled) return;

        var type = _serve ? SwingType.Serve : SwingType.Forehand;
        Robot.SetReady(1 - Mathf.Clamp01((_hitTime - now - 0.08f) / 0.5f), type);

        if (!_swung && now >= _hitTime - 0.05f)
        {
            Robot.Swing(_willMiss ? SwingType.Lunge : type);
            _swung = true;
        }

        if (now < _hitTime) return;
        _scheduled = false;
        if (_willMiss) return;

        Game.CpuHit(ChooseShot(Ball.Position, _serve));
    }

    // Private members

    float _hitTime, _pressure, _incomingQuality, _knockAt, _lungeAt;
    bool _scheduled, _swung, _willMiss, _serve, _smashed;
    int _shotsSinceLob;

    CpuShot ChooseShot(Vector3 from, bool serve)
    {
        var player = Opponent.transform.position;
        var skill = Game.CpuSkill;

        // Weak lob: smash chance for the player
        var lobChance = 0.1f + _pressure * 0.5f + (_incomingQuality > 0.85f ? 0.15f : 0)
                        + (_shotsSinceLob >= 4 ? 0.3f : 0);
        if (!serve && !Game.IsDemo && (ForceLob || Random.value < Mathf.Min(lobChance, 0.8f)))
        {
            _shotsSinceLob = 0;
            var lobTarget = new Vector3(Random.Range(-1.8f, 1.8f), 0, -Random.Range(4.4f, 5.2f));
            return new CpuShot
            {
                Velocity = ShotPlanner.Solve(from, lobTarget, Random.Range(1.95f, 2.15f)),
                Quality = 0.2f, IsLob = true
            };
        }
        _shotsSinceLob++;

        var q = Mathf.Clamp01(Random.Range(0.3f, 0.75f) + skill * 0.25f - _pressure * 0.3f);
        var awayX = player.x > 0.3f ? -1 : player.x < -0.3f ? 1 : (Random.value < 0.5f ? -1 : 1);
        var tx = awayX * Mathf.Lerp(0.6f, 3.1f, q) + Random.Range(-0.4f, 0.4f);
        var tz = -Random.Range(3.0f, 4.9f);

        // Occasional unforced error under pressure
        if (!serve && Random.value < 0.03f + _pressure * 0.07f) tz = -Random.Range(5.9f, 6.6f);

        var time = Mathf.Lerp(1.4f, 0.72f, q) * (serve ? 1.1f : 1);
        return new CpuShot
        {
            Velocity = ShotPlanner.Solve(from, new Vector3(tx, 0, tz), time),
            Quality = q, IsServe = serve
        };
    }

    static float Smooth(float a, float b, float x)
    {
        var t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }
}

} // namespace SmashBots
