using UnityEngine;
using UnityEngine.UIElements;

namespace SmashBots {

public sealed class HudController : MonoBehaviour
{
    // Public properties

    [field:SerializeField] public UIDocument Document { get; set; }

    // Screen / overlay control

    public void ShowTitle(bool on)
    {
        _titleScreen.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        _hud.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;
    }

    public void ShowResult(bool on, bool win = false, int player = 0, int cpu = 0)
    {
        _resultScreen.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        if (!on) return;
        _resultLabel.text = win ? "YOU WIN!" : "YOU LOSE...";
        _resultLabel.style.color = win ? new Color(1, 0.75f, 0.3f) : new Color(0.6f, 0.8f, 1);
        _resultScore.text = $"{player}  -  {cpu}";
        _resultTime = Time.unscaledTime;
    }

    public void SetScores(int player, int cpu)
    {
        if (_playerScore.text != player.ToString()) _playerPunch = 1;
        if (_cpuScore.text != cpu.ToString()) _cpuPunch = 1;
        _playerScore.text = player.ToString();
        _cpuScore.text = cpu.ToString();
    }

    public void SetServer(Side side)
    {
        _playerServe.style.opacity = side == Side.Player ? 1 : 0;
        _cpuServe.style.opacity = side == Side.Cpu ? 1 : 0;
    }

    public void ShowMessage(string text, Color glow, float duration = 1.4f, string sub = null)
    {
        _message.text = text;
        _message.style.textShadow = new TextShadow { color = glow, blurRadius = 22 };
        _messageTime = Time.unscaledTime;
        _messageDuration = duration;
        _subMessage.text = sub ?? "";
    }

    public void Flash(float intensity) => _flash = Mathf.Max(_flash, intensity);

    public void SetLetterbox(bool on) => _letterboxTarget = on ? 1 : 0;

    public void SetSpeedLines(float amount) => _speedLinesTarget = amount;

    // Hit indicator

    public void ShowHitIndicator(Vector3 screenPos, float approach, Color tint)
    {
        var p = ToPanel(screenPos);
        _hitTarget.style.display = DisplayStyle.Flex;
        _hitApproach.style.display = DisplayStyle.Flex;
        SetPosition(_hitTarget, p);
        SetPosition(_hitApproach, p);
        var s = 1 + approach * 2.4f;
        _hitApproach.style.scale = new Scale(new Vector3(s, s, 1));
        _hitApproach.style.opacity = Mathf.Clamp01(1.2f - approach * 0.5f);
        _hitApproach.style.unityBackgroundImageTintColor = tint;
        _hitTarget.style.opacity = 0.55f + 0.45f * (1 - approach);
    }

    public void HideHitIndicator()
    {
        _hitTarget.style.display = DisplayStyle.None;
        _hitApproach.style.display = DisplayStyle.None;
    }

    public void ShowRating(string text, Vector3 screenPos, Color color)
    {
        _rating.text = text;
        _rating.style.color = color;
        _rating.style.textShadow = new TextShadow { color = color * 0.9f, blurRadius = 14 };
        _ratingPos = ToPanel(screenPos);
        _ratingTime = Time.unscaledTime;
    }

    // Smash sequence

    public void ShowSmashChance() => _smashChanceTime = Time.unscaledTime;

    public void ShowQte(bool on)
    {
        _qte.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        _qteLabel.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        if (on) _smashChanceTime = Mathf.Min(_smashChanceTime, Time.unscaledTime - 1.3f);
        _qteNow.style.opacity = 0;
        _qteLockTime = -1;
    }

    // progress: 0 (start) -> 1 (converged)
    public void UpdateQte(Vector3 screenPos, float progress)
    {
        SetPosition(_qte, ToPanel(screenPos));
        var p = Mathf.Clamp01(progress);
        var r = Mathf.Lerp(330, 62, p * p * (3 - 2 * p));
        var spin = (1 - p) * 140;
        for (var i = 0; i < 4; i++)
        {
            var a = (i * 90 + spin) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            _qteChev[i].style.left = dir.x * r;
            _qteChev[i].style.top = dir.y * r;
            // Chevrons point inward
            _qteChev[i].style.rotate = new Rotate(i * 90 + spin + 180);
        }
        var ringScale = Mathf.Lerp(1.6f, 0.85f, p);
        _qteRing.style.scale = new Scale(new Vector3(ringScale, ringScale, 1));
        _qteRing.style.rotate = new Rotate(-spin * 0.5f);
        var locked = progress >= 1;
        var tint = locked ? new Color(1, 0.75f, 0.35f) : Color.white;
        _qteRing.style.unityBackgroundImageTintColor = tint;
        foreach (var c in _qteChev) c.style.unityBackgroundImageTintColor = tint;
        if (locked && _qteLockTime < 0) _qteLockTime = Time.unscaledTime;
    }

    public void ShowSmashTitle() => _smashTitleTime = Time.unscaledTime;

    // MonoBehaviour implementation

    void OnEnable()
    {
        var root = Document.rootVisualElement;
        _hud = root.Q("hud");
        _playerScore = root.Q<Label>("player-score");
        _cpuScore = root.Q<Label>("cpu-score");
        _playerServe = root.Q<Label>("player-serve");
        _cpuServe = root.Q<Label>("cpu-serve");
        _message = root.Q<Label>("message");
        _subMessage = root.Q<Label>("sub-message");
        _hitTarget = root.Q("hit-target");
        _hitApproach = root.Q("hit-approach");
        _rating = root.Q<Label>("rating");
        _qte = root.Q("qte");
        _qteRing = root.Q("qte-ring");
        _qteGlow = root.Q("qte-glow");
        _qteNow = root.Q<Label>("qte-now");
        _qteLabel = root.Q<Label>("qte-label");
        for (var i = 0; i < 4; i++) _qteChev[i] = root.Q("qte-chev-" + i);
        _smashChance = root.Q<Label>("smash-chance");
        _smashTitle = root.Q("smash-title");
        _flashElement = root.Q("flash");
        _letterboxTop = root.Q("letterbox-top");
        _letterboxBottom = root.Q("letterbox-bottom");
        _speedLines = root.Q("speedlines");
        _titleScreen = root.Q("title-screen");
        _startLabel = root.Q<Label>("start-label");
        _resultScreen = root.Q("result-screen");
        _resultLabel = root.Q<Label>("result-label");
        _resultScore = root.Q<Label>("result-score");
        _continueLabel = root.Q<Label>("continue-label");
    }

    void Update()
    {
        var now = Time.unscaledTime;
        var dt = Time.unscaledDeltaTime;

        // Blinking start labels
        var blink = 0.55f + 0.45f * Mathf.Sin(now * 4);
        _startLabel.style.opacity = blink;
        _continueLabel.style.opacity = now - _resultTime > 1.2f ? blink : 0;

        // Score punch
        _playerPunch = Mathf.Max(0, _playerPunch - dt * 2.5f);
        _cpuPunch = Mathf.Max(0, _cpuPunch - dt * 2.5f);
        var ps = 1 + Mathf.Sin(_playerPunch * Mathf.PI) * 0.6f;
        var cs = 1 + Mathf.Sin(_cpuPunch * Mathf.PI) * 0.6f;
        _playerScore.style.scale = new Scale(new Vector3(ps, ps, 1));
        _cpuScore.style.scale = new Scale(new Vector3(cs, cs, 1));

        // Message: pop in, hold, fade out
        var mt = now - _messageTime;
        var mo = mt < _messageDuration ? Mathf.Clamp01(mt / 0.08f) : Mathf.Clamp01(1 - (mt - _messageDuration) / 0.3f);
        var mscale = mt < 0.25f ? Mathf.Lerp(1.8f, 1, EaseOutBack(mt / 0.25f)) : 1;
        _message.style.opacity = mo;
        _message.style.scale = new Scale(new Vector3(mscale, mscale, 1));
        _subMessage.style.opacity = mo;

        // Rating popup: rise and fade
        var rt = now - _ratingTime;
        _rating.style.opacity = Mathf.Clamp01(1 - (rt - 0.45f) / 0.35f) * Mathf.Clamp01(rt / 0.05f);
        var rs = rt < 0.2f ? Mathf.Lerp(1.7f, 1, EaseOutBack(rt / 0.2f)) : 1;
        _rating.style.scale = new Scale(new Vector3(rs, rs, 1));
        SetPosition(_rating, _ratingPos + new Vector2(0, -90 - rt * 60));

        // Smash chance banner
        var sc = now - _smashChanceTime;
        _smashChance.style.opacity = sc < 1.3f ? Mathf.Clamp01(sc / 0.1f) * (0.75f + 0.25f * Mathf.Sin(sc * 30)) : Mathf.Clamp01(1 - (sc - 1.3f) / 0.3f);
        var scx = sc < 0.3f ? Mathf.Lerp(-900, 0, EaseOutBack(sc / 0.3f)) : sc > 1.3f ? (sc - 1.3f) * 2000 : 0;
        _smashChance.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
        _smashChance.style.left = new Length(50, LengthUnit.Percent);
        _smashChance.style.marginLeft = scx;

        // QTE lock-on flash
        if (_qteLockTime >= 0)
        {
            var lt = now - _qteLockTime;
            _qteNow.style.opacity = 0.7f + 0.3f * Mathf.Sin(lt * 40);
            var ns = lt < 0.15f ? Mathf.Lerp(2, 1, lt / 0.15f) : 1;
            _qteNow.style.scale = new Scale(new Vector3(ns, ns, 1));
            _qteGlow.style.opacity = Mathf.Clamp01(1 - lt * 2) * 0.9f;
            var gs = 0.6f + lt * 2;
            _qteGlow.style.scale = new Scale(new Vector3(gs, gs, 1));
        }
        else
        {
            _qteGlow.style.opacity = 0;
        }
        _qteLabel.style.opacity = 0.75f + 0.25f * Mathf.Sin(now * 10);

        // SMASH! title slam
        var st = now - _smashTitleTime;
        var so = st < 1.5f ? 1 : Mathf.Clamp01(1 - (st - 1.5f) / 0.4f);
        var ss = st < 0.18f ? Mathf.Lerp(3.2f, 1, EaseOutBack(st / 0.18f)) : 1 + (st - 0.18f) * 0.05f;
        _smashTitle.style.opacity = st < 0 ? 0 : so;
        _smashTitle.style.scale = new Scale(new Vector3(ss, ss, 1));
        _smashTitle.style.rotate = new Rotate(st < 0.18f ? Mathf.Lerp(-12, -4, st / 0.18f) : -4);

        // Flash
        _flash = Mathf.Max(0, _flash - dt * 3.5f);
        _flashElement.style.opacity = _flash;

        // Letterbox
        _letterbox = Mathf.MoveTowards(_letterbox, _letterboxTarget, dt * 4);
        var lb = new Length(_letterbox * _letterbox * (3 - 2 * _letterbox) * 11, LengthUnit.Percent);
        _letterboxTop.style.height = lb;
        _letterboxBottom.style.height = lb;

        // Speed lines with flicker
        _speedLinesAmount = Mathf.Lerp(_speedLinesAmount, _speedLinesTarget, 1 - Mathf.Exp(-10 * dt));
        _speedLines.style.opacity = _speedLinesAmount * (0.8f + 0.2f * Mathf.PerlinNoise(now * 20, 0));
        var sls = 1 + 0.03f * Mathf.Sin(now * 37);
        _speedLines.style.scale = new Scale(new Vector3(sls, sls, 1));
    }

    // Private members

    VisualElement _hud, _hitTarget, _hitApproach, _qte, _qteRing, _qteGlow, _smashTitle;
    VisualElement _flashElement, _letterboxTop, _letterboxBottom, _speedLines, _titleScreen, _resultScreen;
    readonly VisualElement[] _qteChev = new VisualElement[4];
    Label _playerScore, _cpuScore, _playerServe, _cpuServe, _message, _subMessage, _rating, _qteNow, _qteLabel;
    Label _smashChance, _startLabel, _resultLabel, _resultScore, _continueLabel;

    float _messageTime = -10, _messageDuration, _ratingTime = -10, _smashChanceTime = -10, _smashTitleTime = -10;
    float _qteLockTime = -1, _resultTime, _flash, _letterbox, _letterboxTarget;
    float _speedLinesAmount, _speedLinesTarget, _playerPunch, _cpuPunch;
    Vector2 _ratingPos;

    Vector2 ToPanel(Vector3 screenPos)
    {
        var panel = Document.rootVisualElement.panel;
        if (panel == null) return Vector2.zero;
        return RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
    }

    static void SetPosition(VisualElement e, Vector2 p)
    {
        e.style.left = p.x;
        e.style.top = p.y;
    }

    static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f, c3 = c1 + 1;
        return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
    }
}

} // namespace SmashBots
