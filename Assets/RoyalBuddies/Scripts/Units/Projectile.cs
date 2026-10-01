using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Projectile à tête chercheuse (fire() / update_shots() de main.gd) :
    /// sphère r = 0.15, vitesse 12 m/s, suit sa cible (AimPoint), impact à moins de 0.32, traînée aléatoire (22 %/image).
    /// Il est détruit sans dégâts si la cible meurt en vol.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        ITarget target;
        float damage;
        float speed = RB.ProjectileSpeed;

        public static Projectile Create(Vector3 start, ITarget target, float damage, Team source)
        {
            var go = PrimitiveFactory.Sphere(start, RB.ProjectileRadius,
                source == Team.Player ? RB.HexColor("#79dcff") : RB.HexColor("#ff786d"), null, "RB_Projectile");
            go.transform.position = start;
            var p = go.AddComponent<Projectile>();
            p.target = target;
            p.damage = damage;
            return p;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm != null && gm.GameOver) return;   // Godot : update_shots() ne tourne plus après la fin de partie
            if (target == null || !target.IsAlive) { Destroy(gameObject); return; }

            Vector3 v = target.AimPoint - transform.position;
            if (v.magnitude < RB.ProjectileHitDistance)
            {
                target.ApplyDamage(damage);
                Destroy(gameObject);
                return;
            }
            if (Random.value < .22f && FxManager.I != null) FxManager.I.TrailPoint(transform.position);
            transform.position += v.normalized * Mathf.Min(speed * Time.deltaTime, v.magnitude);
        }
    }
}
