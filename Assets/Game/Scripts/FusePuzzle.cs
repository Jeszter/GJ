using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // Room 1. Three ways through the door:
    //  - the powder trail: light it and make sure the water bucket is off it. The flame is slower than the air, so it
    //    lands after you are gone; when it reaches the pipe the blast opens the door.
    //  - loud: a round from the revolver on the workbench into the gas cylinder by the pipe. The blast takes the door
    //    and you with it.
    //  - the gas: open the cylinder's valve and let the room fill. Every breath is shorter; when you go down, the lit
    //    lighter falls out of your hand and the room goes up. Shut the lighter first and it was for nothing.
    public class FusePuzzle : RoomPuzzle
    {
        public Fuse fuse;
        public Transform bucket;
        public float igniteRange = 1.3f;
        public AudioClip shoveClip;

        public GameObject benchGun;          // the revolver lying on the workbench until it is taken
        public Transform benchGunPoint;
        public AudioClip takeClip;

        public Shootable gasCylinder;
        public Transform gasValve;           // the cylinder's hand wheel; spins about its local Y
        public AudioSource gasHiss;
        public AudioClip valveClip;
        public float gasNeeded = 8f;         // seconds of leak before there is enough gas in the room to go up
        public float gasAirDrain = 2.2f;

        bool _gasOpen, _busyValve, _blown, _igniting, _diedDark;
        float _gas;
        Color _fogColor;
        float _fogEnd;

        public override bool Working => fuse.Current == Fuse.State.Burning || _igniting;

        public override Vector3? Focus =>
            _igniting ? gasCylinder.transform.position + Vector3.up * 0.8f :
            fuse.Current == Fuse.State.Burning ? fuse.FrontWorld + Vector3.up * 0.05f : (Vector3?)null;

        public override float AirDrain => _gasOpen ? gasAirDrain : 1f;

        public override string FailReason =>
            _gasOpen && _diedDark ? "the lighter was shut when you fell. the gas just hung there" :
            _gasOpen && _gas < gasNeeded ? "not enough gas had leaked to go up" :
            fuse.Current == Fuse.State.Idle ? "the fuse was never lit" :
            fuse.Current == Fuse.State.Fizzled ? "the water in the bucket put the fire out" : null;

        void Start()
        {
            fuse.Finished += RaiseSolved;
            fuse.Fizzled += () => Say("the water in the bucket put it out");
            if (gasCylinder != null) gasCylinder.Shot += _ => Blow(true);
            _fogColor = RenderSettings.fogColor;
            _fogEnd = RenderSettings.fogEndDistance;
            if (gasHiss != null) gasHiss.volume = 0f;
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            var gun = Director != null ? Director.Gun : null;
            if (benchGun != null && benchGun.activeSelf && gun != null && Aim(eye, forward, benchGunPoint.position, 1.7f) > 0.86f)
                return new Option { prompt = "[E] take the revolver", act = TakeGun, gesture = Gesture.Grab };

            if (!_blown && !_busyValve && gasValve != null && Aim(eye, forward, gasValve.position, 1.5f) > 0.9f)
                return new Option
                {
                    prompt = _gasOpen ? "[E] close the gas valve" : "[E] open the gas valve",
                    act = () => StartCoroutine(TurnValve())
                };

            // The bucket first while it still sits on the powder and is what you're looking at: once the flame has
            // died against it, the fuse's front is right beside it and relighting there only drowns it again.
            if (bucket != null && BucketBlocksTrail())
            {
                float bucketAim = Aim(eye, forward, bucket.position + Vector3.up * 0.2f, 1.9f);
                float frontAim = Aim(eye, forward, fuse.FrontWorld, 1.9f);
                if (bucketAim > 0.8f && (fuse.Current == Fuse.State.Fizzled || bucketAim >= frontAim))
                    return new Option { prompt = "[E] push the bucket off the powder", act = ShoveBucket, gesture = Gesture.Push };
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

            if (!_blown && gun != null && gun.Owned && !gun.Drawn && gun.rounds > 0 && gasCylinder != null &&
                Aim(eye, forward, gasCylinder.transform.position + Vector3.up * 0.6f, 7f) > 0.985f)
                return new Option { prompt = "[Q] draw the revolver" };
            return null;
        }

        void TakeGun()
        {
            benchGun.SetActive(false);
            if (takeClip != null) AudioSource.PlayClipAtPoint(takeClip, benchGunPoint.position, 0.8f);
            Director.Gun.Give(2);
            Say("a revolver. two rounds in it\n[Q] draw / put away   [LMB] fire");
        }

        IEnumerator TurnValve()
        {
            _busyValve = true;
            if (valveClip != null) AudioSource.PlayClipAtPoint(valveClip, gasValve.position, 0.8f);
            Quaternion start = gasValve.localRotation;
            float dir = _gasOpen ? 1f : -1f;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                gasValve.localRotation = start * Quaternion.Euler(0f, dir * 300f * Mathf.SmoothStep(0f, 1f, t / 0.7f), 0f);
                yield return null;
            }
            gasValve.localRotation = start;
            _gasOpen = !_gasOpen;
            _busyValve = false;
            Say(_gasOpen ? "gas hisses out of the cylinder\nevery breath is shorter now" : "the hissing stops");
        }

        void Update()
        {
            // Already carrying one (brought in, or kept from how this attempt began): no second gun on the bench.
            if (benchGun != null && benchGun.activeSelf && Director != null && Director.Gun != null && Director.Gun.Owned)
                benchGun.SetActive(false);

            if (_gasOpen && !_blown) _gas += Time.deltaTime;

            // The gas shows: the air thickens and turns sickly yellow as the room fills.
            float thick = Mathf.Clamp01(_gas / 30f);
            if (!_blown)
            {
                RenderSettings.fogColor = Color.Lerp(_fogColor, new Color(0.13f, 0.14f, 0.05f), thick);
                RenderSettings.fogEndDistance = Mathf.Lerp(_fogEnd, 6f, thick);
            }
            if (gasHiss != null)
            {
                gasHiss.volume = Mathf.MoveTowards(gasHiss.volume, _gasOpen && !_blown ? 0.7f : 0f, Time.deltaTime * 2f);
                if (gasHiss.volume > 0f && !gasHiss.isPlaying) gasHiss.Play();
                if (gasHiss.volume <= 0f && gasHiss.isPlaying) gasHiss.Stop();
            }
        }

        // Falling with the lighter still burning drops a flame into a room full of gas.
        public override void PlayerDied(bool lit)
        {
            if (!_gasOpen || _blown) return;
            if (!lit) { _diedDark = true; return; }
            if (_gas < gasNeeded) return;
            StartCoroutine(GasIgnites());
        }

        IEnumerator GasIgnites()
        {
            _igniting = true;
            yield return new WaitForSeconds(1.6f);   // the lighter tumbles out of the hand and across the floor
            _igniting = false;
            Blow(false);
        }

        // The cylinder goes up: the blast runs down the pipe to the mechanism and the door comes open. Shot while you
        // stand in here, it takes you too.
        void Blow(bool shot)
        {
            if (_blown) return;
            _blown = true;
            _gasOpen = false;
            gasCylinder.gameObject.SetActive(false);
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogEndDistance = _fogEnd;
            RaiseSolved();
            if (shot && Director != null) Director.Kill("the blast took the door.\nand you with it.");
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
