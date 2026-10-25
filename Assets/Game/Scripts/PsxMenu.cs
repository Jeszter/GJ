using System;
using System.Collections.Generic;
using GJ.PSX;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GJ.Game
{
    // A vertical list of text items drawn inside the low-res PSX frame. Keyboard: W/S or arrows to move,
    // Enter/Space/E to pick, A/D or left/right to change a value. Mouse: hover selects, left click picks
    // (or steps a value forward), right click steps a value back. Runs on unscaled time so it works paused.
    public class PsxMenu
    {
        class Item
        {
            public string Label;
            public Func<string> Value;
            public Action Submit;
            public Action<int> Adjust;
            public Text Text;
        }

        static readonly Color Idle = new Color(0.62f, 0.58f, 0.52f);
        static readonly Color Lit = new Color(1f, 0.86f, 0.55f);

        readonly RectTransform _root;
        readonly Camera _camera;
        readonly Font _font;
        readonly int _fontSize;
        readonly float _line;
        readonly List<Item> _items = new List<Item>();
        readonly AudioSource _audio;
        readonly AudioClip _move, _pick;
        int _index;
        Vector2 _lastMouse;

        public bool Visible => _root.gameObject.activeSelf;

        public PsxMenu(Transform canvas, Camera camera, Font font, Vector2 center, int fontSize, float line,
                       AudioSource audio, AudioClip move, AudioClip pick)
        {
            _camera = camera;
            _font = font;
            _fontSize = fontSize;
            _line = line;
            _audio = audio;
            _move = move;
            _pick = pick;
            _root = new GameObject("Menu").AddComponent<RectTransform>();
            _root.SetParent(canvas, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = center;
            _root.sizeDelta = Vector2.zero;
        }

        public void Add(string label, Action submit) => AddItem(new Item { Label = label, Submit = submit });

        public void AddOption(string label, Func<string> value, Action<int> adjust) =>
            AddItem(new Item { Label = label, Value = value, Adjust = adjust });

        void AddItem(Item item)
        {
            var text = new GameObject(item.Label).AddComponent<Text>();
            text.transform.SetParent(_root, false);
            text.font = _font;
            text.fontSize = _fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(1, -1);
            var rt = text.rectTransform;
            rt.sizeDelta = new Vector2(260, _line);
            item.Text = text;
            _items.Add(item);
            for (int i = 0; i < _items.Count; i++)
                _items[i].Text.rectTransform.anchoredPosition = new Vector2(0, ((_items.Count - 1) * 0.5f - i) * _line);
            Refresh();
        }

        public void Show(bool on)
        {
            _root.gameObject.SetActive(on);
            if (!on) return;
            _index = 0;
            _lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Refresh();
        }

        public void Tick()
        {
            if (!Visible || _items.Count == 0) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null)
            {
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Move(-1);
                if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Move(1);
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Step(-1);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Step(1);
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Pick();
            }

            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                int hover = ItemUnder(pos);
                if (hover >= 0 && (pos - _lastMouse).sqrMagnitude > 1f && hover != _index)
                {
                    _index = hover;
                    Play(_move);
                    Refresh();
                }
                _lastMouse = pos;
                if (hover >= 0 && mouse.leftButton.wasPressedThisFrame)
                {
                    if (_items[hover].Adjust != null) Step(1);
                    else Pick();
                }
                if (hover >= 0 && mouse.rightButton.wasPressedThisFrame) Step(-1);
            }
        }

        // The frame is shown through the TV's curved glass, so map the window point onto the camera's low-res pixels
        // the same way the screen shader does, then test against the item rects in that space.
        int ItemUnder(Vector2 screen)
        {
            if (Screen.width <= 0 || Screen.height <= 0 || _camera.pixelHeight <= 0) return -1;
            var uv = PSXScreen.WindowToFrame(new Vector2(screen.x / Screen.width, screen.y / Screen.height),
                                             (float)_camera.pixelWidth / _camera.pixelHeight);
            var p = new Vector2(uv.x * _camera.pixelWidth, uv.y * _camera.pixelHeight);
            for (int i = 0; i < _items.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_items[i].Text.rectTransform, p, _camera)) return i;
            return -1;
        }

        public void Move(int dir)
        {
            _index = (_index + dir + _items.Count) % _items.Count;
            Play(_move);
            Refresh();
        }

        void Step(int dir)
        {
            var item = _items[_index];
            if (item.Adjust == null) return;
            item.Adjust(dir);
            Play(_move);
            Refresh();
        }

        void Pick()
        {
            var item = _items[_index];
            if (item.Adjust != null) { Step(1); return; }
            Play(_pick);
            item.Submit?.Invoke();
        }

        public void Refresh()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                bool sel = i == _index;
                string name = Loc.T(item.Label);
                string label = item.Value != null ? $"{name}   < {Loc.T(item.Value())} >" : name;
                item.Text.text = sel ? $"- {label} -" : label;
                item.Text.color = sel ? Lit : Idle;
            }
        }

        void Play(AudioClip clip)
        {
            if (_audio != null && clip != null) _audio.PlayOneShot(clip, 0.6f);
        }
    }
}
