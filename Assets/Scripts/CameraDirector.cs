using UnityEngine;

namespace SmashBots {

public enum CameraShot { Title, Gameplay, SmashSetup, SmashImpact, SmashFollow, CpuReaction }

public sealed class CameraDirector : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public Camera Camera { get; set; }
    [field:SerializeField] public Robot Player { get; set; }
    [field:SerializeField] public Robot Cpu { get; set; }
    [field:SerializeField] public Ball Ball { get; set; }

    public CameraShot Shot => _shot;

    // Smash shot parameters (set by the smash director)
    public Vector3 SmashContact { get; set; }

    // Public methods

    public void SetShot(CameraShot shot, float blendTime)
    {
        _from = _current;
        _shot = shot;
        _shotStart = Time.unscaledTime;
        _blendTime = blendTime;
        if (blendTime <= 0) _current = Evaluate(shot);
        if (shot == CameraShot.SmashFollow) _followAnchor = Camera.transform.position;
    }

    public void Shake(float amplitude) => _shake = Mathf.Max(_shake, amplitude);

    public void KickFov(float amount) => _fovKick = amount;

    // MonoBehaviour implementation

    void Start()
    {
        _smoothedPlayer = Player.transform.position;
        _current = Evaluate(_shot);
        _from = _current;
    }

    void LateUpdate()
    {
        var dt = Time.unscaledDeltaTime;
        _smoothedPlayer = Vector3.Lerp(_smoothedPlayer, Player.transform.position, 1 - Mathf.Exp(-3 * dt));
        _smoothedBall = Vector3.Lerp(_smoothedBall, Ball.Position, 1 - Mathf.Exp(-4 * dt));

        var target = Evaluate(_shot);
        var p = _blendTime > 0 ? Mathf.Clamp01((Time.unscaledTime - _shotStart) / _blendTime) : 1;
        _current = Pose.Lerp(_from, target, p * p * (3 - 2 * p));

        // Shake and FOV kick
        _shake = Mathf.Max(0, _shake - dt * 2.2f * Mathf.Max(0.3f, _shake));
        _fovKick = Mathf.Lerp(_fovKick, 0, 1 - Mathf.Exp(-6 * dt));
        var n = Time.unscaledTime * 28;
        var shakeRot = new Vector3(Mathf.PerlinNoise(n, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, n) - 0.5f, Mathf.PerlinNoise(n, n) - 0.5f) * (_shake * 6);

        Camera.transform.SetPositionAndRotation(_current.Position, _current.Rotation * Quaternion.Euler(shakeRot));
        Camera.fieldOfView = _current.Fov + _fovKick;
    }

    // Private members

    struct Pose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Fov;

        public static Pose LookAt(Vector3 position, Vector3 target, float fov, float roll = 0)
          => new Pose { Position = position, Rotation = Quaternion.LookRotation(target - position) * Quaternion.Euler(0, 0, roll), Fov = fov };

        public static Pose Lerp(Pose a, Pose b, float t)
          => new Pose { Position = Vector3.Lerp(a.Position, b.Position, t), Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t), Fov = Mathf.Lerp(a.Fov, b.Fov, t) };
    }

    CameraShot _shot = CameraShot.Title;
    Pose _current, _from;
    float _shotStart, _blendTime, _shake, _fovKick;
    Vector3 _smoothedPlayer, _smoothedBall, _followAnchor;

    Pose Evaluate(CameraShot shot)
    {
        var t = Time.unscaledTime - _shotStart;
        var player = Player.transform.position;
        switch (shot)
        {
            case CameraShot.Title:
            {
                // Slow sweeping crane shot from behind the player's court
                var a = Time.unscaledTime * 0.15f;
                var pos = new Vector3(Mathf.Sin(a) * 5.0f, 3.6f + Mathf.Sin(a * 0.6f) * 0.8f, -13.5f + Mathf.Cos(a * 0.8f) * 1.5f);
                var look = new Vector3(Mathf.Sin(a) * 1.5f, 0.6f, 1.0f);
                return Pose.LookAt(pos, look, 52, Mathf.Sin(a * 0.9f) * 3);
            }
            case CameraShot.Gameplay:
            {
                // Behind and slightly right of the player robot, framing the court
                var px = _smoothedPlayer.x;
                var pos = new Vector3(px * 0.6f + 0.8f, 2.35f, Mathf.Min(_smoothedPlayer.z - 4.3f, -10.8f));
                var look = new Vector3(px * 0.25f + _smoothedBall.x * 0.1f, 0.7f, 3.5f);
                return Pose.LookAt(pos, look, 46);
            }
            case CameraShot.SmashSetup:
            {
                // Low angle from behind-left of the robot, slowly orbiting
                var orbit = Quaternion.Euler(0, 12 + t * 9, 0);
                var offset = orbit * new Vector3(-2.2f, 0, -1.5f);
                var pos = player + offset + new Vector3(0, -0.9f, 0);
                pos.y = Mathf.Max(0.35f, pos.y);
                var look = Vector3.Lerp(player, SmashContact, 0.6f) + new Vector3(0, 0.15f, 0.6f);
                return Pose.LookAt(pos, look, 60, -6);
            }
            case CameraShot.SmashImpact:
            {
                var orbit = Quaternion.Euler(0, 28 + t * 14, 0);
                var pos = player + orbit * new Vector3(-1.6f, 0, -1.0f) + new Vector3(0, -0.7f, 0);
                pos.y = Mathf.Max(0.35f, pos.y);
                return Pose.LookAt(pos, SmashContact + new Vector3(0, -0.2f, 0.8f), 52, -10);
            }
            case CameraShot.SmashFollow:
            {
                // Chase the ball until mid court, then let it fly away
                var b = Ball.Position;
                var v = Ball.Velocity;
                var dir = v.sqrMagnitude > 0.01f ? v.normalized : Vector3.forward;
                var chase = b - new Vector3(dir.x, 0, dir.z).normalized * 3.2f + new Vector3(0.9f, 1.1f, 0);
                if (b.z > 2.5f) chase = _followAnchor;
                else _followAnchor = chase;
                return Pose.LookAt(chase, b + new Vector3(0, 0.2f, 0), 62, 4);
            }
            case CameraShot.CpuReaction:
            {
                var c = Cpu.transform.position;
                var pos = c + new Vector3(2.2f, 0.4f, -3.5f);
                return Pose.LookAt(pos, c + new Vector3(0, 0.1f, 0), 45);
            }
        }
        return _current;
    }
}

} // namespace SmashBots
