using System.Collections;
using GJ.PSX;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GJ.Game
{
    // One room's loop: a lungful of air, set the room's machinery going, die when the air runs out, and the room keeps
    // going without you. If the puzzle opens the door you come back; if nothing is left that could open it the room
    // starts over ("one more time"). Walking out through the door clears the room.
    public class RoomDirector : MonoBehaviour
    {
        enum Phase { Alive, Dying, Dead, Reviving, Cleared }

        public float airSeconds = 90f;
        public string startMessage = "find a way out\nyou have 90 seconds of air";
        public FirstPersonController controller;
        public Transform head;
        public Transform hand;
        public HandAnimator handAnimator;
        public Light lighterLight;
        public RoomPuzzle puzzle;
        public Revolver revolver;          // only in rooms where one can be found
        public VaultDoor door;
        public SegmentDisplay display;
        public GameFlow flow;
        public AudioClip heartbeatClip;
        public AudioClip paperClip;
        public AudioClip gaspClip;
        public AudioClip[] ambientClips;   // drips and the mountain settling, now and then, somewhere nearby
        public float readRange = 1.6f;
        public float exitZ = 9.6f;
        public float fastForward = 4f;

        Phase _phase = Phase.Alive;
        float _air;
        float _idleDeadTime;
        Vector3 _headRest;
        Vector3 _handRest;
        ReadableNote[] _notes;
        ReadableNote _reading;
        int _closedNoteFrame = -1;

        AudioSource _heart;
        AudioLowPassFilter _muffle;
        float _nextBeat;
        float _nextAmbient = 4f;

        Text _timer, _attempt, _prompt, _message, _noteText, _controls;
        Image _fade, _paper;
        Coroutine _flash;
        float _controlsTime = 9f;

        // Whether the lighter hand belongs on screen right now (it is put away while lying dead).
        public bool HandVisible => _phase == Phase.Alive || _phase == Phase.Cleared;

        // True while something owns Esc or the body (reading a note, collapsing, coming back): no pause menu then.
        public bool Busy => _reading != null || _closedNoteFrame == Time.frameCount
                            || _phase == Phase.Dying || _phase == Phase.Reviving || _phase == Phase.Cleared;

        void Start()
        {
            _air = airSeconds;
            _headRest = head.localPosition;
            _handRest = hand.localPosition;
            _notes = FindObjectsByType<ReadableNote>(FindObjectsSortMode.None);
            puzzle.Solved += door.Open;
            puzzle.Message += text => { if (_phase == Phase.Alive) Flash(text); };
            BuildHud();
            BuildAudio();
            Time.timeScale = 1f;
            if (handAnimator != null) handAnimator.LightUp(0.7f);

            // Another go at the same room: say which, and what went wrong last time (the death screen shows it only
            // for a moment).
            GameSession.BeginAttempt(SceneManager.GetActiveScene().name);
            string lastFail = GameSession.LastFail;
            GameSession.LastFail = null;
            int attempt = GameSession.Attempt;
            _attempt.text = attempt > 1 ? "ATTEMPT " + attempt : "";
            if (attempt > 1 && !string.IsNullOrEmpty(lastFail)) Flash($"ATTEMPT {attempt}\n<size=10>last time: {lastFail}</size>", 4.5f);
            else if (attempt > 1) Flash("ATTEMPT " + attempt, 2.5f);
            else Flash(startMessage, 4f);
        }

        void OnDestroy() => Time.timeScale = 1f;

        void Update()
        {
            var kb = Keyboard.current;
            if (_reading != null) { UpdateReading(kb); return; }
            if (kb != null && kb.rKey.wasPressedThisFrame && _phase != Phase.Cleared) { Restart(); return; }

            switch (_phase)
            {
                case Phase.Alive: UpdateAlive(kb); break;
                case Phase.Dead: UpdateDead(kb); break;
            }

            bool airVisible = _phase == Phase.Alive;
            display.ShowTime(_air, !airVisible || Mathf.Repeat(Time.time, 1f) < 0.5f || _air > 10f);
            _timer.text = "AIR " + (airVisible || _phase == Phase.Dying ? FormatTime(_air) : "--:--");
            _timer.color = _air < 10f && airVisible ? new Color(1f, 0.25f, 0.2f) : new Color(0.95f, 0.9f, 0.8f);

            // The controls sit in the corner for the first few seconds of each room, then fade away.
            _controlsTime -= Time.deltaTime;
            var cc = _controls.color;
            cc.a = Mathf.Clamp01(_controlsTime / 1.5f) * 0.8f;
            _controls.color = cc;

            if (_phase == Phase.Alive && controller.transform.position.z > exitZ) StartCoroutine(Clear());
            UpdateAmbient();
        }

        // ---------------------------------------------------------------- alive

        void UpdateAlive(Keyboard kb)
        {
            _air -= Time.deltaTime;

            bool lit = handAnimator == null || handAnimator.Lit;
            var option = puzzle.Look(head.position, head.forward, lit);
            bool canAct = option.HasValue && option.Value.act != null;
            var note = canAct ? null : NoteInView();
            if (canAct) _prompt.text = option.Value.prompt;
            else if (note != null) _prompt.text = "[E] read";
            else _prompt.text = option.HasValue ? option.Value.prompt : "";

            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                if (canAct)
                {
                    if (option.Value.reach && handAnimator != null) handAnimator.Reach();
                    option.Value.act();
                }
                else if (note != null) OpenNote(note);
            }

            // Snap the Zippo shut (darkness, and the flame is safe in your pocket) or flick it open again.
            var mouse = Mouse.current;
            if (handAnimator != null && ((kb != null && kb.fKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame)))
            {
                // With the revolver out, the first press just puts it away and brings the lighter back up.
                if (revolver != null && revolver.Drawn) revolver.Holster();
                else handAnimator.Toggle();
            }

            UpdateHeart();

            // Running out of air: the edges of the world close in and the hand starts to shake.
            float choke = Mathf.Clamp01(1f - _air / 15f);
            _fade.color = new Color(0f, 0f, 0f, choke * 0.45f);
            if (handAnimator != null) handAnimator.Tremble = Mathf.Clamp01(1f - _air / 20f);

            if (_air <= 0f)
            {
                _air = 0f;
                StartCoroutine(Die());
            }
        }

        ReadableNote NoteInView()
        {
            ReadableNote best = null;
            float bestDot = 0.85f;
            foreach (var n in _notes)
            {
                Vector3 to = n.transform.position - head.position;
                if (to.magnitude > readRange) continue;
                float dot = Vector3.Dot(head.forward, to.normalized);
                if (dot > bestDot) { bestDot = dot; best = n; }
            }
            return best;
        }

        // ---------------------------------------------------------------- notes

        void OpenNote(ReadableNote note)
        {
            _reading = note;
            _noteText.text = note.text;
            _paper.gameObject.SetActive(true);
            _prompt.text = "[E] put it down";
            FirstPersonController.Frozen = true;
            if (paperClip != null) _heart.PlayOneShot(paperClip, 0.8f);
        }

        // The air keeps running while you read.
        void UpdateReading(Keyboard kb)
        {
            _air -= Time.deltaTime;
            _timer.text = "AIR " + FormatTime(_air);
            display.ShowTime(_air);
            bool close = kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
            if (!close && _air > 0f) return;
            _reading = null;
            _closedNoteFrame = Time.frameCount;
            _paper.gameObject.SetActive(false);
            _prompt.text = "";
            FirstPersonController.Frozen = false;
            if (paperClip != null) _heart.PlayOneShot(paperClip, 0.5f);
        }

        // ---------------------------------------------------------------- death and back

        // Where a body lying on the floor should be looking: whatever is still moving, then the door.
        Vector3 DeadFocus() => puzzle.Focus ?? door.transform.position + Vector3.up * 1.2f;

        Quaternion DeadLook() => Quaternion.LookRotation(DeadFocus() - head.position) * Quaternion.Euler(0f, 0f, 18f);

        void UpdateDead(Keyboard kb)
        {
            head.rotation = Quaternion.Slerp(head.rotation, DeadLook(), 1f - Mathf.Exp(-2.5f * Time.unscaledDeltaTime));

            bool working = puzzle.Working || door.IsOpening;
            Time.timeScale = working && kb != null && kb.spaceKey.isPressed ? fastForward : 1f;
            _prompt.text = working && !door.IsOpening ? "hold [SPACE] to wait" : "";

            if (door.IsOpen) { StartCoroutine(Revive()); return; }

            // Nothing left that could open the door: this death was for nothing.
            if (!working)
            {
                _idleDeadTime += Time.deltaTime;
                if (_idleDeadTime > 2.5f) StartCoroutine(OneMoreTime());
            }
        }

        IEnumerator Die()
        {
            _phase = Phase.Dying;
            _prompt.text = "";
            controller.enabled = false;
            GameSession.Deaths++;
            if (handAnimator != null) handAnimator.enabled = false;
            if (revolver != null) revolver.Holster();

            // Collapse onto the floor; the eyes end up low, on whatever is still happening in the room.
            Vector3 startPos = head.position;
            Quaternion startRot = head.rotation;
            Vector3 endPos = controller.transform.position + Vector3.up * 0.22f;
            Vector3 handStart = hand.localPosition;

            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float k = t / 1.1f;
                float fall = k * k;
                head.position = Vector3.Lerp(startPos, endPos, fall);
                Quaternion endRot = Quaternion.LookRotation(DeadFocus() - head.position) * Quaternion.Euler(0f, 0f, 18f);
                head.rotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, k));
                hand.localPosition = handStart + Vector3.down * 0.5f * fall;
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.85f, k));
                yield return null;
            }
            head.position = endPos;
            SetMuffled(true);
            hand.gameObject.SetActive(false);
            if (lighterLight != null) lighterLight.enabled = false;

            Say("you stopped breathing.\nthe room did not.");
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                head.rotation = Quaternion.Slerp(head.rotation, DeadLook(), 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.85f, 0.25f, t / 1.5f));
                yield return null;
            }
            Say("");
            _phase = Phase.Dead;
        }

        IEnumerator Revive()
        {
            _phase = Phase.Reviving;
            Time.timeScale = 1f;
            _prompt.text = "";
            yield return new WaitForSeconds(0.8f);

            Say("breathe.");
            if (gaspClip != null) _heart.PlayOneShot(gaspClip, 0.9f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                _fade.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.8f, t / 0.5f));
                yield return null;
            }

            // Stand back up facing the open door, with a fresh lungful of air and the lighter struck again.
            Vector3 toDoor = door.transform.position - controller.transform.position;
            float yaw = Mathf.Atan2(toDoor.x, toDoor.z) * Mathf.Rad2Deg;
            controller.SetLook(yaw, 0f);
            head.localPosition = _headRest;
            head.localRotation = Quaternion.identity;
            hand.localPosition = _handRest;
            hand.gameObject.SetActive(true);
            if (handAnimator != null)
            {
                handAnimator.enabled = true;
                handAnimator.LightUp(0.5f);
            }
            if (lighterLight != null) lighterLight.enabled = true;
            SetMuffled(false);
            _air = airSeconds;
            controller.enabled = true;

            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                _fade.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.8f, 0f, t / 1.2f));
                yield return null;
            }
            Say("");
            _phase = Phase.Alive;
        }

        IEnumerator OneMoreTime()
        {
            _phase = Phase.Reviving;
            Time.timeScale = 1f;
            string reason = puzzle.FailReason;
            Say(string.IsNullOrEmpty(reason) ? "ONE MORE TIME" : "ONE MORE TIME\n\n<size=10>" + reason + "</size>");
            for (float t = 0f; t < (string.IsNullOrEmpty(reason) ? 1.5f : 3f); t += Time.deltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.35f, 1f, t / 1.5f));
                yield return null;
            }
            GameSession.LastFail = reason;
            Restart();
        }

        IEnumerator Clear()
        {
            _phase = Phase.Cleared;
            controller.enabled = false;
            _prompt.text = "";
            Say("ROOM CLEARED");
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 1f, t / 2f));
                yield return null;
            }
            yield return new WaitForSeconds(0.6f);
            Say("");
            if (flow != null) flow.OnRoomCleared();
            else Restart();
        }

        void Restart()
        {
            Time.timeScale = 1f;
            FirstPersonController.Frozen = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        // ---------------------------------------------------------------- sound

        void BuildAudio()
        {
            _heart = gameObject.AddComponent<AudioSource>();
            _heart.spatialBlend = 0f;
            _heart.playOnAwake = false;

            // On the listener a low-pass filter colours the whole mix: dead, everything goes dull and far away.
            _muffle = head.GetComponent<AudioLowPassFilter>();
            if (_muffle == null) _muffle = head.gameObject.AddComponent<AudioLowPassFilter>();
            _muffle.cutoffFrequency = 22000f;
        }

        // Heartbeat in the last 20 s of air: louder and faster as it runs out.
        void UpdateHeart()
        {
            if (heartbeatClip == null || _air > 20f) return;
            if (Time.time < _nextBeat) return;
            float k = Mathf.Clamp01(_air / 20f);
            _heart.PlayOneShot(heartbeatClip, Mathf.Lerp(1f, 0.35f, k));
            _nextBeat = Time.time + Mathf.Lerp(0.48f, 1.1f, k);
        }

        void UpdateAmbient()
        {
            if (ambientClips == null || ambientClips.Length == 0) return;
            _nextAmbient -= Time.deltaTime;
            if (_nextAmbient > 0f) return;
            _nextAmbient = Random.Range(5f, 13f);
            var clip = ambientClips[Random.Range(0, ambientClips.Length)];
            if (clip == null) return;
            Vector2 off = Random.insideUnitCircle.normalized * Random.Range(1.5f, 4f);
            AudioSource.PlayClipAtPoint(clip, head.position + new Vector3(off.x, 0.8f, off.y), Random.Range(0.35f, 0.7f));
        }

        void SetMuffled(bool muffled) => _muffle.cutoffFrequency = muffled ? 900f : 22000f;

        // ---------------------------------------------------------------- HUD

        // Steady centred line; cancels any fading one.
        void Say(string text)
        {
            if (_flash != null) { StopCoroutine(_flash); _flash = null; }
            _message.text = text;
            var c = _message.color;
            c.a = 1f;
            _message.color = c;
        }

        // Short centred line that fades out on its own.
        void Flash(string text, float seconds = 2.5f)
        {
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRoutine(text, seconds));
        }

        IEnumerator FlashRoutine(string text, float seconds)
        {
            _message.text = text;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                var c = _message.color;
                c.a = Mathf.Clamp01((seconds - t) / 0.6f);
                _message.color = c;
                yield return null;
            }
            _message.text = "";
            var reset = _message.color;
            reset.a = 1f;
            _message.color = reset;
            _flash = null;
        }

        // The HUD is drawn by the player camera itself, so it gets the same low-res dithered PSX treatment.
        void BuildHud()
        {
            var cam = head.GetComponent<Camera>();
            var canvasGo = new GameObject("HUD");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.02f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(427, 240);
            scaler.matchWidthOrHeight = 1f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _fade = new GameObject("Fade").AddComponent<Image>();
            _fade.transform.SetParent(canvasGo.transform, false);
            Stretch(_fade.rectTransform);
            _fade.color = new Color(0, 0, 0, 0);
            _fade.raycastTarget = false;

            // A scrap of yellowed paper held up to the light.
            _paper = new GameObject("Paper").AddComponent<Image>();
            _paper.transform.SetParent(canvasGo.transform, false);
            _paper.sprite = PaperSprite();
            _paper.color = Color.white;
            _paper.raycastTarget = false;
            _paper.rectTransform.sizeDelta = new Vector2(230, 170);
            _paper.rectTransform.localRotation = Quaternion.Euler(0, 0, 1.5f);
            _noteText = MakeText(_paper.transform, font, 11, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero);
            _noteText.color = new Color(0.1f, 0.11f, 0.22f);
            _noteText.fontStyle = FontStyle.Italic;
            Destroy(_noteText.GetComponent<Shadow>());
            _noteText.rectTransform.sizeDelta = new Vector2(200, 150);
            _noteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _noteText.lineSpacing = 1.1f;
            _paper.gameObject.SetActive(false);

            _timer = MakeText(canvasGo.transform, font, 18, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(10, -6));
            _attempt = MakeText(canvasGo.transform, font, 9, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(11, -28));
            _attempt.color = new Color(0.7f, 0.65f, 0.58f, 0.85f);
            _controls = MakeText(canvasGo.transform, font, 9, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-10, -8));
            _controls.text = "[E] use   [F] lighter   [R] restart";
            _controls.lineSpacing = 1.2f;
            _prompt = MakeText(canvasGo.transform, font, 11, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 18));
            _message = MakeText(canvasGo.transform, font, 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero);
        }

        // The sheet held up to read: yellowed, darker toward ragged edges, a fold, a coffee ring, a pinhole.
        static Sprite PaperSprite()
        {
            const int w = 115, h = 85;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    // Ragged outline: each edge eaten in by a noisy amount.
                    float bite = Mathf.PerlinNoise(x * 0.4f, y * 0.4f + 7f) * 3f;
                    bool outside = x < bite || y < bite || x > w - 1 - bite || y > h - 1 - bite;
                    if (outside) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    var c = new Color(0.82f, 0.76f, 0.62f) * (0.92f + n * 0.12f) * Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(edge / 9f));
                    if (y == h / 2) c *= 0.9f;
                    if (y == h / 2 - 1) c *= 1.05f;
                    float ring = Vector2.Distance(new Vector2(x, y), new Vector2(w * 0.8f, h * 0.22f));
                    if (Mathf.Abs(ring - 9f) < 1f) c = Color.Lerp(c, new Color(0.5f, 0.35f, 0.2f), 0.45f);
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(w / 2f, h - 5f)) < 1f) c = new Color(0.2f, 0.17f, 0.13f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Text MakeText(Transform parent, Font font, int size, TextAnchor anchor, Vector2 pivot, Vector2 offset)
        {
            var text = new GameObject("Text").AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.95f, 0.9f, 0.8f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = pivot;
            rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(400, 40);
            return text;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
