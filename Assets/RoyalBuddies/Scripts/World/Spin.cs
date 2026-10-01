using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>Rotation continue (roues à aubes du décor).</summary>
    public class Spin : MonoBehaviour
    {
        public float degreesPerSecond = 40f;
        public Vector3 localAxis = Vector3.right;
        void Update() { transform.Rotate(localAxis, degreesPerSecond * Time.deltaTime, Space.Self); }
    }
}
