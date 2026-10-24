using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // Room 3, the armory. The door's bolt is held home by a hopper of sand hanging up under the ceiling, out of reach,
    // pulling against an iron counterweight on the other side of the pulley. Empty the hopper and the iron wins: the
    // bolt slides back. The revolver in the locker has two rounds. Put one through the right hopper and the sand runs
    // out through the hole, slower than your air. A second hopper only works the vent shutter, and the lamp, once shot,
    // stays dark.
    public class HopperPuzzle : RoomPuzzle
    {
        public Revolver revolver;
        public Transform lockerDoor;           // swings open about its local Y
        public Transform lockerPoint;
        public GameObject revolverInLocker;
        public int rounds = 2;

        public Shootable boltHopper;           // the one on the bolt's chain
        public Transform boltHopperBody;       // rises as it empties
        public Transform boltWeight;           // the iron counterweight; drops as the hopper empties
        public Transform bolt;                 // slides sideways out of the door once the hopper is light enough
        public Vector3 boltTravel = new Vector3(-0.25f, 0f, 0f);
        public Shootable ventHopper;
        public Transform ventHopperBody;
        public Transform ventShutter;          // tips open about its local X when the vent hopper empties
        public Shootable lamp;
        public Light lampLight;
        public GameObject lampGlow;
        public GameObject boltStream, ventStream;   // sand pouring from each bullet hole
        public Transform boltPile, ventPile;        // heaps growing on the floor under them
        public AudioSource boltPour, ventPour;
        public AudioClip lockerClip, takeClip, hitClip, glassClip, clunkClip;

        public float drainSeconds = 80f;

        bool _lockerOpen, _taken, _boltHoled, _ventHoled, _solved, _busy, _saidSpent;
        float _boltSand = 1f, _ventSand = 1f;
        Vector3 _boltHopperRest, _boltWeightRest, _boltRest, _ventHopperRest;
        Quaternion _shutterRest;

        // The sand is running out of the right hopper: worth waiting for.
        public override bool Working => _boltHoled && !_solved;

        public override Vector3? Focus => _boltHoled && !_solved ? boltHopperBody.position + Vector3.down * 0.6f : (Vector3?)null;

        public override string FailReason =>
            _solved || _boltHoled ? null :
            !_taken ? "the revolver stayed in the locker" :
            _ventHoled ? "the sand ran from the wrong hopper" : "the bolt's hopper was still full of sand";

        void Awake()
        {
            _boltHopperRest = boltHopperBody.localPosition;
            _boltWeightRest = boltWeight.localPosition;
            _boltRest = bolt.localPosition;
            _ventHopperRest = ventHopperBody.localPosition;
            _shutterRest = ventShutter.localRotation;
            boltStream.SetActive(false);
            ventStream.SetActive(false);
            boltPile.gameObject.SetActive(false);
            ventPile.gameObject.SetActive(false);
            boltHopper.Shot += p => Hole(true, p);
            ventHopper.Shot += p => Hole(false, p);
            lamp.Shot += _ => ShootLamp();
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            if (_busy) return null;
            if (Aim(eye, forward, lockerPoint.position, 1.7f) > 0.84f)
            {
                if (!_lockerOpen) return new Option { prompt = "[E] open the locker", act = () => StartCoroutine(OpenLocker()) };
                if (!_taken) return new Option { prompt = "[E] take the revolver", act = TakeRevolver };
            }
            // Either hopper: the hint shouldn't tell you which one is on the bolt.
            if (revolver != null && revolver.Owned && !revolver.Drawn && revolver.rounds > 0 &&
                (Aim(eye, forward, boltHopperBody.position, 6f) > 0.97f || Aim(eye, forward, ventHopperBody.position, 6f) > 0.97f))
                return new Option { prompt = "[Q] draw the revolver" };
            return null;
        }

        IEnumerator OpenLocker()
        {
            _busy = true;
            Play(lockerClip, 0.8f, lockerDoor.position);
            Quaternion from = lockerDoor.localRotation, to = from * Quaternion.Euler(0f, -110f, 0f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                lockerDoor.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.8f));
                yield return null;
            }
            lockerDoor.localRotation = to;
            _lockerOpen = true;
            _busy = false;
        }

        void TakeRevolver()
        {
            _taken = true;
            revolverInLocker.SetActive(false);
            Play(takeClip, 0.8f, lockerPoint.position);
            if (revolver != null) revolver.Give(rounds);
            Say("a revolver. two rounds left in it\n[Q] draw / put away   [LMB] fire");
        }

        void Hole(bool boltSide, Vector3 point)
        {
            Play(hitClip, 1f, point);
            if (boltSide && !_boltHoled) { _boltHoled = true; Say("sand starts to run out of the hopper"); }
            if (!boltSide && !_ventHoled) { _ventHoled = true; Say("sand starts to run out of the hopper"); }
        }

        void ShootLamp()
        {
            if (!lampLight.enabled) return;
            Play(glassClip, 1f, lampLight.transform.position);
            lampLight.enabled = false;
            if (lampGlow != null) lampGlow.SetActive(false);
        }

        void Update()
        {
            // Both rounds gone and the bolt's hopper still full: nothing in here can open the door now.
            if (!_saidSpent && _taken && revolver != null && revolver.rounds <= 0 && !_boltHoled)
            {
                _saidSpent = true;
                Say("no rounds left\n[R] try again");
            }

            float dt = Time.deltaTime / drainSeconds;
            if (_boltHoled && _boltSand > 0f) _boltSand = Mathf.Max(0f, _boltSand - dt);
            if (_ventHoled && _ventSand > 0f) _ventSand = Mathf.Max(0f, _ventSand - dt);

            // An emptier hopper is lighter: it rides up and the iron on the other side comes down.
            float boltLift = Mathf.SmoothStep(0f, 1f, 1f - _boltSand);
            boltHopperBody.localPosition = _boltHopperRest + Vector3.up * 0.35f * boltLift;
            boltWeight.localPosition = _boltWeightRest + Vector3.down * 0.35f * boltLift;
            // The bolt only gives once the hopper is nearly empty.
            float give = Mathf.Clamp01((boltLift - 0.85f) / 0.15f);
            bolt.localPosition = _boltRest + boltTravel * give;
            float ventLift = Mathf.SmoothStep(0f, 1f, 1f - _ventSand);
            ventHopperBody.localPosition = _ventHopperRest + Vector3.up * 0.35f * ventLift;
            ventShutter.localRotation = _shutterRest * Quaternion.Euler(70f * Mathf.Clamp01((ventLift - 0.85f) / 0.15f), 0f, 0f);

            Pour(boltStream, boltPile, boltPour, _boltHoled && _boltSand > 0f, 1f - _boltSand);
            Pour(ventStream, ventPile, ventPour, _ventHoled && _ventSand > 0f, 1f - _ventSand);

            if (!_solved && (give >= 0.999f || _boltSand <= 0f))
            {
                _solved = true;
                Play(clunkClip, 1f, bolt.position);
                RaiseSolved();
            }
        }

        static void Pour(GameObject stream, Transform pile, AudioSource sound, bool pouring, float poured)
        {
            stream.SetActive(pouring);
            if (poured > 0.01f)
            {
                pile.gameObject.SetActive(true);
                float s = Mathf.Sqrt(poured);
                pile.localScale = new Vector3(0.2f + s * 0.8f, 0.1f + s * 0.9f, 0.2f + s * 0.8f);
            }
            if (sound != null)
            {
                if (pouring && !sound.isPlaying) sound.Play();
                if (!pouring && sound.isPlaying) sound.Stop();
            }
        }

        // For the autoplay test: take the revolver and put a round through the right hopper, with fast sand.
        public void Autosolve()
        {
            drainSeconds = 8f;
            _lockerOpen = true;
            TakeRevolver();
            boltHopper.Hit(boltHopperBody.position);
        }

        static void Play(AudioClip clip, float volume, Vector3 at)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
        }
    }
}
