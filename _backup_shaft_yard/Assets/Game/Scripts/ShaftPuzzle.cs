using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // Room 2, the lift shaft: the pump room's cistern, chute and counterweight bucket, but the bolt is the catch under
    // a cage at the bottom of a tall shaft. Once the bucket is full the catch lets go, the gate drops and the cage's own
    // counterweight takes it up to the top landing, with whatever lies in it. So be in the cage when the air runs out:
    // the vault door up there only opens for a cage that brings someone.
    public class ShaftPuzzle : CisternPuzzle
    {
        public Transform cage;               // rises straight up by riseHeight
        public float riseHeight = 11f;
        public float rideSeconds = 13f;
        public Transform cageGate;           // drops from where it hangs to local height 0 to shut the cage
        public Transform cable;              // from the top sheave down to the cage roof, stretched along its local Y
        public float cableTop;               // world height of the sheave
        public float cageRoof = 2.3f;        // the roof above the cage floor
        public Vector2 floorMin, floorMax;   // the cage floor in world X/Z
        public Transform body;               // the player
        public Transform landing;            // what a body riding up looks at
        public AudioSource rideSource;
        public AudioClip gateClip;

        bool _riding, _rides, _arrived;

        bool BodyInCage
        {
            get
            {
                if (body == null) return false;
                Vector3 p = body.position;
                return p.x > floorMin.x && p.x < floorMax.x && p.z > floorMin.y && p.z < floorMax.y && p.y < cage.position.y + 1f;
            }
        }

        public override bool Working => _riding ? _rides : base.Working && BodyInCage;

        public override Vector3? Focus => _riding && _rides ? landing.position : base.Focus;

        // Everything else right, but the body was left behind (or is about to be).
        public override string FailReason => base.FailReason ?? ((_riding || _arrived ? _rides : BodyInCage) ? null : "you weren't in the cage");

        protected override void Released() => StartCoroutine(Ride());

        IEnumerator Ride()
        {
            _riding = true;
            _rides = BodyInCage;

            // The gate drops shut, then the catch lets the counterweight have the cage.
            Play(gateClip, 0.9f, cageGate.position);
            Vector3 open = cageGate.localPosition, shut = new Vector3(open.x, 0f, open.z);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                cageGate.localPosition = Vector3.Lerp(open, shut, t * t / 0.25f);
                yield return null;
            }
            cageGate.localPosition = shut;
            Play(clunkClip, 0.8f, cageGate.position);
            yield return new WaitForSeconds(0.6f);

            // A body rides with the floor under it, not walking: its controller is off until the top.
            var walker = _rides ? body.GetComponent<CharacterController>() : null;
            if (walker != null) walker.enabled = false;
            if (rideSource != null) rideSource.Play();
            Vector3 bottom = cage.position;
            for (float t = 0f; t < rideSeconds; t += Time.deltaTime)
            {
                MoveCage(bottom + Vector3.up * riseHeight * Mathf.SmoothStep(0f, 1f, t / rideSeconds));
                yield return null;
            }
            MoveCage(bottom + Vector3.up * riseHeight);
            if (rideSource != null) rideSource.Stop();
            Play(clunkClip, 1f, cage.position);
            if (walker != null) walker.enabled = true;

            _riding = false;
            _arrived = true;
            if (_rides) RaiseSolved();
        }

        void MoveCage(Vector3 to)
        {
            Vector3 step = to - cage.position;
            cage.position = to;
            if (_rides) body.position += step;
            if (cable != null)
                cable.localScale = new Vector3(1f, Mathf.Max(0.01f, cableTop - (cage.position.y + cageRoof)), 1f);
        }
    }
}
