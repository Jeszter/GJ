using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // Room 2, the pump room. The door's bolt hangs on a chain over two pulleys to a counterweight bucket. Fill the
    // bucket and it sinks, and the chain lifts the bolt out of its hasp. Water comes from the old cistern through
    // a valve, down a swinging chute that points at the floor drain to begin with; the chain has slipped off its hook.
    // The cistern only holds so much and pours slower than you can hold your breath, so the order matters:
    // hook the chain, swing the chute over the bucket, then open the valve, and let the room finish without you.
    // Two other ways: a round through the rusted hasp lets the bolt go at once (loud, and a round you won't get back),
    // or hang your own weight on the bucket. It sinks, but slower than you can hold on breathing: you have to still be
    // hanging there when you stop.
    public class CisternPuzzle : RoomPuzzle
    {
        public Shootable hasp;
        public GameObject haspPiece;
        public AudioClip snapClip;
        public float hangSeconds = 110f;   // hanging on it this long brings the bucket all the way down
        public Transform valveWheel;      // spins about its local Y while turning
        public Transform chute;           // swings about its local Y between the two yaws below
        public float drainYaw = -67f;
        public float bucketYaw = 51f;
        public Transform chuteTip;        // interaction point and where its stream starts
        public Transform counterweight;   // the hanging bucket; sinks as it fills
        public float sinkDistance = 0.45f;
        public Transform bucketWater;     // water disc inside it, rises with the fill
        public float waterLow = 0.03f, waterHigh = 0.38f;
        public Transform chainToBucket;   // chain from the pulley down to the bucket, stretched along its local Y
        public Transform pulleyLow;       // the pulley over the bucket, for the chain length
        public Transform tankLevel;       // water column in the sight glass, scaled along Y
        public Transform bolt;            // slides up out of the door's hasp as the bucket sinks (only when hooked)
        public GameObject looseChain;
        public Transform hookedChain;     // chain from the door-side pulley down to the bolt, stretched along its local Y
        public Transform hookPoint;       // the loose chain end, where the player hooks it
        public GameObject streamFromSpout, streamToBucket, streamToDrain;
        public Transform drainPuddle;     // grows while water is wasted
        public AudioSource flowSource;
        public AudioClip valveClip;
        public AudioClip chainClip;
        public AudioClip clunkClip;

        public float tankSeconds = 105f;   // a full cistern pours this long
        public float fillSeconds = 80f;    // pouring this long into the bucket is enough to pull the bolt

        bool _valveOpen, _onBucket, _hooked, _solved, _busyValve, _busyChute;
        bool _hanging, _haspShot;
        float _tank, _fill, _wasted, _hang;
        Vector3 _hangSpot, _headRest;
        bool _saidDrain, _saidFilling, _saidFull, _saidEmpty, _saidShort;
        Vector3 _weightTop, _boltRest;
        float _chainBase, _hookBase;

        bool Flowing => _valveOpen && _tank > 0f;
        bool Full => _fill >= fillSeconds || _hang >= 1f;
        bool IntoBucket => Flowing && _onBucket && !_busyChute;

        // Still worth waiting for: the bucket is going down (filling, or with you hanging on it) and will pull the bolt.
        public override bool Working => !_solved && _hooked && ((IntoBucket && EnoughLeft) || _hanging);

        public override Vector3? Focus => Working ? counterweight.position + Vector3.up * 0.3f : (Vector3?)null;

        // The first thing in the way, in the order the water has to go: out of the valve, down the chute, enough of it.
        public override string FailReason =>
            _solved ? null :
            _hanging && !_hooked ? "you hung on the bucket, but the chain was not on the bolt" :
            !_valveOpen && _tank > 0f ? "the valve was shut" :
            !_onBucket ? "the water was running into the drain" :
            !EnoughLeft ? "there wasn't enough water left in the cistern" :
            !_hooked ? "the chain was not on the bolt" : null;

        bool EnoughLeft => _tank >= fillSeconds - _fill;

        void Awake()
        {
            _tank = tankSeconds;
            _weightTop = counterweight.localPosition;
            _boltRest = bolt.localPosition;
            _chainBase = pulleyLow.position.y - counterweight.position.y;
            _hookBase = hookedChain.localScale.y;
            chute.localRotation = Quaternion.Euler(0f, drainYaw, 0f);
            looseChain.SetActive(true);
            hookedChain.gameObject.SetActive(false);
            if (flowSource != null) flowSource.volume = 0f;
            if (hasp != null) hasp.Shot += _ => SnapHasp();
            UpdateVisuals();
        }

        // A round through the rusted hasp: it snaps, the bolt drops free and the door swings.
        void SnapHasp()
        {
            if (_solved || _haspShot) return;
            _haspShot = true;
            _solved = true;
            if (haspPiece != null) haspPiece.SetActive(false);
            Play(snapClip, 1f, hasp.transform.position);
            Say("the hasp snaps. the bolt drops free");
            RaiseSolved();
        }

        // Grab the bail and lift your feet: the bucket starts to give, a hand's width at a time.
        void Hang()
        {
            var body = Director.Controller.transform;
            _hanging = true;
            _headRest = Director.Head.localPosition;
            Vector3 away = body.position - counterweight.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            _hangSpot = new Vector3(counterweight.position.x, body.position.y, counterweight.position.z) + away.normalized * 0.42f;
            body.position = _hangSpot;
            Director.Controller.canMove = false;
            Play(chainClip, 0.9f, counterweight.position);
            Say(_hooked ? "the bucket gives under you. slowly.\ntoo slowly to wait it out breathing"
                        : "the bucket gives under you.\nbut the chain is not on the bolt");
        }

        void LetGo()
        {
            _hanging = false;
            Director.Controller.canMove = true;
            Director.Head.localPosition = _headRest;
            Play(clunkClip, 0.6f, counterweight.position);
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            Option? best = null;
            float bestAim = 0.82f;
            void Consider(Vector3 point, float range, Option option)
            {
                float aim = Aim(eye, forward, point, range);
                if (aim <= bestAim) return;
                bestAim = aim;
                best = option;
            }

            if (!_busyValve)
                Consider(valveWheel.position, 1.6f, new Option
                {
                    prompt = _valveOpen ? "[E] close the valve" : "[E] open the valve",
                    act = () => StartCoroutine(TurnValve())
                });
            if (!_busyChute)
                Consider(chuteTip.position, 1.9f, new Option
                {
                    prompt = "[E] swing the chute",
                    act = () => StartCoroutine(SwingChute()),
                    gesture = Gesture.Push
                });
            if (!_hooked)
                Consider(hookPoint.position, 1.6f, new Option
                {
                    prompt = "[E] hook the chain onto the bolt",
                    act = Hook,
                    gesture = Gesture.Grab
                });
            if (!_solved && Director != null)
            {
                if (_hanging) return new Option { prompt = "[E] let go", act = LetGo };
                Consider(counterweight.position + Vector3.down * 0.3f, 1.5f, new Option
                {
                    prompt = "[E] hang your weight on the bucket",
                    act = Hang
                });
            }
            var gun = Director != null ? Director.Gun : null;
            if (best == null && !_solved && hasp != null && gun != null && gun.Owned && !gun.Drawn && gun.rounds > 0 &&
                Aim(eye, forward, hasp.transform.position, 6f) > 0.985f)
                return new Option { prompt = "[Q] draw the revolver" };
            return best;
        }

        IEnumerator TurnValve()
        {
            _busyValve = true;
            Play(valveClip, 0.8f, valveWheel.position);
            Quaternion start = valveWheel.localRotation;
            float dir = _valveOpen ? 1f : -1f;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                valveWheel.localRotation = start * Quaternion.Euler(0f, dir * 360f * Mathf.SmoothStep(0f, 1f, t / 0.9f), 0f);
                yield return null;
            }
            valveWheel.localRotation = start;
            _valveOpen = !_valveOpen;
            _busyValve = false;
            if (_valveOpen && _tank <= 0f) Say("the cistern is empty");
        }

        IEnumerator SwingChute()
        {
            _busyChute = true;
            float from = _onBucket ? bucketYaw : drainYaw, to = _onBucket ? drainYaw : bucketYaw;
            Play(clunkClip, 0.6f, chuteTip.position);
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                chute.localRotation = Quaternion.Euler(0f, Mathf.LerpAngle(from, to, Mathf.SmoothStep(0f, 1f, t / 0.7f)), 0f);
                yield return null;
            }
            chute.localRotation = Quaternion.Euler(0f, to, 0f);
            _onBucket = !_onBucket;
            _busyChute = false;
            Play(clunkClip, 0.8f, chuteTip.position);
        }

        void Hook()
        {
            _hooked = true;
            looseChain.SetActive(false);
            hookedChain.gameObject.SetActive(true);
            Play(chainClip, 0.9f, hookPoint.position);
            Say("the chain is on the bolt");
        }

        void Update()
        {
            // Hanging: the bucket sinks under you; let go and it rides back up.
            if (_hanging) _hang = Mathf.Min(1f, _hang + Time.deltaTime / hangSeconds);
            else if (_hang > 0f && !_solved) _hang = Mathf.Max(0f, _hang - Time.deltaTime / 4f);
            if (_hanging && Director != null && Director.Alive)
            {
                // Knees up off the floor, the body going down with the bucket.
                float drop = 0.35f + sinkDistance * Smooth(_hang) * 0.6f;
                Director.Head.localPosition = _headRest + Vector3.down * drop;
                Director.Controller.transform.position = _hangSpot;
            }

            if (Flowing)
            {
                _tank = Mathf.Max(0f, _tank - Time.deltaTime);
                if (IntoBucket) _fill = Mathf.Min(fillSeconds, _fill + Time.deltaTime);
                else if (!_busyChute) _wasted += Time.deltaTime;
            }

            if (Flowing && !_onBucket && !_busyChute && _wasted > 1.2f && !_saidDrain) { _saidDrain = true; Say("the water is running into the drain"); }
            if (IntoBucket && _fill > 1.5f && !_saidFilling) { _saidFilling = true; Say("the bucket is filling"); }
            if (_valveOpen && _tank <= 0f && !Full && !_saidEmpty) { _saidEmpty = true; Say("the cistern is empty"); }
            if (Full && !_hooked && !_saidFull) { _saidFull = true; Say("the bucket is down. the chain is not on the bolt"); }
            // Too much went down the drain: the bucket can't fill any more, whatever you do now.
            if (!Full && !EnoughLeft && !_saidShort) { _saidShort = true; Say("not enough water left to fill the bucket\n[R] try again"); }

            if (!_solved && _hooked && Full)
            {
                _solved = true;
                Play(clunkClip, 1f, bolt.position);
                RaiseSolved();
            }

            UpdateVisuals();
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        void UpdateVisuals()
        {
            float fill = Mathf.Clamp01(_fill / fillSeconds);
            // Water or a body: whichever is pulling harder sets how far the bucket has gone down.
            float sink = Mathf.Max(Smooth(fill), Smooth(_hang));
            counterweight.localPosition = _weightTop + Vector3.down * sinkDistance * sink;
            bucketWater.localPosition = new Vector3(0f, Mathf.Lerp(waterLow, waterHigh, fill), 0f);
            bucketWater.gameObject.SetActive(_fill > 0.05f);
            chainToBucket.localScale = new Vector3(1f, _chainBase + sinkDistance * sink, 1f);

            var level = tankLevel.localScale;
            level.y = Mathf.Max(0.001f, _tank / tankSeconds);
            tankLevel.localScale = level;

            // Hooked, the bucket's drop comes out of the chain on the door side: the bolt rises by as much.
            float rise = _hooked ? sinkDistance * sink : 0f;
            bolt.localPosition = _boltRest + Vector3.up * rise;
            hookedChain.localScale = new Vector3(1f, Mathf.Max(0.01f, _hookBase - rise), 1f);

            streamFromSpout.SetActive(Flowing);
            streamToBucket.SetActive(IntoBucket);
            streamToDrain.SetActive(Flowing && !_onBucket && !_busyChute);
            float puddle = Mathf.Clamp01(_wasted / 25f);
            drainPuddle.localScale = new Vector3(0.3f + puddle * 1.1f, 1f, 0.3f + puddle * 1.1f);
            drainPuddle.gameObject.SetActive(_wasted > 0.3f);

            if (flowSource != null)
            {
                flowSource.volume = Mathf.MoveTowards(flowSource.volume, Flowing ? 0.75f : 0f, Time.deltaTime * 2f);
                if (flowSource.volume > 0f && !flowSource.isPlaying) flowSource.Play();
                if (flowSource.volume <= 0f && flowSource.isPlaying) flowSource.Stop();
            }
        }

        static void Play(AudioClip clip, float volume, Vector3 at)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
        }
    }
}
