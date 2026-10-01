using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace RoyalBuddies.EditorTools
{
    /// <summary>
    /// Générateur du projet : matériaux, ScriptableObjects (troupes et cartes), Animator Controllers + clips avec Animation Events,
    /// prefabs de troupes, asset RBGameAssets et scènes (Menu, Battle, Sandbox).
    /// Menu Unity : « Royal Buddies ▸ Build All ». Ligne de commande :
    ///   Unity -batchmode -executeMethod RoyalBuddies.EditorTools.RBBuilder.BuildAllBatch -quit -projectPath ...
    /// Le générateur est idempotent : on peut le relancer sans dupliquer les assets.
    /// </summary>
    public static class RBBuilder
    {
        public const string Root = "Assets/RoyalBuddies";

        public class MatSet
        {
            public Material lit, unlit, particle, vertexColor, water, grass, deployHint, flowWater, waterfall;
        }

        [MenuItem("Royal Buddies/Build All (assets, prefabs, scenes)")]
        public static void BuildAll()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            var mats = CreateMaterials();
            var troops = CreateTroops(mats);
            var cards = CreateCards(troops);
            var assets = CreateGameAssets(mats, cards);
            CreateScenes(assets, mats);
            SetupProjectSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RoyalBuddies] Build All terminé : " + troops.Count + " troupes, " + cards.Count + " cartes, 3 scènes.");
        }

        public static void BuildAllBatch()
        {
            try { BuildAll(); }
            catch (Exception e)
            {
                Debug.LogError("[RoyalBuddies] Build All a échoué : " + e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ dossiers

        static void EnsureFolders()
        {
            foreach (var d in new[] { "Scripts", "Scenes", "Prefabs", "Prefabs/Units", "Models", "Materials", "Materials/Generated", "Materials/Models",
                                       "Animations", "UI", "Audio", "Data", "Data/Troops", "Data/Cards" })
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "RoyalBuddies", d));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            return a;
        }

        // ------------------------------------------------------------------ matériaux

        static Material MakeMaterial(string path, Shader shader, Action<Material> setup)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D CreateDotTexture(string path)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (!File.Exists(full))
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32)) / 32f;
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                tex.Apply();
                File.WriteAllBytes(full, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void SetTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        static MatSet CreateMaterials()
        {
            string mp = Root + "/Materials/";
            var s = new MatSet();
            s.lit = MakeMaterial(mp + "RB_Lit.mat", Shader.Find("Universal Render Pipeline/Lit"), m => m.SetColor("_BaseColor", Color.white));
            s.unlit = MakeMaterial(mp + "RB_Unlit.mat", Shader.Find("Universal Render Pipeline/Unlit"), m => m.SetColor("_BaseColor", Color.white));
            var dot = CreateDotTexture(Root + "/Materials/RB_dot.png");
            s.particle = MakeMaterial(mp + "RB_Particle.mat", Shader.Find("Universal Render Pipeline/Particles/Unlit"), m =>
            {
                m.SetTexture("_BaseMap", dot);
                m.SetColor("_BaseColor", Color.white);
                SetTransparent(m);
            });
            s.vertexColor = MakeMaterial(mp + "RB_VertexColorLit.mat", Shader.Find("RoyalBuddies/VertexColorLit"), m => m.SetColor("_BaseColor", Color.white));
            s.water = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Environment/Fjord/Materials/Fjord_Sea.mat");
            if (s.water == null) s.water = MakeMaterial(mp + "RB_Water.mat", Shader.Find("RoyalBuddies/Water"), null);
            s.grass = MakeMaterial(mp + "RB_Grass.mat", Shader.Find("RoyalBuddies/Grass"), null);
            var flowShader = Shader.Find("RoyalBuddies/FlowWater");
            if (flowShader != null)
            {
                s.flowWater = MakeMaterial(mp + "RB_FlowWater.mat", flowShader, m =>
                {
                    m.SetFloat("_Speed", .9f); m.SetFloat("_StreakScale", 1.6f); m.SetFloat("_EdgeFoam", .14f); m.SetFloat("_FoamAmount", .3f);
                });
                s.waterfall = MakeMaterial(mp + "RB_Waterfall.mat", flowShader, m =>
                {
                    m.SetFloat("_Speed", 2.6f); m.SetFloat("_StreakScale", .9f); m.SetFloat("_EdgeFoam", .22f); m.SetFloat("_FoamAmount", .65f);
                    m.SetColor("_Shallow", new Color(.45f, .80f, .92f, 1f));
                });
            }
            else Debug.LogWarning("[RoyalBuddies] Shader RoyalBuddies/FlowWater introuvable : les ruisseaux utiliseront l'eau standard.");
            s.deployHint = MakeMaterial(mp + "RB_DeployHint.mat", Shader.Find("Universal Render Pipeline/Unlit"), m =>
            {
                m.SetColor("_BaseColor", new Color(0.20f, 0.65f, 1f, 0.10f));
                SetTransparent(m);
            });
            return s;
        }

        /// <summary>Fournisseur de matériaux « couleur unie » qui crée de vrais assets (pour que les scènes les référencent).</summary>
        static readonly Dictionary<uint, Material> colorAssets = new Dictionary<uint, Material>();
        static Material ColorAsset(Color c)
        {
            Color32 k = c;
            uint key = ((uint)k.r << 24) | ((uint)k.g << 16) | ((uint)k.b << 8) | k.a;
            if (colorAssets.TryGetValue(key, out var cached) && cached != null) return cached;
            string path = string.Format("{0}/Materials/Generated/RB_c_{1:x2}{2:x2}{3:x2}{4:x2}.mat", Root, k.r, k.g, k.b, k.a);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", 0.3f);
                AssetDatabase.CreateAsset(m, path);
            }
            colorAssets[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ troupes

        static List<TroopData> CreateTroops(MatSet mats)
        {
            var list = new List<TroopData>();
            foreach (var r in RBDataTables.Troops)
            {
                var so = LoadOrCreate<TroopData>(Root + "/Data/Troops/RB_Troop_" + r.ascii + ".asset");
                so.id = r.id; so.displayName = r.display;
                so.hp = r.hp; so.damage = r.dmg; so.speed = r.speed; so.range = r.range; so.attackInterval = r.interval; so.size = r.size;
                so.attackKind = r.kind; so.splashRadius = r.splash;
                so.isAir = r.air; so.buildingsOnly = r.buildingsOnly; so.canHitAir = r.canHitAir; so.jumpsRiver = r.jumps;
                so.rigKind = r.rig; so.spawnHeight = r.spawnHeight; so.hpBarHeight = 2.75f; so.hpBarWidth = 1.48f; so.armorColor = r.armor;
                so.attackStates = r.attackClips ?? new string[0];
                if (r.ascii == "GOBELIN") { so.rigKind = RigKind.Glb; so.attackStates = new[] { "Attack_01", "Attack_02", "Attack_03" }; }
                so.attackWeights = new float[0];
                so.noImmediateRepeat = true;
                so.hasHitAnimation = !string.IsNullOrEmpty(r.hitClip);
                so.animBlend = r.blend;
                so.sprintDuration = r.sprint;
                so.useAnimationEvents = false;      // comportement Godot par défaut (dégâts au début de l'animation)
                so.impactNormalizedTime = r.impactTime;
                so.prefab = BuildPrefab(r, so, mats);
                EditorUtility.SetDirty(so);
                list.Add(so);
            }
            AssetDatabase.SaveAssets();
            return list;
        }

        static GameObject BuildPrefab(RBDataTables.TroopRow r, TroopData so, MatSet mats)
        {
            var root = new GameObject("RB_Unit_" + r.ascii);
            root.AddComponent<Unit>();
            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;      // activé par Unit.Init une fois posé sur le NavMesh
            var rig = new GameObject("Rig");
            rig.transform.SetParent(root.transform, false);

            if (r.ascii == "GOBELIN") { rig.transform.localScale = Vector3.one * 1.85f; rig.AddComponent<GoblinVikingVisual>(); }
            else if (r.model != null)
            {
                string glb = Root + "/Models/" + r.model;
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(glb);
                if (modelAsset == null) throw new Exception("Modèle introuvable : " + glb);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, rig.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * r.modelScale;
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = BuildController(r, so, glb);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (model.GetComponent<UnitAnimationEvents>() == null) model.AddComponent<UnitAnimationEvents>();
                if (r.vertexColors) ReplaceWithVertexColorMaterials(model, Path.GetFileNameWithoutExtension(r.model), mats);
            }

            string path = Root + "/Prefabs/Units/RB_Unit_" + r.ascii + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Les GLB Blender stockent leurs couleurs dans COLOR_0 : on remplace les matériaux glTFast (qui l'ignorent)
        /// par RoyalBuddies/VertexColorLit en gardant couleur de base, métal / rugosité et émission du GLB.
        /// </summary>
        static void ReplaceWithVertexColorMaterials(GameObject model, string modelName, MatSet mats)
        {
            var cache = new Dictionary<Material, Material>();
            foreach (var rend in model.GetComponentsInChildren<Renderer>(true))
            {
                var src = rend.sharedMaterials;
                var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var s = src[i];
                    if (s == null) { dst[i] = null; continue; }
                    if (!cache.TryGetValue(s, out var m))
                    {
                        string safe = new string(s.name.Where(char.IsLetterOrDigit).ToArray());
                        string p = Root + "/Materials/Models/RB_VC_" + modelName + "_" + safe + ".mat";
                        m = MakeMaterial(p, mats.vertexColor.shader, mm =>
                        {
                            Color baseCol = s.HasProperty("baseColorFactor") ? s.GetColor("baseColorFactor") : Color.white;
                            float metallic = s.HasProperty("metallicFactor") ? s.GetFloat("metallicFactor") : 0f;
                            float rough = s.HasProperty("roughnessFactor") ? s.GetFloat("roughnessFactor") : 0.6f;
                            Color emis = s.HasProperty("emissiveFactor") ? s.GetColor("emissiveFactor") : Color.black;
                            mm.SetColor("_BaseColor", baseCol);
                            mm.SetFloat("_Metallic", metallic);
                            mm.SetFloat("_Smoothness", 1f - rough);
                            mm.SetColor("_EmissionColor", emis);
                        });
                        cache[s] = m;
                    }
                    dst[i] = m;
                }
                rend.sharedMaterials = dst;
            }
        }

        // ------------------------------------------------------------------ animations

        static readonly Dictionary<string, AnimationClip> clipCopies = new Dictionary<string, AnimationClip>();

        static AnimationClip ClipCopy(string glbPath, string modelName, string clipName, bool loop, params AnimationEvent[] events)
        {
            string key = glbPath + "|" + clipName;
            if (clipCopies.TryGetValue(key, out var done) && done != null) return done;
            var src = AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName && !c.name.StartsWith("__preview"));
            if (src == null) throw new Exception("Clip introuvable : " + clipName + " dans " + glbPath);
            string dir = Root + "/Animations/" + modelName;
            Directory.CreateDirectory(Application.dataPath + "/../" + dir);
            string path = dir + "/" + modelName + "_" + clipName + ".anim";
            AssetDatabase.DeleteAsset(path);
            var copy = UnityEngine.Object.Instantiate(src);
            copy.name = modelName + "_" + clipName;
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            // Les events sont calés en secondes : le temps normalisé de TroopData est converti ici.
            var evs = new List<AnimationEvent>();
            foreach (var e in events) { if (e != null) evs.Add(e); }
            AnimationUtility.SetAnimationEvents(copy, evs.ToArray());
            AssetDatabase.CreateAsset(copy, path);
            clipCopies[key] = copy;
            return copy;
        }

        static AnimationEvent Ev(string fn, float time) { return new AnimationEvent { functionName = fn, time = time }; }

        static RuntimeAnimatorController BuildController(RBDataTables.TroopRow r, TroopData so, string glbPath)
        {
            string modelName = Path.GetFileNameWithoutExtension(glbPath);
            string ctrlPath = Root + "/Animations/RB_" + r.ascii + ".controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Sprint", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;

            // --- Locomotion
            string lengthProbe = r.idleClip ?? r.moveClip;
            bool gargoyle = r.rig == RigKind.GlbGargoyle;
            var idleClip = ClipCopy(glbPath, modelName, r.idleClip, true);
            AnimatorState idle = sm.AddState("Idle"); idle.motion = idleClip; sm.defaultState = idle;
            if (gargoyle) return ctrl;

            var moveSrc = ClipLength(glbPath, r.moveClip);
            var moveClip = ClipCopy(glbPath, modelName, r.moveClip, true, Ev("OnFootstep", moveSrc * .25f), Ev("OnFootstep", moveSrc * .75f));
            AnimatorState move = sm.AddState("Move"); move.motion = moveClip;
            AnimatorState walk = null;
            if (!string.IsNullOrEmpty(r.walkClip))
            {
                float wl = ClipLength(glbPath, r.walkClip);
                var walkClip = ClipCopy(glbPath, modelName, r.walkClip, true, Ev("OnFootstep", wl * .25f), Ev("OnFootstep", wl * .75f));
                walk = sm.AddState("Walk"); walk.motion = walkClip;
            }
            bool sprintLogic = walk != null;

            // Idle <-> Move / Walk
            var t = idle.AddTransition(move); Setup(t, false, .1f); t.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            if (sprintLogic) t.AddCondition(AnimatorConditionMode.If, 0, "Sprint");
            t = move.AddTransition(idle); Setup(t, false, .1f); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            if (sprintLogic)
            {
                t = idle.AddTransition(walk); Setup(t, false, .1f); t.AddCondition(AnimatorConditionMode.If, 0, "Moving"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Sprint");
                t = move.AddTransition(walk); Setup(t, false, .1f); t.AddCondition(AnimatorConditionMode.If, 0, "Moving"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Sprint");
                t = walk.AddTransition(idle); Setup(t, false, .1f); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            }

            // --- Attaques (un état par clip ; l'unité choisit au hasard par CrossFade)
            bool ranged = r.kind == AttackKind.Ranged;
            var attackStates = new List<AnimatorState>();
            foreach (var ac in r.attackClips ?? new string[0])
            {
                float len = ClipLength(glbPath, ac);
                float when = Mathf.Clamp01(r.impactTime) * len;
                var evs = new List<AnimationEvent> { Ev(ranged ? "OnArrowRelease" : "OnAttackImpact", when) };
                if (r.id == "GEANT" || r.id == "COUREUR") evs.Add(Ev("OnFistImpact", when));
                var clip = ClipCopy(glbPath, modelName, ac, false, evs.ToArray());
                var st = sm.AddState(ac); st.motion = clip;
                attackStates.Add(st);
            }
            AnimatorState hit = null, jump = null;
            if (!string.IsNullOrEmpty(r.hitClip)) { hit = sm.AddState("Hit"); hit.motion = ClipCopy(glbPath, modelName, r.hitClip, false); }
            if (!string.IsNullOrEmpty(r.jumpClip)) { jump = sm.AddState("Jump"); jump.motion = ClipCopy(glbPath, modelName, r.jumpClip, false); }
            var death = sm.AddState("Death"); death.motion = ClipCopy(glbPath, modelName, r.deathClip, false);

            var returnStates = new List<AnimatorState>(attackStates);
            if (hit != null) returnStates.Add(hit);
            if (jump != null) returnStates.Add(jump);
            foreach (var st in returnStates)
            {
                // retour à Idle / Move (ou Walk) à la fin du clip
                t = st.AddTransition(idle); Setup(t, true, .1f); t.exitTime = .95f; t.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
                t = st.AddTransition(move); Setup(t, true, .1f); t.exitTime = .95f; t.AddCondition(AnimatorConditionMode.If, 0, "Moving");
                if (sprintLogic) t.AddCondition(AnimatorConditionMode.If, 0, "Sprint");
                if (walk != null)
                {
                    t = st.AddTransition(walk); Setup(t, true, .1f); t.exitTime = .95f;
                    t.AddCondition(AnimatorConditionMode.If, 0, "Moving"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Sprint");
                }
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void Setup(AnimatorStateTransition t, bool exitTime, float duration)
        {
            t.hasExitTime = exitTime;
            t.exitTime = 0.95f;
            t.hasFixedDuration = true;
            t.duration = duration;
            t.canTransitionToSelf = false;
        }

        static float ClipLength(string glbPath, string clipName)
        {
            var src = AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
            return src != null ? src.length : 1f;
        }

        // ------------------------------------------------------------------ cartes

        static List<CardData> CreateCards(List<TroopData> troops)
        {
            var list = new List<CardData>();
            int idx = 0;
            foreach (var r in RBDataTables.Cards)
            {
                var so = LoadOrCreate<CardData>(Root + "/Data/Cards/RB_Card_" + r.ascii + ".asset");
                so.id = r.id; so.displayName = r.display; so.cost = r.cost; so.catalogIndex = idx++;
                so.kind = r.spell ? CardKind.Spell : CardKind.Troop;
                so.spell = r.spellKind;
                so.troop = r.spell ? null : troops.First(t => t.id == r.id);
                so.portraitRect = r.portrait; so.portraitIsSeparateTexture = r.separate; so.accent = r.accent;
                EditorUtility.SetDirty(so);
                list.Add(so);
            }
            AssetDatabase.SaveAssets();
            return list;
        }

        // ------------------------------------------------------------------ RBGameAssets

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new Exception("Asset introuvable : " + path);
            return a;
        }

        static RBGameAssets CreateGameAssets(MatSet mats, List<CardData> cards)
        {
            var a = LoadOrCreate<RBGameAssets>(Root + "/Data/RB_GameAssets.asset");
            a.cards = cards.ToArray();
            a.litMaterial = mats.lit; a.unlitMaterial = mats.unlit; a.particleMaterial = mats.particle; a.vertexColorMaterial = mats.vertexColor;
            a.fireballPrefab = Load<GameObject>(Root + "/Models/RB_Fireball_ForgedMeteor.glb");
            // Tour de mage : facultative (si absente, l'arène garde les tours en primitives).
            string mt = Root + "/Models/MageTower/";
            a.mageTowerModel = AssetDatabase.LoadAssetAtPath<GameObject>(mt + "RB_MageTower.fbx");
            a.mageTowerPlayerMaterial = AssetDatabase.LoadAssetAtPath<Material>(mt + "RB_MageTower_Blue.mat");
            a.mageTowerEnemyMaterial = AssetDatabase.LoadAssetAtPath<Material>(mt + "RB_MageTower_Red.mat");
            if (a.mageTowerModel == null) Debug.LogWarning("[RoyalBuddies] Tour de mage introuvable : " + mt + "RB_MageTower.fbx");
            // Décor viking : données + tous les modèles du dossier Models/Viking
            a.vikingLayout = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/RB_VikingLayout.json");
            var vModels = new List<GameObject>(); var vNames = new List<string>();
            if (AssetDatabase.IsValidFolder(Root + "/Models/Viking"))
                foreach (var guid in AssetDatabase.FindAssets("", new[] { Root + "/Models/Viking" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    vModels.Add(go); vNames.Add(Path.GetFileNameWithoutExtension(path));
                }
            a.vikingModels = vModels.ToArray(); a.vikingModelNames = vNames.ToArray();
            a.flowWaterMaterial = mats.flowWater; a.waterfallMaterial = mats.waterfall;
            string ui = Root + "/UI/";
            a.cardsAtlas = Load<Texture2D>(ui + "RB_cards_atlas.png");
            a.zapTexture = Load<Texture2D>(ui + "RB_zap.png");
            a.hudAtlas = Load<Texture2D>(ui + "RB_hud_v86.png");
            a.pauseSkin = Load<Texture2D>(ui + "RB_pause_v86.png");
            a.fluidWood = Load<Texture2D>(ui + "RB_fluid_wood.png");
            a.fluidDrop = Load<Texture2D>(ui + "RB_fluid_drop.png");
            a.menuSkin = Load<Texture2D>(ui + "RB_menu_skin.png");
            a.scenery = Load<Texture2D>(ui + "RB_scenery.png");
            a.settingsGear = Load<Texture2D>(ui + "RB_settings_gear.png");
            a.loadingScreen = Load<Texture2D>(ui + "RB_loading_screen.png");
            a.menuMusic = Load<AudioClip>(Root + "/Audio/RB_Champ_de_Duel.mp3");
            a.battleMusic = Load<AudioClip>(Root + "/Audio/RB_Victoire_Rapide.mp3");
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
            return a;
        }

        // ------------------------------------------------------------------ scènes

        static void CreateScenes(RBGameAssets assets, MatSet mats)
        {
            string sp = Root + "/Scenes/";
            BuildGameScene(sp + "RB_Battle.unity", assets, mats, sandbox: false);
            BuildGameScene(sp + "RB_Sandbox.unity", assets, mats, sandbox: true);
            BuildMenuScene(sp + "RB_Menu.unity", assets);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(sp + "RB_Menu.unity", true),
                new EditorBuildSettingsScene(sp + "RB_Battle.unity", true),
                new EditorBuildSettingsScene(sp + "RB_Sandbox.unity", true),
            };
        }

        static void BuildGameScene(string path, RBGameAssets assets, MatSet mats, bool sandbox)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrimitiveFactory.MaterialProvider = ColorAsset;
            ArenaBuilder.WaterMaterial = mats.water;
            ArenaBuilder.GrassMaterial = mats.grass;
            ArenaBuilder.DeployHintMaterial = mats.deployHint;
            ArenaBuilder.TowerModel = assets.mageTowerModel;
            ArenaBuilder.TowerMaterialPlayer = assets.mageTowerPlayerMaterial;
            ArenaBuilder.TowerMaterialEnemy = assets.mageTowerEnemyMaterial;

            var cam = ArenaBuilder.BuildLightingAndCamera();
            var arena = ArenaBuilder.Build();
            var viking = VikingDecorBuilder.Parse(assets.vikingLayout);
            FairyBorderBuilder.Exclude = viking != null ? (x, z) => VikingDecorBuilder.IsExcluded(viking, x, z) : (System.Func<float, float, bool>)null;
            FairyBorderBuilder.Build(new GameObject("RB_ForestBorder").transform, mats.particle);
            FairyBorderBuilder.Exclude = null;
            if (viking != null && assets.vikingModels != null)
                VikingDecorBuilder.Build(null, viking, assets.vikingModels, assets.vikingModelNames,
                                         mats.flowWater, mats.waterfall, mats.water, mats.particle, arena.root.transform);

            var bootGo = new GameObject(sandbox ? "RB_Bootstrap_Sandbox" : "RB_Bootstrap");
            var boot = bootGo.AddComponent<GameBootstrap>();
            boot.assets = assets;
            boot.navSurface = arena.navSurface;
            boot.deployHint = arena.deployHint;
            boot.cam = cam;
            boot.sandbox = sandbox;
            boot.startBattleOnPlay = true;
            boot.enableAI = !sandbox;
            boot.enablePlacement = !sandbox;
            boot.enableHud = !sandbox;

            PrimitiveFactory.MaterialProvider = null;
            EditorSceneManager.SaveScene(scene, path);
        }

        static void BuildMenuScene(string path, RBGameAssets assets)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0e, 0x19, 0x30, 0xff);
            camGo.AddComponent<AudioListener>();
            var boot = new GameObject("RB_MenuBootstrap").AddComponent<MenuBootstrap>();
            boot.assets = assets;
            EditorSceneManager.SaveScene(scene, path);
        }

        static void SetupProjectSettings()
        {
            PlayerSettings.productName = "Royal Buddies";
            PlayerSettings.companyName = "Royal Buddies";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1170;
        }
    }
}
