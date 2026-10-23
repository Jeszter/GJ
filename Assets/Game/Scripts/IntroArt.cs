using UnityEngine;

namespace GJ.Game
{
    // Pixel-art plates for the backstory cards, painted in code into a small point-filtered texture: the mine at
    // night, the bunker in cross-section, the sealed vault door, the lighter and the powder, and the body on the floor
    // with the fire still crawling to the door. Each is repainted a dozen times a second, so flames flicker, lamps
    // blink, smoke drifts and the fuse creeps along.
    public class IntroArt
    {
        public const int W = 180, H = 90;
        public readonly Texture2D Texture;

        readonly Color[] _c = new Color[W * H];      // y runs down while painting; flipped on upload
        readonly Color32[] _out = new Color32[W * H];
        int _scene;
        float _t, _nextPaint;

        public IntroArt()
        {
            Texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Intro Art"
            };
        }

        public void Show(int scene)
        {
            _scene = scene;
            _t = 0f;
            Paint();
        }

        public void Tick(float dt)
        {
            _t += dt;
            if (_t >= _nextPaint) Paint();
        }

        void Paint()
        {
            _nextPaint = _t + 1f / 12f;
            switch (_scene)
            {
                case 0: Mine(); break;
                case 1: Section(); break;
                case 2: Vault(); break;
                case 3: Lighter(); break;
                default: Aftermath(); break;
            }
            Finish();
        }

        // ---------------------------------------------------------------- painting kit

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }

        static readonly float[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Dither(int x, int y) => (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f - 0.5f;

        static bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        void Set(int x, int y, Color c) { if (In(x, y)) _c[y * W + x] = c; }
        Color Get(int x, int y) => In(x, y) ? _c[y * W + x] : Color.black;
        void Add(int x, int y, Color c) { if (In(x, y)) _c[y * W + x] += c; }
        void Mix(int x, int y, Color c, float a) { if (In(x, y)) _c[y * W + x] = Color.Lerp(_c[y * W + x], c, Mathf.Clamp01(a)); }

        void Rect(int x, int y, int w, int h, Color c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++) Set(i, j, c);
        }

        // Vertical gradient, ordered-dithered so it bands like an old palette instead of going smooth.
        void Gradient(int y0, int y1, Color top, Color bottom)
        {
            for (int y = y0; y < y1; y++)
                for (int x = 0; x < W; x++)
                {
                    float k = (float)(y - y0) / Mathf.Max(1, y1 - y0 - 1);
                    k = Mathf.Clamp01(Mathf.Round(k * 10f + Dither(x, y)) / 10f);
                    Set(x, y, Color.Lerp(top, bottom, k));
                }
        }

        void Disc(float cx, float cy, float r, Color c)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Set(x, y, c);
        }

        void DiscMix(float cx, float cy, float r, Color c, float a)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Mix(x, y, c, a);
        }

        void Ring(float cx, float cy, float r, float width, Color c)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
                for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (Mathf.Abs(d - r) <= width * 0.5f) Set(x, y, c);
                }
        }

        void Line(float x0, float y0, float x1, float y1, Color c)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float k = (float)i / steps;
                Set(Mathf.RoundToInt(Mathf.Lerp(x0, x1, k)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, k)), c);
            }
        }

        void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            float area = Cross(a, b, c);
            if (Mathf.Abs(area) < 0.001f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross(b, c, p) / area, w1 = Cross(c, a, p) / area, w2 = Cross(a, b, p) / area;
                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f) Set(x, y, col);
                }
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            Tri(a, b, c, col);
            Tri(a, c, d, col);
        }

        // Additive light with a banded falloff, the PS1 way.
        void Glow(float cx, float cy, float r, Color c)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                    if (d >= 1f) continue;
                    float a = (1f - d) * (1f - d);
                    a = Mathf.Round(a * 5f + Dither(x, y) * 0.8f) / 5f;
                    Add(x, y, c * a);
                }
        }

        // Seven-segment digit, 5 x 9 pixels.
        static readonly int[] SegDigits = { 0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110, 0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111 };

        void Digit(int x, int y, int d, Color on, Color off)
        {
            int bits = SegDigits[Mathf.Clamp(d, 0, 9)];
            Rect(x + 1, y, 3, 1, (bits & 1) != 0 ? on : off);
            Rect(x + 4, y + 1, 1, 3, (bits & 2) != 0 ? on : off);
            Rect(x + 4, y + 5, 1, 3, (bits & 4) != 0 ? on : off);
            Rect(x + 1, y + 8, 3, 1, (bits & 8) != 0 ? on : off);
            Rect(x, y + 5, 1, 3, (bits & 16) != 0 ? on : off);
            Rect(x, y + 1, 1, 3, (bits & 32) != 0 ? on : off);
            Rect(x + 1, y + 4, 3, 1, (bits & 64) != 0 ? on : off);
        }

        // Point on a polyline by fraction of its length.
        static Vector2 Along(Vector2[] pts, float k)
        {
            float total = 0f;
            for (int i = 1; i < pts.Length; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
            float d = Mathf.Clamp01(k) * total;
            for (int i = 1; i < pts.Length; i++)
            {
                float seg = Vector2.Distance(pts[i - 1], pts[i]);
                if (d <= seg) return Vector2.Lerp(pts[i - 1], pts[i], seg > 0f ? d / seg : 0f);
                d -= seg;
            }
            return pts[pts.Length - 1];
        }

        void Trail(Vector2[] pts, float from, float to, int width, Color c, int seed)
        {
            for (float k = from; k <= to; k += 0.004f)
            {
                Vector2 p = Along(pts, k);
                for (int j = 0; j < width; j++)
                {
                    int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y) + j - width / 2;
                    Set(x, y, Hash(x, y, seed) > 0.85f ? c * 1.8f : c);
                }
            }
        }

        // A little flame: blue root, orange body, pale core, swaying.
        void Flame(float cx, float baseY, float height, float width, float seed)
        {
            float n = Mathf.PerlinNoise(_t * 7f + seed, seed);
            float h = height * (0.85f + 0.3f * n);
            for (int y = Mathf.FloorToInt(baseY - h); y <= Mathf.CeilToInt(baseY); y++)
            {
                float v = (baseY - y) / h;
                if (v < 0f || v > 1f) continue;
                float sway = Mathf.Sin(_t * 9f + v * 4f + seed) * v * 1.2f;
                float half = width * Mathf.Sin(v * Mathf.PI * 0.95f + 0.1f) * (1f - v * 0.45f);
                for (int x = Mathf.FloorToInt(cx - half - 2); x <= Mathf.CeilToInt(cx + half + 2); x++)
                {
                    float u = Mathf.Abs(x - (cx + sway)) / Mathf.Max(0.5f, half);
                    if (u > 1f) continue;
                    Color col = u < 0.4f && v < 0.75f ? C(1f, 0.95f, 0.75f) : u < 0.75f ? C(1f, 0.66f, 0.2f) : C(0.9f, 0.32f, 0.08f);
                    if (v < 0.16f && u < 0.8f) col = C(0.3f, 0.42f, 1f);
                    Set(x, y, col);
                }
            }
        }

        void Stars(int count, int maxY, int seed)
        {
            for (int k = 0; k < count; k++)
            {
                int x = (int)(Hash(k, 0, seed) * W), y = (int)(Hash(k, 1, seed) * maxY);
                float tw = 0.55f + 0.45f * Mathf.Sin(_t * (1.5f + Hash(k, 2, seed) * 3f) + k);
                float b = 0.25f + Hash(k, 3, seed) * 0.5f;
                Add(x, y, C(0.75f, 0.78f, 0.95f) * b * tw);
            }
        }

        void Dust(int count, float speed, Color c, int seed)
        {
            for (int k = 0; k < count; k++)
            {
                float x = Mathf.Repeat(Hash(k, 0, seed) * W + _t * (1.5f + Hash(k, 1, seed) * 2f), W);
                float y = Mathf.Repeat(Hash(k, 2, seed) * H + _t * speed * (0.6f + Hash(k, 3, seed)), H);
                Mix(Mathf.RoundToInt(x), Mathf.RoundToInt(y), c, 0.55f);
            }
        }

        // Vignette, colour depth, then into the texture upside down (Texture2D rows run bottom-up).
        void Finish()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) / W - 0.5f, dy = (y + 0.5f) / H - 0.5f;
                    float v = 1f - (dx * dx * 1.1f + dy * dy * 1.6f) * 0.6f;
                    Color c = _c[y * W + x] * v * 1.2f;
                    float d = Dither(x, y) / 28f;
                    c.r = Mathf.Round(Mathf.Clamp01(c.r + d) * 28f) / 28f;
                    c.g = Mathf.Round(Mathf.Clamp01(c.g + d) * 28f) / 28f;
                    c.b = Mathf.Round(Mathf.Clamp01(c.b + d) * 28f) / 28f;
                    c.a = 1f;
                    _out[(H - 1 - y) * W + x] = c;
                }
            Texture.SetPixels32(_out);
            Texture.Apply(false);
        }

        // ---------------------------------------------------------------- 1987: the mine at night

        void Mine()
        {
            Gradient(0, 72, C(0.05f, 0.07f, 0.16f), C(0.34f, 0.22f, 0.34f));
            // The glow of a far-off town over the horizon.
            for (int y = 36; y < 72; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Pow((x - 132f) / 80f, 2f) + Mathf.Pow((y - 72f) / 24f, 2f);
                    if (d < 1f) Add(x, y, C(0.22f, 0.09f, 0.05f) * Mathf.Round((1f - d) * 4f + Dither(x, y)) / 4f);
                }
            Stars(80, 50, 1);

            // Moon with its halo and a few dark seas.
            Glow(146, 17, 22, C(0.12f, 0.12f, 0.16f));
            for (int y = 8; y <= 26; y++)
                for (int x = 137; x <= 155; x++)
                {
                    float d = Mathf.Sqrt((x - 146f) * (x - 146f) + (y - 17f) * (y - 17f)) / 8.5f;
                    if (d > 1f) continue;
                    Color m = C(0.88f, 0.85f, 0.72f) * (1f - 0.25f * d * d);
                    if (Hash(x / 3, y / 3, 4) > 0.7f) m *= 0.86f;
                    Set(x, y, m);
                }

            // Far ridge, then the spoil heap with moonlight along one flank.
            for (int x = 0; x < W; x++)
            {
                int top = 60 + Mathf.RoundToInt(3f * Mathf.Sin(x * 0.07f) + 2f * Mathf.Sin(x * 0.19f + 1f));
                for (int y = top; y < 72; y++) Set(x, y, C(0.07f, 0.07f, 0.12f));
            }
            Tri(new Vector2(98, 74), new Vector2(146, 38), new Vector2(194, 74), C(0.09f, 0.08f, 0.11f));
            Line(146, 38, 194, 74, C(0.2f, 0.17f, 0.23f));
            Line(145, 39, 192, 74, C(0.14f, 0.12f, 0.17f));

            Gradient(72, H, C(0.05f, 0.05f, 0.07f), C(0.015f, 0.015f, 0.02f));
            Color dark = C(0.02f, 0.02f, 0.035f);
            for (int x = 96; x < W; x += 5) Line(x, 65, x, 72, dark);
            Line(96, 67, W, 67, dark);

            // Headframe: two raking legs, braces, a back strut and the sheave wheel turning, very slowly.
            Line(49, 73, 61, 26, dark); Line(50, 73, 62, 26, dark);
            Line(79, 73, 67, 26, dark); Line(80, 73, 68, 26, dark);
            Line(94, 73, 70, 30, dark); Line(95, 73, 71, 30, dark);
            float prevL = 49; int prevY = 73;
            foreach (int y in new[] { 63, 53, 43, 33 })
            {
                float l = 49 + (73 - y) * 12f / 47f, r = 80 - (73 - y) * 12f / 47f;
                Line(l, y, r, y, dark);
                Line(prevL, prevY, r, y, dark);
                prevL = l; prevY = y;
            }
            Rect(57, 24, 16, 3, dark);
            Ring(65, 18, 6.5f, 1.4f, dark);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI / 4f + _t * 0.25f;
                Line(65 - Mathf.Cos(a) * 6f, 18 - Mathf.Sin(a) * 6f, 65 + Mathf.Cos(a) * 6f, 18 + Mathf.Sin(a) * 6f, dark);
            }
            Line(65, 18, 88, 50, C(0.06f, 0.06f, 0.09f));

            // Winding house with one window still lit.
            Rect(22, 58, 32, 15, C(0.04f, 0.04f, 0.06f));
            Tri(new Vector2(19, 59), new Vector2(38, 49), new Vector2(57, 59), C(0.04f, 0.04f, 0.06f));
            float lamp = 0.75f + 0.25f * Mathf.PerlinNoise(_t * 2.5f, 3f);
            Rect(29, 62, 4, 3, C(0.95f, 0.62f, 0.25f) * lamp);
            Rect(41, 62, 4, 3, C(0.12f, 0.1f, 0.1f));
            Glow(31, 63.5f, 11, C(0.3f, 0.16f, 0.06f) * lamp);

            // Chimney and its smoke.
            Rect(85, 42, 4, 31, dark);
            for (int k = 0; k < 7; k++)
            {
                float age = Mathf.Repeat(_t * 0.12f + k / 7f, 1f);
                DiscMix(87 + age * 16f + Mathf.Sin(age * 6f + k) * 2f, 41 - age * 34f, 1.5f + age * 4.5f, C(0.13f, 0.12f, 0.17f), (1f - age) * 0.7f);
            }

            // Ground mist and ash coming down.
            for (int y = 60; y < 78; y++)
                for (int x = 0; x < W; x++)
                {
                    float a = 0.28f * (1f - Mathf.Abs(y - 69f) / 9f) * (0.6f + 0.4f * Mathf.Sin(x * 0.08f + _t * 0.5f));
                    if (a > 0f) Mix(x, y, C(0.17f, 0.16f, 0.23f), Mathf.Round(a * 8f + Dither(x, y)) / 8f);
                }
            Dust(50, 7f, C(0.6f, 0.6f, 0.68f), 5);
        }

        // ---------------------------------------------------------------- the bunker beneath the mine

        void Section()
        {
            Gradient(0, 18, C(0.03f, 0.04f, 0.1f), C(0.12f, 0.1f, 0.18f));
            Stars(25, 15, 7);
            // Rock strata, wavy, with two black coal seams and loose stones.
            Color[] bands =
            {
                C(0.2f, 0.17f, 0.13f), C(0.26f, 0.21f, 0.16f), C(0.07f, 0.06f, 0.06f), C(0.22f, 0.18f, 0.14f),
                C(0.3f, 0.25f, 0.19f), C(0.18f, 0.15f, 0.12f), C(0.06f, 0.05f, 0.05f), C(0.24f, 0.2f, 0.15f), C(0.2f, 0.16f, 0.13f)
            };
            for (int y = 18; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float wave = 2.5f * Mathf.Sin(x * 0.045f + y * 0.02f) + 1.5f * Mathf.Sin(x * 0.11f + 2f);
                    int band = Mathf.Clamp((int)((y - 18 + wave) / 8.5f), 0, bands.Length - 1);
                    Color c = bands[band] * (0.92f + Hash(x, y, 8) * 0.16f);
                    if (Hash(x / 2, y / 2, 9) > 0.96f) c *= 1.35f;
                    Set(x, y, c);
                }
            Rect(0, 18, W, 2, C(0.12f, 0.1f, 0.08f));
            for (int x = 0; x < W; x += 3) if (Hash(x, 0, 10) > 0.4f) Set(x, 17, C(0.1f, 0.12f, 0.08f));

            // Tiny headframe on the surface and its shaft dropping into the dark.
            Color dark = C(0.02f, 0.02f, 0.035f);
            Line(26, 18, 30, 4, dark); Line(36, 18, 32, 4, dark); Line(28, 11, 34, 11, dark);
            Ring(31, 4, 2.5f, 1f, dark);
            Rect(27, 20, 8, 56, C(0.03f, 0.025f, 0.02f));
            for (int y = 22; y < 76; y += 6) { Rect(26, y, 1, 3, C(0.36f, 0.27f, 0.17f)); Rect(35, y, 1, 3, C(0.36f, 0.27f, 0.17f)); }
            Line(31, 4, 31, 60, C(0.45f, 0.42f, 0.36f));
            Rect(28, 60, 6, 8, C(0.3f, 0.26f, 0.2f));
            Rect(29, 61, 4, 6, C(0.08f, 0.07f, 0.06f));

            // The adit out to the bunker, propped with timber, a few bulbs along it.
            Rect(27, 70, 108, 8, C(0.035f, 0.03f, 0.025f));
            for (int x = 38; x < 132; x += 11)
            {
                Rect(x, 70, 1, 8, C(0.36f, 0.27f, 0.17f));
                Rect(x - 1, 70, 4, 1, C(0.36f, 0.27f, 0.17f));
            }
            for (int k = 0; k < 4; k++)
            {
                float x = 50 + k * 24, f = 0.7f + 0.3f * Mathf.PerlinNoise(_t * 3f + k, 0f);
                Set((int)x, 71, C(1f, 0.75f, 0.4f) * f);
                Glow(x, 72, 6, C(0.3f, 0.18f, 0.07f) * f);
            }

            // The bunker: a concrete box, warm inside, the round vault door and its red lamp blinking.
            Rect(130, 55, 44, 30, C(0.3f, 0.31f, 0.32f));
            Rect(130, 55, 44, 1, C(0.42f, 0.43f, 0.44f));
            for (int y = 56; y < 85; y++)
                for (int x = 130; x < 174; x++)
                    if (Hash(x, y, 11) > 0.9f) Set(x, y, Get(x, y) * 0.8f);
            Gradient2(134, 59, 36, 22, C(0.32f, 0.2f, 0.1f), C(0.12f, 0.08f, 0.05f));
            Disc(160, 70, 7, C(0.42f, 0.45f, 0.5f));
            Ring(160, 70, 7, 1f, C(0.18f, 0.19f, 0.21f));
            Ring(160, 70, 3.5f, 1f, C(0.62f, 0.66f, 0.7f));
            Line(156, 70, 164, 70, C(0.62f, 0.66f, 0.7f)); Line(160, 66, 160, 74, C(0.62f, 0.66f, 0.7f));
            Rect(139, 74, 9, 7, C(0.4f, 0.26f, 0.14f));                         // crates of charges
            Rect(140, 72, 2, 2, C(0.7f, 0.15f, 0.1f)); Rect(143, 72, 2, 2, C(0.7f, 0.15f, 0.1f));
            bool blink = Mathf.Repeat(_t, 1f) < 0.55f;
            Set(151, 61, blink ? C(1f, 0.2f, 0.12f) : C(0.3f, 0.06f, 0.05f));
            if (blink) Glow(151, 61, 9, C(0.35f, 0.04f, 0.02f));

            // Depth ticks down the left edge.
            for (int y = 24; y < H; y += 8) Rect(2, y, y % 16 == 8 ? 6 : 3, 1, C(0.45f, 0.4f, 0.34f));
            Dust(20, 3f, C(0.35f, 0.3f, 0.25f), 12);
        }

        void Gradient2(int x0, int y0, int w, int h, Color top, Color bottom)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    float k = Mathf.Clamp01(Mathf.Round(((float)(y - y0) / Mathf.Max(1, h - 1)) * 6f + Dither(x, y)) / 6f);
                    Set(x, y, Color.Lerp(top, bottom, k));
                }
        }

        // ---------------------------------------------------------------- the door seals behind you

        void Vault()
        {
            Color wall = C(0.12f, 0.12f, 0.14f);
            Rect(0, 0, W, H, wall);
            // Corridor in perspective: back wall of stone blocks, side walls and a tiled floor running to it.
            Rect(44, 12, 92, 50, C(0.2f, 0.2f, 0.22f));
            for (int y = 12; y < 62; y++)
                for (int x = 44; x < 136; x++)
                {
                    int row = (y - 12) / 6;
                    bool joint = (y - 12) % 6 == 0 || (x - 44 + (row % 2) * 6) % 12 == 0;
                    if (joint) Set(x, y, C(0.1f, 0.1f, 0.11f));
                    else if (Hash(x, y, 13) > 0.92f) Set(x, y, Get(x, y) * 1.15f);
                }
            Quad(new Vector2(0, 0), new Vector2(44, 12), new Vector2(44, 62), new Vector2(0, 90), C(0.1f, 0.1f, 0.115f));
            Quad(new Vector2(180, 0), new Vector2(136, 12), new Vector2(136, 62), new Vector2(180, 90), C(0.09f, 0.09f, 0.1f));
            Quad(new Vector2(0, 0), new Vector2(180, 0), new Vector2(136, 12), new Vector2(44, 12), C(0.06f, 0.06f, 0.07f));
            Quad(new Vector2(44, 62), new Vector2(136, 62), new Vector2(180, 90), new Vector2(0, 90), C(0.13f, 0.12f, 0.12f));
            for (int k = -4; k <= 4; k++) Line(90 + k * 11.5f, 62, 90 + k * 26f, 90, C(0.07f, 0.07f, 0.07f));
            foreach (int y in new[] { 66, 72, 80 }) Line(0, y, W, y, C(0.07f, 0.07f, 0.07f));

            // The vault door: frame, the round leaf with rivets, the locking wheel.
            Rect(66, 14, 48, 48, C(0.08f, 0.08f, 0.09f));
            Disc(90, 38, 21, C(0.38f, 0.41f, 0.45f));
            Ring(90, 38, 20, 2f, C(0.2f, 0.22f, 0.25f));
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                Set(Mathf.RoundToInt(90 + Mathf.Cos(a) * 17f), Mathf.RoundToInt(38 + Mathf.Sin(a) * 17f), C(0.62f, 0.65f, 0.7f));
            }
            Ring(90, 38, 9, 1.4f, C(0.6f, 0.63f, 0.68f));
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI / 4f;
                Line(90 - Mathf.Cos(a) * 9f, 38 - Mathf.Sin(a) * 9f, 90 + Mathf.Cos(a) * 9f, 38 + Mathf.Sin(a) * 9f, C(0.56f, 0.59f, 0.64f));
            }
            Disc(90, 38, 2.5f, C(0.75f, 0.78f, 0.82f));
            for (int x = 66; x < 114; x++)
                for (int y = 62; y < 66; y++)
                    Set(x, y, ((x + y) / 3) % 2 == 0 ? C(0.62f, 0.48f, 0.08f) : C(0.05f, 0.05f, 0.05f));

            // Red beacon over the door, its beam sweeping the walls.
            float beam = _t * 2.6f;
            for (int y = 0; y < 62; y++)
                for (int x = 0; x < W; x++)
                {
                    float a = Mathf.Atan2(y - 9f, x - 90f);
                    float k = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a - beam)), 24f);
                    float fall = 1f - Mathf.Clamp01(Mathf.Sqrt((x - 90f) * (x - 90f) + (y - 9f) * (y - 9f)) / 110f);
                    if (k * fall > 0.02f) Add(x, y, C(0.45f, 0.04f, 0.02f) * Mathf.Round(k * fall * 4f + Dither(x, y)) / 4f);
                }
            Rect(88, 7, 5, 4, C(0.9f, 0.15f, 0.08f));
            Glow(90, 9, 8, C(0.3f, 0.03f, 0.02f));

            // Crates of old charges on the left, sticks poking out.
            Rect(8, 64, 26, 18, C(0.3f, 0.21f, 0.12f));
            for (int y = 64; y < 82; y += 4) Rect(8, y, 26, 1, C(0.14f, 0.09f, 0.05f));
            Rect(8, 64, 2, 18, C(0.18f, 0.12f, 0.07f)); Rect(32, 64, 2, 18, C(0.18f, 0.12f, 0.07f));
            for (int k = 0; k < 5; k++) Rect(11 + k * 4, 59 + (k % 2), 3, 6, C(0.62f, 0.12f, 0.08f));
            Line(13, 59, 15, 55, C(0.5f, 0.45f, 0.4f));

            // You, from behind, a dark shape against the door, lighter held low.
            Color body = C(0.02f, 0.02f, 0.03f), rim = C(0.24f, 0.24f, 0.28f);
            Disc(128, 47, 6, body);
            Quad(new Vector2(114, 56), new Vector2(142, 56), new Vector2(150, 90), new Vector2(108, 90), body);
            Disc(116, 59, 4, body); Disc(140, 59, 4, body);
            Quad(new Vector2(141, 58), new Vector2(146, 58), new Vector2(149, 75), new Vector2(144, 76), body);
            Line(122, 42, 125, 41, rim); Line(113, 57, 116, 55, rim);
            float f = 0.7f + 0.3f * Mathf.PerlinNoise(_t * 6f, 1f);
            Flame(148, 74, 6, 1.6f, 2f);
            Glow(148, 72, 16, C(0.3f, 0.16f, 0.05f) * f);
            Dust(25, 2f, C(0.4f, 0.38f, 0.36f), 14);
        }

        // ---------------------------------------------------------------- ninety seconds, a lighter, a trail

        void Lighter()
        {
            // Floor tiles running away into the dark.
            Gradient(0, H, C(0.015f, 0.014f, 0.014f), C(0.16f, 0.14f, 0.12f));
            for (int k = 1; k < 9; k++)
            {
                int y = 14 + Mathf.RoundToInt(k * k * 1.05f);
                if (y < H) Line(0, y, W, y, C(0.03f, 0.028f, 0.026f));
            }
            for (int k = -8; k <= 8; k++) Line(90 + k * 6, 10, 90 + k * 34, H, C(0.03f, 0.028f, 0.026f));

            // Warm light round the flame.
            float f = 0.8f + 0.2f * Mathf.PerlinNoise(_t * 6f, 0f);
            Glow(67, 40, 46, C(0.34f, 0.18f, 0.06f) * f);

            // The trail of powder curling off toward the back of the room, a few grains catching the light.
            var trail = new[] { new Vector2(84, 76), new Vector2(110, 66), new Vector2(122, 54), new Vector2(150, 44), new Vector2(182, 36) };
            Trail(trail, 0f, 1f, 3, C(0.04f, 0.035f, 0.03f), 15);
            for (int k = 0; k < 12; k++)
            {
                Vector2 p = Along(trail, Hash(k, 0, 16));
                if (Mathf.Sin(_t * 3f + k * 2f) > 0.6f) Add(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y) - 1, C(0.35f, 0.25f, 0.15f));
            }

            // The Zippo: brushed case, open lid tipped back, chimney, wheel, and the flame.
            for (int y = 47; y < 73; y++)
                for (int x = 58; x < 78; x++)
                {
                    float u = (x - 58f) / 20f;
                    float shine = 0.45f + 0.5f * Mathf.Exp(-Mathf.Pow((u - 0.35f) / 0.16f, 2f));
                    Color c = C(0.58f, 0.6f, 0.64f) * shine;
                    if (Hash(x, 0, 17) > 0.8f) c *= 0.92f;
                    if ((x == 58 || x == 77) && (y == 47 || y == 72)) continue;
                    Set(x, y, c);
                }
            Rect(58, 52, 20, 1, C(0.14f, 0.15f, 0.17f));
            Quad(new Vector2(58, 47), new Vector2(46, 37), new Vector2(49, 33), new Vector2(60, 43), C(0.42f, 0.44f, 0.48f));
            Line(47, 36, 58, 46, C(0.7f, 0.72f, 0.76f));
            Rect(61, 38, 13, 9, C(0.66f, 0.68f, 0.72f));
            for (int y = 39; y < 46; y += 2)
                for (int x = 62; x < 73; x += 3) Set(x, y, C(0.08f, 0.08f, 0.09f));
            Disc(76, 40, 2.2f, C(0.5f, 0.52f, 0.55f));
            Set(76, 39, C(0.85f, 0.85f, 0.88f));
            Flame(67, 37, 16, 4.2f, 0f);
            Glow(67, 30, 14, C(0.45f, 0.26f, 0.08f) * f);

            // AIR left on the counter, ticking down.
            int left = Mathf.Max(0, 90 - Mathf.FloorToInt(_t));
            Color on = C(1f, 0.16f, 0.08f), off = C(0.14f, 0.03f, 0.02f);
            Rect(132, 6, 42, 15, C(0.03f, 0.03f, 0.03f));
            Digit(135, 9, left / 600, on, off);
            Digit(142, 9, (left / 60) % 10, on, off);
            Digit(158, 9, (left % 60) / 10, on, off);
            Digit(165, 9, left % 10, on, off);
            if (Mathf.Repeat(_t, 1f) < 0.5f) { Set(151, 11, on); Set(151, 15, on); }
            Glow(153, 13, 18, C(0.12f, 0.01f, 0.0f));
        }

        // ---------------------------------------------------------------- the room goes on without you

        void Aftermath()
        {
            // Back wall and floor of the room.
            Rect(0, 0, W, 60, C(0.09f, 0.09f, 0.11f));
            for (int y = 0; y < 60; y++)
                for (int x = 0; x < W; x++)
                {
                    int row = y / 7;
                    if (y % 7 == 0 || (x + (row % 2) * 8) % 16 == 0) Set(x, y, C(0.05f, 0.05f, 0.06f));
                }
            Gradient(60, H, C(0.11f, 0.105f, 0.1f), C(0.19f, 0.175f, 0.16f));

            // The door at the right stands open; cold light spills across the floor.
            Rect(128, 12, 38, 48, C(0.14f, 0.15f, 0.17f));
            Gradient2(132, 16, 28, 44, C(0.75f, 0.82f, 0.95f), C(0.5f, 0.58f, 0.72f));
            Quad(new Vector2(132, 16), new Vector2(122, 22), new Vector2(122, 62), new Vector2(132, 60), C(0.32f, 0.35f, 0.39f));
            for (int y = 60; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float spread = (y - 60f) / 30f;
                    float l = 132f - spread * 60f, r = 160f + spread * 10f;
                    if (x < l || x > r) continue;
                    float a = 0.35f * (1f - spread * 0.6f);
                    Mix(x, y, C(0.45f, 0.5f, 0.6f), Mathf.Round(a * 6f + Dither(x, y)) / 6f);
                }
            Glow(146, 38, 40, C(0.08f, 0.1f, 0.14f));

            // The fuse: ash behind the spark, powder ahead, the spark crawling to the door.
            var trail = new[] { new Vector2(16, 82), new Vector2(44, 86), new Vector2(82, 81), new Vector2(108, 71), new Vector2(130, 62) };
            float k = 0.25f + Mathf.Repeat(_t / 9f, 1f) * 0.72f;
            Trail(trail, 0f, k, 2, C(0.26f, 0.24f, 0.22f), 18);
            Trail(trail, k, 1f, 2, C(0.03f, 0.028f, 0.025f), 19);
            Vector2 s = Along(trail, k);
            float f = 0.7f + 0.3f * Mathf.PerlinNoise(_t * 8f, 4f);
            Glow(s.x, s.y - 1, 18, C(0.4f, 0.18f, 0.05f) * f);
            Flame(s.x, s.y, 4, 1.3f, 5f);
            for (int p = 0; p < 6; p++)
            {
                float age = Mathf.Repeat(_t * 2f + p / 6f, 1f);
                Set(Mathf.RoundToInt(s.x + (Hash(p, 0, 20) - 0.5f) * 10f * age), Mathf.RoundToInt(s.y - age * 7f), C(1f, 0.7f, 0.3f) * (1f - age));
            }

            // And you, lying where the air ran out, the lighter dropped by your hand.
            Color body = C(0.015f, 0.015f, 0.02f), rim = C(0.42f, 0.45f, 0.54f);
            Disc(24, 73, 4.5f, body);
            Quad(new Vector2(28, 69), new Vector2(56, 70), new Vector2(56, 78), new Vector2(28, 78), body);
            Quad(new Vector2(56, 71), new Vector2(82, 73), new Vector2(82, 78), new Vector2(56, 77), body);
            Rect(82, 72, 4, 6, body);
            Quad(new Vector2(28, 76), new Vector2(30, 78), new Vector2(14, 84), new Vector2(12, 82), body);
            Line(29, 69, 55, 70, rim); Line(57, 71, 81, 73, rim); Line(21, 69, 26, 69, rim); Line(20, 70, 20, 75, rim);
            Line(82, 72, 85, 72, rim); Line(13, 82, 27, 76, rim * 0.7f);
            for (int x = 10; x < 90; x++) Mix(x, 79, C(0f, 0f, 0f), 0.5f);   // its shadow on the floor
            Rect(9, 82, 4, 3, C(0.45f, 0.47f, 0.5f));
            Dust(25, 2.5f, C(0.35f, 0.35f, 0.4f), 21);
        }
    }
}
