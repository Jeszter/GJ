using UnityEngine;

namespace GJ.Game
{
    // Red seven-segment "MM:SS" on the display above the door. Draws into a small point-filtered texture laid out like
    // the builder's static one: four 10x16 digits in the bottom third, unlit segments faintly visible.
    public class SegmentDisplay : MonoBehaviour
    {
        public Renderer target;

        const int Size = 64;
        static readonly int[] DigitX = { 6, 18, 36, 48 };
        // Segment bits: a=top, b=top right, c=bottom right, d=bottom, e=bottom left, f=top left, g=middle.
        static readonly int[] Digits = { 0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110, 0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111 };
        static readonly Color32 On = new Color32(255, 255, 255, 255);
        static readonly Color32 Off = new Color32(22, 3, 3, 255);
        static readonly Color32 Black = new Color32(0, 0, 0, 255);

        Texture2D _tex;
        Color32[] _px;
        string _shown;

        void Awake()
        {
            _tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _px = new Color32[Size * Size];
            if (target != null) target.material.SetTexture("_BaseMap", _tex);
            Show("0000", true);
        }

        public void ShowTime(float seconds, bool colon = true)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            Show($"{s / 60:00}{s % 60:00}", colon);
        }

        // Four characters: digits, or '-' for a dash, or ' ' for blank.
        public void Show(string four, bool colon)
        {
            string key = four + (colon ? ":" : " ");
            if (key == _shown || _tex == null) return;
            _shown = key;

            for (int i = 0; i < _px.Length; i++) _px[i] = Black;
            for (int d = 0; d < 4; d++)
            {
                char ch = d < four.Length ? four[d] : ' ';
                int bits = ch >= '0' && ch <= '9' ? Digits[ch - '0'] : ch == '-' ? 0b1000000 : 0;
                DrawDigit(DigitX[d], 3, bits);
            }
            if (colon)
                foreach (int y in new[] { 10, 11, 16, 17 })
                {
                    _px[y * Size + 31] = On;
                    _px[y * Size + 32] = On;
                }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void DrawDigit(int x0, int y0, int bits)
        {
            Seg(x0 + 2, y0 + 14, 6, 2, (bits & 1) != 0);         // a
            Seg(x0 + 8, y0 + 9, 2, 5, (bits & 2) != 0);          // b
            Seg(x0 + 8, y0 + 2, 2, 5, (bits & 4) != 0);          // c
            Seg(x0 + 2, y0, 6, 2, (bits & 8) != 0);              // d
            Seg(x0, y0 + 2, 2, 5, (bits & 16) != 0);             // e
            Seg(x0, y0 + 9, 2, 5, (bits & 32) != 0);             // f
            Seg(x0 + 2, y0 + 7, 6, 2, (bits & 64) != 0);         // g
        }

        void Seg(int x, int y, int w, int h, bool lit)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    _px[(y + j) * Size + x + i] = lit ? On : Off;
        }
    }
}
