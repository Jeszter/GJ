using System;
using UnityEngine;

namespace GJ.Game
{
    // What a room asks of the player. The director owns air, death and coming back; a puzzle owns the room's own
    // machinery (a fuse, a cistern) and tells the director what can be done right now, where a dead body should look,
    // and when the door may open.
    public abstract class RoomPuzzle : MonoBehaviour
    {
        // Something the player can do with what they are looking at. A null act is a plain hint ("the bucket is in
        // the way"); reach dips the lighter hand toward it.
        public struct Option
        {
            public string prompt;
            public Action act;
            public bool reach;
        }

        public event Action Solved;
        public event Action<string> Message;

        // Still moving and still able to open the door: a dead player keeps watching instead of starting over.
        public abstract bool Working { get; }

        // Where a body lying on the floor should look; null means at the door.
        public virtual Vector3? Focus => null;

        // Once nothing is left working: what went wrong, shown under "one more time" so the next try can go better.
        public virtual string FailReason => null;

        // The best option for an eye looking along forward, or null. lit: whether the lighter is burning.
        public abstract Option? Look(Vector3 eye, Vector3 forward, bool lit);

        protected void RaiseSolved() => Solved?.Invoke();
        protected void Say(string text) => Message?.Invoke(text);

        // How squarely the eye looks at a point (dot of view and direction), or -1 when it is out of reach.
        protected static float Aim(Vector3 eye, Vector3 forward, Vector3 point, float range)
        {
            Vector3 to = point - eye;
            if (to.magnitude > range) return -1f;
            return Vector3.Dot(forward, to.normalized);
        }
    }
}
