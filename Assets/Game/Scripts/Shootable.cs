using System;
using UnityEngine;

namespace GJ.Game
{
    // Something a revolver round can hit and do something about (a sand hopper, a lamp, a pipe). Whatever owns it
    // listens to Shot; the point is where the round struck.
    public class Shootable : MonoBehaviour
    {
        public event Action<Vector3> Shot;

        public void Hit(Vector3 point) => Shot?.Invoke(point);
    }
}
