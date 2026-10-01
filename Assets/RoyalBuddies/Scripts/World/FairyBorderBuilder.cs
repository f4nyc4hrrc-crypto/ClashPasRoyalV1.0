using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Décor « Forêt des Brumes » autour de l'arène (version allégée de fairy_border.gd) : sol sombre, rochers, collines lointaines,
    /// arbres, cristaux et lucioles. Purement décoratif : aucune collision ni logique.
    /// Simplification assumée : Godot génère des milliers de triangles procéduraux ; ici on assemble ~250 primitives,
    /// adaptées au mobile.
    /// </summary>
    public static class FairyBorderBuilder
    {
        static Color C(string h) => RB.HexColor(h);

        /// <summary>Zones laissées libres pour le décor viking (lacs, village, fjord) : rien n'y est posé.</summary>
        public static System.Func<float, float, bool> Exclude;

        static bool Free(float x, float z) => Exclude == null || !Exclude(x, z);

        public static void Build(Transform parent, Material particleMaterial = null)
        {
            var rng = new System.Random(2792026);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Color fog = C("#293347");

            // Sol de forêt (Godot z ∈ [−105 ; 45] → Unity z ∈ [−45 ; 105])
            PrimitiveFactory.Box(new Vector3(0, -1.15f, 30f), new Vector3(170f, .1f, 150f), C("#17242c"), parent, "ForestFloor");

            // Rochers de lisière et parois de schiste sous les bords du plateau
            for (int side = -1; side <= 1; side += 2)
            {
                for (int z = -24; z <= 30; z += 3)
                {
                    float x = side * R(13.2f, 17.0f);
                    if (!Free(x, -z)) { R(.45f, 1.1f); R(2.5f, 4f); R(.45f, 1.1f); R(-25, 25); continue; }
                    var rock = PrimitiveFactory.Box(new Vector3(x, -1.1f + R(.45f, 1.1f) / 2f, -z), new Vector3(R(2.5f, 4f), R(.45f, 1.1f), 3.4f), C("#263638"), parent, "Rock");
                    rock.transform.rotation = Quaternion.Euler(0, R(-25, 25), 0);
                }
                for (int z = -24; z <= 24; z += 2)
                    if (Free(side * 12.6f, -z)) PrimitiveFactory.Box(new Vector3(side * 12.2f, -1.7f, -z), new Vector3(.9f, 1.05f, 1.35f), C("#33404a"), parent, "Cliff");
            }
            for (int x = -12; x <= 12; x += 3)
                PrimitiveFactory.Box(new Vector3(x, -1.65f, 24.3f), new Vector3(1.75f, 1.05f, 1.15f), C("#33404a"), parent, "CliffBack");

            // Collines / montagnes lointaines (dômes aplatis) teintées de brume
            for (int i = 0; i < 14; i++)
            {
                float x = -90f + i * 14f + R(-4, 4);
                float w = R(22f, 34f), h = R(10f, 20f);
                var hill = PrimitiveFactory.Sphere(new Vector3(x, -2f, R(78f, 100f)), .5f, Color.Lerp(C("#22303a"), fog, .55f), parent, "Mountain");
                hill.transform.localScale = new Vector3(w, h * 2f, w * .8f);
            }
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var hill = PrimitiveFactory.Sphere(new Vector3(side * R(45f, 60f), -2f, R(-10f, 50f)), .5f, Color.Lerp(C("#22303a"), fog, .4f), parent, "MountainSide");
                float w = R(18f, 26f);
                hill.transform.localScale = new Vector3(w, R(12f, 22f), w);
            }

            // Arbres noueux : tronc + deux touffes de feuillage
            Color[] leaf = { C("#1c3b34"), C("#24503f"), C("#2b5a45"), C("#1f4636") };
            for (int i = 0; i < 90; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * R(15f, 70f);
                float z = R(-40f, 95f);
                if (Mathf.Abs(x) < 13.6f && z > -25f && z < 25f) continue;
                float s = R(.8f, 1.7f);
                if (!Free(x, z)) { R(-.5f, .5f); R(-.5f, .5f); continue; }
                PrimitiveFactory.Cylinder(new Vector3(x, -1.1f + 1.6f * s, z), .35f * s, 3.2f * s, C("#3a2c22"), parent, "Trunk");
                var f1 = PrimitiveFactory.Sphere(new Vector3(x, -1.1f + 3.9f * s, z), 1f, leaf[i % 4], parent, "Foliage");
                f1.transform.localScale = new Vector3(3.4f * s, 3.0f * s, 3.4f * s);
                var f2 = PrimitiveFactory.Sphere(new Vector3(x + R(-.5f, .5f), -1.1f + 5.4f * s, z + R(-.5f, .5f)), 1f, leaf[(i + 1) % 4], parent, "Foliage");
                f2.transform.localScale = new Vector3(2.4f * s, 2.2f * s, 2.4f * s);
            }

            // Cristaux luminescents
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * R(14f, 34f);
                float z = R(-24f, 50f);
                if (!Free(x, z)) { R(.8f, 1.6f); R(-12, 12); R(0, 90); R(-12, 12); continue; }
                var c = PrimitiveFactory.Box(new Vector3(x, -1.1f + .5f, z), new Vector3(.35f, R(.8f, 1.6f), .35f), i % 3 == 0 ? C("#b58bff") : C("#69d7ff"), parent, "GlowCrystal");
                c.transform.rotation = Quaternion.Euler(R(-12, 12), R(0, 90), R(-12, 12));
            }

            // Lucioles
            var fx = new GameObject("Fireflies");
            fx.transform.SetParent(parent, false);
            fx.transform.position = new Vector3(0, 2f, 25f);
            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = 7f; main.startSpeed = .25f; main.startSize = .16f;
            main.startColor = new ParticleSystem.MinMaxGradient(C("#d6ff7a"), C("#7affd6"));
            main.maxParticles = 80; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 12f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(110f, 6f, 120f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(1, .75f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var rend = fx.GetComponent<ParticleSystemRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (particleMaterial != null) rend.sharedMaterial = particleMaterial;
        }
    }
}
