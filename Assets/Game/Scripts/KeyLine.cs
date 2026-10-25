using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace GJ.Game
{
    // A few lines of on-screen words in the typewriter look. Keys in square brackets ([E], [SPACE]) are drawn on round
    // typewriter keys, a line wrapped in <size=N> takes that size, long lines wrap to maxWidth, and the words can be typed
    // out a letter at a time. The Paper look lays the lot on a torn strip of the notes' paper. Set .text the way you
    // would on a Text: the pieces are rebuilt only when it changes. .color only carries the alpha, for fading.
    public class KeyLine : MonoBehaviour
    {
        public enum Look { Light, Dim, Paper }

        public int size = 11;
        public TextAnchor anchor = TextAnchor.MiddleCenter;
        public Look look = Look.Light;
        public float maxWidth = 380f;
        public float lineSpacing = 1.35f;
        public float typeSpeed;              // letters a second; 0 shows everything at once
        public System.Action Typed;          // every letter as it lands (for the key click)

        const float PadX = 7f, PadY = 3f;
        static readonly Regex Sized = new Regex(@"^<size=(\d+)>(.*)</size>$", RegexOptions.Singleline);
        static readonly Regex KeyName = new Regex(@"\[([^\]]+)\]");

        class Item { public string str; public bool key; public float width; }
        class Piece { public Text text; public string body; public int start; public GameObject key; }

        RectTransform _rt;
        CanvasGroup _group;
        Image _paper;
        string _text = "";
        readonly List<Piece> _pieces = new List<Piece>();
        int _total, _shown;
        float _clock;

        public static KeyLine Create(Transform parent, string name, int size, TextAnchor anchor, Vector2 pivot, Vector2 offset, Look look, float maxWidth)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var k = go.AddComponent<KeyLine>();
            k.size = size;
            k.anchor = anchor;
            k.look = look;
            k.maxWidth = maxWidth;
            k._rt = (RectTransform)go.transform;
            k._rt.anchorMin = k._rt.anchorMax = k._rt.pivot = pivot;
            k._rt.anchoredPosition = offset;
            k._rt.sizeDelta = Vector2.zero;
            k._group = go.AddComponent<CanvasGroup>();
            k._group.blocksRaycasts = false;
            k._group.interactable = false;
            if (look == Look.Paper)
            {
                k._paper = new GameObject("Paper", typeof(RectTransform)).AddComponent<Image>();
                k._paper.transform.SetParent(go.transform, false);
                k._paper.sprite = UiStyle.PaperStrip;
                k._paper.type = Image.Type.Sliced;
                k._paper.pixelsPerUnitMultiplier = 1.4f;
                k._paper.raycastTarget = false;
                var prt = k._paper.rectTransform;
                prt.anchorMin = Vector2.zero;
                prt.anchorMax = Vector2.one;
                prt.offsetMin = prt.offsetMax = Vector2.zero;
                k._paper.gameObject.SetActive(false);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 0.6f);
            }
            return k;
        }

        public string text
        {
            get => _text;
            set
            {
                value = value ?? "";
                if (value == _text) return;
                _text = value;
                Rebuild();
            }
        }

        public Color color
        {
            get => new Color(1f, 1f, 1f, _group.alpha);
            set => _group.alpha = value.a;
        }

        public bool Typing => _shown < _total;

        void Update()
        {
            if (_shown >= _total) return;
            _clock += Time.unscaledDeltaTime * typeSpeed;
            bool moved = false;
            while (_clock >= 1f && _shown < _total)
            {
                _clock -= 1f;
                _shown++;
                moved = true;
                Typed?.Invoke();
            }
            if (moved) Reveal();
        }

        void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (_paper == null || c != _paper.transform) Destroy(c.gameObject);
            }
            _pieces.Clear();
            _total = 0;
            _clock = 0f;
            if (_paper != null) _paper.gameObject.SetActive(_text.Length > 0);
            if (_text.Length == 0) { _rt.sizeDelta = Vector2.zero; return; }

            // Lay the words out into rows: wrap at maxWidth, a <size> line keeps its own size.
            var rows = new List<List<Item>>();
            var rowSize = new List<int>();
            foreach (var raw in _text.Split('\n'))
            {
                string line = raw;
                int sz = size;
                var m = Sized.Match(line);
                if (m.Success) { sz = Mathf.Max(7, int.Parse(m.Groups[1].Value)); line = m.Groups[2].Value; }
                var row = new List<Item>();
                float x = 0f;
                foreach (var item in Split(line, sz))
                {
                    if (row.Count > 0 && x + item.width > maxWidth && item.str.Trim().Length > 0)
                    {
                        rows.Add(row); rowSize.Add(sz);
                        row = new List<Item>();
                        x = 0f;
                    }
                    row.Add(item);
                    x += item.width;
                }
                rows.Add(row); rowSize.Add(sz);
            }

            float block = 0f;
            var widths = new float[rows.Count];
            for (int r = 0; r < rows.Count; r++)
            {
                float w = 0f;
                foreach (var it in rows[r]) w += it.width;
                // Trailing spaces don't count toward where the row sits.
                var last = rows[r].Count > 0 ? rows[r][rows[r].Count - 1] : null;
                if (last != null && !last.key) w -= UiStyle.Measure(last.str, rowSize[r], Style) - UiStyle.Measure(last.str.TrimEnd(), rowSize[r], Style);
                widths[r] = w;
                block = Mathf.Max(block, w);
            }
            float height = 0f;
            for (int r = 0; r < rows.Count; r++) height += RowHeight(rowSize[r]);
            _rt.sizeDelta = new Vector2(block + PadX * 2f, height + PadY * 2f);

            float h = HorizontalAlign;
            float y = PadY;
            for (int r = 0; r < rows.Count; r++)
            {
                int sz = rowSize[r];
                float lineH = RowHeight(sz);
                float x = PadX + (block - widths[r]) * h;
                var run = new StringBuilder();
                float runX = x;
                foreach (var it in rows[r])
                {
                    if (it.key)
                    {
                        Flush(run, runX, y, lineH, sz);
                        AddKey(it, x, y, lineH, sz);
                        x += it.width;
                        runX = x;
                    }
                    else
                    {
                        if (run.Length == 0) runX = x;
                        run.Append(it.str);
                        x += it.width;
                    }
                }
                Flush(run, runX, y, lineH, sz);
                y += lineH;
            }

            _shown = typeSpeed > 0f ? 0 : _total;
            Reveal();
        }

        FontStyle Style => FontStyle.Bold;

        // Room for the keys as well as the type.
        float RowHeight(int sz) => Mathf.Max(sz * lineSpacing, FaceHeight(sz) + 2f);

        float HorizontalAlign
        {
            get
            {
                switch (anchor)
                {
                    case TextAnchor.UpperLeft: case TextAnchor.MiddleLeft: case TextAnchor.LowerLeft: return 0f;
                    case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: return 1f;
                    default: return 0.5f;
                }
            }
        }

        // Words (each with the spaces after it) and keys, measured.
        IEnumerable<Item> Split(string line, int sz)
        {
            int at = 0;
            foreach (Match k in KeyName.Matches(line))
            {
                foreach (var w in Words(line.Substring(at, k.Index - at), sz)) yield return w;
                string name = k.Groups[1].Value;
                float face = Mathf.Max(FaceHeight(sz), UiStyle.Measure(name, KeySize(sz), Style) + 11f);
                yield return new Item { str = name, key = true, width = face + 4f };
                at = k.Index + k.Length;
            }
            foreach (var w in Words(line.Substring(at), sz)) yield return w;
        }

        IEnumerable<Item> Words(string s, int sz)
        {
            int i = 0;
            while (i < s.Length)
            {
                int j = i;
                while (j < s.Length && s[j] != ' ') j++;
                while (j < s.Length && s[j] == ' ') j++;
                string w = s.Substring(i, j - i);
                yield return new Item { str = w, width = UiStyle.Measure(w, sz, Style) };
                i = j;
            }
        }

        // The letter on a key is as big as the words beside it; the key round it a little bigger again.
        static int KeySize(int sz) => sz;
        static float FaceHeight(int sz) => sz + 6f;

        void Flush(StringBuilder run, float x, float y, float lineH, int sz)
        {
            if (run.Length == 0) return;
            string body = run.ToString();
            run.Length = 0;
            var t = NewText("Words", transform, sz, look == Look.Paper ? UiStyle.Ink : look == Look.Dim ? UiStyle.Dim : UiStyle.Type, look != Look.Paper);
            t.alignment = TextAnchor.MiddleLeft;
            Place(t.rectTransform, x, y, UiStyle.Measure(body, sz, Style) + 2f, lineH);
            _pieces.Add(new Piece { text = t, body = body, start = _total });
            _total += body.Length;
        }

        void AddKey(Item it, float x, float y, float lineH, int sz)
        {
            float faceH = FaceHeight(sz);
            float faceW = it.width - 4f;
            var key = new GameObject("Key", typeof(RectTransform)).AddComponent<Image>();
            key.transform.SetParent(transform, false);
            key.sprite = UiStyle.Key;
            key.type = Image.Type.Sliced;
            key.pixelsPerUnitMultiplier = 7f / (faceH * 0.5f);
            key.raycastTarget = false;
            Place(key.rectTransform, x + 2f, y + (lineH - faceH) * 0.5f, faceW, faceH);
            if (look != Look.Paper)
            {
                var drop = key.gameObject.AddComponent<Shadow>();
                drop.effectColor = UiStyle.Shade;
                drop.effectDistance = new Vector2(1f, -1f);
            }
            var label = NewText("Name", key.transform, KeySize(sz), UiStyle.Ink, false);
            label.alignment = TextAnchor.MiddleCenter;
            label.text = it.str;
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(0f, 0f);
            lrt.offsetMax = new Vector2(0f, 1f);
            _pieces.Add(new Piece { key = key.gameObject, start = _total });
            _total += 1;
        }

        static Text NewText(string name, Transform parent, int sz, Color color, bool shadow)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = UiStyle.Font;
            t.fontSize = sz;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = UiStyle.Shade;
                s.effectDistance = new Vector2(1f, -1f);
            }
            return t;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        // Typed so far: the rest of each run is kept in place but invisible, so nothing shifts as the letters land.
        void Reveal()
        {
            foreach (var p in _pieces)
            {
                int seen = Mathf.Clamp(_shown - p.start, 0, p.key != null ? 1 : p.body.Length);
                if (p.key != null) { if (p.key.activeSelf != (seen > 0)) p.key.SetActive(seen > 0); continue; }
                p.text.text = seen >= p.body.Length ? p.body
                            : p.body.Substring(0, seen) + "<color=#00000000>" + p.body.Substring(seen) + "</color>";
            }
        }
    }
}
