using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Reçoit les Animation Events posés sur les clips (OnAttackImpact, OnArrowRelease, OnFootstep, OnFistImpact)
    /// et les transmet à l'unité parente. Ce composant doit être sur le GameObject qui porte l'Animator.
    /// </summary>
    public class UnitAnimationEvents : MonoBehaviour
    {
        Unit unit;

        Unit Owner
        {
            get { if (unit == null) unit = GetComponentInParent<Unit>(); return unit; }
        }

        /// <summary>Contact d'un coup (corps à corps, sorcier, bombardeur).</summary>
        public void OnAttackImpact() { var u = Owner; if (u != null) u.AnimEventAttackImpact(); }

        /// <summary>Relâchement de la flèche (archère, tireur) : création du projectile à cet instant.</summary>
        public void OnArrowRelease() { var u = Owner; if (u != null) u.AnimEventAttackImpact(); }

        /// <summary>Pas : petite poussière au sol.</summary>
        public void OnFootstep()
        {
            var u = Owner;
            if (u != null && FxManager.I != null && u.IsAlive) FxManager.I.Dust(u.Position);
        }

        /// <summary>Poing / arme qui frappe le sol (géant, coureur) : gerbe de poussière.</summary>
        public void OnFistImpact()
        {
            var u = Owner;
            if (u != null && FxManager.I != null && u.IsAlive) FxManager.I.GroundImpact(u.Position + u.transform.forward * 0.8f, new Color(0.7f, 0.62f, 0.5f, 0.8f));
        }
    }
}
