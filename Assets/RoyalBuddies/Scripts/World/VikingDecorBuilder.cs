using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Habillage « viking » de l'arène, piloté par les données de RB_VikingLayout.json :
    /// modèles 3D Kenney (CC0), ruisseaux qui prolongent la rivière dans la zone jouable, cascades,
    /// lacs, fjord et feux. Purement décoratif : aucune collision, aucun effet sur le gameplay ni sur le NavMesh.
    /// Appelé par RBBuilder (Build All) : tout est enregistré dans les scènes.
    /// </summary>
    public static class VikingDecorBuilder
    {
        // ------------------------------------------------------------------ données (JsonUtility)

        [Serializable] public class Prop { public string m; public float x, y, z, yaw, h, s; public int shadow; public float spin; }
        [Serializable] public class Stream { public float[] pts; public float w; public float y; }
        [Serializable] public class Fall { public float x, z, w, dir, top, bottom, reach; }
        [Serializable] public class Lake { public float x, z, sx, sz, y, seed; }
        [Serializable] public class Layout
        {
            public Prop[] props;
            public Stream[] streams;
            public Fall[] falls;
            public Lake[] lakes;
            public float[] fires;     // triplets x, y, z
            public float[] exclude;   // quadruplets xmin, xmax, zmin, zmax (zones vidées dans la forêt de bordure)
        }

        public static Layout Parse(TextAsset json) => json == null ? null : JsonUtility.FromJson<Layout>(json.text);

        /// <summary>Vrai si (x, z) tombe dans une zone réservée au décor viking (lacs, village, fjord).</summary>
        public static bool IsExcluded(Layout lay, float x, float z)
        {
            if (lay == null || lay.exclude == null) return false;
            for (int i = 0; i + 3 < lay.exclude.Length; i += 4)
                if (x >= lay.exclude[i] && x <= lay.exclude[i + 1] && z >= lay.exclude[i + 2] && z <= lay.exclude[i + 3]) return true;
            return false;
        }

        // ------------------------------------------------------------------ construction

        public static GameObject Build(Transform parent, Layout lay, IList<GameObject> models, IList<string> names,
                                       Material flowWater, Material waterfall, Material lakeWater, Material particle, Transform arenaRoot = null)
        {
            var root = new GameObject("VikingEnvironment");
            if (parent != null) root.transform.SetParent(parent, false);
            if (lay == null) return root;

            var byName = new Dictionary<string, GameObject>();
            for (int i = 0; i < models.Count && i < names.Count; i++)
                if (models[i] != null) byName[names[i]] = models[i];

            var water = Group("Water", root.transform);
            var props = Group("Props", root.transform);
            var fx = Group("Fx", root.transform);

            if (lay.lakes != null)
                foreach (var l in lay.lakes) BuildLake(water, l, lakeWater != null ? lakeWater : flowWater);
            // Royal Buddies V3: decorative brooks removed by design; keep only exterior lakes/sea and waterfalls.
            // The central gameplay river belongs to RB_Arena and is intentionally untouched.
            if (lay.falls != null)
                foreach (var f in lay.falls) BuildFall(water, fx, f, waterfall != null ? waterfall : (flowWater != null ? flowWater : lakeWater), particle);

            int placed = 0, missing = 0;
            if (lay.props != null)
                foreach (var p in lay.props)
                {
                    if (string.IsNullOrEmpty(p.m) || !byName.TryGetValue(p.m, out var model)) { missing++; continue; }
                    if (PlaceProp(props, model, p) != null) placed++;
                }

            if (lay.fires != null)
                for (int i = 0; i + 2 < lay.fires.Length; i += 3)
                    BuildFire(fx, new Vector3(lay.fires[i], lay.fires[i + 1], lay.fires[i + 2]), particle);

            // Environment is strictly visual and exterior-only: never delete or modify arena objects.

            Debug.Log("[RoyalBuddies] Décor viking : " + placed + " éléments 3D placés" + (missing > 0 ? (", " + missing + " modèles manquants") : "") + ".");
            return root;
        }

        static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }

        // ------------------------------------------------------------------ modèles

        /// <summary>
        /// h > 0 : mise à l'échelle sur la hauteur h, centré en (x, z), base posée en y.
        /// sinon : pose « modulaire » au pivot du modèle, échelle s (maisons, palissades).
        /// </summary>
        static GameObject PlaceProp(Transform parent, GameObject model, Prop p)
        {
            var go = UnityEngine.Object.Instantiate(model, parent, false);
            go.name = p.m;
            var t = go.transform;
            t.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
            t.localScale = Vector3.one;
            t.localPosition = Vector3.zero;

            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Kill(col);
            var rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                r.shadowCastingMode = p.shadow != 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
            }

            if (p.h > 0f && rends.Length > 0)
            {
                Bounds b0 = GetBounds(rends);
                float s = b0.size.y > 1e-4f ? p.h / b0.size.y : 1f;
                t.localScale = Vector3.one * s;
                Bounds b1 = GetBounds(rends);
                Vector3 target = parent.TransformPoint(new Vector3(p.x, p.y, p.z));
                t.position += new Vector3(target.x - b1.center.x, target.y - b1.min.y, target.z - b1.center.z);
            }
            else
            {
                t.localScale = Vector3.one * (p.s > 0f ? p.s : 1f);
                t.localPosition = new Vector3(p.x, p.y, p.z);
            }

            if (Mathf.Abs(p.spin) > 0.01f)
            {
                // Roue à aubes : on fait tourner le modèle autour de son axe (X local) via un pivot qui garde le cadrage.
                var spin = go.AddComponent<Spin>();
                spin.degreesPerSecond = p.spin;
                spin.localAxis = Vector3.right;
            }
            return go;
        }

        static Bounds GetBounds(Renderer[] rends)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        // ------------------------------------------------------------------ eau

        static MeshRenderer MeshObject(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        /// <summary>Lac / fjord : contour organique (super-ellipse bruitée) + rive boueuse un peu plus large.</summary>
        static void BuildLake(Transform parent, Lake l, Material mat)
        {
            MeshObject(parent, "LakeShore", Blob(l, .7f, l.y - .03f, false), PrimitiveFactory.ColorMaterial(RB.HexColor("#5b5040")));
            MeshObject(parent, "Lake", Blob(l, 0f, l.y, true), mat);
        }

        static Mesh Blob(Lake l, float grow, float y, bool uvs)
        {
            const int n = 48;
            var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var tri = new int[n * 3];
            v[0] = new Vector3(l.x, y, l.z); uv[0] = new Vector2(l.x / 6f, l.z / 6f);
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.PI * 2f * i / n, c = Mathf.Cos(t), s = Mathf.Sin(t);
                float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(c), 4f) + Mathf.Pow(Mathf.Abs(s), 4f), -.25f);
                r *= 1f + .06f * Mathf.Sin(3f * t + l.seed) + .04f * Mathf.Sin(5f * t + 2f * l.seed);
                float x = l.x + c * r * (l.sx * .5f + grow), z = l.z + s * r * (l.sz * .5f + grow);
                v[i + 1] = new Vector3(x, y, z); uv[i + 1] = new Vector2(x / 6f, z / 6f);
                tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % n; tri[i * 3 + 2] = 1 + i;
            }
            var m = new Mesh { name = uvs ? "RB_Lake" : "RB_LakeShore", vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Ruisseau : ruban lissé (Catmull-Rom) le long de la polyligne, berge boueuse et galets.</summary>
        static void BuildStream(Transform parent, Stream s, Material mat, int index)
        {
            if (s.pts == null || s.pts.Length < 4) return;
            var ctrl = new List<Vector2>();
            for (int i = 0; i + 1 < s.pts.Length; i += 2) ctrl.Add(new Vector2(s.pts[i], s.pts[i + 1]));
            var path = Smooth(ctrl, 6);

            var g = Group("Brook_" + index, parent);
            MeshObject(g, "Water", Ribbon(path, s.w, s.y, true), mat);
            MeshObject(g, "Bank", Ribbon(path, s.w + .34f, s.y - .012f, false), PrimitiveFactory.ColorMaterial(RB.HexColor("#4f4130")));

            // Galets le long des berges (déterministes)
            var rng = new System.Random(4242 + index);
            Color[] stones = { RB.HexColor("#7d8580"), RB.HexColor("#8f948a"), RB.HexColor("#6c7470") };
            for (int i = 2; i < path.Count - 2; i += 3)
            {
                Vector2 d = (path[i + 1] - path[i - 1]).normalized;
                Vector2 n = new Vector2(d.y, -d.x);
                float side = rng.NextDouble() < .5 ? -1f : 1f;
                Vector2 p = path[i] + n * side * (s.w * .5f + .12f);
                var st = PrimitiveFactory.Sphere(new Vector3(p.x, s.y + .02f, p.y), .07f + (float)rng.NextDouble() * .08f, stones[i % 3], g, "Pebble");
                st.transform.localScale = Vector3.Scale(st.transform.localScale, new Vector3(1.3f, .55f, 1f));
                var r = st.GetComponent<Renderer>(); if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        static List<Vector2> Smooth(List<Vector2> c, int sub)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < c.Count - 1; i++)
            {
                Vector2 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Count - 1)];
                for (int k = 0; k < sub; k++)
                {
                    float t = k / (float)sub, t2 = t * t, t3 = t2 * t;
                    o.Add(.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            o.Add(c[c.Count - 1]);
            return o;
        }

        static Mesh Ribbon(List<Vector2> path, float width, float y, bool flowUv)
        {
            int n = path.Count;
            var v = new Vector3[n * 2]; var uv = new Vector2[n * 2]; var tri = new int[(n - 1) * 6];
            float len = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) len += Vector2.Distance(path[i], path[i - 1]);
                Vector2 d = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 nn = new Vector2(d.y, -d.x) * (width * .5f);
                // Légère variation de largeur pour un tracé naturel
                float wob = 1f + .12f * Mathf.Sin(len * 2.1f);
                v[i * 2] = new Vector3(path[i].x + nn.x * wob, y, path[i].y + nn.y * wob);
                v[i * 2 + 1] = new Vector3(path[i].x - nn.x * wob, y, path[i].y - nn.y * wob);
                uv[i * 2] = new Vector2(0f, flowUv ? len : 0f);
                uv[i * 2 + 1] = new Vector2(1f, flowUv ? len : 0f);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2;
                // sens horaire vu du dessus (face avant Unity)
                tri[i * 6] = a; tri[i * 6 + 1] = a + 1; tri[i * 6 + 2] = a + 2;
                tri[i * 6 + 3] = a + 1; tri[i * 6 + 4] = a + 3; tri[i * 6 + 5] = a + 2;
            }
            var m = new Mesh { name = "RB_Ribbon", vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Cascade : nappe courbe qui tombe du bord du plateau (x) vers l'extérieur, plus brume d'écume en bas.</summary>
        static void BuildFall(Transform parent, Transform fxParent, Fall f, Material mat, Material particle)
        {
            const int seg = 8;
            var v = new Vector3[(seg + 1) * 2]; var uv = new Vector2[(seg + 1) * 2]; var tri = new int[seg * 6];
            float hw = f.w * .5f, len = 0f; Vector3 prev = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float x = f.x + f.dir * f.reach * Mathf.Sqrt(t);        // jaillit puis tombe
                float y = Mathf.Lerp(f.top, f.bottom, t * t);
                var c = new Vector3(x, y, f.z);
                if (i > 0) len += Vector3.Distance(c, prev);
                prev = c;
                v[i * 2] = new Vector3(x, y, f.z - hw); v[i * 2 + 1] = new Vector3(x, y, f.z + hw);
                uv[i * 2] = new Vector2(0, len); uv[i * 2 + 1] = new Vector2(1, len);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                tri[i * 6] = a; tri[i * 6 + 1] = a + 1; tri[i * 6 + 2] = a + 2;
                tri[i * 6 + 3] = a + 1; tri[i * 6 + 4] = a + 3; tri[i * 6 + 5] = a + 2;
            }
            var m = new Mesh { name = "RB_Waterfall", vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            MeshObject(parent, "Waterfall", m, mat);

            // Brume / écume au pied de la cascade
            var go = new GameObject("WaterfallMist");
            go.transform.SetParent(fxParent, false);
            go.transform.position = new Vector3(f.x + f.dir * f.reach, f.bottom + .05f, f.z);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, .9f); main.startSize = new ParticleSystem.MinMaxCurve(.25f, .55f);
            main.startColor = new Color(.92f, .98f, 1f, .55f); main.gravityModifier = .15f;
            main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 10f + f.w * 6f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(.3f, f.w, .05f);
            sh.rotation = new Vector3(-90f, 0, 0);
            FadeOut(ps);
            SetupRenderer(go, particle);
        }

        // ------------------------------------------------------------------ feux

        static void BuildFire(Transform parent, Vector3 pos, Material particle)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 1.2f); main.startSize = new ParticleSystem.MinMaxCurve(.22f, .42f);
            main.startColor = new ParticleSystem.MinMaxGradient(RB.HexColor("#ffcf5a"), RB.HexColor("#ff7a2a"));
            main.maxParticles = 50; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 26f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = .14f;
            sh.rotation = new Vector3(-90f, 0, 0);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, .15f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(RB.HexColor("#fff0a0"), 0), new GradientColorKey(RB.HexColor("#ff8a30"), .45f), new GradientColorKey(RB.HexColor("#b8321e"), 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            SetupRenderer(go, particle);
        }

        static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1) });
            col.color = g;
        }

        static void SetupRenderer(GameObject go, Material particle)
        {
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            if (particle != null) rend.sharedMaterial = particle;
        }

        // ------------------------------------------------------------------ nettoyage

        /// <summary>Retire les petits éléments existants de l'arène (cailloux, brins, bordures, rochers) qui chevauchent les ruisseaux.</summary>
        static void ClearArenaClutter(Transform arenaRoot, Layout lay)
        {
            var kill = new List<GameObject>();
            foreach (var tr in arenaRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name;
                if (n != "Pebble" && n != "Blade" && n != "BorderStone" && n != "Rock" && n != "Bush") continue;
                if (tr.GetComponentInParent<Tower>() != null) continue;
                Vector3 p = tr.position;
                foreach (var s in lay.streams)
                {
                    if (NearPolyline(s, p.x, p.z, s.w * .5f + .45f)) { kill.Add(tr.gameObject); break; }
                }
            }
            foreach (var g in kill) Kill(g);
        }

        static bool NearPolyline(Stream s, float x, float z, float r)
        {
            var q = new Vector2(x, z);
            for (int i = 0; i + 3 < s.pts.Length; i += 2)
            {
                var a = new Vector2(s.pts[i], s.pts[i + 1]); var b = new Vector2(s.pts[i + 2], s.pts[i + 3]);
                Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                if (Vector2.Distance(q, a + ab * t) < r) return true;
            }
            return false;
        }
    }
}
