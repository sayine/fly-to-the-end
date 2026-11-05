using System;
using UnityEngine;

namespace Game.Levels
{
    /// <summary>Entering the target room completes a ReachRoom level.</summary>
    public sealed class RoomTrigger : MissionTrigger
    {
        private Action _entered;

        public void Place(Bounds room, Rigidbody plane, Action entered)
        {
            transform.position = room.center;
            var box = GetComponent<BoxCollider>();
            box.size = room.size - new Vector3(0.6f, 0.2f, 0.6f); // a little inside the walls
            _entered = entered;
            Arm(plane);
        }

        protected override void OnPlaneEntered() => _entered?.Invoke();
    }
}
