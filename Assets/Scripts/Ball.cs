using UnityEngine;

namespace SmashBots {

public sealed class Ball : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public Transform Visual { get; set; }
    [field:SerializeField] public TrailRenderer Trail { get; set; }
    [field:SerializeField] public Light Glow { get; set; }
    [field:SerializeField] public Transform Shadow { get; set; }
    [field:SerializeField] public Renderer ShadowRenderer { get; set; }

    [field:SerializeField, ColorUsage(false, true)]
    public Color NormalTrailColor { get; set; } = new Color(1.4f, 2.0f, 0.3f);

    [field:SerializeField, ColorUsage(false, true)]
    public Color PowerTrailColor { get; set; } = new Color(4.0f, 1.6f, 0.3f);

    public Trajectory Trajectory => _traj;
    public bool IsActive => _active;
    public Vector3 Position => transform.position;
    public Vector3 Velocity => !_active ? Vector3.zero :
      _rolling ? _traj.Velocity * Mathf.Exp(-1.5f * (Time.time - _traj.StartTime)) : _traj.VelocityAt(Time.time);

    public event System.Action<Vector3, float> Bounced;
    public event System.Action<Vector3> NetHit;
    public event System.Action<Vector3, float> WallHit;

    // Public methods

    public void Launch(Vector3 position, Vector3 velocity)
    {
        _traj = new Trajectory(position, velocity, Time.time);
        _lastTime = Time.time;
        _netDone = false;
        _active = true;
        _noFriction = false;
        _rolling = false;
        transform.position = position;
        SetVisible(true);
    }

    public void Hold(Vector3 position)
    {
        _active = false;
        transform.position = position;
        SetVisible(true);
        Trail.Clear();
    }

    public void Hide()
    {
        _active = false;
        SetVisible(false);
    }

    public void SetPowerMode(bool on)
    {
        _power = on;
        _noFriction = on;
        Trail.widthMultiplier = on ? 0.55f : 0.2f;
        Trail.time = on ? 0.45f : 0.2f;
        var c = on ? PowerTrailColor : NormalTrailColor;
        Trail.startColor = c;
        Trail.endColor = new Color(c.r, c.g, c.b, 0);
        Glow.color = on ? new Color(1, 0.6f, 0.2f) : new Color(0.8f, 1, 0.3f);
        Glow.intensity = on ? 6 : 1.2f;
    }

    // MonoBehaviour implementation

    void Awake()
    {
        _shadowProps = new MaterialPropertyBlock();
        SetPowerMode(false);
    }

    void Update()
    {
        if (_active) Simulate(Time.time);
        UpdateVisuals();
    }

    // Private members

    Trajectory _traj;
    float _lastTime;
    bool _active, _netDone, _power, _noFriction, _rolling;

    const float WallX = 8.6f;
    const float WallZ = 21.6f;

    void Simulate(float now)
    {
        if (_rolling) { Roll(now); return; }

        for (var guard = 0; guard < 8; guard++)
        {
            var tGround = _traj.TimeAtHeightDescending(Court.BallRadius);
            var tNet = _netDone ? float.NaN : _traj.TimeAtZ(0);
            var tWall = WallTime();

            if (!float.IsNaN(tNet) && tNet < _lastTime - 1e-4f) { _netDone = true; tNet = float.NaN; }

            var next = Earliest(tGround, tNet, tWall);
            if (float.IsNaN(next) || next > now) break;

            if (next == tNet)
            {
                _netDone = true;
                var p = _traj.PositionAt(tNet);
                if (p.y < Court.NetHeight && Mathf.Abs(p.x) < Court.NetHalfWidth)
                {
                    var v = _traj.VelocityAt(tNet);
                    var back = -Mathf.Sign(v.z) * (Court.BallRadius + 0.02f);
                    _traj = new Trajectory(new Vector3(p.x, p.y, back),
                                           new Vector3(v.x * 0.2f, Mathf.Min(v.y, 0) * 0.3f, -v.z * 0.12f), tNet);
                    NetHit?.Invoke(p);
                }
            }
            else if (next == tGround)
            {
                var speed = _traj.VelocityAt(tGround).magnitude;
                _traj = _noFriction ? _traj.BounceNoFriction(tGround) : _traj.Bounce(tGround);
                Bounced?.Invoke(_traj.Origin, speed);
                if (_traj.Velocity.y < 0.7f)
                {
                    // Settle and roll
                    var v = _traj.Velocity;
                    v.y = 0;
                    _traj = new Trajectory(_traj.Origin, v, tGround);
                    _rolling = true;
                    Roll(now);
                    return;
                }
            }
            else
            {
                var p = _traj.PositionAt(tWall);
                var v = _traj.VelocityAt(tWall) * 0.5f;
                if (Mathf.Abs(p.x) >= WallX - 1e-3f) v.x = -v.x;
                if (Mathf.Abs(p.z) >= WallZ - 1e-3f) v.z = -v.z;
                p.x = Mathf.Clamp(p.x, -WallX + 0.01f, WallX - 0.01f);
                p.z = Mathf.Clamp(p.z, -WallZ + 0.01f, WallZ - 0.01f);
                _traj = new Trajectory(p, v, tWall);
                WallHit?.Invoke(p, v.magnitude * 2);
            }
        }
        _lastTime = now;
        transform.position = _traj.PositionAt(now);
    }

    void Roll(float now)
    {
        // Exponentially decaying roll on the floor
        const float k = 1.5f;
        var dt = now - _traj.StartTime;
        var d = (1 - Mathf.Exp(-k * dt)) / k;
        var p = _traj.Origin + _traj.Velocity * d;
        p.x = Mathf.Clamp(p.x, -WallX, WallX);
        p.z = Mathf.Clamp(p.z, -WallZ, WallZ);
        p.y = Court.BallRadius;
        transform.position = p;
        _lastTime = now;
    }

    float WallTime()
    {
        var v = _traj.Velocity;
        var o = _traj.Origin;
        var t = float.NaN;
        if (Mathf.Abs(v.x) > 1e-4f)
        {
            var tx = (Mathf.Sign(v.x) * WallX - o.x) / v.x;
            if (tx > 1e-4f) t = tx;
        }
        if (Mathf.Abs(v.z) > 1e-4f)
        {
            var tz = (Mathf.Sign(v.z) * WallZ - o.z) / v.z;
            if (tz > 1e-4f && (float.IsNaN(t) || tz < t)) t = tz;
        }
        return float.IsNaN(t) ? t : _traj.StartTime + t;
    }

    static float Earliest(float a, float b, float c)
    {
        var r = float.NaN;
        foreach (var x in new[] { a, b, c })
            if (!float.IsNaN(x) && (float.IsNaN(r) || x < r)) r = x;
        return r;
    }

    void SetVisible(bool on)
    {
        Visual.gameObject.SetActive(on);
        Glow.enabled = on;
        Trail.emitting = on;
        Shadow.gameObject.SetActive(on);
    }

    void UpdateVisuals()
    {
        if (!Visual.gameObject.activeSelf) return;

        // Spin around the axis perpendicular to the motion
        var v = Velocity;
        var axis = Vector3.Cross(Vector3.up, v);
        if (axis.sqrMagnitude > 1e-4f)
            Visual.Rotate(axis.normalized, v.magnitude * 120 * Time.deltaTime, Space.World);

        // Blob shadow right under the ball
        var p = transform.position;
        var h = Mathf.Max(0, p.y - Court.BallRadius);
        Shadow.position = new Vector3(p.x, 0.012f, p.z);
        Shadow.localScale = Vector3.one * (0.35f + h * 0.12f);
        _shadowProps.SetColor("_Color", new Color(0, 0, 0, Mathf.Lerp(0.55f, 0.12f, h / 4)));
        ShadowRenderer.SetPropertyBlock(_shadowProps);
    }

    MaterialPropertyBlock _shadowProps;
}

} // namespace SmashBots
