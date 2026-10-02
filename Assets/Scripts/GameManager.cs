using System.Collections;
using UnityEngine;

namespace SmashBots {

public sealed class GameManager : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public Ball Ball { get; set; }
    [field:SerializeField] public Robot PlayerRobot { get; set; }
    [field:SerializeField] public Robot CpuRobot { get; set; }
    [field:SerializeField] public PlayerController Player { get; set; }
    [field:SerializeField] public CpuController Cpu { get; set; }
    [field:SerializeField] public SmashDirector Smash { get; set; }
    [field:SerializeField] public HudController Hud { get; set; }
    [field:SerializeField] public CameraDirector CameraDirector { get; set; }
    [field:SerializeField] public AudioController Audio { get; set; }
    [field:SerializeField] public Effects Effects { get; set; }
    [field:SerializeField] public TimeController TimeControl { get; set; }
    [field:SerializeField] public LandingMarker Marker { get; set; }
    [field:SerializeField] public int WinScore { get; set; } = 7;

    public float CpuSkill => Mathf.Clamp01((_scores[0] + _scores[1]) / 10f);

    // Attract mode: an automatic rally behind the title screen
    public bool IsDemo => _demo;

    // Shot entry points

    public void PlayerHit(float q, Vector2 click, bool serve)
    {
        if (_state != State.Rally && _state != State.Serving) return;
        _state = State.Rally;
        _tossPending = false;

        var from = Ball.Position;
        var target = ChoosePlayerTarget(q, serve);
        var v = ShotPlanner.Solve(from, target, Mathf.Lerp(1.45f, 0.62f, q) * (serve ? 1.1f : 1));

        PlayerRobot.Swing(serve ? SwingType.Serve : SwingType.Forehand);
        LaunchShot(Side.Player, from, v, q, false);

        var (label, color) = PlayerController.Rate(q);
        if (!_demo) Hud.ShowRating(label, click, color);

        Cpu.OnIncoming(Ball.Trajectory, q);
    }

    public void CpuHit(CpuShot shot)
    {
        if (_state != State.Rally && _state != State.Serving) return;
        _state = State.Rally;
        _tossPending = false;

        var from = Ball.Position;
        CpuRobot.Swing(shot.IsServe ? SwingType.Serve : SwingType.Forehand);
        LaunchShot(Side.Cpu, from, shot.Velocity, shot.Quality, false);

        if (shot.IsLob) Smash.Begin(Ball.Trajectory);
        else ExpectPlayer();
    }

    public void ExecuteSmash(Vector3 from, Vector3 velocity)
    {
        LaunchShot(Side.Player, from, velocity, 1, true);
        Effects.Spawn(Effects.HitSparkPlayer, from, 2);
    }

    public void PlayerMissedWindow(bool serve)
    {
        if (serve) _tossPending = true; // re-toss when the ball lands
    }

    // MonoBehaviour implementation

    void Start()
    {
        Ball.Bounced += OnBallBounce;
        Ball.NetHit += OnNetHit;
        Ball.WallHit += OnWallHit;
        if (Audio.Music != null) Audio.Music.Play();
        EnterTitle();
    }

    void Update()
    {
        if (!PointerInput.Pressed) return;
        if (_demo && Time.unscaledTime > _demoStartTime + 0.3f) StartMatch();
        else if (_state == State.MatchOver && Time.unscaledTime > _stateTime + 1.5f) EnterTitle();
    }

    // Game flow

    enum State { Title, Serving, Rally, PointOver, MatchOver }

    State _state;
    float _stateTime, _demoStartTime;
    readonly int[] _scores = new int[2];
    Side _server, _lastHitter;
    Vector3 _servePosition;
    int _bounces;
    bool _tossPending, _smashShot, _demo;

    void SetState(State state)
    {
        _state = state;
        _stateTime = Time.unscaledTime;
    }

    void EnterTitle()
    {
        ResetRally();
        SetState(State.Title);
        Hud.ShowResult(false);
        Hud.ShowTitle(true);
        CameraDirector.SetShot(CameraShot.Title, 1.5f);
        _scores[0] = _scores[1] = 0;
        _demo = true;
        _demoStartTime = Time.unscaledTime;
        Player.Demo = true;
        StartCoroutine(ServeRoutine(1.5f));
    }

    void ResetRally()
    {
        StopAllCoroutines();
        Smash.Abort();
        Player.Cancel();
        Cpu.Cancel();
        Cpu.ResetRally();
        Marker.Hide();
        Ball.Hide();
        TimeControl.ResetTime();
        PlayerRobot.ResetPose();
        CpuRobot.ResetPose();
        PlayerRobot.MoveTo(PlayerRobot.HomePosition, Time.time + 0.8f);
        CpuRobot.MoveTo(CpuRobot.HomePosition, Time.time + 0.8f);
    }

    void StartMatch()
    {
        ResetRally();
        _demo = false;
        Player.Demo = false;
        Audio.Play(Audio.UiClick);
        _scores[0] = _scores[1] = 0;
        Hud.SetScores(0, 0);
        Hud.ShowTitle(false);
        Cpu.ResetRally();
        CameraDirector.SetShot(CameraShot.Gameplay, 1.4f);
        StartCoroutine(ServeRoutine(1.4f));
    }

    IEnumerator ServeRoutine(float delay)
    {
        SetState(State.Serving);
        _server = (_scores[0] + _scores[1]) / 2 % 2 == 0 ? Side.Player : Side.Cpu;
        Hud.SetServer(_server);
        Ball.Hide();
        Ball.SetPowerMode(false);
        _smashShot = false;
        PlayerRobot.ResetPose();
        CpuRobot.ResetPose();

        var server = _server == Side.Player ? PlayerRobot : CpuRobot;
        var receiver = _server == Side.Player ? CpuRobot : PlayerRobot;
        _servePosition = new Vector3(0.9f * -_server.Sign(), Court.HoverHeight, _server.Sign() * 7.2f);
        server.MoveTo(_servePosition, Time.time + 0.8f);
        receiver.MoveTo(receiver.HomePosition, Time.time + 0.8f);

        yield return new WaitForSeconds(delay);
        if (!_demo)
        {
            if (_server == Side.Player)
                Hud.ShowMessage("YOUR SERVE", new Color(1, 0.55f, 0.1f), 1.2f, "CLICK TO SERVE");
            else
                Hud.ShowMessage("CPU SERVE", new Color(0.3f, 0.6f, 1), 0.9f);
        }
        yield return new WaitForSeconds(1.0f);
        Toss();
    }

    void Toss()
    {
        var server = _server == Side.Player ? PlayerRobot : CpuRobot;
        var p = _servePosition + new Vector3(0.3f * -_server.Sign(), 0.5f, server.Forward.z * 0.2f);
        Ball.Launch(p, new Vector3(0, 6.4f, 0));
        _tossPending = false;

        var traj = Ball.Trajectory;
        var ideal = traj.TimeAtHeightDescending(2.4f);
        var point = traj.PositionAt(ideal);
        server.MoveTo(server.PositionForContact(point, SwingType.Serve), ideal - 0.2f);

        if (_server == Side.Player)
            Player.Expect(point, ideal, ideal - 0.5f, traj.TimeAtHeightDescending(0.8f), SwingType.Serve, Time.time + 0.15f);
        else
            Cpu.ScheduleServe(ideal);
    }

    IEnumerator RetossRoutine()
    {
        yield return new WaitForSeconds(0.6f);
        if (_state != State.Serving) yield break;
        if (!_demo && _server == Side.Player)
            Hud.ShowMessage("", new Color(1, 0.55f, 0.1f), 1.0f, "CLICK TO SERVE");
        Toss();
    }

    void ExpectPlayer()
    {
        var plan = ShotPlanner.PredictReceive(Ball.Trajectory, Side.Player);
        PlayerRobot.MoveTo(PlayerRobot.PositionForContact(plan.Point, SwingType.Forehand), plan.Time - 0.08f);
        var close = plan.SecondBounceTime - 0.02f;
        var show = Mathf.Max(Time.time + 0.1f, plan.Time - 1.1f);
        Player.Expect(plan.Point, plan.Time, plan.Time - 0.45f, close, SwingType.Forehand, show);
        if (!_demo) Marker.Show(plan.Bounce, plan.BounceTime);
    }

    Vector3 ChoosePlayerTarget(float q, bool serve)
    {
        var cpu = CpuRobot.transform.position;
        var dir = cpu.x > 0.3f ? -1 : cpu.x < -0.3f ? 1 : (Random.value < 0.5f ? -1 : 1);
        var hard = new Vector3(dir * 3.15f, 0, serve || Random.value < 0.75f ? 4.8f : 2.4f);
        var easy = new Vector3(Mathf.Clamp(cpu.x, -2, 2) + Random.Range(-0.5f, 0.5f), 0, 3.4f);
        var t = Vector3.Lerp(easy, hard, Mathf.Pow(q, 1.3f));
        var noise = Random.insideUnitCircle * (1 - q) * 0.7f;
        t.x = Mathf.Clamp(t.x + noise.x, -3.3f, 3.3f);
        t.z = Mathf.Clamp(t.z + noise.y, 1.6f, 5.0f);
        return t;
    }

    void LaunchShot(Side hitter, Vector3 from, Vector3 velocity, float power, bool smash)
    {
        Ball.Launch(from, velocity);
        Ball.SetPowerMode(smash);
        _lastHitter = hitter;
        _bounces = 0;
        _smashShot = smash;

        Audio.PlayHit(power);
        var spark = hitter == Side.Player ? Effects.HitSparkPlayer : Effects.HitSparkCpu;
        Effects.Spawn(spark, from, 0.7f + power * 0.6f);
        if (!smash && power >= 0.85f)
        {
            Effects.Spawn(Effects.PerfectBurst, from, 0.8f);
            TimeControl.Hitstop(0.06f);
            CameraDirector.Shake(0.25f);
        }
    }

    // Rules

    void OnBallBounce(Vector3 p, float speed)
    {
        Audio.PlayBounce(speed);
        if (speed > 4) Effects.Spawn(Effects.BounceRing, p + Vector3.up * 0.02f, Mathf.Clamp(speed / 12, 0.4f, 1.2f));

        if (_state == State.Serving)
        {
            if (_tossPending || !Player.IsExpecting) StartCoroutine(RetossRoutine());
            return;
        }
        if (_state != State.Rally) return;

        var side = Court.SideOf(p);
        if (_bounces == 0)
        {
            if (side == _lastHitter) { AwardPoint(_lastHitter.Opponent(), "FAULT"); return; }
            if (!Court.IsInBounds(p)) { AwardPoint(_lastHitter.Opponent(), "OUT"); return; }
            _bounces = 1;
            if (_smashShot)
            {
                Smash.OnSmashBounce(p);
                AwardPoint(Side.Player, "SMASH");
            }
        }
        else
        {
            AwardPoint(_lastHitter, _lastHitter == Side.Player ? "CPU MISSED" : "");
        }
    }

    void OnNetHit(Vector3 p)
    {
        Audio.PlayBounce(6);
        if (_state == State.Rally) AwardPoint(_lastHitter.Opponent(), "NET");
    }

    void OnWallHit(Vector3 p, float speed)
    {
        Audio.PlayBounce(speed);
        if (_state != State.Rally) return;
        AwardPoint(_bounces > 0 ? _lastHitter : _lastHitter.Opponent(), _bounces > 0 ? "" : "OUT");
    }

    void AwardPoint(Side winner, string reason)
    {
        if (_state != State.Rally) return;
        SetState(State.PointOver);
        Player.Cancel();
        Cpu.Cancel();
        Marker.Hide();
        if (_demo)
        {
            StartCoroutine(AfterPointRoutine());
            return;
        }
        _scores[(int)winner]++;
        Hud.SetScores(_scores[0], _scores[1]);

        if (winner == Side.Player)
        {
            if (reason != "SMASH")
                Hud.ShowMessage("POINT!", new Color(1, 0.5f, 0.1f), 1.3f, reason);
            Audio.Play(Audio.PointWin, 0.8f);
        }
        else
        {
            Hud.ShowMessage(reason == "" ? "MISSED!" : reason, new Color(0.25f, 0.55f, 1), 1.3f, "CPU SCORES");
            Audio.Play(Audio.PointLose, 0.8f);
        }
        StartCoroutine(AfterPointRoutine());
    }

    IEnumerator AfterPointRoutine()
    {
        yield return new WaitForSecondsRealtime(1.6f);
        while (Smash.IsActive) yield return null;
        PlayerRobot.MoveTo(PlayerRobot.HomePosition, Time.time + 0.6f);
        CpuRobot.MoveTo(CpuRobot.HomePosition, Time.time + 0.6f);
        yield return new WaitForSeconds(0.4f);

        if (!_demo && Mathf.Max(_scores[0], _scores[1]) >= WinScore)
        {
            SetState(State.MatchOver);
            var win = _scores[0] > _scores[1];
            Ball.Hide();
            Hud.ShowResult(true, win, _scores[0], _scores[1]);
            Audio.Play(win ? Audio.Jingle : Audio.PointLose, 1);
        }
        else
        {
            StartCoroutine(ServeRoutine(0.2f));
        }
    }
}

} // namespace SmashBots
