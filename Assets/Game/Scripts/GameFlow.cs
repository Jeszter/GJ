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
    // old TV set that clicks on at the start and zooms away into the game. Restarting a room skips straight back into
    // play; "main menu" goes back to the first room's title.
    public class GameFlow : MonoBehaviour
    {
        enum State { Title, Intro, Playing, Paused, End }

        public RoomDirector director;
        public FirstPersonController controller;
        public Transform head;
        public GameObject hand;
        public int room = 1;
        public string nextScene;            // scene to go on to once this room is cleared; empty for the last one
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
            "Ninety seconds of air.\nA lighter.\nA trail of powder.",
            "In Sector 4, dying is not the end.\n\nDying for nothing is."
        };

        State _state;
        State _settingsReturn;
        float _crt = 1f;
        float _shotDim = 0.35f;   // how dark the drifting title shots sit behind whatever is on screen
        static readonly int CrtId = Shader.PropertyToID("_PsxCrt");
        static readonly int TvOffId = Shader.PropertyToID("_PsxTvOff");
        static readonly int StaticId = Shader.PropertyToID("_PsxStatic");
        float _static;
        Vector3 _headRest;
        Quaternion _headRestRot;

        Font _font;
        AudioSource _ui;
        Image _black;
        Text _title, _tagline, _story, _hint, _skip, _endText;
        RectTransform _artPanel;
        RawImage _art;
        Image[] _dots;
        IntroArt _introArt;
        PsxMenu _titleMenu, _pauseMenu, _settingsMenu, _endMenu, _nextMenu;
        PsxMenu _activeMenu;
        Coroutine _shots;
        AudioSource _titleMusic, _roomMusic;

        bool IsLastRoom => string.IsNullOrEmpty(nextScene) || !Application.CanStreamedLevelBeLoaded(nextScene);

        void Awake()
        {
            GameSettings.Load();
            _headRest = head.localPosition;
            _headRestRot = head.localRotation;
        }

        void Start()
        {
            BuildUi();
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

            // Later rooms are only ever reached from the one before (or straight from the editor): no title there.
            if (GameSession.SkipTitle || room > 1)
            {
                GameSession.SkipTitle = false;
                Shader.SetGlobalFloat(TvOffId, 0f);
                BeginPlay(false);
            }
            else
            {
                ShowTitle();
                StartCoroutine(PowerOn());
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
            director.enabled = on;
            controller.enabled = on;
            FirstPersonController.Frozen = !on;
            hand.SetActive(on);
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
                _introArt.Show(i);
                for (int d = 0; d < _dots.Length; d++)
                    _dots[d].color = d == i ? new Color(0.95f, 0.8f, 0.5f) : d < i ? new Color(0.5f, 0.42f, 0.32f) : new Color(0.22f, 0.2f, 0.18f);
                bool skipAll = false;
                yield return Type(Story[i], s => skipAll = s);
                if (skipAll) break;
            }
            _artPanel.gameObject.SetActive(false);
            _skip.gameObject.SetActive(false);
            _story.text = "";
            _hint.text = "";
            Snow(1f);
            BeginPlay(true);
        }

        // Types one card out; a key finishes it, the next key moves on. Esc (or Backspace) skips the whole story.
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
                if (Advance() || t > 7f) break;
                _hint.text = Mathf.Repeat(t, 1.2f) < 0.8f ? "[ENTER] next" : "";
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

        IEnumerator FadeFromBlack(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
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
            _title.text = "PAUSED";
            hand.SetActive(false);
            Open(_pauseMenu);
        }

        void Resume()
        {
            _state = State.Playing;
            Open(null);
            _title.gameObject.SetActive(false);
            _title.text = "ONE MORE TIME";
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
            _state = State.End;
            SetPlayActive(false);
            controller.enabled = false;
            _black.color = Color.black;
            int s = Mathf.FloorToInt(GameSession.Time);
            bool last = IsLastRoom;
            _endText.text = $"SECTOR 4  -  ROOM {room} CLEARED\n\n" +
                            $"deaths     {GameSession.Deaths}\n" +
                            $"time       {s / 60:00}:{s % 60:00}\n\n" +
                            (last ? "the last door is open.\nthank you for playing." : "somewhere ahead, water is running.");
            _endText.gameObject.SetActive(true);
            Snow(0.8f);
            Open(last ? _endMenu : _nextMenu);
        }

        static void LoadScene(string scene, bool skipTitle)
        {
            GameSession.SkipTitle = skipTitle;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            FirstPersonController.Frozen = false;
            SceneManager.LoadScene(scene);
        }

        static void RestartRoom() => LoadScene(SceneManager.GetActiveScene().name, true);

        // The title lives in the first room of the build.
        static void MainMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            FirstPersonController.Frozen = false;
            GameSession.SkipTitle = false;
            SceneManager.LoadScene(0);
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
            _title.text = "SETTINGS";
            _tagline.gameObject.SetActive(false);
            Open(_settingsMenu);
        }

        void CloseSettings()
        {
            GameSettings.Save();
            bool paused = _settingsReturn == State.Paused;
            _title.text = paused ? "PAUSED" : "ONE MORE TIME";
            _tagline.gameObject.SetActive(!paused);
            Open(paused ? _pauseMenu : _titleMenu);
        }

        void Open(PsxMenu menu)
        {
            foreach (var m in new[] { _titleMenu, _pauseMenu, _settingsMenu, _endMenu, _nextMenu }) m?.Show(m == menu);
            _activeMenu = menu;
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat(CrtId, 0f);
            Shader.SetGlobalFloat(TvOffId, 0f);
            Shader.SetGlobalFloat(StaticId, 0f);
        }

        void Update()
        {
            // Every screen that isn't the game itself is shown on the old TV; going into the game the camera dives
            // into the glass, coming out the set pulls back into view.
            float crtTarget = _state == State.Playing ? 0f : 1f;
            _crt = Mathf.MoveTowards(_crt, crtTarget, Time.unscaledDeltaTime * 1.6f);
            Shader.SetGlobalFloat(CrtId, Mathf.SmoothStep(0f, 1f, _crt));
            _static = Mathf.MoveTowards(_static, 0f, Time.unscaledDeltaTime * 4f);
            Shader.SetGlobalFloat(StaticId, _static);

            if (_state == State.Intro) _introArt?.Tick(Time.unscaledDeltaTime);
            UpdateMusic();

            _activeMenu?.Tick();
            if (_state == State.Playing) GameSession.Time += Time.deltaTime;

            if (!Escape()) return;
            if (_activeMenu == _settingsMenu) { CloseSettings(); return; }
            if (_state == State.Playing && !director.Busy) Pause();
            else if (_state == State.Paused) Resume();
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
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            _title = Label(root, "ONE MORE TIME", 30, new Vector2(0, 62), new Color(0.95f, 0.88f, 0.75f));
            _tagline = Label(root, "death is part of the solution", 10, new Vector2(0, 38), new Color(0.75f, 0.35f, 0.25f));
            BuildStoryPanel(root);
            _story = Label(root, "", 12, new Vector2(0, -50), new Color(0.9f, 0.86f, 0.78f));
            _story.lineSpacing = 1.1f;
            _hint = Label(root, "", 9, new Vector2(140, -96), new Color(0.6f, 0.55f, 0.5f));
            _skip = Label(root, "[ESC] skip", 9, new Vector2(-140, -96), new Color(0.5f, 0.46f, 0.42f));
            _skip.gameObject.SetActive(false);
            _endText = Label(root, "", 12, new Vector2(0, 40), new Color(0.92f, 0.88f, 0.8f));
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
            _settingsMenu.Add("BACK", CloseSettings);

            _nextMenu = new PsxMenu(root, cam, _font, new Vector2(0, -62), 12, 17, _ui, moveClip, pickClip);
            _nextMenu.Add("NEXT ROOM", () => LoadScene(nextScene, true));
            _nextMenu.Add("MAIN MENU", MainMenu);

            _endMenu = new PsxMenu(root, cam, _font, new Vector2(0, -62), 12, 17, _ui, moveClip, pickClip);
            _endMenu.Add("PLAY AGAIN", () => { GameSession.Reset(); GameSession.SkipTitle = true; SceneManager.LoadScene(0); });
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
