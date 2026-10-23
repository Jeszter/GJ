using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // Room 2, the pump room. The door's bolt hangs on a chain over two pulleys to a counterweight bucket. Fill the
    // bucket and it sinks, and the chain lifts the bolt out of its hasp. Water comes from the old cistern through
    // a valve, down a swinging chute that points at the floor drain to begin with; the chain has slipped off its hook.
    // The cistern only holds so much and pours slower than you can hold your breath, so the order matters:
    // hook the chain, swing the chute over the bucket, then open the valve, and let the room finish without you.
    public class CisternPuzzle : RoomPuzzle
    {
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
        float _tank, _fill, _wasted;
        bool _saidDrain, _saidFilling, _saidFull, _saidEmpty;
        Vector3 _weightTop, _boltRest;
        float _chainBase, _hookBase;

        bool Flowing => _valveOpen && _tank > 0f;
        bool Full => _fill >= fillSeconds;
        bool IntoBucket => Flowing && _onBucket && !_busyChute;

        // Still worth waiting for: water is going into the bucket, the bucket will pull the bolt, and there is enough
        // left in the cistern to finish the job.
        public override bool Working => !_solved && IntoBucket && _hooked && _tank >= fillSeconds - _fill;

        public override Vector3? Focus => Working ? counterweight.position + Vector3.up * 0.3f : (Vector3?)null;

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
            UpdateVisuals();
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
                    act = () => StartCoroutine(SwingChute())
                });
            if (!_hooked)
                Consider(hookPoint.position, 1.6f, new Option
                {
                    prompt = "[E] hook the chain onto the bolt",
                    act = Hook
                });
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

            if (!_solved && _hooked && Full)
            {
                _solved = true;
                Play(clunkClip, 1f, bolt.position);
                RaiseSolved();
            }

            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            float fill = Mathf.Clamp01(_fill / fillSeconds);
            float sink = fill * fill * (3f - 2f * fill);
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
