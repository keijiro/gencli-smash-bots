using UnityEngine;

namespace SmashBots {

// Orchestrates the smash chance: slow motion, QTE reticle, cinematic
// camera, impact effects and the recovery back to normal play.
public sealed class SmashDirector : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public GameManager Game { get; set; }
    [field:SerializeField] public Robot Player { get; set; }
    [field:SerializeField] public Robot Cpu { get; set; }
    [field:SerializeField] public CpuController CpuAi { get; set; }
    [field:SerializeField] public Ball Ball { get; set; }
    [field:SerializeField] public Camera Camera { get; set; }
    [field:SerializeField] public CameraDirector CameraDirector { get; set; }
    [field:SerializeField] public HudController Hud { get; set; }
    [field:SerializeField] public TimeController TimeControl { get; set; }
    [field:SerializeField] public PostFxController PostFx { get; set; }
    [field:SerializeField] public AudioController Audio { get; set; }
    [field:SerializeField] public Effects Effects { get; set; }

    public bool IsActive => _phase != Phase.None;
    public string PhaseName => _phase.ToString();
    public float QteProgress => _phase == Phase.Qte ? Elapsed / Converge : 0;

    // Public methods

    public void Begin(Trajectory traj)
    {
        _contactTime = traj.TimeAtHeightDescending(ContactHeight);
        _contact = traj.PositionAt(_contactTime);
        Player.MoveTo(Player.PositionForContact(_contact, SwingType.Smash), _contactTime - 0.3f);
        Hud.ShowSmashChance();
        Audio.Play(Audio.UiClick, 0.8f, 0.7f);
        _lockSounded = false;
        SetPhase(Phase.Approach);
    }

    public void OnSmashBounce(Vector3 p)
    {
        Effects.Spawn(Effects.GroundExplosion, p, 1.2f);
        Audio.Play(Audio.GroundExplode, 1);
        CameraDirector.Shake(0.7f);
        PostFx.Impact(0.45f);
        Hud.Flash(0.15f);
    }

    public void Abort()
    {
        Hud.ShowQte(false);
        Hud.SetLetterbox(false);
        Hud.SetSpeedLines(0);
        Audio.SetSlowMo(0);
        PostFx.SetSlowMo(0);
        TimeControl.ResetTime();
        SetPhase(Phase.None);
    }

    // MonoBehaviour implementation

    void Update()
    {
        switch (_phase)
        {
            case Phase.Approach: UpdateApproach(); break;
            case Phase.Qte: UpdateQte(); break;
            case Phase.Impact: if (Elapsed > 0.45f) StartFollow(); break;
            case Phase.Follow: UpdateFollow(); break;
            case Phase.Recover: UpdateRecover(); break;
        }
    }

    // Private members

    enum Phase { None, Approach, Qte, Impact, Follow, Recover }

    const float ContactHeight = 2.35f;
    const float Tau = 0.5f;        // game seconds before contact when the QTE starts
    const float RampIn = 0.3f;     // real seconds to reach full slow motion
    const float Converge = 1.5f;   // real seconds until the reticle locks on
    const float LateLimit = 0.45f; // real seconds after lock-on before missing

    Phase _phase;
    float _phaseStart, _contactTime, _slow, _recoverDuration, _recoverFrom;
    Vector3 _contact;
    bool _lockSounded;

    float Elapsed => Time.unscaledTime - _phaseStart;

    void SetPhase(Phase phase)
    {
        _phase = phase;
        _phaseStart = Time.unscaledTime;
    }

    void UpdateApproach()
    {
        var remain = _contactTime - Time.time;
        Player.SetReady(Mathf.Clamp01(1 - (remain - Tau) / 0.8f), SwingType.Smash);
        if (remain <= Tau) StartQte();
    }

    void StartQte()
    {
        SetPhase(Phase.Qte);
        // Slow-motion factor that makes the ball meet the racket at lock-on
        var tau = _contactTime - Time.time;
        _slow = Mathf.Clamp((tau - RampIn * 0.5f) / (Converge - RampIn * 0.5f), 0.04f, 1);
        CameraDirector.SmashContact = _contact;
        CameraDirector.SetShot(CameraShot.SmashSetup, 0.35f);
        Hud.ShowQte(true);
        Hud.SetLetterbox(true);
        Hud.SetSpeedLines(0.35f);
        Audio.Play(Audio.SlowMoIn, 0.9f);
        Audio.SetSlowMo(1);
        PostFx.SetSlowMo(1);
        Player.SetReady(1, SwingType.Smash);
    }

    void UpdateQte()
    {
        var u = Elapsed;
        TimeControl.SlowMo = u < RampIn ? Mathf.Lerp(1, _slow, u / RampIn) : _slow;

        var screen = Camera.WorldToScreenPoint(_contact);
        Hud.UpdateQte(screen, u / Converge);

        if (u >= Converge && !_lockSounded)
        {
            _lockSounded = true;
            Audio.Play(Audio.QteLock, 1);
        }

        var auto = PlayerController.AutoPlay && u >= Converge;
        if (u > 0.25f && (PointerInput.Pressed || auto))
        {
            var click = auto ? (Vector2)screen : PointerInput.Position;
            var dt = u - Converge;
            var timing = 1 - Smooth(0.07f, 0.4f, Mathf.Abs(dt));
            var d = Vector2.Distance(click, screen) / Screen.height;
            var aim = 1 - Smooth(0.05f, 0.2f, d);
            var score = timing * (0.25f + 0.75f * aim);
            if (score >= 0.45f) DoSmash();
            else DoNormalReturn(Mathf.Lerp(0.25f, 0.6f, score / 0.45f), click);
            return;
        }

        if (u > Converge + LateLimit) DoMiss();
    }

    void DoSmash()
    {
        Hud.ShowQte(false);

        // Aim at the far corner away from the opponent
        var from = Ball.Position;
        var cx = Cpu.transform.position.x;
        var side = cx > 0.2f ? -1 : cx < -0.2f ? 1 : (Random.value < 0.5f ? -1 : 1);
        var target = new Vector3(side * 3.0f, 0, 4.0f);
        var v = ShotPlanner.Solve(from, target, 0.3f, 0.1f);

        Player.Swing(SwingType.Smash);
        Game.ExecuteSmash(from, v);
        CpuAi.ForceMiss(Ball.Trajectory);

        TimeControl.Hitstop(0.16f);
        TimeControl.SlowMo = 0.3f;
        CameraDirector.SetShot(CameraShot.SmashImpact, 0);
        CameraDirector.Shake(1);
        CameraDirector.KickFov(-12);
        Hud.Flash(0.4f);
        Hud.ShowSmashTitle();
        Hud.SetSpeedLines(1);
        PostFx.Impact(1);
        Audio.Play(Audio.SmashImpact, 1);
        Effects.Spawn(Effects.SmashBurst, from, Quaternion.LookRotation(Camera.transform.position - from), 1.2f);

        SetPhase(Phase.Impact);
    }

    void StartFollow()
    {
        SetPhase(Phase.Follow);
        CameraDirector.SetShot(CameraShot.SmashFollow, 0.12f);
        Hud.SetSpeedLines(0.6f);
    }

    void UpdateFollow()
    {
        TimeControl.SlowMo = Mathf.Lerp(0.3f, 0.5f, Elapsed / 1.2f);
        if (Elapsed > 1.3f || Ball.Position.z > 16) StartRecover(0.7f, 1.0f);
    }

    void StartRecover(float duration, float cameraBlend)
    {
        SetPhase(Phase.Recover);
        _recoverDuration = duration;
        _recoverFrom = TimeControl.SlowMo;
        CameraDirector.SetShot(CameraShot.Gameplay, cameraBlend);
        Hud.ShowQte(false);
        Hud.SetLetterbox(false);
        Hud.SetSpeedLines(0);
        Audio.SetSlowMo(0);
        PostFx.SetSlowMo(0);
    }

    void UpdateRecover()
    {
        var p = Mathf.Clamp01(Elapsed / _recoverDuration);
        TimeControl.SlowMo = Mathf.Lerp(_recoverFrom, 1, p * p);
        if (p < 1) return;
        TimeControl.ResetTime();
        SetPhase(Phase.None);
    }

    void DoNormalReturn(float quality, Vector2 click)
    {
        StartRecover(0.25f, 0.4f);
        Game.PlayerHit(quality, click, false);
    }

    void DoMiss()
    {
        Player.Swing(SwingType.Whiff);
        Audio.Play(Audio.Whiff, 0.8f);
        Hud.ShowRating("MISS", Camera.WorldToScreenPoint(_contact), new Color(0.6f, 0.7f, 0.8f));
        StartRecover(0.35f, 0.6f);
    }

    static float Smooth(float a, float b, float x)
    {
        var t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3 - 2 * t);
    }
}

} // namespace SmashBots
