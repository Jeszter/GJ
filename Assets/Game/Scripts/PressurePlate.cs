using GJ.PSX;
using UnityEngine;

namespace GJ.Game
{
    // A steel plate let into the floor on a spring. Enough weight on it pushes it down and holds open whatever it is
    // linked to; take the weight off and the spring brings it back up. A body counts: yours, standing or fallen, and
    // the ones left lying from earlier tries.
    public class PressurePlate : MonoBehaviour
    {
        public Vector2 size = new Vector2(0.8f, 0.8f);   // local X and Z
        public Transform top;                            // sinks while pressed
        public float travel = 0.025f;
        public AudioClip downClip, upClip;

        public bool Pressed { get; private set; }

        Transform _player;
        Vector3 _topRest;
        float _press;

        void Start()
        {
            var controller = FindFirstObjectByType<FirstPersonController>();
            if (controller != null) _player = controller.transform;
            if (top != null) _topRest = top.localPosition;
        }

        // Whether a point is over the plate (and near the floor, not up in the air above it).
        public bool Covers(Vector3 world, float margin = 0f)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            return Mathf.Abs(l.x) <= size.x * 0.5f + margin && Mathf.Abs(l.z) <= size.y * 0.5f + margin && l.y > -0.3f && l.y < 0.6f;
        }

        void Update()
        {
            // The player's feet (or where the player lies) and every old body.
            bool weighed = _player != null && Covers(_player.position, 0.15f);
            foreach (var corpse in Corpse.All)
                if (!weighed && corpse.LiesOn(this)) weighed = true;

            if (weighed != Pressed)
            {
                Pressed = weighed;
                var clip = weighed ? downClip : upClip;
                if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, 0.8f);
            }
            _press = Mathf.MoveTowards(_press, Pressed ? 1f : 0f, Time.deltaTime * 8f);
            if (top != null) top.localPosition = _topRest + Vector3.down * travel * _press;
        }
    }
}
