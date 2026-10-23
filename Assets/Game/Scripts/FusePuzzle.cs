using UnityEngine;

namespace GJ.Game
{
    // Room 1: light the powder trail and make sure the water bucket is off it. The flame is slower than the air,
    // so it lands after you are gone; when it reaches the pipe the blast opens the door.
    public class FusePuzzle : RoomPuzzle
    {
        public Fuse fuse;
        public Transform bucket;
        public float igniteRange = 1.3f;

        public override bool Working => fuse.Current == Fuse.State.Burning;

        public override Vector3? Focus =>
            fuse.Current == Fuse.State.Burning ? fuse.FrontWorld + Vector3.up * 0.05f : (Vector3?)null;

        void Start()
        {
            fuse.Finished += RaiseSolved;
            fuse.Fizzled += () => Say("the water put it out");
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            if (FuseInReach(eye, forward))
            {
                if (!lit) return new Option { prompt = "[F] open the lighter" };
                return new Option
                {
                    prompt = fuse.Current == Fuse.State.Fizzled ? "[E] relight the fuse" : "[E] light the fuse",
                    reach = true,
                    act = () => { if (fuse.Ignite()) Say("the fuse is lit"); }
                };
            }
            if (bucket != null && BucketBlocksTrail() && Vector3.Distance(eye, bucket.position) < 1.6f)
                return new Option { prompt = "the bucket is in the way - walk into it to push it" };
            return null;
        }

        bool FuseInReach(Vector3 eye, Vector3 forward)
        {
            if (fuse.Current != Fuse.State.Idle && fuse.Current != Fuse.State.Fizzled) return false;
            Vector3 front = fuse.FrontWorld;
            Vector3 flat = new Vector3(front.x - eye.x, 0f, front.z - eye.z);
            if (flat.magnitude > igniteRange) return false;
            return Vector3.Dot(forward, (front - eye).normalized) > 0.55f;
        }

        // The bucket still sits on the part of the trail the flame hasn't reached yet.
        bool BucketBlocksTrail()
        {
            for (float d = fuse.Burnt; d <= fuse.Length; d += 0.1f)
            {
                Vector3 p = fuse.transform.TransformPoint(fuse.PointAt(d));
                if (new Vector2(p.x - bucket.position.x, p.z - bucket.position.z).sqrMagnitude < 0.35f * 0.35f) return true;
            }
            return false;
        }
    }
}
