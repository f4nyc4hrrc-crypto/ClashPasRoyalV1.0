using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Effets visuels légers (mobile) : particules groupées dans UN seul ParticleSystem, textes de dégâts en pool.
    /// Équivalents de particles(), floating_damage() et des effets de sorts de main.gd.
    /// </summary>
    public class FxManager : MonoBehaviour
    {
        public static FxManager I { get; private set; }

        ParticleSystem sparks;
        ParticleSystem.EmitParams ep;
        readonly Queue<FloatingText> textPool = new Queue<FloatingText>();
        Font uiFont;

        public static FxManager Ensure()
        {
            if (I != null) return I;
            var go = new GameObject("RB_FxManager");
            I = go.AddComponent<FxManager>();
            return I;
        }

        void Awake()
        {
            I = this;
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildSparks();
        }

        void OnDestroy() { if (I == this) I = null; }

        void BuildSparks()
        {
            var go = new GameObject("RB_Sparks");
            go.transform.SetParent(transform, false);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = 0.15f;
            var emission = sparks.emission; emission.enabled = false;
            var shape = sparks.shape; shape.enabled = false;
            var sol = sparks.sizeOverLifetime;
            sol.enabled = true;
            // taille pleine jusqu'à 60 % de la vie, puis réduction (tween d'échelle de Godot : .2 s puis .12 s)
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0.05f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var assets = RBGameAssets.Current;
            if (assets != null && assets.particleMaterial != null) r.sharedMaterial = assets.particleMaterial;
            sparks.Play();
        }

        /// <summary>particles(pos, couleur, n) de main.gd : n petites boules qui jaillissent puis rétrécissent.</summary>
        public void Burst(Vector3 pos, Color color, int count = 6)
        {
            if (sparks == null) return;
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + new Vector3(Random.Range(-.18f, .18f), Random.Range(.4f, 1.3f), Random.Range(-.18f, .18f));
                Vector3 offset = new Vector3(Random.Range(-.75f, .75f), Random.Range(.2f, .85f), Random.Range(-.75f, .75f));
                ep.velocity = offset / 0.3f;
                ep.startColor = color;
                ep.startSize = 0.15f;
                ep.startLifetime = 0.32f;
                sparks.Emit(ep, 1);
            }
        }

        /// <summary>Poussière de pas (Animation Event OnFootstep) : 2 petites particules, très bon marché.</summary>
        public void Dust(Vector3 pos)
        {
            if (sparks == null) return;
            for (int i = 0; i < 2; i++)
            {
                ep.position = pos + new Vector3(Random.Range(-.15f, .15f), .05f, Random.Range(-.15f, .15f));
                ep.velocity = new Vector3(Random.Range(-.5f, .5f), Random.Range(.2f, .6f), Random.Range(-.5f, .5f));
                ep.startColor = new Color(0.78f, 0.72f, 0.6f, 0.7f);
                ep.startSize = Random.Range(.16f, .26f);
                ep.startLifetime = 0.4f;
                sparks.Emit(ep, 1);
            }
        }

        /// <summary>Gerbe de poussière/impact au sol (Animation Event OnFistImpact / impact au sol).</summary>
        public void GroundImpact(Vector3 pos, Color color)
        {
            if (sparks == null) return;
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                ep.position = pos + new Vector3(Mathf.Cos(a) * .3f, .08f, Mathf.Sin(a) * .3f);
                ep.velocity = new Vector3(Mathf.Cos(a), .5f, Mathf.Sin(a)) * 1.6f;
                ep.startColor = color;
                ep.startSize = .2f;
                ep.startLifetime = .35f;
                sparks.Emit(ep, 1);
            }
        }

        /// <summary>Traînée d'un projectile : petite sphère claire qui rétrécit en .16 s.</summary>
        public void TrailPoint(Vector3 pos)
        {
            if (sparks == null) return;
            ep.position = pos;
            ep.velocity = Vector3.zero;
            ep.startColor = new Color32(0xd9, 0xf3, 0xff, 0xff);
            ep.startSize = 0.11f;
            ep.startLifetime = 0.16f;
            sparks.Emit(ep, 1);
        }

        /// <summary>floating_damage() : "-123" qui monte de 1.2 m en .45 s en s'estompant.</summary>
        public void FloatingDamage(Vector3 pos, float amount)
        {
            FloatingText ft = textPool.Count > 0 ? textPool.Dequeue() : null;
            if (ft == null)
            {
                var go = new GameObject("RB_FloatingText");
                go.transform.SetParent(transform, false);
                ft = go.AddComponent<FloatingText>();
                ft.Setup(uiFont, this);
            }
            ft.Show(pos + new Vector3(0, 2.3f, 0), "-" + Mathf.FloorToInt(amount));
        }

        public void Recycle(FloatingText t) { textPool.Enqueue(t); }

        // ------------------------------------------------------------ primitives temporaires (sorts)

        public GameObject TempPrimitive(PrimitiveType type, Vector3 pos, Vector3 scale, Color color, bool unlit = true)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var assets = RBGameAssets.Current;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (assets != null) mr.sharedMaterial = unlit ? assets.UnlitColorMaterial(color) : assets.ColorMaterial(color);
            return go;
        }

        public Coroutine Run(IEnumerator routine) { return StartCoroutine(routine); }

        public static IEnumerator ScaleTo(Transform t, Vector3 to, float duration, AnimationCurve ease = null)
        {
            Vector3 from = t.localScale;
            for (float e = 0; e < duration; e += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = Mathf.Clamp01(e / duration);
                if (ease != null) k = ease.Evaluate(k);
                t.localScale = Vector3.LerpUnclamped(from, to, k);
                yield return null;
            }
            if (t != null) t.localScale = to;
        }
    }

    /// <summary>Texte de dégâts flottant (TextMesh), orienté face caméra.</summary>
    public class FloatingText : MonoBehaviour
    {
        TextMesh tm;
        FxManager owner;
        float t;
        Vector3 start;

        public void Setup(Font font, FxManager fx)
        {
            owner = fx;
            tm = gameObject.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color32(0xff, 0xf1, 0xa8, 0xff);
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gameObject.SetActive(false);
        }

        public void Show(Vector3 pos, string text)
        {
            start = pos; t = 0f; tm.text = text;
            transform.position = pos;
            gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.45f);
            transform.position = start + new Vector3(0, 1.2f * k, 0);
            var c = tm.color; c.a = 1f - k; tm.color = c;
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            if (t >= 0.45f)
            {
                gameObject.SetActive(false);
                owner.Recycle(this);
            }
        }
    }
}
