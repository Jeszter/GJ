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
        public AudioClip shoveClip;

        public override bool Working => fuse.Current == Fuse.State.Burning;

        public override Vector3? Focus =>
            fuse.Current == Fuse.State.Burning ? fuse.FrontWorld + Vector3.up * 0.05f : (Vector3?)null;

        public override string FailReason =>
            fuse.Current == Fuse.State.Idle ? "the fuse was never lit" :
            fuse.Current == Fuse.State.Fizzled ? "the water in the bucket put the fire out" : null;

        void Start()
        {
            fuse.Finished += RaiseSolved;
            fuse.Fizzled += () => Say("the water in the bucket put it out");
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            // The bucket first while it still sits on the powder and is what you're looking at: once the flame has
            // died against it, the fuse's front is right beside it and relighting there only drowns it again.
            if (bucket != null && BucketBlocksTrail())
            {
                float bucketAim = Aim(eye, forward, bucket.position + Vector3.up * 0.2f, 1.9f);
                float frontAim = Aim(eye, forward, fuse.FrontWorld, 1.9f);
                if (bucketAim > 0.8f && (fuse.Current == Fuse.State.Fizzled || bucketAim >= frontAim))
                    return new Option { prompt = "[E] push the bucket off the powder", act = ShoveBucket };
            }
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
            return null;
        }

        // Shove the bucket sideways, off the trail and away from the walls.
        public void ShoveBucket()
        {
            var body = bucket.GetComponent<Rigidbody>();
            Vector3 p = bucket.position;
            // Away from the nearest point of the trail, flat on the floor.
            Vector3 nearest = fuse.transform.TransformPoint(fuse.PointAt(fuse.Burnt));
            float best = float.MaxValue;
            for (float d = fuse.Burnt; d <= fuse.Length; d += 0.05f)
            {
                Vector3 q = fuse.transform.TransformPoint(fuse.PointAt(d));
                float dist = new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude;
                if (dist < best) { best = dist; nearest = q; }
            }
            // The trail bends, so "away from the nearest bit" can land on a later bit: try spots round the bucket and
            // take the closest one that is clear of the whole unburnt trail and of walls and props.
            Vector3 away = new Vector3(p.x - nearest.x, 0f, p.z - nearest.z);
            if (away.sqrMagnitude < 0.0001f) away = Vector3.right;
            Vector3 target = p + away.normalized * 0.7f;
            var col = bucket.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            bool found = false;
            for (float r = 0.6f; r <= 1.6f && !found; r += 0.2f)
                for (int k = 0; k < 16 && !found; k++)
                {
                    float a = Mathf.Atan2(away.z, away.x) + (k % 2 == 0 ? 1 : -1) * (k / 2) * Mathf.PI / 8f;
                    var c = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (TrailDistance(c) < 0.55f) continue;
                    if (Physics.CheckSphere(new Vector3(c.x, p.y + 0.2f, c.z), 0.17f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    target = c;
                    found = true;
                }
            if (col != null) col.enabled = true;
            // Move the object and its body together and tell physics at once, or the body snaps it straight back.
            var moved = new Vector3(target.x, p.y, target.z);
            bucket.position = moved;
            if (body != null)
            {
                body.position = moved;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            if (shoveClip != null) AudioSource.PlayClipAtPoint(shoveClip, target, 0.8f);
            Say("the bucket scrapes aside");
        }

        // Flat distance from a point to the part of the trail the flame hasn't reached yet.
        float TrailDistance(Vector3 point)
        {
            float best = float.MaxValue;
            for (float d = fuse.Burnt; d <= fuse.Length; d += 0.05f)
            {
                Vector3 q = fuse.transform.TransformPoint(fuse.PointAt(d));
                best = Mathf.Min(best, new Vector2(q.x - point.x, q.z - point.z).magnitude);
            }
            return best;
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
