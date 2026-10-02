using UnityEngine;

namespace SmashBots {

// Pulsing floor ring that marks where the incoming ball will bounce
public sealed class LandingMarker : MonoBehaviour
{
    [field:SerializeField] public Renderer Renderer { get; set; }
    [field:SerializeField, ColorUsage(true, true)] public Color Color { get; set; } = new Color(2, 0.9f, 0.3f, 1);

    public void Show(Vector3 point, float bounceTime)
    {
        _bounceTime = bounceTime;
        _showTime = Time.time;
        transform.position = new Vector3(point.x, 0.015f, point.z);
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    void Awake()
    {
        _props = new MaterialPropertyBlock();
        gameObject.SetActive(false);
    }

    void Update()
    {
        var now = Time.time;
        if (now > _bounceTime + 0.15f) { Hide(); return; }
        var remain = Mathf.Max(0, _bounceTime - now);
        var fadeIn = Mathf.Clamp01((now - _showTime) / 0.15f);
        var s = 0.45f + remain * 0.5f + Mathf.Sin(now * 18) * 0.03f;
        transform.localScale = new Vector3(s, s, s);
        var c = Color;
        c.a *= fadeIn * (now > _bounceTime ? 1 - (now - _bounceTime) / 0.15f : 1);
        _props.SetColor("_Color", c);
        Renderer.SetPropertyBlock(_props);
    }

    MaterialPropertyBlock _props;
    float _bounceTime, _showTime;
}

} // namespace SmashBots
