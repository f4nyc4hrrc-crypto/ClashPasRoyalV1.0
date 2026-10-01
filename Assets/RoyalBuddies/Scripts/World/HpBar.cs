using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Barre de vie 3D (hpbar / set_hpbar de main.gd) : fond sombre + remplissage vert.
    /// Le remplissage est légèrement en avant du fond pour rester visible depuis la caméra.
    /// </summary>
    public class HpBar : MonoBehaviour
    {
        public Transform fill;
        public float fillWidth = 1.39f;

        /// <summary>Crée la barre (hpbar(parent, pos, w) de main.gd). Les matériaux sont fournis par PrimitiveFactory.</summary>
        public static HpBar Create(Transform parent, Vector3 localPos, float width)
        {
            var root = new GameObject("HpBar");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            var bar = root.AddComponent<HpBar>();
            NoShadow(PrimitiveFactory.Box(Vector3.zero, new Vector3(width, 0.16f, 0.14f), RB.HexColor("#171b25"), root.transform, "Back"));
            bar.fillWidth = width * 0.94f;
            bar.fill = NoShadow(PrimitiveFactory.Box(new Vector3(0, 0.03f, -0.06f), new Vector3(bar.fillWidth, 0.12f, 0.1f), RB.HexColor("#58e26b"), root.transform, "Fill")).transform;
            return bar;
        }

        static GameObject NoShadow(GameObject go)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public void SetRatio(float ratio)
        {
            if (fill == null) return;
            float r = Mathf.Clamp01(ratio);
            var s = fill.localScale; s.x = Mathf.Max(0.001f, r) * fillWidth;
            fill.localScale = s;
            var p = fill.localPosition; p.x = -(fillWidth * (1f - r)) / 2f;
            fill.localPosition = p;
        }
    }
}
