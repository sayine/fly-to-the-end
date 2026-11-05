using UnityEngine;

namespace Game.Levels
{
    /// <summary>
    /// Bullseye on a furniture top. Its collider sits just above the surface; touching it softly is a landing
    /// (PaperPlane checks for this component), anything else a crash.
    /// </summary>
    public sealed class LandingTarget : MonoBehaviour
    {
        public void Place(Vector3 topCentre, Vector2 size)
        {
            transform.position = topCentre;
            transform.localScale = new Vector3(size.x, 1f, size.y);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Horizontal distance from the bullseye (m).</summary>
        public float DistanceFromCentre(Vector3 point)
        {
            var d = point - transform.position;
            return new Vector2(d.x, d.z).magnitude;
        }
    }
}
