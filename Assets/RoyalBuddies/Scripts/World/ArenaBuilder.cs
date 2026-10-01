using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace RoyalBuddies
{
    /// <summary>
    /// Construit l'arène (port de world(), arena_visual_upgrade() et tower() de main.gd).
    /// Toutes les valeurs numériques sont celles de Godot ; seul l'axe Z est inversé (Unity Z = −Godot Z),
    /// ce que fait la fonction locale G().
    /// Appelé dans l'éditeur par RBBuilder (l'arène est alors enregistrée dans la scène) ou à l'exécution.
    /// </summary>
    public static class ArenaBuilder
    {
        public static Material WaterMaterial;
        public static Material GrassMaterial;
        public static Material DeployHintMaterial;
        // Tour de mage (Asset Store « Awesome Stylized Mage Tower ») : modèle + matériau par camp.
        // Branchés par RBBuilder dans l'éditeur ; à l'exécution, repli sur RBGameAssets.Current.
        public static GameObject TowerModel;
        public static Material TowerMaterialPlayer;
        public static Material TowerMaterialEnemy;

        /// <summary>Convertit une position Godot (x, y, z) en position Unity (x, y, −z).</summary>
        static Vector3 G(float x, float y, float z) => new Vector3(x, y, -z);
        static Color C(string hex) => RB.HexColor(hex);

        public class Result
        {
            public GameObject root;
            public NavMeshSurface navSurface;
            public GameObject deployHint;
            public Tower[] towers;
        }

        public static Result Build(Transform parent = null)
        {
            var res = new Result();
            var root = new GameObject("RB_Arena");
            if (parent != null) root.transform.SetParent(parent, false);
            res.root = root;

            var terrain = Group("Terrain", root.transform);
            var bridges = Group("Bridges", root.transform);
            var decor = Group("Decor", root.transform);
            var towersG = Group("Towers", root.transform);

            BuildTerrain(terrain, bridges);
            BuildVisualUpgrade(decor, bridges);
            BuildDecor(decor);

            var list = new System.Collections.Generic.List<Tower>();
            // Godot : tour(±6.4, −15) et (0, −20) = adversaire ; (±6.4, 15) et (0, 20) = joueur
            list.Add(BuildTower(towersG, G(-6.4f, 0, -15f), Team.Enemy, false));
            list.Add(BuildTower(towersG, G(6.4f, 0, -15f), Team.Enemy, false));
            list.Add(BuildTower(towersG, G(0f, 0, -20f), Team.Enemy, true));
            list.Add(BuildTower(towersG, G(-6.4f, 0, 15f), Team.Player, false));
            list.Add(BuildTower(towersG, G(6.4f, 0, 15f), Team.Player, false));
            list.Add(BuildTower(towersG, G(0f, 0, 20f), Team.Player, true));
            res.towers = list.ToArray();

            res.navSurface = BuildNavGround(root.transform);
            res.deployHint = BuildDeployHint(root.transform);
            return res;
        }

        static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        // ------------------------------------------------------------------ terrain

        static void BuildTerrain(Transform t, Transform bridges)
        {
            // Socle
            PrimitiveFactory.Box(G(0, -0.40f, 0), new Vector3(22.2f, 0.45f, 45.7f), C("#38463b"), t, "Plinth");
            // Deux moitiés de pelouse : la rivière est un vrai vide entre elles.
            var north = PrimitiveFactory.Box(G(0, -0.20f, -12.675f), new Vector3(22.5f, 0.28f, 20.65f), C("#567b49"), t, "TurfEnemy");
            var south = PrimitiveFactory.Box(G(0, -0.20f, 12.675f), new Vector3(22.5f, 0.28f, 20.65f), C("#567b49"), t, "TurfPlayer");
            if (GrassMaterial != null)
            {
                north.GetComponent<MeshRenderer>().sharedMaterial = GrassMaterial;
                south.GetComponent<MeshRenderer>().sharedMaterial = GrassMaterial;
            }
            // Dalles de lane
            for (int side = -1; side <= 1; side += 2)
                for (int z = -21; z < 22; z += 2)
                    PrimitiveFactory.Box(G(side * 5.65f, -0.01f, z), new Vector3(3.0f, 0.035f, 0.08f), C("#587750"), t, "LaneStone");
            // Bordures de pierre
            for (int z = -23; z < 24; z += 2)
            {
                if (Mathf.Abs(z) < 3 || ((z + 23) / 2) % 6 == 1 || ((z + 23) / 2) % 6 == 5) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    var stone = PrimitiveFactory.Box(G(side * (10.72f + .12f * Mathf.Sin(z * 1.3f)), .07f, z), new Vector3(.40f, .30f, 1.0f), C("#606f68"), t, "BorderStone");
                    stone.transform.rotation = Quaternion.Euler(0, -(.12f * Mathf.Sin(z * .8f + side)) * Mathf.Rad2Deg, 0);
                }
            }
            // Rivière + berges
            var water = PrimitiveFactory.Box(G(0, -0.12f, 0), new Vector3(22.5f, 0.11f, 4f), C("#2b6e8e"), t, "Water");
            if (WaterMaterial != null) water.GetComponent<MeshRenderer>().sharedMaterial = WaterMaterial;
            PrimitiveFactory.Box(G(0, 0.08f, -2.18f), new Vector3(22.5f, 0.18f, 0.35f), C("#acaa7b"), t, "BankEnemy");
            PrimitiveFactory.Box(G(0, 0.08f, 2.18f), new Vector3(22.5f, 0.18f, 0.35f), C("#acaa7b"), t, "BankPlayer");
            // Ponts (structure de base ; face supérieure à Y = 0)
            foreach (float x in new[] { -5.6f, 5.6f })
            {
                PrimitiveFactory.Box(G(x, -0.25f, 0), new Vector3(3.35f, 0.5f, 4.7f), C("#96663a"), bridges, "BridgeBase");
                foreach (float z in new[] { -1.7f, -.55f, .55f, 1.7f })
                    PrimitiveFactory.Box(G(x, -0.06f, z), new Vector3(3.0f, 0.12f, 0.13f), C("#c39052"), bridges, "BridgeSlat");
                PrimitiveFactory.Box(G(x - 1.42f, 0.18f, 0), new Vector3(0.14f, 0.36f, 4.7f), C("#503722"), bridges, "BridgeRailL");
                PrimitiveFactory.Box(G(x + 1.42f, 0.18f, 0), new Vector3(0.14f, 0.36f, 4.7f), C("#503722"), bridges, "BridgeRailR");
            }
        }

        // arena_visual_upgrade() : cailloux, herbes et habillage des ponts
        static void BuildVisualUpgrade(Transform t, Transform bridges)
        {
            var rng = new System.Random(29092026);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Color[] stones = { C("#73796b"), C("#85887a"), C("#676f64") };
            Color[] blades = { C("#456f3e"), C("#527d45"), C("#3f6639") };
            for (int i = 0; i < 58; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float x = side * R(7.7f, 10.15f);
                float z = R(-21.5f, 21.5f);
                if (Mathf.Abs(z) < 2.7f) continue;
                var stone = PrimitiveFactory.Sphere(G(x, .075f, z), R(.065f, .15f), stones[i % 3], t, "Pebble");
                stone.transform.localScale = Vector3.Scale(stone.transform.localScale, new Vector3(R(.8f, 1.35f), R(.55f, .9f), R(.8f, 1.4f)));
                if (i % 2 == 0)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var blade = PrimitiveFactory.Box(G(x + R(-.13f, .13f), .105f, z + R(-.13f, .13f)), new Vector3(.025f, R(.13f, .25f), .025f), blades[k], t, "Blade");
                        blade.transform.rotation = Quaternion.Euler(0, 0, R(-.32f, .32f) * Mathf.Rad2Deg);
                    }
                }
            }
            for (int i = 0; i < 34; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float x = side * R(8.8f, 10.35f);
                float z = R(-21.8f, 21.8f);
                if (Mathf.Abs(z) < 2.8f) continue;
                for (int k = 0; k < 2; k++)
                {
                    var blade = PrimitiveFactory.Box(G(x + R(-.09f, .09f), .09f, z + R(-.09f, .09f)), new Vector3(.022f, R(.12f, .22f), .022f), C("#4b7440"), t, "Blade");
                    blade.transform.rotation = Quaternion.Euler(0, 0, R(-.28f, .28f) * Mathf.Rad2Deg);
                }
            }

            // Habillage des ponts V8
            Color[] tones = { C("#b8793f"), C("#c78a4a"), C("#9f6537"), C("#d09551") };
            foreach (float bx in new[] { -5.6f, 5.6f })
            {
                for (int j = 0; j < 10; j++)
                {
                    float pz = -2.03f + j * .45f;
                    var plank = PrimitiveFactory.Box(G(bx, -.065f, pz), new Vector3(3.02f, .13f, .39f), tones[j % 4], bridges, "Plank");
                    plank.transform.rotation = Quaternion.Euler(0, -(.012f * Mathf.Sin(j * 1.7f)) * Mathf.Rad2Deg, 0);
                }
                foreach (int sx in new[] { -1, 1 })
                {
                    PrimitiveFactory.Box(G(bx + sx * 1.48f, -.10f, 0), new Vector3(.15f, .20f, 4.55f), C("#49301f"), bridges, "Rail");
                    foreach (float pz in new[] { -1.75f, -.9f, 0f, .9f, 1.75f })
                        PrimitiveFactory.Cylinder(G(bx + sx * 1.49f, .16f, pz), .075f, .32f, C("#6f4b2b"), bridges, "Post");
                    PrimitiveFactory.Box(G(bx + sx * 1.49f, .34f, 0), new Vector3(.09f, .09f, 4.25f), C("#7b542f"), bridges, "Handrail");
                }
                foreach (float pz in new[] { -1.78f, 1.78f })
                    foreach (int sx in new[] { -1, 1 })
                        PrimitiveFactory.Cylinder(G(bx + sx * 1.49f, .08f, pz), .11f, .16f, C("#b88742"), bridges, "PostCap");
            }
        }

        static void BuildDecor(Transform t)
        {
            // Braseros / cristaux d'arène (bleu côté joueur, rouge côté adversaire)
            foreach (float x in new[] { -10f, 10f })
                foreach (float z in new[] { -20f, -11f, 11f, 20f })
                {
                    PrimitiveFactory.Cylinder(G(x, .35f, z), .22f, .7f, C("#4b5361"), t, "Brazier");
                    PrimitiveFactory.Sphere(G(x, .9f, z), .24f, z > 0 ? C("#69d7ff") : C("#ff6f69"), t, "Crystal");
                }
            // Rochers et buissons des lisières
            foreach (int side in new[] { -1, 1 })
                foreach (float zi in new[] { -20f, -15f, -9f, 9f, 15f, 20f })
                {
                    float rx = side * Random.Range(9.2f, 10.5f);
                    PrimitiveFactory.Sphere(G(rx, .28f, zi), Random.Range(.28f, .52f), C("#69705f"), t, "Rock");
                    PrimitiveFactory.Cylinder(G(rx + side * .35f, .32f, zi + .25f), .18f, .64f, C("#507448"), t, "Bush");
                }
            // Drapeaux
            foreach (float bx in new[] { -8.5f, 8.5f })
            {
                PrimitiveFactory.Box(G(bx, 1.25f, 0), new Vector3(.16f, 2.5f, .16f), C("#58432f"), t, "FlagPole");
                PrimitiveFactory.Box(G(bx, 1.85f, 0), new Vector3(.85f, .72f, .08f), bx < 0 ? C("#3e8fd7") : C("#d84f4b"), t, "Flag");
            }
            // Monuments d'angle
            foreach (float x in new[] { -10.6f, 10.6f })
                foreach (float z in new[] { -22f, 22f })
                {
                    PrimitiveFactory.Cylinder(G(x, 1.0f, z), .42f, 2.0f, C("#313b50"), t, "Pillar");
                    PrimitiveFactory.Cylinder(G(x, 2.05f, z), .62f, .16f, C("#d2b85b"), t, "PillarCap");
                    PrimitiveFactory.Sphere(G(x, 2.55f, z), .34f, z > 0 ? C("#61d5ff") : C("#ff6969"), t, "PillarOrb");
                }
            // Emblème central et repères de lane
            PrimitiveFactory.Cylinder(G(0, .09f, 0), 1.15f, .06f, C("#d8bd62"), t, "EmblemOuter");
            PrimitiveFactory.Cylinder(G(0, .13f, 0), .72f, .07f, C("#405a7b"), t, "EmblemInner");
            foreach (float x in new[] { -5.6f, 5.6f })
                foreach (float z in new[] { -12f, 12f })
                    PrimitiveFactory.Box(G(x, .02f, z), new Vector3(1.1f, .035f, .16f), new Color(.85f, .85f, .75f, 1f), t, "LaneMarker");
        }

        // ------------------------------------------------------------------ tours

        /// <summary>tower() de main.gd : forteresse de pierre + occupant (roi ou gardien).</summary>
        static Tower BuildTower(Transform parent, Vector3 pos, Team team, bool king)
        {
            var root = new GameObject(king ? (team == Team.Player ? "KingTower_Player" : "KingTower_Enemy")
                                            : (team == Team.Player ? "SideTower_Player" : "SideTower_Enemy") + (pos.x < 0 ? "_L" : "_R"));
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            // Nouvelle tour : modèle « Mage Tower » (bleu côté joueur, rouge côté adversaire).
            var assets = RBGameAssets.Current;
            var model = TowerModel != null ? TowerModel : (assets != null ? assets.mageTowerModel : null);
            var towerMat = team == Team.Player
                ? (TowerMaterialPlayer != null ? TowerMaterialPlayer : (assets != null ? assets.mageTowerPlayerMaterial : null))
                : (TowerMaterialEnemy != null ? TowerMaterialEnemy : (assets != null ? assets.mageTowerEnemyMaterial : null));
            if (model != null && towerMat != null &&
                BuildMageTowerVisual(root.transform, model, towerMat, king, out float topY, out float fireY))
                return FinishTower(root, team, king, topY + .45f, fireY);

            Color teamCol = team == Team.Player ? C("#3e8fd7") : C("#d84f4b");
            Color stone = C("#c9c5b8"), dark = C("#555c68"), wood = C("#8b633f"), gold = C("#e3b93f");
            float sv = king ? 1.18f : .96f;
            var r = root.transform;

            PrimitiveFactory.Box(new Vector3(0, .48f, 0), new Vector3(3.15f * sv, .85f, 2.75f * sv), dark, r, "Base");
            PrimitiveFactory.Box(new Vector3(0, 1.38f, 0), new Vector3(2.75f * sv, 1.35f, 2.45f * sv), stone, r, "Keep");
            PrimitiveFactory.Box(new Vector3(0, 2.18f, 0), new Vector3(2.95f * sv, .48f, 2.65f * sv), teamCol, r, "TeamBand");
            PrimitiveFactory.Box(new Vector3(0, 2.58f, 0), new Vector3(2.45f * sv, .22f, 2.15f * sv), wood, r, "Platform");
            // Porte (côté caméra : Godot +Z local → Unity −Z local) et blason doré
            PrimitiveFactory.Box(new Vector3(0, 1.18f, -1.39f * sv), new Vector3(.92f, .92f, .12f), C("#343943"), r, "Gate");
            PrimitiveFactory.Box(new Vector3(0, .72f, -1.48f * sv), new Vector3(.78f, .18f, .10f), gold, r, "GateStep");
            PrimitiveFactory.Cylinder(new Vector3(0, .92f, -1.49f * sv), .30f, .10f, gold, r, "Crest");
            // Créneaux
            foreach (float x in new[] { -1.18f, 0f, 1.18f })
            {
                PrimitiveFactory.Box(new Vector3(x * sv, 3.03f, 1.02f * sv), new Vector3(.52f, .72f, .48f), stone, r, "Merlon");
                PrimitiveFactory.Box(new Vector3(x * sv, 3.03f, -1.02f * sv), new Vector3(.52f, .72f, .48f), stone, r, "Merlon");
            }
            foreach (float z in new[] { -.52f, .52f })
            {
                PrimitiveFactory.Box(new Vector3(-1.22f * sv, 3.03f, z * sv), new Vector3(.48f, .72f, .52f), stone, r, "Merlon");
                PrimitiveFactory.Box(new Vector3(1.22f * sv, 3.03f, z * sv), new Vector3(.48f, .72f, .52f), stone, r, "Merlon");
            }
            // Occupant : roi (tour royale) ou gardien-archer
            var actor = new GameObject("Occupant").transform;
            actor.SetParent(r, false);
            actor.localPosition = new Vector3(0, 2.82f, 0);
            Color skin = C("#d99a72");
            if (king)
            {
                PrimitiveFactory.Box(new Vector3(0, .55f, 0), new Vector3(1.15f, .92f, .72f), teamCol, actor, "Torso");
                PrimitiveFactory.Sphere(new Vector3(0, 1.35f, 0), .43f, skin, actor, "Head");
                PrimitiveFactory.Box(new Vector3(0, 1.10f, -.35f), new Vector3(.72f, .50f, .18f), C("#2e2422"), actor, "Beard");
                PrimitiveFactory.Box(new Vector3(-.72f, .58f, 0), new Vector3(.28f, .82f, .28f), skin, actor, "ArmL");
                PrimitiveFactory.Box(new Vector3(.72f, .58f, 0), new Vector3(.28f, .82f, .28f), skin, actor, "ArmR");
                PrimitiveFactory.Box(new Vector3(0, 1.78f, 0), new Vector3(.82f, .22f, .58f), gold, actor, "CrownBase");
                foreach (float cx in new[] { -.30f, 0f, .30f })
                    PrimitiveFactory.Box(new Vector3(cx, 2.02f, 0), new Vector3(.14f, .42f, .14f), gold, actor, "CrownProng");
                PrimitiveFactory.Box(new Vector3(0, .62f, -.39f), new Vector3(.42f, .32f, .08f), gold, actor, "ChestEmblem");
            }
            else
            {
                PrimitiveFactory.Box(new Vector3(0, .52f, 0), new Vector3(.82f, .88f, .62f), teamCol, actor, "Torso");
                PrimitiveFactory.Sphere(new Vector3(0, 1.25f, 0), .35f, skin, actor, "Head");
                PrimitiveFactory.Box(new Vector3(0, 1.57f, 0), new Vector3(.72f, .20f, .58f), dark, actor, "Helmet");
                PrimitiveFactory.Box(new Vector3(.55f, .65f, 0), new Vector3(.10f, 1.05f, .10f), wood, actor, "Bow");
            }

            return FinishTower(root, team, king, king ? 4.45f : 4.05f, RB.TowerFireHeight);
        }

        /// <summary>Barre de PV, obstacle NavMesh et composant Tower (communs aux deux visuels).</summary>
        static Tower FinishTower(GameObject root, Team team, bool king, float barY, float fireY)
        {
            var bar = HpBar.Create(root.transform, new Vector3(0, barY, 0), king ? 2.55f : 2.25f);

            // Obstacle NavMesh (capsule creusante) : les unités contournent la tour mais peuvent l'approcher au corps à corps.
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = king ? RB.KingTowerCarveRadius : RB.SideTowerCarveRadius;
            obstacle.height = 3f;
            obstacle.center = new Vector3(0, 1.5f, 0);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            var tower = root.AddComponent<Tower>();
            tower.Configure(team, king, bar, obstacle, fireY);
            return tower;
        }

        /// <summary>
        /// Instancie le modèle de tour de mage sous <paramref name="r"/>, applique le matériau du camp,
        /// tourne l'entrée vers la caméra (−Z), met à l'échelle sur le rayon de la tour et pose la base au sol.
        /// Renvoie la hauteur du sommet et la hauteur de tir (cristal), en local.
        /// </summary>
        static bool BuildMageTowerVisual(Transform r, GameObject model, Material mat, bool king, out float topY, out float fireY)
        {
            topY = fireY = 0f;
            var go = UnityEngine.Object.Instantiate(model, r, false);
            go.name = "MageTower";
            var t = go.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
                return false;
            }
            Renderer walls = null, entrance = null, crystal = null;
            foreach (var rd in rends)
            {
                var mats = new Material[Mathf.Max(1, rd.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                rd.sharedMaterials = mats;
                string n = rd.gameObject.name;
                if (n.StartsWith("Walls")) walls = rd;
                else if (n.StartsWith("Enterance") || n.StartsWith("Entrance")) entrance = rd;
                else if (n.StartsWith("Crystal")) crystal = rd;
            }
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(col); else UnityEngine.Object.DestroyImmediate(col);
            }

            // 1) Orientation : l'entrée face à la caméra (−Z), comme l'ancienne porte.
            if (entrance != null)
            {
                Vector3 core = (walls != null ? walls.bounds.center : AllBounds(rends).center);
                Vector3 d = entrance.bounds.center - core; d.y = 0f;
                if (d.sqrMagnitude > 1e-6f)
                    t.localRotation = Quaternion.Euler(0f, Vector3.SignedAngle(d, Vector3.back, Vector3.up), 0f);
            }

            // 2) Échelle : diamètre du fût = diamètre logique de la tour (RB.*TowerRadius), hauteur plafonnée.
            Bounds core0 = walls != null ? walls.bounds : AllBounds(rends);
            Bounds all0 = AllBounds(rends);
            float width = Mathf.Max(core0.size.x, core0.size.z);
            float targetW = (king ? RB.KingTowerRadius : RB.SideTowerRadius) * 2f * .95f;
            float s = width > 1e-4f ? targetW / width : 1f;
            float maxH = king ? 7.6f : 6.6f;
            if (all0.size.y * s > maxH) s = maxH / all0.size.y;
            t.localScale = Vector3.one * s;

            // 3) Position : fût centré sur la tour, base posée au sol.
            Bounds core1 = walls != null ? walls.bounds : AllBounds(rends);
            Bounds all1 = AllBounds(rends);
            Vector3 origin = r.position;
            t.position += new Vector3(origin.x - core1.center.x, origin.y - all1.min.y, origin.z - core1.center.z);

            Bounds all2 = AllBounds(rends);
            topY = all2.max.y - origin.y;
            fireY = crystal != null ? crystal.bounds.center.y - origin.y : topY * .8f;
            return true;
        }

        static Bounds AllBounds(Renderer[] rends)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        // ------------------------------------------------------------------ navigation

        /// <summary>Sol de navigation : deux moitiés de pelouse (|x| ≤ 10.35, |z| ≤ 22.5) et les deux ponts. La rivière est un vide.</summary>
        static NavMeshSurface BuildNavGround(Transform parent)
        {
            var nav = new GameObject("NavGround");
            nav.transform.SetParent(parent, false);
            float halfX = RB.ArenaHalfX, riv = RB.RiverHalfWidth, halfZ = RB.ArenaHalfZ;
            float lenZ = halfZ - riv;
            foreach (float s in new[] { -1f, 1f })
            {
                var c = nav.AddComponent<BoxCollider>();
                c.center = new Vector3(0, -.5f, s * (riv + lenZ / 2f));
                c.size = new Vector3(halfX * 2f, 1f, lenZ);
            }
            foreach (float x in new[] { -RB.BridgeX, RB.BridgeX })
            {
                var c = nav.AddComponent<BoxCollider>();
                c.center = new Vector3(x, -.5f, 0);
                c.size = new Vector3(3.35f, 1f, riv * 2f + .3f);
            }
            var surface = nav.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = 0;
            return surface;
        }

        /// <summary>Halo translucide sur la moitié de déploiement du joueur (deploy_hint de rb_battle_hud.gd).</summary>
        static GameObject BuildDeployHint(Transform parent)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "DeployHint";
            var col = q.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(col); else UnityEngine.Object.DestroyImmediate(col); }
            q.transform.SetParent(parent, false);
            q.transform.localRotation = Quaternion.Euler(90, 0, 0);
            float z0 = -RB.ArenaHalfZ, z1 = -RB.DeployNearZ;
            q.transform.localPosition = new Vector3(0, .04f, (z0 + z1) / 2f);
            q.transform.localScale = new Vector3(RB.ArenaHalfX * 2f, z1 - z0, 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (DeployHintMaterial != null) mr.sharedMaterial = DeployHintMaterial;
            q.SetActive(false);
            return q;
        }

        /// <summary>Matériau URP Unlit transparent (halo de déploiement).</summary>
        public static Material MakeTransparentUnlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "RB_DeployHint" };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        // ------------------------------------------------------------------ éclairage / caméra

        /// <summary>Environnement (ciel #0e1930, ambiance #e0e8df ×0.52), soleil (−52°, −28°, énergie 1.15) et caméra.</summary>
        public static Camera BuildLightingAndCamera(Transform parent = null)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = C("#e0e8df") * 0.52f;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var sun = new GameObject("RB_Sun");
            if (parent != null) sun.transform.SetParent(parent, false);
            // Direction Godot (0.289, −0.788, −0.544) → Unity (0.289, −0.788, +0.544)
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.28905f, -0.78801f, 0.54360f));
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.15f;
            l.color = Color.white;
            l.shadows = LightShadows.Soft;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            if (parent != null) camGo.transform.SetParent(parent, false);
            camGo.transform.position = RB.CameraPosition;
            camGo.transform.rotation = Quaternion.Euler(RB.CameraEuler);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = RB.CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = C("#0e1930");
            camGo.AddComponent<AudioListener>();
            return cam;
        }
    }
}
