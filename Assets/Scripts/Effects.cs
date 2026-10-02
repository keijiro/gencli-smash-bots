using UnityEngine;

namespace SmashBots {

public sealed class Effects : MonoBehaviour
{
    [field:SerializeField] public GameObject HitSparkPlayer { get; set; }
    [field:SerializeField] public GameObject HitSparkCpu { get; set; }
    [field:SerializeField] public GameObject PerfectBurst { get; set; }
    [field:SerializeField] public GameObject SmashBurst { get; set; }
    [field:SerializeField] public GameObject BounceRing { get; set; }
    [field:SerializeField] public GameObject GroundExplosion { get; set; }

    public void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, position, rotation);
        go.transform.localScale = Vector3.one * scale;
        Destroy(go, 4);
    }

    public void Spawn(GameObject prefab, Vector3 position, float scale = 1)
      => Spawn(prefab, position, Quaternion.identity, scale);
}

} // namespace SmashBots
