using UnityEngine;

namespace SmashBots {

// Soft contact shadow that stays on the floor under a target
public sealed class BlobShadow : MonoBehaviour
{
    [field:SerializeField] public Transform Target { get; set; }
    [field:SerializeField] public float Size { get; set; } = 1.2f;
    [field:SerializeField] public float Opacity { get; set; } = 0.45f;

    void Start()
    {
        _props = new MaterialPropertyBlock();
        _renderer = GetComponent<Renderer>();
    }

    void LateUpdate()
    {
        var p = Target.position;
        transform.position = new Vector3(p.x, 0.011f, p.z);
        var h = Mathf.Max(0, p.y);
        transform.localScale = Vector3.one * (Size * (1 + h * 0.15f));
        _props.SetColor("_Color", new Color(0, 0, 0, Opacity / (1 + h * 0.6f)));
        _renderer.SetPropertyBlock(_props);
    }

    MaterialPropertyBlock _props;
    Renderer _renderer;
}

} // namespace SmashBots
