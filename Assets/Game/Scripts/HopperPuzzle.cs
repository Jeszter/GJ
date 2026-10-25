using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // Room 3, the armory. The door's bolt is held home by a hopper of sand hanging up under the ceiling, out of reach,
    // pulling against an iron counterweight on the other side of the pulley. Empty the hopper and the iron wins: the
    // bolt slides back. Three ways to empty it:
    //  - a round through it (the locker has the armory's revolver, or a box with one round if you brought your own);
    //    the sand runs out through the hole, slower than your air. The other hopper only works the vent shutter, and
    //    the lamp, once shot, stays dark.
    //  - the crowbar: reach up and pry the hopper's gate open. No round spent. It hangs in a wall cage that only opens
    //    while the plate across the room is held down, and nothing in here is heavy enough but a body: die on the
    //    plate, and the next try finds it held.
    //  - the rope lashing it to the chain: hold the lighter to it and it smoulders, longer than your air lasts, until
    //    it parts and the hopper falls.
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
        public GameObject lampBulb, lampStub;  // the lit bulb, and the stub of glass left once it is shot
        public GameObject boltStream, ventStream;   // sand pouring from each bullet hole
        public Transform boltPile, ventPile;        // heaps growing on the floor under them
        public AudioSource boltPour, ventPour;
        public AudioClip lockerClip, takeClip, hitClip, glassClip, clunkClip;

        public GameObject crowbar;             // in its wall cage until taken
        public Transform crowbarPoint;
        public HeldGate crowbarCage;           // only open while something holds the plate across the room down
        public Transform rope;                 // the lashing between the bolt hopper's bail and its chain
        public Transform hopperChain;          // stays hanging from the pulley when the hopper drops away
        public GameObject ropeEmber;           // glows while it smoulders
        public AudioClip pryClip, igniteClip, snapClip;
        public float ropeSeconds = 70f;        // longer than the air in here

        public float drainSeconds = 80f;

        bool _lockerOpen, _taken, _boltHoled, _ventHoled, _solved, _busy, _saidSpent;
        bool _hasCrowbar, _ropeLit, _ropeSnapped;
        float _boltSand = 1f, _ventSand = 1f, _rope, _fall, _dropDistance;
        Vector3 _boltHopperRest, _boltWeightRest, _boltRest, _ventHopperRest, _ropeScale, _dropFrom;
        Quaternion _shutterRest;

        // The sand is running out of the right hopper, or the rope is burning through: worth waiting for.
        public override bool Working => (_boltHoled || (_ropeLit && !_ropeSnapped) || (_ropeSnapped && _fall < 1f)) && !_solved;

        public override Vector3? Focus =>
            _ropeLit && !_ropeSnapped ? rope.position :
            _boltHoled && !_solved ? boltHopperBody.position + Vector3.down * 0.6f : (Vector3?)null;

        public override string FailReason =>
            _solved || _boltHoled || _ropeLit ? null :
            _ventHoled ? "the sand ran from the wrong hopper" :
            !_taken && !_hasCrowbar && (revolver == null || !revolver.Owned) ? "the revolver stayed in the locker" :
            "the bolt's hopper was still full of sand";

        void Awake()
        {
            _boltHopperRest = boltHopperBody.localPosition;
            _boltWeightRest = boltWeight.localPosition;
            _boltRest = bolt.localPosition;
            _ventHopperRest = ventHopperBody.localPosition;
            _shutterRest = ventShutter.localRotation;
            if (rope != null) _ropeScale = rope.localScale;
            boltStream.SetActive(false);
            ventStream.SetActive(false);
            boltPile.gameObject.SetActive(false);
            ventPile.gameObject.SetActive(false);
            if (ropeEmber != null) ropeEmber.SetActive(false);
            boltHopper.Shot += p => Hole(true, p);
            ventHopper.Shot += p => Hole(false, p);
            lamp.Shot += _ => ShootLamp();
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            if (_busy) return null;
            if (Aim(eye, forward, lockerPoint.position, 1.7f) > 0.84f)
            {
                if (!_lockerOpen) return new Option { prompt = "[E] open the locker", act = () => StartCoroutine(OpenLocker()), gesture = Gesture.Push };
                if (!_taken) return new Option { prompt = revolver != null && revolver.Owned ? "[E] take the box of rounds" : "[E] take the revolver", act = TakeRevolver, gesture = Gesture.Grab };
            }
            if (crowbar != null && crowbar.activeSelf && Aim(eye, forward, crowbarPoint.position, 1.8f) > 0.86f)
            {
                if (crowbarCage != null && !crowbarCage.IsOpen)
                    return new Option { prompt = "the cage is shut. something has to hold the plate down" };
                return new Option { prompt = "[E] take the crowbar", act = TakeCrowbar, gesture = Gesture.Grab };
            }

            // Up at the hopper itself: burn its rope, or pry its gate (either hopper: the hint shouldn't say which).
            if (!_ropeLit && rope != null && Aim(eye, forward, rope.position, 1.9f) > 0.93f)
            {
                if (!lit) return new Option { prompt = "[F] open the lighter" };
                return new Option { prompt = "[E] hold the flame to the rope", reach = true, act = LightRope };
            }
            if (_hasCrowbar)
            {
                Vector3 boltGate = boltHopperBody.position + Vector3.down * 0.45f, ventGate = ventHopperBody.position + Vector3.down * 0.45f;
                if (!_boltHoled && Aim(eye, forward, boltGate, 1.6f) > 0.9f)
                    return new Option { prompt = "[E] pry the hopper's gate open", act = () => Pry(true) };
                if (!_ventHoled && Aim(eye, forward, ventGate, 1.6f) > 0.9f)
                    return new Option { prompt = "[E] pry the hopper's gate open", act = () => Pry(false) };
            }
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

        // Brought your own from the workbench upstairs? Then all that's left here is a box with a single round in it.
        void TakeRevolver()
        {
            _taken = true;
            revolverInLocker.SetActive(false);
            Play(takeClip, 0.8f, lockerPoint.position);
            if (revolver == null) return;
            if (revolver.Owned)
            {
                revolver.AddRounds(1);
                Say("a box of shells. one round left in it");
                return;
            }
            revolver.Give(rounds);
            Say("a revolver. two rounds left in it\n[Q] draw / put away   [LMB] fire");
        }

        void TakeCrowbar()
        {
            _hasCrowbar = true;
            crowbar.SetActive(false);
            Play(clunkClip, 0.5f, crowbarPoint.position);
            Say("a crowbar. long enough to reach a hopper's gate");
        }

        void Pry(bool boltSide)
        {
            Play(pryClip, 1f, (boltSide ? boltHopperBody : ventHopperBody).position);
            Say("the gate grinds open. sand starts to run");
            if (boltSide) _boltHoled = true;
            else _ventHoled = true;
        }

        void LightRope()
        {
            _ropeLit = true;
            if (ropeEmber != null) ropeEmber.SetActive(true);
            Play(igniteClip, 0.7f, rope.position);
            Say("the rope catches and starts to smoulder");
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
            if (lampBulb != null) lampBulb.SetActive(false);
            if (lampStub != null) lampStub.SetActive(true);
        }

        void Update()
        {
            // Both rounds gone and nothing else started on the bolt's hopper.
            if (!_saidSpent && _taken && revolver != null && revolver.rounds <= 0 && !_boltHoled && !_ropeLit)
            {
                _saidSpent = true;
                Say("no rounds left");
            }

            // The rope smoulders through, then parts: the hopper drops and the iron on the other side wins at once.
            if (_ropeLit && !_ropeSnapped)
            {
                _rope += Time.deltaTime / ropeSeconds;
                if (ropeEmber != null) ropeEmber.transform.localScale = Vector3.one * (0.8f + Mathf.PerlinNoise(Time.time * 3f, 0f) * 0.5f);
                if (rope != null) rope.localScale = new Vector3(_ropeScale.x * (1f - _rope * 0.5f), _ropeScale.y, _ropeScale.z * (1f - _rope * 0.5f));
                if (_rope >= 1f)
                {
                    _ropeSnapped = true;
                    if (rope != null) rope.gameObject.SetActive(false);
                    if (ropeEmber != null) ropeEmber.SetActive(false);
                    if (hopperChain != null) hopperChain.SetParent(boltHopperBody.parent, true);
                    // Down until the funnel's tip is on the floor.
                    _dropFrom = boltHopperBody.localPosition;
                    _dropDistance = boltHopperBody.position.y - 0.47f;
                    Play(snapClip, 1f, boltHopperBody.position);
                }
            }
            if (_ropeSnapped && _fall < 1f) _fall = Mathf.Min(1f, _fall + Time.deltaTime / 0.5f);

            float dt = Time.deltaTime / drainSeconds;
            if (_boltHoled && _boltSand > 0f) _boltSand = Mathf.Max(0f, _boltSand - dt);
            if (_ventHoled && _ventSand > 0f) _ventSand = Mathf.Max(0f, _ventSand - dt);

            // An emptier hopper is lighter: it rides up and the iron on the other side comes down. Cut loose, it is
            // no weight at all on the chain, and falls to the floor.
            float boltLift = _ropeSnapped ? 1f : Mathf.SmoothStep(0f, 1f, 1f - _boltSand);
            if (_ropeSnapped) boltHopperBody.localPosition = _dropFrom + Vector3.down * _dropDistance * (_fall * _fall);
            else boltHopperBody.localPosition = _boltHopperRest + Vector3.up * 0.35f * boltLift;
            boltWeight.localPosition = _boltWeightRest + Vector3.down * 0.35f * boltLift;
            // The bolt only gives once the hopper is nearly empty.
            float give = Mathf.Clamp01((boltLift - 0.85f) / 0.15f);
            bolt.localPosition = _boltRest + boltTravel * give;
            float ventLift = Mathf.SmoothStep(0f, 1f, 1f - _ventSand);
            ventHopperBody.localPosition = _ventHopperRest + Vector3.up * 0.35f * ventLift;
            ventShutter.localRotation = _shutterRest * Quaternion.Euler(70f * Mathf.Clamp01((ventLift - 0.85f) / 0.15f), 0f, 0f);

            Follow(boltStream, boltHopperBody, boltPile);
            Follow(ventStream, ventHopperBody, ventPile);
            Pour(boltStream, boltPile, boltPour, _boltHoled && _boltSand > 0f && !_ropeSnapped, 1f - _boltSand);
            Pour(ventStream, ventPile, ventPour, _ventHoled && _ventSand > 0f, 1f - _ventSand);

            if (!_solved && (give >= 0.999f || _boltSand <= 0f))
            {
                _solved = true;
                Play(clunkClip, 1f, bolt.position);
                RaiseSolved();
            }
        }

        // The sand falls from the funnel's tip, wherever the hopper has risen to, down to the top of its heap.
        static void Follow(GameObject stream, Transform hopper, Transform pile)
        {
            Vector3 tip = hopper.position + Vector3.down * 0.47f;
            float bottom = pile.gameObject.activeSelf ? pile.position.y + 0.25f * pile.localScale.y : 0f;
            stream.transform.position = tip;
            stream.transform.localScale = new Vector3(1f, Mathf.Max(0.05f, (tip.y - bottom) / 1.85f), 1f);
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
