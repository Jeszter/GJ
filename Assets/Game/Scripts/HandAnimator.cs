using System.Collections;
using GJ.PSX;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GJ.Game
{
    // Keeps the lighter hand alive: it lags behind the mouse, bobs with your steps, breathes when still and shakes
    // when the air runs low; the fingers shift on the case, now and then the index finger taps it, and they shake
    // too. Lighting the Zippo is a full little act (the thumb lifts and flicks the lid, comes down on the wheel, a
    // spark, the flame catches, the fist squeezes), snapping it shut smothers the flame, and reaching for the fuse
    // dips the hand toward the floor.
    public class HandAnimator : MonoBehaviour
    {
        public CharacterController body;
        public Transform[] fingers;     // index, middle, ring, pinky, three joints each, knuckle first; curl about X
        public Transform[] thumb;       // base, middle, tip
        public Transform lid;
        public Transform wheel;
        public Transform spark;         // tiny glow shown for a frame or two when the wheel is struck
        public FlameFlicker flame;
        public AudioSource audioSource;
        public AudioClip lidClip;
        public AudioClip closeClip;
        public AudioClip strikeClip;

        public float sway = 0.0007f;
        public float bob = 0.012f;

        // 0..1, set from outside: how hard the hand trembles (running out of air).
        public float Tremble { get; set; }
        public bool Lit { get; private set; }
        // Set while the revolver is out: the lighter hand drops out of view.
        public bool Lowered { get; set; }
        // No lighter in it at all (the graveyard): a bare fist, and nothing to light.
        public bool Bare { get; private set; }

        Vector3 _restPos;
        Quaternion _restRot;
        Quaternion[] _fingerRest, _thumbRest;
        Vector2 _swayNow;
        float _bobPhase;
        float _reach;          // 0..1 how far the hand dips toward the floor
        float _squeeze;        // 0..1 extra grip
        float _lower;          // 0..1 how far the hand has dropped out of view
        float _thumbLift;      // 0..1 thumb up off the lighter, to flick the lid
        float _thumbPress;     // 0..1 thumb rolled down hard on the wheel
        float _tap, _nextTap = 3f;   // the index finger lifting off the case and dropping back, every few seconds
        float _thrust;         // 0..1+ hand driven forward (grabbing, pushing, punching)
        float _open;           // 0..1 fingers spread off the case (grabbing, pushing)
        float _palm;           // 0..1 wrist tipped back, palm out (pushing)
        Coroutine _act;
        System.Action _finishAct;  // lands the running act at once if it gets cut short

        const float LidOpen = 105f;

        void Awake()
        {
            _restPos = transform.localPosition;
            _restRot = transform.localRotation;
            _fingerRest = RestOf(fingers);
            _thumbRest = RestOf(thumb);
        }

        static Quaternion[] RestOf(Transform[] joints)
        {
            var rest = new Quaternion[joints != null ? joints.Length : 0];
            for (int i = 0; i < rest.Length; i++) rest[i] = joints[i].localRotation;
            return rest;
        }

        void OnEnable()
        {
            transform.localPosition = _restPos;
            transform.localRotation = _restRot;
        }

        // Hiding the hand (pause, death) kills its coroutines; finish the act instead of leaving it half done.
        void OnDisable()
        {
            if (_act != null) StopCoroutine(_act);
            _act = null;
            _finishAct?.Invoke();
            _finishAct = null;
            _reach = 0f;
            _squeeze = 0f;
            _thumbLift = 0f;
            _thumbPress = 0f;
            _thrust = _open = _palm = 0f;
        }

        // Start dark (lid shut, no flame), then light up on our own.
        public void LightUp(float delay = 0.4f)
        {
            if (Bare) return;
            if (_act != null) StopCoroutine(_act);
            _act = StartCoroutine(LightRoutine(delay));
        }

        // Shut a burning lighter, or open and strike a closed one.
        public void Toggle()
        {
            if (_act != null || Bare) return;
            _act = StartCoroutine(Lit ? CloseRoutine() : LightRoutine(0f));
        }

        public void SetUnlit()
        {
            Lit = false;
            if (lid != null) lid.localRotation = Quaternion.identity;
            if (flame != null) flame.strength = 0f;
            if (spark != null) spark.gameObject.SetActive(false);
        }

        void SetLit()
        {
            Lit = true;
            if (lid != null) lid.localRotation = Quaternion.Euler(0f, 0f, LidOpen);
            if (flame != null) flame.strength = 1f;
            if (spark != null) spark.gameObject.SetActive(false);
        }

        IEnumerator LightRoutine(float delay)
        {
            SetUnlit();
            _finishAct = SetLit;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // The thumb comes up off the case...
            for (float t = 0f; t < 0.12f; t += Time.deltaTime)
            {
                _thumbLift = Mathf.SmoothStep(0f, 1f, t / 0.12f);
                yield return null;
            }
            // ...and flicks the lid over: fast at first, then it bounces against the hinge.
            Play(lidClip, 0.9f);
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                float k = t / 0.22f;
                float overshoot = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f;
                if (lid != null) lid.localRotation = Quaternion.Euler(0f, 0f, LidOpen * Mathf.Min(1f, k * overshoot));
                _thumbLift = 1f - k;
                _thumbPress = k * 0.4f;
                _squeeze = k;
                yield return null;
            }
            if (lid != null) lid.localRotation = Quaternion.Euler(0f, 0f, LidOpen);
            _thumbLift = 0f;
            yield return new WaitForSeconds(0.12f);

            // Roll the wheel down with the thumb: a spark, and the wick catches.
            Play(strikeClip, 1f);
            Quaternion wheelStart = wheel != null ? wheel.localRotation : Quaternion.identity;
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                if (wheel != null) wheel.localRotation = wheelStart * Quaternion.Euler(0f, -900f * t, 0f);
                if (spark != null) spark.gameObject.SetActive(t > 0.05f && t < 0.11f);
                _thumbPress = Mathf.Lerp(0.4f, 1f, t / 0.14f);
                _squeeze = 1f;
                yield return null;
            }
            if (spark != null) spark.gameObject.SetActive(false);

            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                if (flame != null) flame.strength = Mathf.SmoothStep(0f, 1f, k) * (1f + Mathf.Sin(k * Mathf.PI) * 0.4f);
                _thumbPress = 1f - k;
                _squeeze = 1f - k;
                yield return null;
            }
            if (flame != null) flame.strength = 1f;
            _thumbPress = 0f;
            _squeeze = 0f;
            Lit = true;
            _finishAct = null;
            _act = null;
        }

        // Thumb pushes the lid back over: the chimney is covered and the flame dies under it with a dull clack.
        IEnumerator CloseRoutine()
        {
            Lit = false;
            _finishAct = SetUnlit;
            Play(closeClip != null ? closeClip : lidClip, 0.8f);
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                float k = t / 0.16f;
                if (lid != null) lid.localRotation = Quaternion.Euler(0f, 0f, LidOpen * (1f - k * k));
                if (flame != null) flame.strength = Mathf.Clamp01(1f - k / 0.7f);
                _squeeze = Mathf.Sin(k * Mathf.PI) * 0.7f;
                _thumbLift = Mathf.Sin(k * Mathf.PI) * 0.6f;
                yield return null;
            }
            SetUnlit();
            _squeeze = 0f;
            _thumbLift = 0f;
            _finishAct = null;
            _act = null;
        }

        // Dip toward the fuse and back, as if holding the flame to the powder.
        public void Reach()
        {
            if (_act != null) return;
            _act = StartCoroutine(ReachRoutine());
        }

        IEnumerator ReachRoutine()
        {
            _finishAct = null;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float k = t / 0.9f;
                _reach = Mathf.Sin(k * Mathf.PI);
                _squeeze = _reach * 0.6f;
                yield return null;
            }
            _reach = 0f;
            _squeeze = 0f;
            _act = null;
        }

        // Out and take it: the hand goes forward with the fingers spread, closes on the thing and comes back.
        public void Grab()
        {
            if (_act != null) return;
            _act = StartCoroutine(Move(0.16f, 0.1f, 0.22f, 1f, 1f, 0f, true));
        }

        // Palm out against a door or a weight and shove.
        public void Push()
        {
            if (_act != null) return;
            _act = StartCoroutine(Move(0.2f, 0.12f, 0.3f, 1.1f, 0.8f, 1f, false));
        }

        // A short punch, the fist squeezed shut on the way out.
        public void Jab()
        {
            if (_act != null) return;
            _act = StartCoroutine(Move(0.06f, 0.04f, 0.16f, 1.5f, 0f, 0f, false));
        }

        public bool Busy => _act != null;

        // The lighter put away and the fingers open: a bare hand (for when something takes it by the wrist).
        public void Empty(bool empty)
        {
            if (lid != null) lid.parent.gameObject.SetActive(!empty);
            _open = empty ? 0.8f : 0f;
        }

        // The lighter gone for good and the hand closed into a fist.
        public void MakeBare()
        {
            if (_act != null) StopCoroutine(_act);
            _act = null;
            _finishAct = null;
            SetUnlit();
            Bare = true;
            if (lid != null) lid.parent.gameObject.SetActive(false);
            _open = 0f;
        }

        // Out for "outT" seconds (reaching thrust, opening the fingers by open and tipping the palm by palm), hold for
        // "holdT" (closing them if close), back for "backT".
        IEnumerator Move(float outT, float holdT, float backT, float thrust, float open, float palm, bool close)
        {
            _finishAct = null;
            for (float t = 0f; t < outT; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / outT);
                _thrust = thrust * k;
                _open = open * k;
                _palm = palm * k;
                _squeeze = open <= 0f ? k : 0f;
                yield return null;
            }
            for (float t = 0f; t < holdT; t += Time.deltaTime)
            {
                if (close) { _open = open * (1f - t / holdT); _squeeze = t / holdT; }
                yield return null;
            }
            float t0 = _thrust, o0 = _open, p0 = _palm, s0 = _squeeze;
            for (float t = 0f; t < backT; t += Time.deltaTime)
            {
                float k = 1f - Mathf.SmoothStep(0f, 1f, t / backT);
                _thrust = t0 * k;
                _open = o0 * k;
                _palm = p0 * k;
                _squeeze = s0 * k;
                yield return null;
            }
            _thrust = _open = _palm = _squeeze = 0f;
            _act = null;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            // Lag behind the view a little.
            Vector2 look = Vector2.zero;
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked && !FirstPersonController.Frozen)
                look = Mouse.current.delta.ReadValue();
            Vector2 target = Vector2.ClampMagnitude(-look * sway, 0.03f);
            _swayNow = Vector2.Lerp(_swayNow, target, 1f - Mathf.Exp(-10f * dt));

            // Step bob from actual movement.
            float speed = body != null ? new Vector2(body.velocity.x, body.velocity.z).magnitude : 0f;
            _bobPhase += dt * speed * 4.2f;
            float walk = Mathf.Clamp01(speed / 2.2f);
            Vector3 bobOffset = new Vector3(Mathf.Cos(_bobPhase) * bob * 0.6f, -Mathf.Abs(Mathf.Sin(_bobPhase)) * bob, 0f) * walk;

            // Breathing when still; trembling when choking.
            float breath = Mathf.Sin(Time.time * 1.7f) * 0.0035f * (1f - walk);
            Vector3 shake = Tremble > 0f
                ? new Vector3(Mathf.PerlinNoise(Time.time * 18f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 18f) - 0.5f, 0f) * 0.012f * Tremble
                : Vector3.zero;

            Vector3 reach = new Vector3(-0.05f, -0.07f, 0.09f) * _reach;
            _lower = Mathf.MoveTowards(_lower, Lowered ? 1f : 0f, dt * 4f);
            reach += new Vector3(0.04f, -0.3f, 0f) * Mathf.SmoothStep(0f, 1f, _lower);
            // Grabbing, pushing, punching: forward and in toward the middle of the view.
            reach += new Vector3(-0.045f, 0.03f, 0.13f) * _thrust;

            transform.localPosition = _restPos + new Vector3(_swayNow.x, _swayNow.y + breath, 0f) + bobOffset + shake + reach;
            transform.localRotation = _restRot
                * Quaternion.Euler(-_swayNow.y * 400f + _reach * 28f - _palm * 35f, _swayNow.x * 400f - _thrust * 8f, -_swayNow.x * 300f + _reach * -10f);

            AnimateFingers(dt);
        }

        // On top of the grip they were posed in, where the fingertips already rest on the case: each finger eases a
        // little off it on its own slow wander (never further in), presses a touch harder when the hand squeezes,
        // shakes with the rest of it when the air runs low, and while nothing else is going on the index finger lifts
        // off the case and drops back every few seconds. The thumb lifts for the lid and rolls down on the wheel.
        void AnimateFingers(float dt)
        {
            if (_act == null)
            {
                _nextTap -= dt;
                if (_nextTap <= 0f) { _tap = 1f; _nextTap = Random.Range(3.5f, 8f); }
            }
            _tap = Mathf.MoveTowards(_tap, 0f, dt * 2.5f);
            float tapLift = Mathf.Sin(_tap * Mathf.PI) * 18f;
            float now = Time.time;

            for (int i = 0; i < _fingerRest.Length; i++)
            {
                int finger = i / 3, joint = i % 3;
                float wander = -Mathf.PerlinNoise(now * 0.5f + finger * 3.7f, joint * 1.3f) * 7f;
                float shake = Tremble > 0f ? -Mathf.PerlinNoise(now * 15f + finger * 2.1f, joint * 4.3f) * 14f * Tremble : 0f;
                float grip = _squeeze * (joint == 0 ? 2f : 1f);
                float tap = finger == 0 && joint < 2 ? -tapLift : 0f;
                float spread = -_open * (joint == 0 ? 35f : 25f);
                // With nothing to hold, the fingers close the rest of the way into a fist.
                float fist = Bare ? (joint == 0 ? 45f : 30f) : 0f;
                if (Bare) tap = 0f;
                fingers[i].localRotation = _fingerRest[i] * Quaternion.Euler(wander + shake + grip + tap + spread + fist, 0f, 0f);
            }

            if (_thumbRest.Length == 3)
            {
                float rest = (Mathf.PerlinNoise(now * 0.4f, 7.7f) - 0.5f) * 5f;
                thumb[0].localRotation = _thumbRest[0] * Quaternion.Euler(-25f * _thumbLift + 5f * _thumbPress, 0f, 0f);
                float fold = Bare ? 1f : 0f;   // the thumb folded across the fist
                thumb[1].localRotation = _thumbRest[1] * Quaternion.Euler(rest - 35f * _thumbLift + 20f * _thumbPress + 30f * fold, 0f, 0f);
                thumb[2].localRotation = _thumbRest[2] * Quaternion.Euler(rest - 35f * _thumbLift + 25f * _thumbPress + 25f * fold, 0f, 0f);
            }
        }

        void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }
    }
}
