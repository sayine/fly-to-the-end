using UnityEngine;

namespace Game.Levels
{
    /// <summary>Base for pooled mission items: a trigger that reports when the plane's body enters it.</summary>
    public abstract class MissionTrigger : MonoBehaviour
    {
        protected Rigidbody Plane { get; private set; }

        public void Arm(Rigidbody plane)
        {
            Plane = plane;
            gameObject.SetActive(true);
        }

        public void Disarm() => gameObject.SetActive(false);

        private void OnTriggerEnter(Collider other)
        {
            if (Plane != null && other.attachedRigidbody == Plane) OnPlaneEntered();
        }

        protected abstract void OnPlaneEntered();
    }
}
