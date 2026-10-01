using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace RoyalBuddies
{
    /// <summary>
    /// Une troupe en jeu. Port de spawn(), update_units(), move_to(), route(), attack_anim(), damage_unit()
    /// et nearest_unit() / nearest_tower() de main.gd.
    ///
    /// Déplacement : NavMeshAgent (contournement des tours et des autres troupes fourni par Unity) entre les mêmes
    /// waypoints que Godot (entrée / sortie du pont). Les unités volantes (gargouille) se déplacent en ligne droite.
    /// Si aucun NavMesh n'est disponible, repli sur un déplacement direct (comme move_to sans évitement).
    /// </summary>
    [DisallowMultipleComponent]
    public class Unit : MonoBehaviour, ITarget
    {
        static int nextId = 1;

        /// <summary>Type d'agent NavMesh commun (rayon 0.3) créé par GameBootstrap ; 0 = type Humanoid par défaut.</summary>
        public static int NavAgentTypeId = 0;

        public TroopData Data { get; private set; }
        public Team Team { get; private set; }
        public int Id { get; private set; }

        float hp, maxHp, cool, freezeTime;
        bool alive;
        ITarget target;
        int phase;               // 0 = vers l'entrée du pont, 1 = traversée, 2 = libre (route() de Godot)
        float bridgeX;
        bool moved;
        float sprintTime;
        bool jumping; float jumpT; Vector3 jumpStart, jumpEnd;
        float bobPhase, attacking;   // gargouille
        Vector3 lastDest; float destTimer;
        ITarget pendingTarget;       // attaque en attente d'un Animation Event

        NavMeshAgent agent;
        UnitAnimation anim;
        ProceduralRig proc;
        HpBar bar;
        Transform rigT;

        // --- ITarget ---
        public bool IsAlive => alive;
        public bool IsAir => Data != null && Data.isAir;
        public bool IsBuilding => false;
        public Vector3 Position => transform.position;
        public Vector3 AimPoint => transform.position + new Vector3(0, 1.35f, 0);   // tpos() d'une troupe
        public float Hp => hp;
        public float MaxHp => maxHp;
        public ITarget CurrentTarget => target;
        public float GroundRadius => Mathf.Max(.34f, Data.size * .78f);              // ground_radius()
        public bool IsFrozen => freezeTime > 0f;
        /// <summary>Animator du modèle (tests / débogage).</summary>
        public Animator ModelAnimator => anim != null ? anim.Animator : null;
        public int Phase => phase;
        bool Sprint => Data.sprintDuration > 0f && sprintTime < Data.sprintDuration;

        // ------------------------------------------------------------------ création

        /// <summary>spawn() de main.gd pour une troupe (les sorts passent par <see cref="SpellSystem"/>).</summary>
        public static Unit Spawn(TroopData data, Vector3 pos, Team team)
        {
            if (data == null || data.prefab == null)
            {
                Debug.LogError("Unit.Spawn : TroopData ou prefab manquant.");
                return null;
            }
            var go = Instantiate(data.prefab, new Vector3(pos.x, data.spawnHeight, pos.z), Quaternion.Euler(0f, 180f, 0f));
            go.name = "RB_" + data.id + (team == Team.Player ? "_P" : "_E");
            var u = go.GetComponent<Unit>();
            if (u == null) u = go.AddComponent<Unit>();
            u.Init(data, team);
            GameManager.I.Register(u);
            if (FxManager.I != null) FxManager.I.Burst(go.transform.position, team == Team.Player ? RB.PlayerColor : RB.EnemyColor, 5);
            u.StartCoroutine(u.PopIn());
            return u;
        }

        void Init(TroopData data, Team team)
        {
            Data = data; Team = team; Id = nextId++;
            hp = maxHp = data.hp;
            alive = true;
            bridgeX = transform.position.x < 0f ? -RB.BridgeX : RB.BridgeX;

            // Rig visuel
            if (data.rigKind == RigKind.Procedural)
            {
                proc = ProceduralRig.Build(transform, data, team);
                rigT = proc.rig;
            }
            else
            {
                rigT = transform.Find("Rig");
                if (rigT == null) rigT = transform;
            }

            var animator = GetComponentInChildren<Animator>();
            anim = new UnitAnimation(animator, data, GetComponentInChildren<GoblinVikingVisual>());
            if (data.rigKind == RigKind.GlbGargoyle && anim.HasAnimator)
                animator.Play(0, 0, Random.value);   // ailes : phase de départ aléatoire (advance(randf()*length))

            // Anneau d'équipe + barre de vie
            Color teamColor = team == Team.Player ? RB.PlayerColor : RB.EnemyColor;
            PrimitiveFactory.Cylinder(new Vector3(0, .03f - data.spawnHeight, 0), data.size * 1.25f, .035f, teamColor, transform, "TeamRing");
            bar = HpBar.Create(transform, new Vector3(0, data.hpBarHeight, 0), data.hpBarWidth);
            bar.SetRatio(1f);

            SetupAgent();
        }

        void SetupAgent()
        {
            agent = GetComponent<NavMeshAgent>();
            if (Data.isAir) { if (agent != null) agent.enabled = false; return; }
            if (agent == null)
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
                agent.enabled = false;
            }
            agent.agentTypeID = NavAgentTypeId;
            agent.radius = GroundRadius;
            agent.height = 2f;
            agent.speed = Data.speed;
            agent.acceleration = 40f;
            agent.angularSpeed = 0f;
            agent.updateRotation = false;
            agent.autoBraking = false;
            agent.stoppingDistance = 0f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(30, 70);
            if (NavMesh.SamplePosition(transform.position, out var hit, 4f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
            }
        }

        IEnumerator PopIn()
        {
            // root.scale .35 → 1 en .18 s
            transform.localScale = Vector3.one * .35f;
            for (float t = 0; t < .18f; t += Time.deltaTime)
            {
                transform.localScale = Vector3.one * Mathf.Lerp(.35f, 1f, t / .18f);
                yield return null;
            }
            transform.localScale = Vector3.one;
        }

        void OnDestroy()
        {
            if (GameManager.I != null) GameManager.I.Unregister(this);
        }

        // ------------------------------------------------------------------ boucle principale (update_units)

        /// <summary>Arrête net la troupe (fin de partie : Godot ne met plus à jour les unités).</summary>
        public void Halt() { StopAgent(); }

        public void Freeze(float seconds) { freezeTime = seconds; }
        public void Stun(float seconds) { cool = Mathf.Max(cool, seconds); }

        public void Tick(float dt)
        {
            if (!alive) return;
            moved = false;

            if (freezeTime > 0f)
            {
                freezeTime = Mathf.Max(0f, freezeTime - dt);
                anim.SetSpeed(0f);
                StopAgent();
                return;
            }
            anim.SetSpeed(1f);

            if (Data.rigKind == RigKind.GlbGargoyle)
            {
                // Gargouille : léger rebond + coup de tête lors d'une attaque
                bobPhase += dt * 3.4f;
                attacking = Mathf.Max(0f, attacking - dt);
                float strike = Mathf.Sin((attacking / .22f) * Mathf.PI);
                rigT.localPosition = new Vector3(0, Mathf.Sin(bobPhase) * .10f, strike * .12f);
                rigT.localRotation = Quaternion.Euler(strike * .18f * Mathf.Rad2Deg, 0, 0);
            }

            cool = Mathf.Max(0f, cool - dt);

            // --- Verrouillage de cible : une troupe engagée reste sur sa cible jusqu'à sa destruction
            if (Data.buildingsOnly && target is Unit) target = null;
            if (target != null)
            {
                if (!target.IsAlive) target = null;
                else if (target is Unit tu && !CanTarget(tu)) target = null;
            }

            // --- PRIORITÉ COMBAT > FORMATION > TOUR
            if (!Data.buildingsOnly && (target == null || !(target is Unit)))
            {
                var enemy = NearestUnit();
                if (enemy != null) target = enemy;
            }

            if (target is Unit tgt)
            {
                float d = RB.FlatDistance(transform.position, tgt.Position);
                if (d > Data.range) MoveTo(tgt.Position, dt);
                else if (cool <= 0f) BeginAttack(tgt);
                EndTick();
                return;
            }

            Tower t = target as Tower ?? NearestTower();
            if (t == null) { EndTick(); return; }

            if (!Data.isAir && phase < 2)
            {
                Route(dt);
            }
            else
            {
                float td = RB.FlatDistance(transform.position, t.Position);
                if (td > Data.range + 1f)
                {
                    MoveTo(t.Position, dt);
                }
                else if (cool <= 0f)
                {
                    // Être simplement arrivé à portée ne verrouille PAS la tour : elle l'est au premier coup.
                    target = t;
                    BeginAttack(t);
                }
            }
            EndTick();
        }

        void EndTick()
        {
            anim.SetMoving(moved, Sprint);
            if (!moved) StopAgent();
        }

        void StopAgent()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ ciblage

        bool CanTarget(Unit q)
        {
            return !q.Data.isAir || Data.canHitAir;   // can_target_unit()
        }

        /// <summary>nearest_unit() : ennemi le plus proche dans un rayon de 7.25, en privilégiant ceux situés devant.</summary>
        Unit NearestUnit()
        {
            Unit best = null;
            float bd = RB.AggroRadius;
            float fwd = RB.Forward(Team);
            foreach (var q in GameManager.I.Units)
            {
                if (!q.alive || q.Team == Team || !CanTarget(q)) continue;
                Vector3 rel = q.Position - transform.position;
                float d = RB.FlatDistance(transform.position, q.Position);
                float ahead = rel.z * fwd;
                // Un ennemi déjà dépassé ne provoque plus de demi-tour, sauf contact.
                if (ahead < RB.AggroBehindTolerance && d > RB.AggroBehindMaxDistance) continue;
                if (d < bd) { bd = d; best = q; }
            }
            return best;
        }

        /// <summary>nearest_tower() : tour ennemie vivante la plus proche (recalculée à chaque appel).</summary>
        Tower NearestTower()
        {
            Tower best = null;
            float bd = float.PositiveInfinity;
            foreach (var t in GameManager.I.Towers)
            {
                if (!t.IsAlive || t.Team == Team) continue;
                float d = RB.FlatDistance(transform.position, t.Position);
                if (d < bd - .001f) { bd = d; best = t; }
            }
            return best;
        }

        // ------------------------------------------------------------------ déplacement

        /// <summary>formation_speed() : une troupe rapide reste derrière un allié en marche au lieu de le doubler.</summary>
        float FormationSpeed(Vector3 dir)
        {
            if (Data.isAir) return Data.speed;
            float speed = Data.speed;
            float ur = GroundRadius;
            foreach (var q in GameManager.I.Units)
            {
                if (q == this || !q.alive || q.Team != Team || q.Data.isAir) continue;
                if (q.target != null) continue;   // seul l'allié qui progresse sert de leader
                Vector3 rel = q.Position - transform.position; rel.y = 0f;
                float dist = rel.magnitude;
                if (dist < .01f) continue;
                float ahead = Vector3.Dot(dir, rel / dist);
                float safe = ur + q.GroundRadius + .18f;
                if (ahead > .72f && dist < safe + 1.15f)
                {
                    if (dist <= safe + .08f) return Mathf.Max(.55f, speed * .28f);
                    speed = Mathf.Min(speed, q.Data.speed);
                }
            }
            return speed;
        }

        /// <summary>move_to() : avance vers <paramref name="dest"/> en tournant progressivement (lerp_angle · dt·7).</summary>
        void MoveTo(Vector3 dest, float dt)
        {
            Vector3 v = dest - transform.position; v.y = 0f;
            float dist = v.magnitude;
            if (dist <= .03f) return;
            moved = true;
            Vector3 dir = v / dist;
            float spd = FormationSpeed(dir);

            bool nav = agent != null && agent.enabled && agent.isOnNavMesh && !Data.isAir;
            if (nav)
            {
                agent.isStopped = false;
                agent.speed = spd;
                destTimer -= dt;
                if ((dest - lastDest).sqrMagnitude > .09f || destTimer <= 0f || (!agent.hasPath && !agent.pathPending))
                {
                    agent.SetDestination(dest);
                    lastDest = dest;
                    destTimer = .25f;
                }
                Vector3 dv = agent.desiredVelocity; dv.y = 0f;
                if (dv.sqrMagnitude > .01f) dir = dv.normalized;
            }
            else
            {
                Vector3 p = transform.position + dir * Mathf.Min(spd * dt, dist);
                p.x = Mathf.Clamp(p.x, -RB.ArenaHalfX, RB.ArenaHalfX);
                p.z = Mathf.Clamp(p.z, -RB.ArenaHalfZ, RB.ArenaHalfZ);
                transform.position = p;
            }

            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(transform.eulerAngles.y, yaw, Mathf.Min(1f, dt * 7f)), 0f);

            if (Data.sprintDuration > 0f) sprintTime += dt;
            if (proc != null) proc.Walk(dt, Data.speed);
        }

        bool Crossed(float lineZ)
        {
            // « franchissement de ligne » de route() : robuste même si l'évitement empêche d'atteindre le waypoint
            return RB.Forward(Team) * (transform.position.z - lineZ) >= 0f;
        }

        /// <summary>route() : phases 0 (entrée du pont) → 1 (traversée) → 2 (objectif libre) + saut du coureur.</summary>
        void Route(float dt)
        {
            float fwd = RB.Forward(Team);

            if (Data.jumpsRiver)
            {
                if (jumping)
                {
                    float jt = Mathf.Min(1f, jumpT + dt / 0.72f);
                    jumpT = jt;
                    Vector3 jp = Vector3.Lerp(jumpStart, jumpEnd, jt);
                    jp.y = Mathf.Sin(jt * Mathf.PI) * 1.35f;
                    transform.position = jp;
                    if (jt >= 1f)
                    {
                        transform.position = new Vector3(jumpEnd.x, 0f, jumpEnd.z);
                        jumping = false;
                        phase = 2;
                        EnableAgentAt(transform.position);
                        anim.PlayRun();
                    }
                    return;
                }

                if (phase == 0)
                {
                    float xNow = transform.position.x;
                    // Zone centrale stricte : seulement ici le coureur a le droit de sauter.
                    if (Mathf.Abs(xNow) < 2.35f)
                    {
                        var jumpEntry = new Vector3(xNow, 0f, -2.55f * fwd);
                        MoveTo(jumpEntry, dt);
                        if (Vector3.Distance(transform.position, jumpEntry) < 0.5f || Crossed(jumpEntry.z))
                        {
                            jumping = true;
                            jumpT = 0f;
                            jumpStart = new Vector3(xNow, 0f, -2.45f * fwd);
                            jumpEnd = new Vector3(xNow, 0f, 2.45f * fwd);
                            if (agent != null && agent.enabled) agent.enabled = false;
                            transform.position = jumpStart;
                            anim.PlayJump();
                        }
                        return;
                    }
                    // Hors du centre : pont obligatoire, aucun saut.
                    bridgeX = xNow < 0f ? -RB.BridgeX : RB.BridgeX;
                    var bridgeEntry = new Vector3(bridgeX, 0f, -2.5f * fwd);
                    MoveTo(bridgeEntry, dt);
                    if (Vector3.Distance(transform.position, bridgeEntry) < 0.45f || Crossed(bridgeEntry.z)) phase = 1;
                    return;
                }
            }

            if (phase == 0)
            {
                var a = new Vector3(bridgeX, 0f, -2.5f * fwd);
                MoveTo(a, dt);
                if (Vector3.Distance(transform.position, a) < .45f || Crossed(a.z)) phase = 1;
            }
            else if (phase == 1)
            {
                var b = new Vector3(bridgeX, 0f, 2.5f * fwd);
                MoveTo(b, dt);
                // Une fois la sortie du pont franchie, la phase 2 est définitive : aucun retour au pont.
                if (Vector3.Distance(transform.position, b) < .45f || Crossed(b.z)) phase = 2;
            }
        }

        void EnableAgentAt(Vector3 pos)
        {
            if (agent == null || Data.isAir) return;
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
            }
        }

        // ------------------------------------------------------------------ attaque

        void AttackAnim()
        {
            attacking = .22f;
            if (Data.rigKind == RigKind.GlbGargoyle) return;   // le modèle entier pique vers sa cible (Tick)
            if (proc != null) { proc.AttackSwing(); return; }
            anim.PlayAttack();
        }

        void BeginAttack(ITarget t)
        {
            cool = Data.attackInterval;
            AttackAnim();
            if (Data.useAnimationEvents && anim.HasAnimator && Data.attackStates != null && Data.attackStates.Length > 0)
                pendingTarget = t;          // dégâts / projectile déclenchés par l'Animation Event
            else
                ResolveAttack(t);           // comportement Godot : effet immédiat au début de l'animation
        }

        /// <summary>Appelé par les Animation Events OnAttackImpact / OnArrowRelease.</summary>
        public void AnimEventAttackImpact()
        {
            if (pendingTarget == null) return;
            var t = pendingTarget; pendingTarget = null;
            if (alive && t.IsAlive) ResolveAttack(t);
        }

        void ResolveAttack(ITarget t)
        {
            var gm = GameManager.I;
            if (t is Unit tu)
            {
                switch (Data.attackKind)
                {
                    case AttackKind.SplashAll:
                        gm.SplashDamage(tu.Position, Team, Data.damage, Data.splashRadius, true);
                        if (FxManager.I != null) FxManager.I.Burst(tu.Position, RB.HexColor("#64e5a0"), 10);
                        break;
                    case AttackKind.SplashGround:
                        gm.SplashDamage(tu.Position, Team, Data.damage, Data.splashRadius, false);
                        break;
                    case AttackKind.Ranged:
                        Projectile.Create(transform.position + new Vector3(0, RB.UnitFireHeight, 0), tu, Data.damage, Team);
                        break;
                    default:
                        tu.ApplyDamage(Data.damage);
                        break;
                }
            }
            else if (t is Tower tw)
            {
                if (Data.range > 2f) Projectile.Create(transform.position + new Vector3(0, RB.UnitFireHeight, 0), tw, Data.damage, Team);
                else tw.ApplyDamage(Data.damage);
            }
        }

        // ------------------------------------------------------------------ dégâts, mort

        /// <summary>damage_unit() : critique 8 % (×1.45), « Rage Royale » (×1.18 sur les unités ennemies).</summary>
        public void ApplyDamage(float dmg)
        {
            if (!alive) return;
            var gm = GameManager.I;
            bool crit = Random.value < RB.CritChance;
            if (gm.RageActive && Team == Team.Enemy) dmg *= RB.RageDamageTakenMultiplier;
            if (crit) { dmg *= RB.CritMultiplier; gm.Announce("COUP CRITIQUE !"); }
            hp -= dmg;
            bar.SetRatio(hp / maxHp);
            if (FxManager.I != null)
            {
                FxManager.I.FloatingDamage(transform.position, dmg);
                FxManager.I.Burst(transform.position, RB.HexColor("#ffd06a"), 5);
            }
            if (hp > 0f) anim.PlayHit();
            if (hp <= 0f) Die();
        }

        void Die()
        {
            alive = false;
            var gm = GameManager.I;
            StopAgent();
            if (agent != null) agent.enabled = false;
            gm.OnUnitKilled(this);
            gm.Unregister(this);
            if (FxManager.I != null) FxManager.I.Burst(transform.position, RB.HexColor("#ff725e"), 9);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            bool glbModel = proc == null && Data.rigKind == RigKind.Glb && anim.HasAnimator;
            if (glbModel)
            {
                anim.PlayDeath();
                yield return new WaitForSeconds(anim.DeathLength() + .08f);
                yield return Shrink(.18f);
            }
            else
            {
                // silhouettes procédurales et gargouille : chute à (80, 0, 20) puis rétrécissement
                Quaternion from = rigT.localRotation, to = Quaternion.Euler(80, 0, 20);
                for (float t = 0; t < .18f; t += Time.deltaTime)
                {
                    rigT.localRotation = Quaternion.Slerp(from, to, t / .18f);
                    yield return null;
                }
                rigT.localRotation = to;
                yield return Shrink(.22f);
            }
            Destroy(gameObject);
        }

        IEnumerator Shrink(float dur)
        {
            Vector3 from = transform.localScale, to = Vector3.one * .05f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                transform.localScale = Vector3.Lerp(from, to, t / dur);
                yield return null;
            }
            transform.localScale = to;
        }
    }
}
