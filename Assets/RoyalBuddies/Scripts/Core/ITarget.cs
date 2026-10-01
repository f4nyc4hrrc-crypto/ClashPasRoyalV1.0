using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>Tout ce qui peut être visé / endommagé : troupes et tours.</summary>
    public interface ITarget
    {
        Team Team { get; }
        bool IsAlive { get; }
        bool IsAir { get; }
        bool IsBuilding { get; }
        Vector3 Position { get; }
        /// <summary>Point visé par les projectiles (tpos() dans main.gd).</summary>
        Vector3 AimPoint { get; }
        void ApplyDamage(float amount);
    }
}
