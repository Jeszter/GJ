using UnityEngine;
using UnityEngine.UI;

namespace GJ.PSX
{
    // Renders the attached camera into a low-res target and shows it full-screen through PSX/Screen
    // (colour quantisation + Bayer dither). Runs in edit mode too, so the Game view shows the real look.
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public class PSXScreen : MonoBehaviour
    {
        [Range(120, 480)] public int verticalResolution = 240;
        public Material screenMaterial;

        Camera _cam;
        RenderTexture _frame;
        GameObject _output;
        Vector2Int _size;
        bool _dirty;

        public RenderTexture Frame => _frame;

        public void SetResolution(int rows)
        {
            if (rows == verticalResolution) return;
            verticalResolution = rows;
            _dirty = true;
        }

        void OnEnable()
        {
            _cam = GetComponent<Camera>();
            Build();
        }

        void OnDisable() => Teardown();

        // Unity forbids creating objects inside OnValidate, so a settings change rebuilds on the next frame.
        void OnValidate() => _dirty = true;

        void LateUpdate()
        {
            if (_dirty || CurrentSize() != _size) { _dirty = false; Teardown(); Build(); }
            Shader.SetGlobalVector(WindowId, WindowSize());
        }

        // Screen size is only meaningful in Play mode; the editor preview assumes 16:9.
        static Vector2 WindowSize() =>
            Application.isPlaying && Screen.height > 0 ? new Vector2(Screen.width, Screen.height) : new Vector2(1920f, 1080f);

        Vector2Int CurrentSize()
        {
            Vector2 window = WindowSize();
            float aspect = window.x / window.y;
            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(verticalResolution * aspect)), verticalResolution);
        }

        // ---------------------------------------------------------------- the TV set (see PSX/Screen)

        static readonly int WindowId = Shader.PropertyToID("_PsxWindow");
        static readonly int CrtId = Shader.PropertyToID("_PsxCrt");
        static readonly Vector2 TvCenter = new Vector2(0.5f, 0.548f);
        static readonly Vector2 TvHalf = new Vector2(0.405f, 0.392f);
        const float TvBulge = 0.085f;

        // Where a point on the window (0..1) lands on the low-res frame (0..1) once the frame is shown through the
        // TV's curved glass, so the mouse can pick menu items on it. Must match the mapping in PSX/Screen.
        public static Vector2 WindowToFrame(Vector2 uv, float frameAspect)
        {
            float crt = Mathf.Clamp01(Shader.GetGlobalFloat(CrtId));
            Vector2 window = WindowSize();
            float windowAspect = window.x / window.y;
            Vector2 center = Vector2.Lerp(new Vector2(0.5f, 0.5f), TvCenter, crt);
            Vector2 half = Vector2.Lerp(new Vector2(0.5f, 0.5f), TvHalf, crt);
            var p = new Vector2((uv.x - center.x) / half.x, (uv.y - center.y) / half.y);
            p += new Vector2(p.x * p.y * p.y, p.y * p.x * p.x) * TvBulge * crt;
            float glassAspect = half.x / half.y * windowAspect;
            Vector2 crop = glassAspect < frameAspect ? new Vector2(glassAspect / frameAspect, 1f) : new Vector2(1f, frameAspect / glassAspect);
            return new Vector2(p.x * crop.x * 0.5f + 0.5f, p.y * crop.y * 0.5f + 0.5f);
        }

        // Hidden outputs survive domain reloads and scene rebuilds without their owner; their frame texture is gone,
        // so a leftover full-screen image draws plain white over the real picture. Sweep them before making ours.
        static void DestroyStrayOutputs()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.name != "PSX Output" || go.transform.parent != null) continue;
                if ((go.hideFlags & HideFlags.HideInHierarchy) == 0) continue;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
        }

        void Build()
        {
            DestroyStrayOutputs();
            _size = CurrentSize();
            _frame = new RenderTexture(_size.x, _size.y, 24)
            {
                filterMode = FilterMode.Point,
                name = "PSX Frame",
                hideFlags = HideFlags.DontSave
            };
            _frame.Create();
            _cam.targetTexture = _frame;

            _output = new GameObject("PSX Output") { hideFlags = HideFlags.HideAndDontSave };

            // Something has to draw to the window, or Unity shows "no cameras rendering".
            var blank = new GameObject("Blank Camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            blank.transform.SetParent(_output.transform, false);
            blank.clearFlags = CameraClearFlags.SolidColor;
            blank.backgroundColor = Color.black;
            blank.cullingMask = 0;
            blank.depth = _cam.depth + 1;

            var canvasGo = new GameObject("Canvas") { hideFlags = HideFlags.HideAndDontSave };
            canvasGo.transform.SetParent(_output.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;

            var imageGo = new GameObject("Frame") { hideFlags = HideFlags.HideAndDontSave };
            imageGo.transform.SetParent(canvasGo.transform, false);
            var image = imageGo.AddComponent<RawImage>();
            image.texture = _frame;
            image.material = screenMaterial;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void Teardown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_output != null)
            {
                if (Application.isPlaying) Destroy(_output); else DestroyImmediate(_output);
                _output = null;
            }
            if (_frame != null)
            {
                _frame.Release();
                if (Application.isPlaying) Destroy(_frame); else DestroyImmediate(_frame);
                _frame = null;
            }
        }
    }
}
