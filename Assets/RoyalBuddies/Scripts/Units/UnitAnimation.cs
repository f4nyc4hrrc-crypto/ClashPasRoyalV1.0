using System.Collections.Generic;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Enveloppe de l'Animator d'une troupe. Reproduit les giant_play / archer_play / chevalier_attack... de main.gd :
    ///  - états de locomotion pilotés par les paramètres Moving / Sprint (Idle ↔ Move ↔ Walk),
    ///  - attaque choisie au hasard (sans répétition immédiate, poids optionnels pour une attaque « spéciale » plus rare),
    ///  - Hit / Death / Jump forcés par CrossFade vers l'état du même nom.
    /// </summary>
    public class UnitAnimation
    {
        static readonly int MovingHash = Animator.StringToHash("Moving");
        static readonly int SprintHash = Animator.StringToHash("Sprint");

        readonly Animator animator;
        readonly GoblinVikingVisual goblin;
        readonly TroopData data;
        int lastAttack = -1;
        readonly Dictionary<string, float> lengths = new Dictionary<string, float>();

        public bool HasAnimator => goblin != null || (animator != null && animator.runtimeAnimatorController != null);
        public Animator Animator => animator;

        public UnitAnimation(Animator animator, TroopData data, GoblinVikingVisual goblin = null)
        {
            this.animator = animator;
            this.goblin = goblin;
            this.data = data;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    // Les clips sont nommés "<Modèle>_<État>" par RBBuilder : on indexe par le suffixe.
                    int i = clip.name.LastIndexOf('_');
                    string state = i >= 0 && clip.name.Length > i + 1 ? clip.name.Substring(i + 1) : clip.name;
                    lengths[clip.name] = clip.length;
                    lengths[state] = clip.length;
                }
            }
        }

        public float ClipLength(string state, float fallback)
        {
            if (goblin != null) return goblin.ClipLength(state, fallback);
            return lengths.TryGetValue(state, out var l) ? l : fallback;
        }

        public void SetSpeed(float s) { if (goblin != null) goblin.SetSpeed(s); else if (HasAnimator) animator.speed = s; }

        public void SetMoving(bool moving, bool sprint)
        {
            if (goblin != null) { goblin.SetMoving(moving); return; }
            if (!HasAnimator) return;
            animator.SetBool(MovingHash, moving);
            animator.SetBool(SprintHash, sprint);
        }

        void Force(string state, float blend)
        {
            if (string.IsNullOrEmpty(state)) return;
            if (goblin != null) { goblin.Play(state, blend, state.StartsWith("Attack") ? data.attackInterval : 0f); return; }
            if (!HasAnimator || string.IsNullOrEmpty(state)) return;
            if (animator.HasState(0, Animator.StringToHash(state)))
                animator.CrossFadeInFixedTime(state, blend, 0, 0f);
        }

        /// <summary>Joue une attaque tirée au hasard et retourne son nom d'état.</summary>
        public string PlayAttack()
        {
            var states = data.attackStates;
            if (states == null || states.Length == 0) return null;
            int idx = PickAttackIndex(states.Length);
            lastAttack = idx;
            Force(states[idx], data.animBlend);
            return states[idx];
        }

        int PickAttackIndex(int count)
        {
            if (count == 1) return 0;
            var w = data.attackWeights;
            bool weighted = w != null && w.Length == count;
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                if (data.noImmediateRepeat && i == lastAttack) continue;
                total += weighted ? Mathf.Max(0f, w[i]) : 1f;
            }
            if (total <= 0f) { int pick=Random.Range(0, data.noImmediateRepeat && lastAttack >= 0 ? count-1 : count); return data.noImmediateRepeat && lastAttack >= 0 && pick >= lastAttack ? pick+1 : pick; }
            float roll = Random.value * total;
            for (int i = 0; i < count; i++)
            {
                if (data.noImmediateRepeat && i == lastAttack) continue;
                roll -= weighted ? Mathf.Max(0f, w[i]) : 1f;
                if (roll <= 0f) return i;
            }
            return count - 1;
        }

        public void PlayHit() { if (data.hasHitAnimation) Force("Hit", data.animBlend); }
        public void PlayDeath() { SetSpeed(1f); Force("Death", data.animBlend); }
        public void PlayJump() { Force("Jump", data.animBlend); }
        public void PlayRun() { Force("Move", data.animBlend); }
        public void PlayIdle() { Force("Idle", data.animBlend); }
        public float DeathLength() { return ClipLength("Death", 1.85f); }
    }
}
