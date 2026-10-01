using System.Collections;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Les 4 sorts : BOULE DE FEU, PLUIE DE FLÈCHES, ZAP, GEL. Port de cast_spell(), fireball_impact_fx(),
    /// spell_aoe(), stun_area() et freeze_area() de main.gd (mêmes dégâts, rayons et durées).
    /// </summary>
    public static class SpellSystem
    {
        public static void Cast(SpellKind kind, Vector3 pos, Team source)
        {
            var gm = GameManager.I;
            switch (kind)
            {
                case SpellKind.Fireball: gm.StartCoroutine(Fireball(pos, source)); break;
                case SpellKind.ArrowRain: gm.StartCoroutine(ArrowRain(pos, source)); break;
                case SpellKind.Zap: Zap(pos, source); break;
                case SpellKind.Freeze: Freeze(pos, source); break;
            }
        }

        // ------------------------------------------------------------------ BOULE DE FEU

        /// <summary>Origine aléatoire parmi 8 zones autour de l'arène (valeurs Godot ; Z inversé pour Unity).</summary>
        static Vector3 FireballSource()
        {
            int side = Random.Range(0, 8);
            Vector3 s;
            switch (side)
            {
                case 0: s = new Vector3(-Random.Range(15.5f, 20.0f), Random.Range(6.5f, 11.5f), Random.Range(-16.0f, 16.0f)); break;
                case 1: s = new Vector3(Random.Range(15.5f, 20.0f), Random.Range(6.5f, 11.5f), Random.Range(-16.0f, 16.0f)); break;
                case 2: s = new Vector3(Random.Range(-12.0f, 12.0f), Random.Range(7.0f, 12.5f), -Random.Range(18.0f, 23.0f)); break;
                case 3: s = new Vector3(Random.Range(-12.0f, 12.0f), Random.Range(7.0f, 12.5f), Random.Range(18.0f, 23.0f)); break;
                case 4: s = new Vector3(-Random.Range(15.0f, 20.0f), Random.Range(8.0f, 13.0f), -Random.Range(16.0f, 21.0f)); break;
                case 5: s = new Vector3(Random.Range(15.0f, 20.0f), Random.Range(8.0f, 13.0f), -Random.Range(16.0f, 21.0f)); break;
                case 6: s = new Vector3(-Random.Range(15.0f, 20.0f), Random.Range(8.0f, 13.0f), Random.Range(16.0f, 21.0f)); break;
                default: s = new Vector3(Random.Range(15.0f, 20.0f), Random.Range(8.0f, 13.0f), Random.Range(16.0f, 21.0f)); break;
            }
            return s;   // les zones sont symétriques en Z : inverser le signe de Z ne change pas la distribution
        }

        static IEnumerator Fireball(Vector3 pos, Team source)
        {
            var gm = GameManager.I;
            gm.Announce("BOULE DE FEU !");
            var assets = RBGameAssets.Current;
            GameObject meteor = null;
            Vector3 start = FireballSource();
            if (assets != null && assets.fireballPrefab != null)
            {
                meteor = Object.Instantiate(assets.fireballPrefab, start, Quaternion.identity);
                meteor.transform.localScale = Vector3.one * 0.72f;
                // Le GLB a été animé autour d'un « carrier » placé en hauteur dans Blender : on le recentre
                // pour piloter nous-mêmes la trajectoire depuis n'importe quel côté.
                var carrier = FindDeep(meteor.transform, "RB_FX_Carrier");
                if (carrier != null) carrier.localPosition = Vector3.zero;
                meteor.transform.LookAt(pos + new Vector3(0, .28f, 0));
            }
            float travel = Random.Range(1.30f, 1.55f);
            Vector3 end = pos + new Vector3(0, .32f, 0);
            for (float t = 0; t < travel; t += Time.deltaTime)
            {
                float k = t / travel;
                if (meteor != null) meteor.transform.position = Vector3.Lerp(start, end, k * k);   // TRANS_QUAD / EASE_IN
                yield return null;
            }
            if (meteor != null) meteor.transform.position = end;
            AreaDamage(pos, source, RB.FireballDamage, RB.FireballRadius, true);
            FireballImpactFx(pos);
            // Le GLB reste 2.10 s après l'impact pour laisser visibles ses débris / fumées.
            yield return new WaitForSeconds(2.10f);
            if (meteor != null) Object.Destroy(meteor);
        }

        static void FireballImpactFx(Vector3 pos)
        {
            var fx = FxManager.I;
            if (fx == null) return;
            fx.Burst(pos + new Vector3(0, .18f, 0), RB.HexColor("#ff4d16"), 58);
            fx.Burst(pos + new Vector3(0, .28f, 0), RB.HexColor("#ffb52e"), 42);
            fx.Burst(pos + new Vector3(0, .38f, 0), RB.HexColor("#ffe59a"), 24);
            foreach (float radius in new[] { 1.8f, 2.8f, 3.8f })
                fx.Run(ShockRing(fx, pos, radius));
            fx.Run(ImpactCore(fx, pos));
            fx.Run(ImpactLight(fx, pos));
        }

        static IEnumerator ShockRing(FxManager fx, Vector3 pos, float radius)
        {
            var ring = fx.TempPrimitive(PrimitiveType.Cylinder, pos + new Vector3(0, .08f, 0), Vector3.zero, RB.HexColor("#ff8a24"));
            Vector3 baseScale = new Vector3(radius * 2f, .055f * .5f, radius * 2f);
            float grow = .22f + radius * .035f;
            for (float t = 0; t < grow; t += Time.deltaTime)
            {
                float k = 1f - (1f - t / grow) * (1f - t / grow);   // QUAD ease-out
                ring.transform.localScale = Vector3.Scale(baseScale, Vector3.Lerp(new Vector3(.12f, .18f, .12f), Vector3.one, k));
                ring.transform.position = new Vector3(pos.x, Mathf.Lerp(pos.y + .08f, pos.y + .16f, Mathf.Min(1f, t / .20f)), pos.z);
                yield return null;
            }
            for (float t = 0; t < .12f; t += Time.deltaTime)
            {
                ring.transform.localScale = Vector3.Scale(baseScale, Vector3.Lerp(Vector3.one, new Vector3(1.18f, .04f, 1.18f), t / .12f));
                yield return null;
            }
            Object.Destroy(ring);
        }

        static IEnumerator ImpactCore(FxManager fx, Vector3 pos)
        {
            var core = fx.TempPrimitive(PrimitiveType.Sphere, pos + new Vector3(0, .45f, 0), Vector3.one * (1.15f * 2f * .18f), RB.HexColor("#ff7a1a"));
            float baseD = 1.15f * 2f;
            for (float t = 0; t < .16f; t += Time.deltaTime)
            {
                core.transform.localScale = Vector3.one * baseD * Mathf.Lerp(.18f, 1.25f, t / .16f);
                yield return null;
            }
            for (float t = 0; t < .20f; t += Time.deltaTime)
            {
                core.transform.localScale = Vector3.one * baseD * Mathf.Lerp(1.25f, .05f, t / .20f);
                yield return null;
            }
            Object.Destroy(core);
        }

        static IEnumerator ImpactLight(FxManager fx, Vector3 pos)
        {
            var go = new GameObject("RB_ImpactFlash");
            go.transform.SetParent(fx.transform, false);
            go.transform.position = pos + new Vector3(0, 1.1f, 0);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = RB.HexColor("#ff9a3d");
            l.range = 9f;
            l.shadows = LightShadows.None;
            const float peak = 60f;   // ≈ light_energy 7 de Godot (unités URP différentes)
            for (float t = 0; t < .32f; t += Time.deltaTime)
            {
                l.intensity = Mathf.Lerp(peak, 0f, t / .32f);
                yield return null;
            }
            Object.Destroy(go);
        }

        // ------------------------------------------------------------------ PLUIE DE FLÈCHES

        static IEnumerator ArrowRain(Vector3 pos, Team source)
        {
            var gm = GameManager.I;
            var fx = FxManager.Ensure();
            gm.Announce("PLUIE DE FLÈCHES !");
            for (int i = 0; i < 14; i++)
            {
                Vector3 off = new Vector3(Random.Range(-2.7f, 2.7f), 0, Random.Range(-2.7f, 2.7f));
                fx.Run(ArrowFall(fx, pos + off, i * .018f));
            }
            yield return new WaitForSeconds(RB.ArrowRainDelay);
            AreaDamage(pos, source, RB.ArrowRainDamage, RB.ArrowRainRadius, false);
            fx.Burst(pos, RB.HexColor("#e9e1b5"), 18);
        }

        static IEnumerator ArrowFall(FxManager fx, Vector3 ground, float delay)
        {
            var root = new GameObject("RB_Arrow");
            root.transform.SetParent(fx.transform, false);
            root.transform.position = ground + new Vector3(0, 6f + Random.value * 2f, 0);
            PrimitiveFactory.Box(Vector3.zero, new Vector3(.055f, .95f, .055f), RB.HexColor("#8b623f"), root.transform);
            PrimitiveFactory.Cylinder(new Vector3(0, -.55f, 0), .10f, .22f, RB.HexColor("#cfd5dc"), root.transform);
            yield return new WaitForSeconds(delay);
            Vector3 from = root.transform.position, to = ground + new Vector3(0, .12f, 0);
            for (float t = 0; t < .24f; t += Time.deltaTime)
            {
                root.transform.position = Vector3.Lerp(from, to, t / .24f);
                yield return null;
            }
            Object.Destroy(root);
        }

        // ------------------------------------------------------------------ ZAP

        static void Zap(Vector3 pos, Team source)
        {
            var gm = GameManager.I;
            var fx = FxManager.Ensure();
            gm.Announce("ZAP !");
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = pos + new Vector3(Random.Range(-.38f, .38f), 1.0f + i * .48f, Random.Range(-.38f, .38f));
                var bolt = fx.TempPrimitive(PrimitiveType.Cube, p, new Vector3(.09f, .62f, .09f), RB.HexColor("#b9f4ff"));
                bolt.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-18f, 18f));
                fx.Run(ShrinkAndDestroy(bolt, new Vector3(.03f, .12f, .03f), .16f));
            }
            var ring = fx.TempPrimitive(PrimitiveType.Cylinder, pos + new Vector3(0, .08f, 0), new Vector3(2.15f * 2f, .05f * .5f, 2.15f * 2f) * .15f, RB.HexColor("#75ddff"));
            fx.Run(GrowAndDestroy(ring, new Vector3(2.15f * 2f, .05f * .5f, 2.15f * 2f), .18f));
            AreaDamage(pos, source, RB.ZapDamage, RB.ZapRadius, false);
            fx.Burst(pos, RB.HexColor("#83e9ff"), 22);
            // stun_area : cool = max(cool, durée) sur les troupes (rayon) et les tours (rayon + 1)
            foreach (var u in gm.Units.ToArray())
                if (u.IsAlive && u.Team != source && Vector3.Distance(u.Position, pos) <= RB.ZapRadius) u.Stun(RB.ZapStun);
            foreach (var t in gm.Towers)
                if (t.IsAlive && t.Team != source && Vector3.Distance(t.Position, pos) <= RB.ZapRadius + 1f) t.Stun(RB.ZapStun);
        }

        // ------------------------------------------------------------------ GEL

        static void Freeze(Vector3 pos, Team source)
        {
            var gm = GameManager.I;
            var fx = FxManager.Ensure();
            gm.Announce("GEL ABSOLU !");
            AreaDamage(pos, source, RB.FreezeDamage, RB.FreezeRadius, false);
            fx.Burst(pos, RB.HexColor("#a9ecff"), 30);
            var ice = fx.TempPrimitive(PrimitiveType.Cylinder, pos + new Vector3(0, .06f, 0), new Vector3(2.85f * 2f, .06f * .5f, 2.85f * 2f) * .15f, RB.HexColor("#a9ecff"));
            fx.Run(IceDisk(ice, new Vector3(2.85f * 2f, .06f * .5f, 2.85f * 2f)));
            for (int j = 0; j < 9; j++)
            {
                float ang = Mathf.PI * 2f * j / 9f;
                Vector3 cp = pos + new Vector3(Mathf.Cos(ang) * Random.Range(1.0f, 2.6f), .28f, Mathf.Sin(ang) * Random.Range(1.0f, 2.6f));
                float h = Random.Range(.55f, 1.15f);
                var crystal = fx.TempPrimitive(PrimitiveType.Cube, cp, new Vector3(.16f, h, .16f), RB.HexColor("#c9f5ff"));
                crystal.transform.rotation = Quaternion.Euler(0, ang * Mathf.Rad2Deg, 0);
                fx.Run(CrystalLife(crystal));
            }
            // freeze_area : rayon 3.0 (tours : +1.0), durée 5.0 s
            foreach (var u in gm.Units.ToArray())
                if (u.IsAlive && u.Team != source && Vector3.Distance(u.Position, pos) <= RB.FreezeRadius) u.Freeze(RB.FreezeDuration);
            foreach (var t in gm.Towers)
                if (t.IsAlive && t.Team != source && Vector3.Distance(t.Position, pos) <= RB.FreezeRadius + 1f) t.Freeze(RB.FreezeDuration);
        }

        static IEnumerator IceDisk(GameObject ice, Vector3 full)
        {
            for (float t = 0; t < .20f; t += Time.deltaTime)
            {
                if (ice == null) yield break;
                ice.transform.localScale = full * Mathf.Lerp(.15f, 1f, t / .20f);
                yield return null;
            }
            if (ice != null) ice.transform.localScale = full;
            yield return new WaitForSeconds(3.0f);
            for (float t = 0; t < .25f; t += Time.deltaTime)
            {
                if (ice == null) yield break;
                ice.transform.localScale = full * Mathf.Lerp(1f, .05f, t / .25f);
                yield return null;
            }
            if (ice != null) Object.Destroy(ice);
        }

        static IEnumerator CrystalLife(GameObject c)
        {
            yield return new WaitForSeconds(3.0f);
            Vector3 from = c.transform.localScale;
            for (float t = 0; t < .25f; t += Time.deltaTime)
            {
                if (c == null) yield break;
                c.transform.localScale = Vector3.Lerp(from, from * .05f, t / .25f);
                yield return null;
            }
            if (c != null) Object.Destroy(c);
        }

        // ------------------------------------------------------------------ utilitaires

        static IEnumerator ShrinkAndDestroy(GameObject go, Vector3 to, float dur)
        {
            Vector3 from = go.transform.localScale;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (go == null) yield break;
                go.transform.localScale = Vector3.Lerp(from, to, t / dur);
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        static IEnumerator GrowAndDestroy(GameObject go, Vector3 to, float dur)
        {
            Vector3 from = go.transform.localScale;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (go == null) yield break;
                go.transform.localScale = Vector3.Lerp(from, to, t / dur);
                yield return null;
            }
            if (go != null) Object.Destroy(go);
        }

        /// <summary>spell_aoe() : dégâts sur les troupes ennemies dans le rayon ; tours à 55 % (rayon + 1) si demandé.</summary>
        public static void AreaDamage(Vector3 pos, Team source, float dmg, float radius, bool hitTowers)
        {
            var gm = GameManager.I;
            foreach (var u in gm.Units.ToArray())
                if (u.IsAlive && u.Team != source && Vector3.Distance(u.Position, pos) <= radius) u.ApplyDamage(dmg);
            if (hitTowers)
                foreach (var t in gm.Towers.ToArray())
                    if (t.IsAlive && t.Team != source && Vector3.Distance(t.Position, pos) <= radius + 1f)
                        t.ApplyDamage(dmg * RB.FireballTowerFactor);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
