using UnityEngine;

namespace SmashBots {

// Prediction of where the receiver meets the ball after its bounce
public struct ReceivePlan
{
    public Vector3 Bounce;
    public float BounceTime;
    public Vector3 Point;
    public float Time;
    public Vector3 SecondBounce;
    public float SecondBounceTime;
}

public static class ShotPlanner
{
    // Initial velocity that lands the ball on the target in about the given
    // flight time, stretched until it clears the net.
    public static Vector3 Solve(Vector3 from, Vector3 target, float time, float clearance = 0.15f)
    {
        target.y = Court.BallRadius;
        var v = Vector3.zero;
        for (var i = 0; i < 24; i++)
        {
            v = Velocity(from, target, time);
            if (ClearsNet(from, v, clearance)) break;
            time *= 1.07f;
        }
        return v;
    }

    static Vector3 Velocity(Vector3 p0, Vector3 p1, float t)
    {
        var d = p1 - p0;
        return new Vector3(d.x / t, d.y / t + 0.5f * Court.Gravity * t, d.z / t);
    }

    public static bool ClearsNet(Vector3 p0, Vector3 v, float clearance)
    {
        if (Mathf.Abs(v.z) < 1e-4f) return true;
        var t = -p0.z / v.z;
        if (t <= 0) return true;
        var y = p0.y + v.y * t - 0.5f * Court.Gravity * t * t;
        return y >= Court.NetHeight + clearance;
    }

    // Where and when the receiving side should hit the ball, given the
    // trajectory right after the opponent's hit.
    public static ReceivePlan PredictReceive(Trajectory traj, Side receiver)
    {
        var plan = new ReceivePlan();
        plan.BounceTime = traj.TimeAtHeightDescending(Court.BallRadius);
        plan.Bounce = traj.PositionAt(plan.BounceTime);

        var after = traj.Bounce(plan.BounceTime);
        plan.SecondBounceTime = after.TimeAtHeightDescending(Court.BallRadius);
        plan.SecondBounce = after.PositionAt(plan.SecondBounceTime);

        // Hit at the apex for low bounces, otherwise on the way down
        var t = after.ApexHeight <= Court.HitHeight + 0.25f
          ? after.ApexTime : after.TimeAtHeightDescending(Court.HitHeight);

        // Don't let the ball run too deep behind the baseline
        var limit = receiver.Sign() * Court.BackLimit;
        if (Mathf.Abs(after.PositionAt(t).z) > Court.BackLimit)
        {
            var tl = after.TimeAtZ(limit);
            if (!float.IsNaN(tl)) t = Mathf.Max(plan.BounceTime + 0.12f, Mathf.Min(t, tl));
        }

        // Leave room for late hits before the second bounce
        t = Mathf.Max(plan.BounceTime + 0.12f, Mathf.Min(t, plan.SecondBounceTime - 0.3f));

        plan.Time = t;
        plan.Point = after.PositionAt(t);
        return plan;
    }
}

} // namespace SmashBots
