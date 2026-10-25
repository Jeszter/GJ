using UnityEngine;

namespace GJ.Game
{
    // The look of every word on screen: bold typewriter type, controls written on the round keys of an old typewriter,
    // what can be done right now on a strip of the notes' yellowed paper, and the revolver's cylinder for its rounds.
    // The face comes from the system (Courier New on Windows), so nothing is bundled; without it the built-in font is used.
    public static class UiStyle
    {
        public static readonly Color Type = new Color(0.93f, 0.88f, 0.78f);     // light type over the dark
        public static readonly Color Dim = new Color(0.74f, 0.68f, 0.6f);
        public static readonly Color Ink = new Color(0.1f, 0.11f, 0.19f);       // on paper and on the keys
        public static readonly Color KeyFace = new Color(0.91f, 0.86f, 0.76f);
        public static readonly Color Brass = new Color(0.87f, 0.7f, 0.38f);
        public static readonly Color Hollow = new Color(0.13f, 0.11f, 0.1f, 0.9f);
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.85f);

        static readonly string[] Faces = { "Courier New", "Liberation Mono", "DejaVu Sans Mono", "Consolas" };
        static Font _font;
        static Sprite _key, _disc, _ring, _strip;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                var installed = new System.Collections.Generic.HashSet<string>(Font.GetOSInstalledFontNames());
                foreach (var f in Faces)
                    if (installed.Contains(f)) { _font = Font.CreateDynamicFontFromOSFont(f, 16); break; }
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // How wide a run of type comes out (the face is monospaced, so this matches what Text draws).
        public static float Measure(string s, int size, FontStyle style)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            var f = Font;
            f.RequestCharactersInTexture(s, size, style);
            float w = 0f;
            foreach (char c in s)
                if (f.GetCharacterInfo(c, out var ci, size, style)) w += ci.advance;
            return w;
        }

        // A typewriter key: a cream face in a dark rim, round at its natural size, stretching into a pill for SPACE.
        public static Sprite Key => _key != null ? _key : _key = MakeKey();
        public static Sprite Disc => _disc != null ? _disc : _disc = MakeCircle(16, 0f);
        public static Sprite Ring => _ring != null ? _ring : _ring = MakeCircle(32, 2.2f);
        public static Sprite PaperStrip => _strip != null ? _strip : _strip = MakeStrip();

        static Sprite MakeKey()
        {
            const int n = 16;
            var tex = NewTexture(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    Color c;
                    if (d > 7.6f) c = Color.clear;
                    else if (d > 6.2f) c = new Color(0.16f, 0.13f, 0.11f);          // the metal rim
                    else c = Color.Lerp(new Color(0.78f, 0.72f, 0.62f), Color.white, Mathf.Clamp01((y - 3f) / 9f));
                    px[y * n + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(7, 7, 7, 7));
        }

        static Sprite MakeCircle(int n, float rim)
        {
            var tex = NewTexture(n, n);
            var px = new Color32[n * n];
            float r = n / 2f - 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    bool on = rim > 0f ? d <= r && d >= r - rim : d <= r;
                    px[y * n + x] = on ? Color.white : Color.clear;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        // Yellowed paper torn along both short edges; the middle stretches to fit the words.
        static Sprite MakeStrip()
        {
            const int w = 48, h = 16;
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float bite = Mathf.PerlinNoise(y * 0.9f, x < w / 2 ? 3f : 11f) * 3.5f;
                    float fray = Mathf.PerlinNoise(x * 0.7f, y < h / 2 ? 5f : 13f) * 1.4f;
                    bool outside = x < bite || x > w - 1 - bite || y < fray || y > h - 1 - fray;
                    if (outside) { px[y * w + x] = Color.clear; continue; }
                    float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x) / 6f, Mathf.Min(y, h - 1 - y) / 3f);
                    var c = new Color(0.84f, 0.78f, 0.64f) * (0.93f + n * 0.1f) * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(edge));
                    c.a = 0.96f;
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 4, 8, 4));
        }

        static Texture2D NewTexture(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
    }
}
