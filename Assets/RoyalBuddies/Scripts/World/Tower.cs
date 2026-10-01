using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace RoyalBuddies
{
    /// <summary>
    /// Tour latérale (2500 PV) ou royale (3600 PV). Port de tower(), update_towers() et damage_tower() de main.gd.
    /// Les visuels sont construits par <see cref="ArenaBuilder"/> ; ce composant porte la logique.
    /// </summary>
    public class Tower : MonoBehaviour, ITarget
    {
        [SerializeField] Team team;
        [SerializeField] bool king;
        [SerializeField] HpBar bar;
        [SerializeField] NavMeshObstacle obstacle;
        [SerializeField] float fireHeight = RB.TowerFireHeight;   // hauteur de tir (sommet du cristal pour la tour de mage)

        float hp, maxHp, cool, freeze;
        bool alive = true;
        Coroutine collapse;

        public Team Team => team;
        public bool IsKing => king;
        public bool IsAlive => alive;
        public bool IsAir => false;
        public bool IsBuilding => true;
        public Vector3 Position => transform.position;
        public Vector3 AimPoint => transform.position + new Vector3(0, 2f, 0);   // tpos() d'une tour
        public float Hp => hp;
        public float MaxHp => maxHp;
        public float Radius => king ? RB.KingTowerRadius : RB.SideTowerRadius;

        public void Configure(Team t, bool isKing, HpBar hpBar, NavMeshObstacle nav, float fireY = RB.TowerFireHeight)
        {
            team = t; king = isKing; bar = hpBar; obstacle = nav; fireHeight = fireY;
            maxHp = king ? RB.KingTowerHp : RB.SideTowerHp;
            hp = maxHp;
        }

        void Awake()
        {
            if (maxHp <= 0f)
            {
                maxHp = king ? RB.KingTowerHp : RB.SideTowerHp;
                hp = maxHp;
            }
        }

        void OnEnable() { if (GameManager.I != null) GameManager.I.Register(this); }
        void OnDisable() { if (GameManager.I != null) GameManager.I.Unregister(this); }

        void Start()
        {
            if (GameManager.I != null) GameManager.I.Register(this);
            if (bar != null) bar.SetRatio(1f);
        }

        public void Freeze(float seconds) { freeze = seconds; }
        public void Stun(float seconds) { cool = Mathf.Max(cool, seconds); }

        /// <summary>update_towers() de main.gd pour une tour.</summary>
        public void Tick(float dt)
        {
            if (!alive) return;
            if (freeze > 0f) { freeze = Mathf.Max(0f, freeze - dt); return; }
            cool = Mathf.Max(0f, cool - dt);
            var gm = GameManager.I;

            if (king)
            {
                // La tour royale dort tant qu'elle n'a perdu ni PV ni tour latérale.
                bool awake = hp < maxHp;
                foreach (var q in gm.Towers)
                    if (q.team == team && !q.king && !q.alive) awake = true;
                if (!awake) return;
            }

            Unit best = null;
            float bd = king ? RB.KingTowerRange : RB.SideTowerRange;
            foreach (var u in gm.Units)
            {
                if (!u.IsAlive || u.Team == team) continue;
                // Tours latérales : chacune défend uniquement sa moitié d'arène (frontière x = 0).
                if (!king)
                {
                    if (transform.position.x < 0f && u.Position.x > 0f) continue;
                    if (transform.position.x > 0f && u.Position.x < 0f) continue;
                }
                float d = Vector3.Distance(transform.position, u.Position);
                if (d < bd) { bd = d; best = u; }
            }
            if (best != null && cool <= 0f)
            {
                cool = RB.TowerAttackInterval;
                if (FxManager.I != null)
                    FxManager.I.Burst(transform.position + new Vector3(0, fireHeight - .07f, 0),
                        team == Team.Player ? RB.HexColor("#8edfff") : RB.HexColor("#ff8b82"), 3);
                float dmg = king ? RB.KingTowerDamage : RB.SideTowerDamage;
                Projectile.Create(transform.position + new Vector3(0, fireHeight, 0), best, dmg, team);
            }
        }

        void Update()
        {
            // arena_pulse() : sous 35 % de PV la tour « respire » légèrement.
            if (alive && maxHp > 0f && hp < maxHp * .35f && GameManager.I != null && !GameManager.I.GameOver)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 6f) * .018f;
                transform.localScale = new Vector3(pulse, .94f, pulse);
            }
        }

        /// <summary>damage_tower() : pas de critique sur les tours.</summary>
        public void ApplyDamage(float dmg)
        {
            if (!alive) return;
            hp -= dmg;
            if (bar != null) bar.SetRatio(hp / maxHp);
            if (FxManager.I != null)
            {
                FxManager.I.FloatingDamage(transform.position, dmg);
                FxManager.I.Burst(transform.position + new Vector3(0, 2, 0), RB.HexColor("#ffd06a"), 6);
            }
            if (hp > 0f) return;

            alive = false;
            if (obstacle != null) obstacle.enabled = false;
            if (FxManager.I != null) FxManager.I.Burst(transform.position + new Vector3(0, 2, 0), RB.HexColor("#ff6958"), 12);
            collapse = StartCoroutine(Collapse());
            GameManager.I.OnTowerDestroyed(this);
        }

        IEnumerator Collapse()
        {
            // tween_property(root, "scale", (1, .08, 1), .38)
            Vector3 from = transform.localScale, to = new Vector3(1f, .08f, 1f);
            for (float t = 0; t < .38f; t += Time.deltaTime)
            {
                transform.localScale = Vector3.Lerp(from, to, t / .38f);
                yield return null;
            }
            transform.localScale = to;
        }
    }
}
