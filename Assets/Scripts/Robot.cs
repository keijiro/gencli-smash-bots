using UnityEngine;

namespace SmashBots {

public enum SwingType { Forehand, Smash, Serve, Whiff, Lunge }

public sealed class Robot : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public Side Side { get; set; }
    [field:SerializeField] public Transform Body { get; set; }
    [field:SerializeField] public Transform RacketPivot { get; set; }
    [field:SerializeField] public Transform RacketHead { get; set; }
    [field:SerializeField] public ParticleSystem Thruster { get; set; }
    [field:SerializeField] public Light ThrusterLight { get; set; }
    [field:SerializeField] public float MaxSpeed { get; set; } = 14;
    [field:SerializeField] public float Acceleration { get; set; } = 60;

    public Vector3 Velocity => _velocity;
    public Vector3 HomePosition => new Vector3(0, Court.HoverHeight, Side.Sign() * 7.0f);
    public Vector3 Forward => new Vector3(0, 0, -Side.Sign());

    // Public methods

    // Moves the robot so that it arrives at the target at the given time
    public void MoveTo(Vector3 target, float arriveTime)
      => (_target, _arriveTime) = (ClampTarget(target), arriveTime);

    public void MoveTo(Vector3 target) => MoveTo(target, 0);

    public void Teleport(Vector3 position)
    {
        _target = ClampTarget(position);
        transform.position = _target;
        _velocity = Vector3.zero;
    }

    // Root position that puts the racket head on the given point
    public Vector3 PositionForContact(Vector3 point, SwingType type)
      => point - ContactOffset(type);

    public void SetReady(float amount, SwingType type)
      => (_ready, _readyType) = (Mathf.Clamp01(amount), type);

    public void Swing(SwingType type)
    {
        _swing = type;
        _swingTime = 0;
        _swingFrom = _pose;
        _ready = 0;
        if (type != SwingType.Whiff && type != SwingType.Lunge) _squash = 1;
        if (type == SwingType.Lunge) _lunge = 1;
    }

    public void Knockback(float strength)
    {
        _knock = strength;
        _knockSpin = 0;
    }

    public void ResetPose()
    {
        _swing = null;
        _ready = 0;
        _knock = 0;
        _knockSpin = 0;
        _lunge = 0;
    }

    // MonoBehaviour implementation

    void Start()
    {
        _target = transform.position;
        _pose = RestPose;
        _headInPivot = RacketPivot.InverseTransformPoint(RacketHead.position);
        _bobPhase = Side == Side.Player ? 0 : 1.7f;
    }

    void Update()
    {
        var dt = Time.deltaTime;
        UpdateMovement(dt);
        UpdateBody(dt);
        UpdateRacket(dt);
        UpdateThruster();
    }

    // Movement

    Vector3 _target, _velocity;
    float _arriveTime;

    static Vector3 ClampTarget(Vector3 p)
      => new Vector3(Mathf.Clamp(p.x, -8, 8), Mathf.Clamp(p.y, 0.7f, 3.2f), Mathf.Clamp(p.z, -11, 11));

    void UpdateMovement(float dt)
    {
        if (dt <= 0) return;
        var to = _target - transform.position;
        var remain = _arriveTime - Time.time;
        var desired = remain > 0.02f ? to / remain : to * 8;
        desired = Vector3.ClampMagnitude(desired, MaxSpeed);
        _velocity = Vector3.MoveTowards(_velocity, desired, Acceleration * dt);
        transform.position += _velocity * dt;
    }

    // Body animation

    float _bobPhase, _squash, _knock, _knockSpin, _lunge;

    void UpdateBody(float dt)
    {
        var t = Time.time;
        var bob = Mathf.Sin(t * 2.4f + _bobPhase) * 0.045f;

        // Lean into the motion (local space)
        var lv = transform.InverseTransformDirection(_velocity);
        var roll = Mathf.Clamp(-lv.x * 2.5f, -28, 28);
        var pitch = Mathf.Clamp(lv.z * 2.5f + lv.y * -2, -28, 28);

        // Lunge: dive toward the racket side
        _lunge = Mathf.Max(0, _lunge - dt * 1.6f);
        roll -= Mathf.Sin(_lunge * Mathf.PI) * 35;

        // Knockback: spin and wobble
        if (_knock > 0)
        {
            _knockSpin += dt * 900 * _knock;
            _knock = Mathf.Max(0, _knock - dt * 0.7f);
            roll += Mathf.Sin(t * 13) * 25 * _knock;
            pitch += Mathf.Cos(t * 11) * 20 * _knock;
        }
        else
        {
            // Settle back to facing forward
            _knockSpin = Mathf.LerpAngle(_knockSpin, 0, 1 - Mathf.Exp(-6 * dt));
        }
        var spin = _knockSpin;

        Body.localPosition = new Vector3(0, bob, 0);
        Body.localRotation = Quaternion.Euler(pitch, spin, roll);

        _squash = Mathf.Max(0, _squash - dt * 5);
        var s = 1 + Mathf.Sin(_squash * Mathf.PI) * 0.1f;
        Body.localScale = new Vector3(s, 2 - s, s);
    }

    // Racket animation (pivot euler angles)

    static readonly Vector3 RestPose = new Vector3(0, 15, 35);
    static readonly Vector3 ForehandContact = new Vector3(0, 0, 30);
    static readonly Vector3 SmashCocked = new Vector3(-80, 10, 95);
    static readonly Vector3 SmashContact = new Vector3(5, 0, 95);

    Vector3 _pose, _swingFrom, _headInPivot;
    SwingType? _swing;
    float _swingTime, _ready;
    SwingType _readyType;

    Vector3 ContactPose(SwingType type)
      => type == SwingType.Smash || type == SwingType.Serve ? SmashContact : ForehandContact;

    Vector3 ContactOffset(SwingType type)
    {
        var head = RacketPivot.localPosition + Quaternion.Euler(ContactPose(type)) * _headInPivot;
        return transform.rotation * head;
    }

    Vector3 ReadyPose()
    {
        if (_readyType == SwingType.Smash || _readyType == SwingType.Serve)
            return Vector3.Lerp(RestPose, SmashCocked, _ready);
        return RestPose + new Vector3(0, 75, -12) * _ready;
    }

    void UpdateRacket(float dt)
    {
        if (_swing is SwingType type)
        {
            _swingTime += dt;
            var overhead = type == SwingType.Smash || type == SwingType.Serve;
            var contact = ContactPose(type);
            var follow = overhead ? new Vector3(85, -10, 95) : new Vector3(0, -100, 50);
            var tc = type == SwingType.Lunge ? 0.12f : 0.05f;
            var tf = tc + (overhead ? 0.1f : 0.09f);
            var tr = tf + 0.35f;

            if (_swingTime < tc) _pose = Vector3.Lerp(_swingFrom, contact, Ease(_swingTime / tc));
            else if (_swingTime < tf) _pose = Vector3.Lerp(contact, follow, Ease((_swingTime - tc) / (tf - tc)));
            else if (_swingTime < tr) _pose = Vector3.Lerp(follow, RestPose, Ease((_swingTime - tf) / (tr - tf)));
            else { _pose = RestPose; _swing = null; }
        }
        else
        {
            _pose = Vector3.Lerp(_pose, ReadyPose(), 1 - Mathf.Exp(-14 * dt));
        }
        RacketPivot.localRotation = Quaternion.Euler(_pose);
    }

    static float Ease(float x) => 1 - (1 - x) * (1 - x);

    // Thruster effect

    void UpdateThruster()
    {
        var speed = _velocity.magnitude;
        var rise = Mathf.Max(0, transform.position.y - Court.HoverHeight);
        if (ThrusterLight != null)
            ThrusterLight.intensity = 1.6f + speed * 0.25f + rise * 1.5f + Mathf.Sin(Time.time * 30) * 0.15f;
        if (Thruster != null)
        {
            var em = Thruster.emission;
            em.rateOverTimeMultiplier = 25 + speed * 8 + rise * 40;
        }
    }
}

} // namespace SmashBots
