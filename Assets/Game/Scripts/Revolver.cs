using System.Collections;
using GJ.PSX;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GJ.Game
{
    // A revolver carried in the right hand once it has been found. Q (or 2) draws and holsters it; the lighter hand
    // drops out of view while it is out (and the Zippo is snapped shut). Left click fires: a ray from the eye, a flash,
    // a kick, and whatever Shootable it hits is told. Rounds are few; the cylinder is shown in the corner.
    public class Revolver : MonoBehaviour
    {
        public Transform viewModel;       // hand and gun, raised into view when drawn
        public Transform cylinder;        // turns a chamber after each shot
        public Transform hammer;
        public Transform muzzle;
        public GameObject muzzleFlash;
        public Light muzzleLight;
        public HandAnimator lighterHand;
        public Transform[] trigger;       // the index finger's joints, knuckle first: they curl in on each shot
        public Camera eye;
        public AudioSource audioSource;
        public AudioClip shotClip, clickClip, drawClip;
        public int rounds = 2;

        public bool Owned { get; private set; }
        public bool Drawn { get; private set; }

        Vector3 _rest;
        float _raise, _kick, _pull;
        Quaternion[] _triggerRest;
        Text _ammo;
        Image _dot;
        bool _busy;

        void Awake()
        {
            _rest = viewModel.localPosition;
            _triggerRest = new Quaternion[trigger != null ? trigger.Length : 0];
            for (int i = 0; i < _triggerRest.Length; i++) _triggerRest[i] = trigger[i].localRotation;
            muzzleFlash.SetActive(false);
            muzzleLight.enabled = false;
            viewModel.gameObject.SetActive(false);
            BuildHud();
        }

        // Carried in from an earlier room: owned, loaded as you left it, still holstered.
        public void SyncFromSession()
        {
            if (!GameSession.HasRevolver) return;
            Owned = true;
            rounds = GameSession.Rounds;
        }

        public void Give(int loaded)
        {
            Owned = true;
            rounds = loaded;
            GameSession.HasRevolver = true;
            GameSession.Rounds = rounds;
            Draw(true);
        }

        public void AddRounds(int count)
        {
            rounds += count;
            GameSession.Rounds = rounds;
        }

        public void Holster() => Draw(false);

        void Draw(bool on)
        {
            if (!Owned || Drawn == on) return;
            Drawn = on;
            if (on)
            {
                if (lighterHand != null && lighterHand.Lit) lighterHand.Toggle();
                if (audioSource != null && drawClip != null) audioSource.PlayOneShot(drawClip, 0.7f);
                viewModel.gameObject.SetActive(true);
            }
            if (lighterHand != null) lighterHand.Lowered = on;
        }

        void Update()
        {
            _raise = Mathf.MoveTowards(_raise, Drawn ? 1f : 0f, Time.deltaTime * 4f);
            _kick = Mathf.MoveTowards(_kick, 0f, Time.deltaTime * 6f);
            // The trigger finger squeezes in and eases back out after each shot.
            _pull = Mathf.MoveTowards(_pull, 0f, Time.deltaTime * 5f);
            for (int i = 0; i < _triggerRest.Length; i++)
                trigger[i].localRotation = _triggerRest[i] * Quaternion.Euler(_pull * (i == 0 ? 18f : 28f), 0f, 0f);
            float r = Mathf.SmoothStep(0f, 1f, _raise);
            viewModel.localPosition = _rest + Vector3.down * 0.3f * (1f - r) + new Vector3(0f, 0.015f, -0.05f) * _kick;
            viewModel.localRotation = Quaternion.Euler(-25f * _kick, 0f, 0f);
            if (_raise <= 0f && !Drawn && viewModel.gameObject.activeSelf) viewModel.gameObject.SetActive(false);
            _ammo.text = Owned && Drawn ? new string('|', rounds) + new string('.', Mathf.Max(0, 6 - rounds)) : "";
            // The round goes where the eye looks, not where the barrel seems to point: show where that is.
            _dot.enabled = Owned && Drawn && _raise > 0.95f;

            // Not while dead or in a menu (the lighter hand is switched off then too).
            if (!Owned || FirstPersonController.Frozen || (lighterHand != null && !lighterHand.enabled)) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && (kb.qKey.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame)) Draw(!Drawn);
            if (kb != null && kb.digit1Key.wasPressedThisFrame && Drawn) Draw(false);
            if (Drawn && _raise > 0.95f && !_busy && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
                StartCoroutine(Fire());
        }

        IEnumerator Fire()
        {
            _busy = true;
            _pull = 1f;
            if (rounds <= 0)
            {
                if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip, 0.9f);
                yield return new WaitForSeconds(0.25f);
                _busy = false;
                yield break;
            }
            rounds--;
            GameSession.Rounds = rounds;
            if (audioSource != null && shotClip != null) audioSource.PlayOneShot(shotClip, 1f);
            _kick = 1f;
            muzzleFlash.SetActive(true);
            muzzleLight.enabled = true;
            var ray = new Ray(eye.transform.position, eye.transform.forward);
            if (Physics.Raycast(ray, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<Shootable>();
                if (target != null) target.Hit(hit.point);
            }
            for (float t = 0f; t < 0.08f; t += Time.deltaTime)
            {
                muzzleLight.intensity = Mathf.Lerp(5f, 0f, t / 0.08f);
                yield return null;
            }
            muzzleFlash.SetActive(false);
            muzzleLight.enabled = false;
            // Thumb the hammer back; the cylinder turns to the next chamber.
            Quaternion from = cylinder.localRotation, to = from * Quaternion.Euler(0f, 0f, 60f);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.3f);
                cylinder.localRotation = Quaternion.Slerp(from, to, k);
                if (hammer != null) hammer.localRotation = Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * -30f, 0f, 0f);
                yield return null;
            }
            _busy = false;
        }

        // Rounds left, drawn by the same low-res camera as everything else.
        void BuildHud()
        {
            var canvasGo = new GameObject("RevolverHUD");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = eye;
            canvas.planeDistance = eye.nearClipPlane + 0.02f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(427, 240);
            scaler.matchWidthOrHeight = 1f;
            _ammo = new GameObject("Rounds").AddComponent<Text>();
            _ammo.transform.SetParent(canvasGo.transform, false);
            _ammo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _ammo.fontSize = 14;
            _ammo.alignment = TextAnchor.LowerRight;
            _ammo.color = new Color(0.95f, 0.85f, 0.6f);
            _ammo.raycastTarget = false;
            var rt = _ammo.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-12f, 10f);
            rt.sizeDelta = new Vector2(120f, 30f);
            var shadow = _ammo.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);

            // A two-pixel aiming dot in the middle of the screen, with a dark edge so it shows on a lit wall too.
            _dot = new GameObject("Aim").AddComponent<Image>();
            _dot.transform.SetParent(canvasGo.transform, false);
            _dot.color = new Color(0.95f, 0.85f, 0.6f, 0.9f);
            _dot.raycastTarget = false;
            _dot.rectTransform.sizeDelta = new Vector2(2f, 2f);
            var edge = _dot.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0, 0, 0, 0.7f);
            edge.effectDistance = new Vector2(1f, -1f);
            _dot.enabled = false;
        }
    }
}
