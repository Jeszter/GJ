using System.Collections;
using GJ.PSX;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GJ.Game
{
    // Everything around the room itself: the title screen (slow camera drifts through the dark room), the illustrated
    // typewriter backstory, pause and settings menus, and the card shown when a room is cleared. All of it plays on an
    // old TV set that clicks on at the start and zooms away into the game. The title and the story live in the main
    // menu scene, which has no player (no director); a room always opens straight into play, diving in through the
    // TV's glass, and "main menu" goes back to that scene.
    public class GameFlow : MonoBehaviour
    {
        enum State { Title, Intro, Playing, Paused, End }

        const string MenuScene = "MainMenu";
        const string FirstScene = "Hub";   // the Sector 4 corridor, a door to every room

        public RoomDirector director;      // none in the main menu
        public StoryStage stage;           // 3D sets filmed for the story cards; the painted plates are the fallback
        public FirstPersonController controller;
        public Transform head;
        public GameObject hand;
        public int room = 1;
        public string nextScene;            // where walking out leads: the corridor from a room; empty for the corridor's way out
        public Vector3[] shotPositions;
        public Vector3[] shotAngles;
        public AudioClip humClip;
        public AudioClip moveClip;
        public AudioClip pickClip;
        public AudioClip typeClip;
        public AudioClip staticClip;
        public AudioClip titleMusic;   // menus and the story
        public AudioClip roomMusic;    // quiet bed under the room while playing

        static readonly string[] Story =
        {
            "1987.",
            "Beneath an old coal mine lies a bunker\nthat no map remembers.\n\nSector 4.",
            "You were sent down to clear\nthe last of the old charges.\n\nThe vault door sealed behind you.",
            "Four sealed rooms.\nA few breaths of air in each.\nA lighter.",
            "In Sector 4, dying is not the end.\n\nDying for nothing is."
        };

        State _state;
        State _settingsReturn;
        float _crt = 1f;
        float _shotDim = 0.35f;   // how dark the drifting title shots sit behind whatever is on screen
        static readonly int CrtId = Shader.PropertyToID("_PsxCrt");
        static readonly int TvOffId = Shader.PropertyToID("_PsxTvOff");
        static readonly int StaticId = Shader.PropertyToID("_PsxStatic");
        static readonly int KnobsId = Shader.PropertyToID("_PsxKnobs");

        // The TV's own controls, worked with the mouse on the menu screens (see PSX/Screen for where they sit).
        const float VolumeMax = 2.36f;   // the volume knob sweeps this far (radians) either side of straight up
        float _channelAngle = 1.9f, _channelTurn, _volumeAngle = float.NaN;
        int _dragKnob, _heldButton;
        bool _tvOn = true;
        float _static;
        Vector3 _headRest;
        Quaternion _headRestRot;

        Font _font;
        AudioSource _ui;
        Image _black;
        Text _title, _tagline, _story, _endText;
        KeyLine _hint, _skip;
        RectTransform _artPanel;
        RawImage _art;
        Image[] _dots;
        IntroArt _introArt;
        PsxMenu _titleMenu, _pauseMenu, _settingsMenu, _endMenu;
        PsxMenu _activeMenu;
        Coroutine _shots;
        AudioSource _titleMusic, _roomMusic;

        public bool IsLastRoom => string.IsNullOrEmpty(nextScene) || !Application.CanStreamedLevelBeLoaded(nextScene);

        const string Tagline = "death is part of the solution";
        const string SkipHint = "[ESC] skip";

        void Awake()
        {
            GameSettings.Load();
            Loc.Load();
            _headRest = head.localPosition;
            _headRestRot = head.localRotation;
        }

        void Start()
        {
            BuildUi();
            Loc.Changed += OnLanguage;
            GameSettings.Apply();

            if (humClip != null)
            {
                var hum = gameObject.AddComponent<AudioSource>();
                hum.clip = humClip;
                hum.loop = true;
                hum.volume = 0.35f;
                hum.Play();
            }
            _titleMusic = MusicSource(titleMusic, true);
            _roomMusic = MusicSource(roomMusic, false);

            if (director == null)
            {
                ShowTitle();
                StartCoroutine(PowerOn());
            }
            else
            {
                Shader.SetGlobalFloat(TvOffId, 0f);
                BeginPlay(GameSession.FromStory);
                GameSession.FromStory = false;
            }
        }

        // ---------------------------------------------------------------- the TV set

        // The set clicks on: a dot, a line, the picture, a moment of snow while the tube warms up.
        IEnumerator PowerOn()
        {
            Shader.SetGlobalFloat(TvOffId, 1f);
            yield return null;
            PlayUi(staticClip, 0.5f);
            for (float t = 0f; t < 0.55f; t += Time.unscaledDeltaTime)
            {
                Shader.SetGlobalFloat(TvOffId, 1f - Mathf.SmoothStep(0f, 1f, t / 0.55f));
                _static = Mathf.Max(_static, 0.7f);
                yield return null;
            }
            Shader.SetGlobalFloat(TvOffId, 0f);
        }

        IEnumerator PowerOff()
        {
            PlayUi(pickClip, 0.4f);
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                Shader.SetGlobalFloat(TvOffId, t / 0.45f);
                yield return null;
            }
            Shader.SetGlobalFloat(TvOffId, 1f);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        // A short burst of snow, like flicking to the next channel.
        void Snow(float amount)
        {
            _static = Mathf.Max(_static, amount);
            PlayUi(staticClip, 0.25f * amount);
        }

        // ---------------------------------------------------------------- states

        void ShowTitle()
        {
            _state = State.Title;
            SetPlayActive(false);
            _title.gameObject.SetActive(true);
            _tagline.gameObject.SetActive(true);
            _black.color = new Color(0, 0, 0, 0.35f);
            Open(_titleMenu);
            _shots = StartCoroutine(TitleShots());
        }

        void SetPlayActive(bool on)
        {
            if (director != null) director.enabled = on;
            if (controller != null) controller.enabled = on;
            FirstPersonController.Frozen = !on;
            if (hand != null) hand.SetActive(on && (director == null || director.HandVisible));
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        // Slow drifting shots through the room behind the title, cut through black.
        IEnumerator TitleShots()
        {
            int shot = 0;
            while (true)
            {
                Vector3 pos = shotPositions[shot];
                Quaternion rot = Quaternion.Euler(shotAngles[shot]);
                head.SetPositionAndRotation(pos, rot);
                for (float t = 0f; t < 7f; t += Time.unscaledDeltaTime)
                {
                    head.position = pos + rot * Vector3.forward * (t * 0.05f);
                    head.rotation = rot * Quaternion.Euler(0f, Mathf.Sin(t * 0.35f) * 2.5f, 0f);
                    float edge = Mathf.Min(t, 7f - t);
                    if (_state == State.Title || _state == State.Intro)
                        _black.color = new Color(0, 0, 0, Mathf.Lerp(_shotDim, 1f, 1f - Mathf.Clamp01(edge / 0.8f)));
                    yield return null;
                }
                shot = (shot + 1) % shotPositions.Length;
            }
        }

        IEnumerator Intro()
        {
            _state = State.Intro;
            Open(null);
            _title.gameObject.SetActive(false);
            _tagline.gameObject.SetActive(false);
            // The room keeps drifting by behind the story, barely there.
            float from = _shotDim;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                _shotDim = Mathf.Lerp(from, 0.95f, t / 0.6f);
                yield return null;
            }
            _shotDim = 0.95f;

            _artPanel.gameObject.SetActive(true);
            _skip.gameObject.SetActive(true);
            for (int i = 0; i < Story.Length; i++)
            {
                Snow(0.9f);
                if (stage != null) { stage.Show(i); _art.texture = stage.Texture; }
                else _introArt.Show(i);
                for (int d = 0; d < _dots.Length; d++)
                    _dots[d].color = d == i ? new Color(0.95f, 0.8f, 0.5f) : d < i ? new Color(0.5f, 0.42f, 0.32f) : new Color(0.22f, 0.2f, 0.18f);
                bool skipAll = false;
                yield return Type(Loc.T(Story[i]), s => skipAll = s);
                if (skipAll) break;
            }
            _artPanel.gameObject.SetActive(false);
            _skip.gameObject.SetActive(false);
            if (stage != null) stage.Hide();
            _story.text = "";
            _hint.text = "";
            Snow(1f);
            // On into the corridor, which opens on the TV and dives in through the glass.
            GameSession.FromStory = true;
            LoadScene(FirstScene);
        }

        // Types one card out, holds it a moment and moves on by itself; a key finishes the typing or moves on sooner.
        // Esc (or Backspace, Tab, right click) skips the whole story.
        IEnumerator Type(string card, System.Action<bool> skipAll)
        {
            _story.text = "";
            _hint.text = "";
            int shown = 0;
            float next = 0.25f;
            while (shown < card.Length)
            {
                if (SkipStory()) { skipAll(true); yield break; }
                if (Advance()) { shown = card.Length; break; }
                next -= Time.unscaledDeltaTime;
                if (next <= 0f)
                {
                    shown++;
                    char c = card[shown - 1];
                    next = c == '\n' ? 0.25f : c == '.' ? 0.18f : 0.035f;
                    if (typeClip != null && c != ' ' && c != '\n' && shown % 2 == 0) _ui.PlayOneShot(typeClip, 0.35f);
                }
                // The untyped rest is laid out but transparent, so centred lines don't shift as they fill in.
                _story.text = card.Substring(0, shown) + "<color=#00000000>" + card.Substring(shown) + "</color>";
                yield return null;
            }
            _story.text = card;
            yield return null;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                if (SkipStory()) { skipAll(true); yield break; }
                if (Advance() || t > 3.5f) break;
                _hint.text = Mathf.Repeat(t, 1.2f) < 0.8f ? Loc.T("[ENTER] next") : "";
                yield return null;
            }
        }

        static bool Advance()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        static bool Escape() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        static bool SkipStory()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame))
                || (mouse != null && mouse.rightButton.wasPressedThisFrame);
        }

        void BeginPlay(bool fadeIn)
        {
            if (_shots != null) StopCoroutine(_shots);
            Open(null);
            _title.gameObject.SetActive(false);
            _tagline.gameObject.SetActive(false);
            head.localPosition = _headRest;
            head.localRotation = _headRestRot;
            Time.timeScale = 1f;
            _state = State.Playing;
            SetPlayActive(true);
            GameSettings.Apply();
            StartCoroutine(FadeFromBlack(fadeIn ? 1.8f : 0.6f));
        }

        // Real time for the screen's own animations, but never more than a short step at once: the first frame after a
        // scene loads carries the whole load, and would skip the fade or the dive into the TV in one go.
        static float Step => Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        IEnumerator FadeFromBlack(float seconds)
        {
            for (float t = 0f; t < seconds; t += Step)
            {
                _black.color = new Color(0, 0, 0, 1f - t / seconds);
                yield return null;
            }
            _black.color = Color.clear;
        }

        void Pause()
        {
            _state = State.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            director.enabled = false;
            FirstPersonController.Frozen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _black.color = new Color(0, 0, 0, 0.6f);
            _title.gameObject.SetActive(true);
            _title.text = Loc.T("PAUSED");
            hand.SetActive(false);
            Open(_pauseMenu);
        }

        void Resume()
        {
            _state = State.Playing;
            Open(null);
            _title.gameObject.SetActive(false);
            _title.text = "AFTERBREATH";
            _black.color = Color.clear;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            hand.SetActive(director.HandVisible);
            director.enabled = true;
            FirstPersonController.Frozen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void OnRoomCleared()
        {
            // Out through a room's door and back into the corridor, which marks it done; the card with the run's
            // numbers waits for the way out at the corridor's end.
            if (!IsLastRoom)
            {
                string cleared = SceneManager.GetActiveScene().name;
                GameSession.Cleared.Add(cleared);
                GameSession.JustCleared = cleared;
                LoadScene(nextScene);
                return;
            }
            ShowEnding("SECTOR 4  -  YOU GOT OUT", "the notes were in your handwriting.\nM. was you, every time you kept going.\nthank you for playing.");
        }

        // The last card, with the run's numbers: out through the chapel, or not.
        public void ShowEnding(string title, string line)
        {
            _state = State.End;
            SetPlayActive(false);
            controller.enabled = false;
            _black.color = Color.black;
            int s = Mathf.FloorToInt(GameSession.Time);
            _endText.text = Loc.T(title) + "\n\n" +
                            Loc.F("deaths     {0}", GameSession.Deaths) + "\n" +
                            Loc.F("time       {0:00}:{1:00}", s / 60, s % 60) + "\n\n" + Loc.T(line);
            _endText.gameObject.SetActive(true);
            Snow(0.8f);
            Open(_endMenu);
        }

        // Through one of the corridor's doors: a short walk into the dark, then the room.
        public void EnterRoom(string scene)
        {
            if (_state != State.Playing) return;
            StartCoroutine(EnterRoutine(scene));
        }

        IEnumerator EnterRoutine(string scene)
        {
            controller.enabled = false;
            for (float t = 0f; t < 0.7f; t += Step)
            {
                _black.color = new Color(0, 0, 0, t / 0.7f);
                yield return null;
            }
            _black.color = Color.black;
            LoadScene(scene);
        }

        static void LoadScene(string scene)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            FirstPersonController.Frozen = false;
            SceneManager.LoadScene(scene);
        }

        void RestartRoom()
        {
            if (director != null) director.LeaveBody();
            LoadScene(SceneManager.GetActiveScene().name);
        }

        static void MainMenu()
        {
            GameSession.FromStory = false;
            LoadScene(MenuScene);
        }

        IEnumerator Quit()
        {
            Open(null);
            yield return PowerOff();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OpenSettings(State returnTo)
        {
            _settingsReturn = returnTo;
            _title.gameObject.SetActive(true);
            _title.text = Loc.T("SETTINGS");
            _tagline.gameObject.SetActive(false);
            Open(_settingsMenu);
        }

        void CloseSettings()
        {
            GameSettings.Save();
            bool paused = _settingsReturn == State.Paused;
            _title.text = paused ? Loc.T("PAUSED") : "AFTERBREATH";
            _tagline.gameObject.SetActive(!paused);
            Open(paused ? _pauseMenu : _titleMenu);
        }

        void Open(PsxMenu menu)
        {
            foreach (var m in new[] { _titleMenu, _pauseMenu, _settingsMenu, _endMenu }) m?.Show(m == menu);
            _activeMenu = menu;
        }

        // Picked a language in the settings: the words already on screen follow at once.
        void OnLanguage()
        {
            _tagline.text = Loc.T(Tagline);
            _skip.text = Loc.T(SkipHint);
            if (_activeMenu == _settingsMenu) _title.text = Loc.T("SETTINGS");
            _activeMenu?.Refresh();
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguage;
            Shader.SetGlobalFloat(CrtId, 0f);
            Shader.SetGlobalFloat(TvOffId, 0f);
            Shader.SetGlobalFloat(StaticId, 0f);
        }

        void Update()
        {
            // Every screen that isn't the game itself is shown on the old TV; going into the game the camera dives
            // into the glass, coming out the set pulls back into view.
            float crtTarget = _state == State.Playing ? 0f : 1f;
            _crt = Mathf.MoveTowards(_crt, crtTarget, Step * 1.6f);
            Shader.SetGlobalFloat(CrtId, Mathf.SmoothStep(0f, 1f, _crt));
            _static = Mathf.MoveTowards(_static, 0f, Time.unscaledDeltaTime * 4f);
            Shader.SetGlobalFloat(StaticId, _static);

            if (_state == State.Intro)
            {
                if (stage != null) stage.Tick(Time.unscaledDeltaTime);
                else _introArt?.Tick(Time.unscaledDeltaTime);
            }
            UpdateMusic();

            UpdateTvControls();
            if (_tvOn) _activeMenu?.Tick();
            if (_state == State.Playing) GameSession.Time += Time.deltaTime;

            if (!Escape() || !_tvOn) return;
            if (_activeMenu == _settingsMenu) { CloseSettings(); return; }
            if (_state == State.Playing && !director.Busy) Pause();
            else if (_state == State.Paused) Resume();
        }

        // ---------------------------------------------------------------- the set's knobs and buttons

        static float VolumeAngle() => Mathf.PI * 0.5f + VolumeMax - GameSettings.Volume / 10f * (VolumeMax + VolumeMax);

        // Which control is under a point in the set's control-strip coordinates: 1 channel, 2 volume, 3-5 buttons.
        static int ControlAt(Vector2 v)
        {
            if ((v - new Vector2(0.2f, -0.42f)).magnitude < 0.046f) return 1;
            if ((v - new Vector2(0.33f, -0.42f)).magnitude < 0.046f) return 2;
            for (int b = 0; b < 3; b++)
            {
                var d = v - new Vector2(0.55f + b * 0.055f, -0.42f);
                if (Mathf.Abs(d.x) < 0.024f && Mathf.Abs(d.y) < 0.017f) return 3 + b;
            }
            return 0;
        }

        void UpdateTvControls()
        {
            int hover = 0;
            var mouse = Mouse.current;
            // Follow the volume setting when it is changed from the settings menu instead.
            if (_dragKnob != 2 && (float.IsNaN(_volumeAngle) || Mathf.Abs(_volumeAngle - VolumeAngle()) > VolumeMax * 0.1f + 0.01f))
                _volumeAngle = VolumeAngle();
            bool menus = _state == State.Title || _state == State.Paused || _state == State.End;
            if (mouse != null && menus && _crt > 0.97f && Screen.height > 0)
            {
                Vector2 pos = mouse.position.ReadValue();
                // Window point in the strip's layout units (window heights, laid out for 16:9).
                var v = new Vector2((pos.x / Screen.width - 0.5f) * 1.7778f, pos.y / Screen.height - 0.5f);
                hover = ControlAt(v);
                float scroll = mouse.scroll.ReadValue().y;

                if (mouse.leftButton.wasPressedThisFrame && hover > 0)
                {
                    if (hover <= 2) _dragKnob = hover;
                    else { _heldButton = hover; PressButton(hover - 3); }
                }
                if (!mouse.leftButton.isPressed)
                {
                    if (_dragKnob == 2) GameSettings.Save();
                    _dragKnob = 0;
                    _heldButton = 0;
                }

                // Turning: drag sideways (right = clockwise) or roll the wheel over the knob.
                float turn = 0f;
                int knob = _dragKnob;
                if (knob > 0) turn = -mouse.delta.ReadValue().x * 0.012f;
                else if (hover > 0 && hover <= 2 && Mathf.Abs(scroll) > 0.01f) { knob = hover; turn = -Mathf.Sign(scroll) * 0.25f; }
                if (knob == 1 && turn != 0f) TurnChannel(turn);
                if (knob == 2 && turn != 0f) TurnVolume(turn, _dragKnob == 0);
                if (_dragKnob > 0) hover = _dragKnob;
            }
            Shader.SetGlobalVector(KnobsId, new Vector4(_channelAngle, _volumeAngle, hover, _heldButton));
        }

        // The channel knob clicks round in steps; each click moves the menu one item, with a flick of snow.
        void TurnChannel(float turn)
        {
            if (!_tvOn) return;
            _channelAngle += turn;
            _channelTurn += turn;
            while (Mathf.Abs(_channelTurn) >= 0.5f)
            {
                int dir = _channelTurn < 0f ? 1 : -1;
                _channelTurn += dir * 0.5f;
                _activeMenu?.Move(dir);
                Snow(0.35f);
            }
        }

        // The volume knob turns smoothly between its stops; the game volume follows it in tenths.
        void TurnVolume(float turn, bool save)
        {
            _volumeAngle = Mathf.Clamp(_volumeAngle + turn, Mathf.PI * 0.5f - VolumeMax, Mathf.PI * 0.5f + VolumeMax);
            int volume = Mathf.RoundToInt((Mathf.PI * 0.5f + VolumeMax - _volumeAngle) / (VolumeMax * 2f) * 10f);
            if (volume == GameSettings.Volume) return;
            GameSettings.Volume = volume;
            GameSettings.Apply();
            PlayUi(moveClip, 0.6f);
            if (save) GameSettings.Save();
            _activeMenu?.Refresh();
        }

        // Buttons: picture mode, a burst of snow, and power.
        void PressButton(int b)
        {
            PlayUi(pickClip, 0.5f);
            if (b == 2) { StartCoroutine(TogglePower()); return; }
            if (!_tvOn) return;
            if (b == 0)
            {
                GameSettings.Pixels = (GameSettings.Pixels + 1) % GameSettings.PixelRows.Length;
                GameSettings.Apply();
                GameSettings.Save();
                _activeMenu?.Refresh();
                Snow(0.6f);
            }
            else Snow(1f);
        }

        IEnumerator TogglePower()
        {
            if (_tvOn)
            {
                _tvOn = false;
                yield return PowerOff();
            }
            else
            {
                yield return PowerOn();
                _tvOn = true;
            }
        }

        AudioSource MusicSource(AudioClip clip, bool ignorePause)
        {
            if (clip == null) return null;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = ignorePause;
            source.Play();
            return source;
        }

        // The theme plays over the title, story and end card; in the room only the low bed is left.
        void UpdateMusic()
        {
            bool menus = _state != State.Playing && _state != State.Paused;
            float dt = Time.unscaledDeltaTime;
            if (_titleMusic != null) _titleMusic.volume = Mathf.MoveTowards(_titleMusic.volume, menus ? 0.6f : 0f, dt * (menus ? 0.4f : 0.3f));
            if (_roomMusic != null) _roomMusic.volume = Mathf.MoveTowards(_roomMusic.volume, menus ? 0f : 0.32f, dt * 0.15f);
        }

        void PlayUi(AudioClip clip, float volume)
        {
            if (clip != null && _ui != null) _ui.PlayOneShot(clip, volume);
        }

        // ---------------------------------------------------------------- UI

        void BuildUi()
        {
            _font = UiStyle.Font;
            var cam = head.GetComponent<Camera>();

            _ui = gameObject.AddComponent<AudioSource>();
            _ui.ignoreListenerPause = true;
            _ui.spatialBlend = 0f;

            var canvasGo = new GameObject("FlowUI");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.012f;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(427, 240);
            scaler.matchWidthOrHeight = 1f;
            var root = canvasGo.transform;

            _black = new GameObject("Black").AddComponent<Image>();
            _black.transform.SetParent(root, false);
            _black.rectTransform.anchorMin = Vector2.zero;
            _black.rectTransform.anchorMax = Vector2.one;
            _black.rectTransform.offsetMin = _black.rectTransform.offsetMax = Vector2.zero;
            _black.raycastTarget = false;
            _black.color = Color.black;

            _title = Label(root, "AFTERBREATH", 30, new Vector2(0, 62), new Color(0.95f, 0.88f, 0.75f));
            _tagline = Label(root, Loc.T(Tagline), 10, new Vector2(0, 38), new Color(0.75f, 0.35f, 0.25f));
            BuildStoryPanel(root);
            _story = Label(root, "", 12, new Vector2(0, -50), new Color(0.9f, 0.86f, 0.78f));
            _story.lineSpacing = 1.1f;
            _hint = KeyLine.Create(root, "Hint", 9, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(140, -96), KeyLine.Look.Dim, 200f);
            _skip = KeyLine.Create(root, "Skip", 9, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(-140, -96), KeyLine.Look.Dim, 200f);
            _skip.text = Loc.T(SkipHint);
            _skip.gameObject.SetActive(false);
            _endText = Label(root, "", 11, new Vector2(0, 40), new Color(0.92f, 0.88f, 0.8f));
            _endText.gameObject.SetActive(false);

            _titleMenu = new PsxMenu(root, cam, _font, new Vector2(0, -45), 12, 17, _ui, moveClip, pickClip);
            _titleMenu.Add("NEW GAME", () => { GameSession.Reset(); StartCoroutine(Intro()); });
            _titleMenu.Add("SETTINGS", () => OpenSettings(State.Title));
            _titleMenu.Add("QUIT", () => StartCoroutine(Quit()));

            _pauseMenu = new PsxMenu(root, cam, _font, new Vector2(0, -30), 12, 17, _ui, moveClip, pickClip);
            _pauseMenu.Add("RESUME", Resume);
            _pauseMenu.Add("SETTINGS", () => OpenSettings(State.Paused));
            _pauseMenu.Add("RESTART ROOM", RestartRoom);
            _pauseMenu.Add("MAIN MENU", MainMenu);
            _pauseMenu.Add("QUIT", () => StartCoroutine(Quit()));

            _settingsMenu = new PsxMenu(root, cam, _font, new Vector2(0, -25), 11, 15, _ui, moveClip, pickClip);
            _settingsMenu.AddOption("MOUSE", () => GameSettings.Sensitivity.ToString(), d => { GameSettings.Sensitivity = Mathf.Clamp(GameSettings.Sensitivity + d, 1, 10); GameSettings.Apply(); });
            _settingsMenu.AddOption("INVERT Y", () => GameSettings.InvertY ? "ON" : "OFF", d => { GameSettings.InvertY = !GameSettings.InvertY; GameSettings.Apply(); });
            _settingsMenu.AddOption("VOLUME", () => GameSettings.Volume.ToString(), d => { GameSettings.Volume = Mathf.Clamp(GameSettings.Volume + d, 0, 10); GameSettings.Apply(); });
            _settingsMenu.AddOption("PIXELS", () => GameSettings.PixelNames[GameSettings.Pixels], d => { GameSettings.Pixels = (GameSettings.Pixels + d + GameSettings.PixelRows.Length) % GameSettings.PixelRows.Length; GameSettings.Apply(); });
            _settingsMenu.AddOption("FULLSCREEN", () => GameSettings.Fullscreen ? "ON" : "OFF", d => { GameSettings.Fullscreen = !GameSettings.Fullscreen; GameSettings.Apply(); });
            _settingsMenu.AddOption("LANGUAGE", () => Loc.Name, d => Loc.Set(Loc.Current + d));
            _settingsMenu.Add("BACK", CloseSettings);

            _endMenu = new PsxMenu(root, cam, _font, new Vector2(0, -62), 12, 17, _ui, moveClip, pickClip);
            _endMenu.Add("PLAY AGAIN", () => { GameSession.Reset(); LoadScene(FirstScene); });
            _endMenu.Add("MAIN MENU", MainMenu);

            Open(null);
            _title.gameObject.SetActive(false);
            _tagline.gameObject.SetActive(false);
        }

        // The picture for each story card: a pixel-art plate in a thin worn frame, with a row of dots under it
        // showing how far into the story you are.
        void BuildStoryPanel(Transform root)
        {
            _introArt = new IntroArt();
            _artPanel = new GameObject("StoryArt").AddComponent<RectTransform>();
            _artPanel.SetParent(root, false);
            _artPanel.anchoredPosition = new Vector2(0, 38);
            _artPanel.sizeDelta = new Vector2(IntroArt.W + 6, IntroArt.H + 6);

            var frame = _artPanel.gameObject.AddComponent<Image>();
            frame.color = new Color(0.42f, 0.36f, 0.28f);
            frame.raycastTarget = false;
            var inner = new GameObject("Mat").AddComponent<Image>();
            inner.transform.SetParent(_artPanel, false);
            inner.rectTransform.sizeDelta = new Vector2(IntroArt.W + 2, IntroArt.H + 2);
            inner.color = new Color(0.06f, 0.05f, 0.045f);
            inner.raycastTarget = false;

            _art = new GameObject("Picture").AddComponent<RawImage>();
            _art.transform.SetParent(_artPanel, false);
            _art.texture = _introArt.Texture;
            _art.rectTransform.sizeDelta = new Vector2(IntroArt.W, IntroArt.H);
            _art.raycastTarget = false;

            _dots = new Image[Story.Length];
            for (int i = 0; i < _dots.Length; i++)
            {
                var dot = new GameObject("Dot").AddComponent<Image>();
                dot.transform.SetParent(_artPanel, false);
                dot.rectTransform.sizeDelta = new Vector2(4, 4);
                dot.rectTransform.anchoredPosition = new Vector2((i - (Story.Length - 1) * 0.5f) * 9f, -IntroArt.H * 0.5f - 9f);
                dot.raycastTarget = false;
                _dots[i] = dot;
            }
            _artPanel.gameObject.SetActive(false);
        }

        Text Label(Transform parent, string text, int size, Vector2 pos, Color color)
        {
            var t = new GameObject("Label").AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.text = text;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(1, -1);
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = new Vector2(400, 40);
            return t;
        }
    }
}
