using System.Collections;
using GJ.PSX;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GJ.Game
{
    // Keeps the lighter hand alive: it lags behind the mouse, bobs with your steps, breathes when still and shakes
    // when the air runs low. Lighting the Zippo is a full little act (thumb flicks the lid, rolls the wheel, a
    // spark, the flame catches, the fist squeezes), snapping it shut smothers the flame, and reaching for the fuse
    // dips the hand toward the floor.
    public class HandAnimator : MonoBehaviour
    {
        public CharacterController body;
        public Transform fist;          // the hand mesh, squeezed a touch on each grip
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

        Vector3 _restPos;
        Quaternion _restRot;
        Vector3 _fistScale;
        Vector2 _swayNow;
        float _bobPhase;
        float _reach;          // 0..1 how far the hand dips toward the floor
        float _squeeze;        // 0..1 extra grip
        Coroutine _act;
        System.Action _finishAct;  // lands the running act at once if it gets cut short

        const float LidOpen = 135f;

        void Awake()
        {
            _restPos = transform.localPosition;
            _restRot = transform.localRotation;
            if (fist != null) _fistScale = fist.localScale;
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
        }

        // Start dark (lid shut, no flame), then light up on our own.
        public void LightUp(float delay = 0.4f)
        {
            if (_act != null) StopCoroutine(_act);
            _act = StartCoroutine(LightRoutine(delay));
        }

        // Shut a burning lighter, or open and strike a closed one.
        public void Toggle()
        {
            if (_act != null) return;
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

            // Thumb flicks the lid over: fast at first, then it bounces against the hinge.
            Play(lidClip, 0.9f);
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                float k = t / 0.22f;
                float overshoot = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f;
                if (lid != null) lid.localRotation = Quaternion.Euler(0f, 0f, LidOpen * Mathf.Min(1f, k * overshoot));
                _squeeze = k;
                yield return null;
            }
            if (lid != null) lid.localRotation = Quaternion.Euler(0f, 0f, LidOpen);
            yield return new WaitForSeconds(0.12f);

            // Roll the wheel: a spark, and the wick catches.
            Play(strikeClip, 1f);
            Quaternion wheelStart = wheel != null ? wheel.localRotation : Quaternion.identity;
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                if (wheel != null) wheel.localRotation = wheelStart * Quaternion.Euler(0f, -900f * t, 0f);
                if (spark != null) spark.gameObject.SetActive(t > 0.05f && t < 0.11f);
                _squeeze = 1f;
                yield return null;
            }
            if (spark != null) spark.gameObject.SetActive(false);

            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                if (flame != null) flame.strength = Mathf.SmoothStep(0f, 1f, k) * (1f + Mathf.Sin(k * Mathf.PI) * 0.4f);
                _squeeze = 1f - k;
                yield return null;
            }
            if (flame != null) flame.strength = 1f;
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
                yield return null;
            }
            SetUnlit();
            _squeeze = 0f;
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

            transform.localPosition = _restPos + new Vector3(_swayNow.x, _swayNow.y + breath, 0f) + bobOffset + shake + reach;
            transform.localRotation = _restRot
                * Quaternion.Euler(-_swayNow.y * 400f + _reach * 28f, _swayNow.x * 400f, -_swayNow.x * 300f + _reach * -10f);

            if (fist != null)
            {
                float s = _squeeze * 0.05f;
                fist.localScale = new Vector3(_fistScale.x * (1f - s), _fistScale.y, _fistScale.z * (1f - s * 0.6f));
            }
        }

        void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }
    }
}
