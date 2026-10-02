using UnityEngine;

namespace SmashBots {

// Ballistic segment of the ball motion, evaluated analytically in game time
public readonly struct Trajectory
{
    public readonly Vector3 Origin;
    public readonly Vector3 Velocity;
    public readonly float StartTime;

    public Trajectory(Vector3 origin, Vector3 velocity, float startTime)
      => (Origin, Velocity, StartTime) = (origin, velocity, startTime);

    public Vector3 PositionAt(float t)
    {
        var dt = t - StartTime;
        return Origin + Velocity * dt + 0.5f * dt * dt * Court.GravityVector;
    }

    public Vector3 VelocityAt(float t)
      => Velocity + (t - StartTime) * Court.GravityVector;

    public float ApexTime => StartTime + Mathf.Max(0, Velocity.y / Court.Gravity);

    public float ApexHeight => PositionAt(ApexTime).y;

    // Absolute time when the ball comes down through height h (NaN if never)
    public float TimeAtHeightDescending(float h)
    {
        var vy = Velocity.y;
        var d = vy * vy - 2 * Court.Gravity * (h - Origin.y);
        if (d < 0) return float.NaN;
        return StartTime + (vy + Mathf.Sqrt(d)) / Court.Gravity;
    }

    // Absolute time when the ball passes the given z (NaN if never)
    public float TimeAtZ(float z)
    {
        if (Mathf.Abs(Velocity.z) < 1e-5f) return float.NaN;
        var t = (z - Origin.z) / Velocity.z;
        return t < 0 ? float.NaN : StartTime + t;
    }

    public Trajectory Bounce(float t)
    {
        var p = PositionAt(t);
        var v = VelocityAt(t);
        p.y = Court.BallRadius;
        v.x *= Court.Friction;
        v.z *= Court.Friction;
        v.y = Mathf.Abs(v.y) * Court.Restitution;
        return new Trajectory(p, v, t);
    }

    public Trajectory BounceNoFriction(float t)
    {
        var p = PositionAt(t);
        var v = VelocityAt(t);
        p.y = Court.BallRadius;
        v.y = Mathf.Abs(v.y) * Court.Restitution;
        return new Trajectory(p, v, t);
    }
}

} // namespace SmashBots
