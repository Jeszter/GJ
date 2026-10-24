using System.Collections;
using GJ.PSX;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GJ.Game
{
    // One room's loop: a few breaths of air, set the room's machinery going, die when the last one runs out (or let it
    // go sooner), and the room keeps going without you. If the puzzle opens the door you come back; if nothing is left
    // that could open it the room starts over ("one more time"). Walking out through the door clears the room.
    public class RoomDirector : MonoBehaviour
    {
        enum Phase { Alive, Dying, Dead, Reviving, Cleared }

        // Air comes in held breaths: near the end of each one the vision closes in and the hand shakes, then a gasp
        // and the next. The last one ends you. Holding Space lets it all go at once.
        public int breaths = 4;
        public float breathSeconds = 20f;
        public float chokeSeconds = 5f;
        public float letGoSeconds = 1.5f;
        public string startMessage = "find a way out\nyou have four breaths";
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
        public Corpse corpseTemplate;      // inactive; copied for every body left from an earlier try at this room
        public float readRange = 1.6f;
        public float exitZ = 9.6f;
        public float fastForward = 4f;

        Phase _phase = Phase.Alive;
        float _air;          // seconds left over all the breaths still to come
        int _breath;         // the breath being held now, counting down to 1
        float _letGo;        // how long Space has been held toward letting go
        bool _spaceLatched;  // Space was already down when this phase began: it has to come up before it counts again
        bool _bodyLeft;      // dead and not (yet) coming back: leaving the room now leaves the body behind
        Vector3 _fellAt;
        float _fellYaw;
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

        Text _attempt, _prompt, _message, _noteText, _controls, _holdText;
        Image _fade, _paper, _holdBar;
        Image[] _pips, _pipRings;
        Coroutine _flash;
        float _controlsTime = 9f;

        // Whether the lighter hand belongs on screen right now (it is put away while lying dead, and gone while there
        // is no lighter in it).
        public bool HandVisible => (_phase == Phase.Alive || _phase == Phase.Cleared) && Holding;

        // The lighter left burning in the room (under the boiler's pot): the hand goes empty and the room dark.
        public bool LighterLeft { get; private set; }

        // A lighter in the hand: found in the corridor at the start of the run, and not left behind in this room.
        bool Holding => GameSession.HasLighter && !LighterLeft;

        public void LeaveLighter(bool leave)
        {
            LighterLeft = leave;
            ShowLighter(Holding);
        }

        // Picked up off the floor: into the hand, and flicked open at once.
        public void PickUpLighter()
        {
            GameSession.HasLighter = true;
            ShowLighter(Holding);
            if (handAnimator != null) handAnimator.LightUp(0.3f);
            _controls.text = FreeAir ? "[E] use   [F] lighter" : "[E] use   [F] lighter   [R] restart\n" + LetGoHint;
            _controlsTime = 6f;
        }

        void ShowLighter(bool on)
        {
            hand.gameObject.SetActive(on);
            if (lighterLight != null) lighterLight.enabled = on;
        }

        public bool Alive => _phase == Phase.Alive;
        public FirstPersonController Controller => controller;
        public Transform Head => head;
        public Revolver Gun => revolver;

        string _deathLine = "you stopped breathing.\nthe room did not.";

        // Die now rather than when the air runs out (caught in a blast); the line shown as the body falls.
        public void Kill(string line)
        {
            if (_phase != Phase.Alive) return;
            if (!string.IsNullOrEmpty(line)) _deathLine = line;
            _air = 0f;
            StartCoroutine(Die());
        }

        // True while something owns Esc or the body (reading a note, collapsing, coming back): no pause menu then.
        public bool Busy => _reading != null || _closedNoteFrame == Time.frameCount
                            || _phase == Phase.Dying || _phase == Phase.Reviving || _phase == Phase.Cleared;

        // The corridor between the rooms: plenty of air, nothing to count, nobody dies out there.
        bool FreeAir => breaths <= 0;
        float FullAir => breaths * breathSeconds;
        int BreathAt(float air) => Mathf.CeilToInt(air / breathSeconds - 0.0001f);
        // What is left of the breath being held now.
        float BreathLeft => _air - (_breath - 1) * breathSeconds;
        float Choke => _breath > 0 ? Mathf.Clamp01(1f - BreathLeft / chokeSeconds) : 1f;

        void Start()
        {
            _air = FullAir;
            _breath = breaths;
            _headRest = head.localPosition;
            _handRest = hand.localPosition;
            _notes = FindObjectsByType<ReadableNote>(FindObjectsSortMode.None);
            puzzle.Bind(this);
            puzzle.Solved += door.Open;
            puzzle.Message += text => { if (_phase == Phase.Alive) Flash(text); };
            BuildHud();
            BuildAudio();
            Time.timeScale = 1f;

            // Another go at the same room: say which, and what went wrong last time (the death screen shows it only
            // for a moment).
            string cameFrom = GameSession.Room;
            // A room played straight from the editor, not reached through the corridor: hand over the lighter that
            // would have been found out there.
            if (cameFrom == null && !FreeAir) GameSession.HasLighter = true;
            if (Holding)
            {
                ShowLighter(true);
                if (handAnimator != null) handAnimator.LightUp(0.7f);
            }
            else
            {
                ShowLighter(false);
                _controls.text = FreeAir ? "[E] use" : "[E] use   [R] restart\n" + LetGoHint;
            }
            GameSession.BeginAttempt(SceneManager.GetActiveScene().name);
            LayOutBodies();
            puzzle.Arrived(cameFrom);
            if (FreeAir)
            {
                if (Holding) _controls.text = "[E] use   [F] lighter";
                Flash(startMessage, 5f);
                return;
            }
            if (revolver != null)
            {
                revolver.SyncFromSession();
                if (revolver.Owned) _controls.text = "[E] use   [F] lighter   [Q] revolver   [R] restart\n" + LetGoHint;
            }
            string lastFail = GameSession.LastFail;
            GameSession.LastFail = null;
            int attempt = GameSession.Attempt;
            _attempt.text = attempt > 1 ? "ATTEMPT " + attempt : "";
            if (attempt > 1 && !string.IsNullOrEmpty(lastFail)) Flash($"ATTEMPT {attempt}\n<size=10>last time: {lastFail}</size>", 4.5f);
            else if (attempt > 1) Flash("ATTEMPT " + attempt, 2.5f);
            else Flash(startMessage, 4f);
        }

        void OnDestroy() => Time.timeScale = 1f;

        // Every body from an earlier try at this room, back where it fell (the player's own capsule kept out of the way
        // while they are fitted between the walls).
        void LayOutBodies()
        {
            if (corpseTemplate == null) return;
            var cc = controller.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            for (int i = 0; i < GameSession.Bodies.Count; i++)
            {
                var body = GameSession.Bodies[i];
                var corpse = Instantiate(corpseTemplate, corpseTemplate.transform.parent);
                corpse.name = "PastBody_" + (i + 1);
                corpse.gameObject.SetActive(true);
                corpse.LayDown(body.position, body.yaw, (i * 37 % 25) - 12f);
            }
            if (cc != null) cc.enabled = true;
        }

        // Leaving the room while lying dead (one more time, or a restart) leaves the body there for the next try.
        public void LeaveBody()
        {
            if (!_bodyLeft) return;
            _bodyLeft = false;
            GameSession.AddBody(_fellAt, _fellYaw);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (_reading != null) { UpdateReading(kb); return; }
            if (kb != null && kb.rKey.wasPressedThisFrame && _phase != Phase.Cleared && !FreeAir) { Restart(); return; }

            switch (_phase)
            {
                case Phase.Alive: UpdateAlive(kb); break;
                case Phase.Dead: UpdateDead(kb); break;
            }

            // In the corridor the display over the door counts the time spent down here instead.
            bool airVisible = _phase == Phase.Alive;
            if (FreeAir) display.ShowTime(GameSession.Time);
            else display.ShowTime(_air, !airVisible || Mathf.Repeat(Time.time, 1f) < 0.5f || Choke <= 0f);
            UpdatePips();

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
            if (!FreeAir) Breathe();

            // Hold Space to stop breathing now instead of waiting for the air to run out.
            bool space = kb != null && kb.spaceKey.isPressed && !FreeAir;
            if (!space) _spaceLatched = false;
            if (space && !_spaceLatched) _letGo += Time.deltaTime;
            else _letGo = Mathf.MoveTowards(_letGo, 0f, Time.deltaTime * 3f);
            if (_letGo >= letGoSeconds)
            {
                _letGo = 0f;
                Kill("you let your breath go.\nthe room did not stop.");
                return;
            }

            bool lit = Holding && (handAnimator == null || handAnimator.Lit);
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
            if (handAnimator != null && Holding && ((kb != null && kb.fKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame)))
            {
                // With the revolver out, the first press just puts it away and brings the lighter back up.
                if (revolver != null && revolver.Drawn) revolver.Holster();
                else handAnimator.Toggle();
            }

            if (FreeAir) return;
            UpdateChoke();

            if (_air <= 0f)
            {
                _air = 0f;
                StartCoroutine(Die());
            }
        }

        // The air goes (faster in gas); when a held breath runs out, a gasp and the next one, while there is one.
        void Breathe()
        {
            _air = Mathf.Max(0f, _air - Time.deltaTime * puzzle.AirDrain);
            int now = BreathAt(_air);
            if (now < _breath && now > 0 && gaspClip != null) _heart.PlayOneShot(gaspClip, 0.6f);
            _breath = now;
        }

        // Near the end of each breath the edges of the world close in, pulsing with the heart, and the hand shakes;
        // the gasp clears it all. The last breath is worse, and letting go darkens it too.
        void UpdateChoke()
        {
            float choke = Choke;
            bool last = _breath <= 1;
            float letGo = Mathf.Clamp01(_letGo / letGoSeconds);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(5f, 10f, choke));
            float dark = choke * (last ? 0.62f : 0.45f) * pulse;
            dark = Mathf.Max(dark, letGo * letGo * 0.8f);
            _fade.color = new Color(0f, 0f, 0f, dark);
            if (handAnimator != null) handAnimator.Tremble = Mathf.Max(choke, letGo * 0.7f);

            // The heart: through the choke of every breath, and all through the last one, quicker as it goes.
            float urgency = Mathf.Max(choke, last ? 0.2f + 0.5f * (1f - BreathLeft / breathSeconds) : 0f);
            UpdateHeart(urgency);
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
            _letGo = 0f;
            if (!FreeAir)
            {
                Breathe();
                UpdateChoke();
                UpdatePips();
                display.ShowTime(_air);
            }
            bool close = kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
            if (!close && (_air > 0f || FreeAir)) return;
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

            // Space still down from letting go doesn't run the clock on: it has to be let up and held again.
            bool working = puzzle.Working || door.IsOpening;
            bool space = kb != null && kb.spaceKey.isPressed;
            if (!space) _spaceLatched = false;
            Time.timeScale = working && space && !_spaceLatched ? fastForward : 1f;
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
            _letGo = 0f;
            _spaceLatched = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            if (handAnimator != null) handAnimator.Tremble = 0f;
            _bodyLeft = true;
            _fellAt = controller.transform.position;
            _fellYaw = controller.transform.eulerAngles.y;
            controller.enabled = false;
            GameSession.Deaths++;
            if (_reading != null) UpdateReading(null);
            puzzle.PlayerDied(Holding && (handAnimator == null || handAnimator.Lit));
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

            Say(_deathLine);
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
            _bodyLeft = false;
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
            hand.gameObject.SetActive(Holding);
            if (handAnimator != null)
            {
                handAnimator.enabled = true;
                if (Holding) handAnimator.LightUp(0.5f);
            }
            if (lighterLight != null) lighterLight.enabled = Holding;
            SetMuffled(false);
            _air = FullAir;
            _breath = breaths;
            controller.canMove = true;
            controller.enabled = true;

            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                _fade.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.8f, 0f, t / 1.2f));
                yield return null;
            }
            Say("");
            _spaceLatched = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
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
            // Into the dark and straight back out into the corridor; only the way out of the bunker gets a moment.
            bool more = flow != null && !flow.IsLastRoom;
            if (!more && !FreeAir) Say("ROOM CLEARED");
            float fade = more ? 1f : 2f;
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 1f, t / fade));
                yield return null;
            }
            if (!more) yield return new WaitForSeconds(0.6f);
            Say("");
            if (flow != null) flow.OnRoomCleared();
            else Restart();
        }

        void Restart()
        {
            LeaveBody();
            Time.timeScale = 1f;
            FirstPersonController.Frozen = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

        // Heartbeat while the body is short of air (urgency 0..1): louder and faster the worse it gets.
        void UpdateHeart(float urgency)
        {
            if (heartbeatClip == null || urgency <= 0f) return;
            if (Time.time < _nextBeat) return;
            _heart.PlayOneShot(heartbeatClip, Mathf.Lerp(0.35f, 1f, urgency));
            _nextBeat = Time.time + Mathf.Lerp(1.1f, 0.48f, urgency);
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
            // Capped per frame: the room's first frame carries the whole scene load and would eat the message at once.
            for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
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

            // The breaths, as a row of little lungs: the pair being held empties from the top down, spent ones are left
            // as faint outlines.
            var airLabel = MakeText(canvasGo.transform, font, 9, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(10, -9));
            airLabel.text = "AIR";
            airLabel.color = new Color(0.7f, 0.65f, 0.58f, 0.85f);
            airLabel.gameObject.SetActive(!FreeAir);
            var lungs = LungsSprite(false);
            var outline = LungsSprite(true);
            _pips = new Image[breaths];
            _pipRings = new Image[breaths];
            for (int i = 0; i < breaths; i++)
            {
                var at = new Vector2(30 + i * 17, -7);
                _pipRings[i] = HudImage(canvasGo.transform, "BreathOutline", outline, new Vector2(0, 1), at, new Vector2(LungsW, LungsH));
                _pips[i] = HudImage(canvasGo.transform, "Breath", lungs, new Vector2(0, 1), at, new Vector2(LungsW, LungsH));
                _pips[i].type = Image.Type.Filled;
                _pips[i].fillMethod = Image.FillMethod.Vertical;
                _pips[i].fillOrigin = (int)Image.OriginVertical.Bottom;
            }

            _attempt = MakeText(canvasGo.transform, font, 9, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(11, -24));
            _attempt.color = new Color(0.7f, 0.65f, 0.58f, 0.85f);
            _controls = MakeText(canvasGo.transform, font, 9, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-10, -8));
            _controls.text = "[E] use   [F] lighter   [R] restart\n" + LetGoHint;
            _controls.lineSpacing = 1.2f;
            _prompt = MakeText(canvasGo.transform, font, 11, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 18));
            _message = MakeText(canvasGo.transform, font, 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero);

            // Letting go: a short bar over the prompt fills while Space is held.
            var white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            _holdText = MakeText(canvasGo.transform, font, 9, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 42));
            _holdText.text = "letting go...";
            var holdBack = HudImage(_holdText.transform, "HoldBack", white, new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(64, 3));
            holdBack.color = new Color(0f, 0f, 0f, 0.6f);
            _holdBar = HudImage(_holdText.transform, "HoldBar", white, new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(64, 3));
            _holdBar.type = Image.Type.Filled;
            _holdBar.fillMethod = Image.FillMethod.Horizontal;
            _holdBar.color = new Color(0.95f, 0.9f, 0.8f, 0.9f);
            _holdText.gameObject.SetActive(false);
        }

        const string LetGoHint = "hold [SPACE] to let go";

        static readonly Color LungColor = new Color(0.93f, 0.84f, 0.78f, 0.95f);
        static readonly Color ChokeColor = new Color(1f, 0.3f, 0.22f, 1f);

        void UpdatePips()
        {
            bool alive = _phase == Phase.Alive;
            for (int i = 0; i < _pips.Length; i++)
            {
                // Pair i holds breath i+1: the rightmost goes first.
                float fill = Mathf.Clamp01((_air - i * breathSeconds) / breathSeconds);
                _pips[i].fillAmount = fill;
                Color c = LungColor;
                if (alive && i == _breath - 1)
                    c = Color.Lerp(c, ChokeColor, Choke * (0.65f + 0.35f * Mathf.Sin(Time.time * 10f)));
                _pips[i].color = c;
                _pipRings[i].color = fill > 0f ? new Color(0.62f, 0.52f, 0.48f, 0.75f) : new Color(0.45f, 0.42f, 0.4f, 0.35f);
            }

            float hold = Mathf.Clamp01(_letGo / letGoSeconds);
            bool showHold = alive && hold > 0.03f;
            if (_holdText.gameObject.activeSelf != showHold) _holdText.gameObject.SetActive(showHold);
            _holdBar.fillAmount = hold;
        }

        static Image HudImage(Transform parent, string name, Sprite sprite, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = pivot;
            rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            return image;
        }

        const int LungsW = 13, LungsH = 12;

        // A pair of lungs in pixels: two lobes, narrow at the top and broad at the base, hanging off the windpipe.
        static bool InLungs(int x, int y)
        {
            float w = 2.4f + 0.5f * Mathf.Max(0f, 6f - y) / 6f;
            float lx = (x - 3.2f) / w, rx = (x - 8.8f) / w, ly = (y - 3.8f) / 4.3f;
            bool lobe = lx * lx + ly * ly <= 1f || rx * rx + ly * ly <= 1f;
            bool windpipe = x == 6 && y >= 7;
            bool bronchi = y == 7 && (x == 5 || x == 7);
            return lobe || windpipe || bronchi;
        }

        // White, to be tinted: full lungs (solid outline, lighter inside), or only the outline of a spent breath.
        static Sprite LungsSprite(bool outlineOnly)
        {
            var tex = new Texture2D(LungsW, LungsH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[LungsW * LungsH];
            for (int y = 0; y < LungsH; y++)
                for (int x = 0; x < LungsW; x++)
                {
                    float a = 0f;
                    if (InLungs(x, y))
                    {
                        bool edge = x == 0 || y == 0 || x == LungsW - 1 || y == LungsH - 1 ||
                                    !InLungs(x - 1, y) || !InLungs(x + 1, y) || !InLungs(x, y - 1) || !InLungs(x, y + 1);
                        a = edge || x == 6 ? 1f : outlineOnly ? 0f : 0.62f;
                    }
                    px[y * LungsW + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, LungsW, LungsH), new Vector2(0.5f, 0.5f), 100f);
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
