using UnityEngine;

namespace SmashBots {

public enum Side { Player, Cpu }

// Court dimensions and ball physics constants (meters, seconds)
public static class Court
{
    public const float HalfWidth = 3.6f;
    public const float HalfLength = 5.4f;
    public const float NetHeight = 0.95f;
    public const float NetHalfWidth = 4.3f;
    public const float BackLimit = 8.2f;

    public const float Gravity = 12f;
    public const float BallRadius = 0.13f;
    public const float Restitution = 0.72f;
    public const float Friction = 0.62f;

    public const float HitHeight = 1.2f;
    public const float HoverHeight = 1.0f;

    public static Vector3 GravityVector => new Vector3(0, -Gravity, 0);

    public static Side Opponent(this Side side) => side == Side.Player ? Side.Cpu : Side.Player;

    // Z direction of the side's half of the court
    public static float Sign(this Side side) => side == Side.Player ? -1 : 1;

    public static Side SideOf(Vector3 p) => p.z < 0 ? Side.Player : Side.Cpu;

    public static bool IsInBounds(Vector3 p, float margin = 0.05f)
      => Mathf.Abs(p.x) <= HalfWidth + margin && Mathf.Abs(p.z) <= HalfLength + margin;
}

} // namespace SmashBots
