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

        // X with the revolver out turns it on yourself, where the room allows it; a round fired like that is the
        // owner's business (ShotSelf), not a ray into the room.
        public System.Func<bool> canAimAtSelf;
        public event System.Action ShotSelf;
        public bool AtHead { get; private set; }

        Vector3 _rest;
        float _raise, _kick, _pull, _head;
        Quaternion[] _triggerRest;
        KeyLine _hint;
        RectTransform _cylinder;
        Image[] _chambers;
        CanvasGroup _cylinderShow;
        float _turn;
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

        // Knocked out of the hand: gone, rounds and all, until it is picked up again.
        public void TakeAway()
        {
            Draw(false);
            Owned = false;
            AtHead = false;
            viewModel.gameObject.SetActive(false);
            GameSession.HasRevolver = false;
        }

        void Draw(bool on)
        {
            if (!Owned || Drawn == on) return;
            Drawn = on;
            AtHead = false;
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
            // To the head: the hand comes up and round until the muzzle is at the right temple, shaking a little.
            _head = Mathf.MoveTowards(_head, AtHead ? 1f : 0f, Time.deltaTime * 2.5f);
            float h = Mathf.SmoothStep(0f, 1f, _head);
            Vector3 shake = new Vector3(Mathf.PerlinNoise(Time.time * 9f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 9f) - 0.5f, 0f) * 0.006f * h;
            viewModel.localPosition = _rest + Vector3.down * 0.3f * (1f - r) + new Vector3(0f, 0.015f, -0.05f) * _kick
                                      + (HeadPose - _rest) * h + shake;
            viewModel.localRotation = Quaternion.Euler(-25f * _kick, 0f, 0f) * Quaternion.Euler(-35f * h, -55f * h, 25f * h);
            if (_raise <= 0f && !Drawn && viewModel.gameObject.activeSelf) viewModel.gameObject.SetActive(false);
            UpdateCylinder();
            // The round goes where the eye looks, not where the barrel seems to point: show where that is.
            _dot.enabled = Owned && Drawn && _raise > 0.95f && _head <= 0f;
            bool canTurn = Owned && Drawn && canAimAtSelf != null && canAimAtSelf();
            if (!canTurn) AtHead = false;
            _hint.text = !canTurn ? "" : Loc.T(AtHead ? "[LMB] pull the trigger   [X] lower it" : "[X] to your head");

            // Not while dead or in a menu (the lighter hand is switched off then too).
            if (!Owned || FirstPersonController.Frozen || (lighterHand != null && !lighterHand.enabled)) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && (kb.qKey.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame)) Draw(!Drawn);
            if (kb != null && kb.digit1Key.wasPressedThisFrame && Drawn) Draw(false);
            if (kb != null && kb.xKey.wasPressedThisFrame && canTurn && !_busy) AtHead = !AtHead;
            if (Drawn && _raise > 0.95f && !_busy && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked
                && (!AtHead || _head >= 1f))
                StartCoroutine(Fire());
        }

        static readonly Vector3 HeadPose = new Vector3(0.2f, -0.02f, 0.3f);

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
            if (AtHead)
            {
                AtHead = false;
                _head = 0f;
                Drawn = false;
                if (lighterHand != null) lighterHand.Lowered = false;
                viewModel.gameObject.SetActive(false);
                _busy = false;
                ShotSelf?.Invoke();
                yield break;
            }
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

        // The cylinder in the corner while the revolver is out; it turns a chamber on with every round spent.
        void UpdateCylinder()
        {
            bool show = Owned && Drawn;
            _cylinderShow.alpha = Mathf.MoveTowards(_cylinderShow.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            for (int i = 0; i < _chambers.Length; i++) _chambers[i].color = i < rounds ? UiStyle.Brass : UiStyle.Hollow;
            float target = (6 - Mathf.Clamp(rounds, 0, 6)) * 60f;
            _turn = Mathf.Lerp(_turn, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            _cylinder.localRotation = Quaternion.Euler(0f, 0f, -_turn);
        }

        Image CylinderPart(string name, Sprite sprite, Vector2 at, float diameter, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(_cylinder, false);
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.rectTransform.anchoredPosition = at;
            img.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            return img;
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
            // The cylinder seen from behind: six chambers round a ring, brass where a round is left, dark where it's spent.
            _cylinder = new GameObject("Cylinder", typeof(RectTransform)).GetComponent<RectTransform>();
            _cylinder.SetParent(canvasGo.transform, false);
            _cylinder.anchorMin = _cylinder.anchorMax = new Vector2(1f, 0f);
            _cylinder.pivot = new Vector2(0.5f, 0.5f);
            _cylinder.anchoredPosition = new Vector2(-24f, 22f);
            _cylinder.sizeDelta = new Vector2(30f, 30f);
            _cylinderShow = _cylinder.gameObject.AddComponent<CanvasGroup>();
            var ring = CylinderPart("Ring", UiStyle.Ring, Vector2.zero, 30f, UiStyle.Dim);
            ring.gameObject.AddComponent<Shadow>().effectColor = UiStyle.Shade;
            _chambers = new Image[6];
            for (int i = 0; i < 6; i++)
            {
                float a = (90f - i * 60f) * Mathf.Deg2Rad;
                _chambers[i] = CylinderPart("Chamber", UiStyle.Disc, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9f, 8f, UiStyle.Brass);
                var rim = _chambers[i].gameObject.AddComponent<Outline>();
                rim.effectColor = UiStyle.Shade;
                rim.effectDistance = new Vector2(0.7f, -0.7f);
            }
            _hint = KeyLine.Create(canvasGo.transform, "Hint", 9, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-42f, 12f), KeyLine.Look.Light, 280f);

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
