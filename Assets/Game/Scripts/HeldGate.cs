using UnityEngine;

namespace GJ.Game
{
    // Something a pressure plate holds open: a grille hauled up on its cable while the plate is down, dropping shut the
    // moment the weight comes off.
    public class HeldGate : MonoBehaviour
    {
        public PressurePlate plate;
        public Transform leaf;                  // moves by openOffset (local) when open
        public Vector3 openOffset = new Vector3(0f, 0.45f, 0f);
        public float openSeconds = 0.7f;
        public float closeSeconds = 0.18f;
        public AudioClip openClip, slamClip;

        public bool IsOpen => _open >= 0.9f;

        float _open;
        Vector3 _rest;
        bool _wasPressed;

        void Awake() => _rest = leaf.localPosition;

        void Update()
        {
            bool pressed = plate != null && plate.Pressed;
            if (pressed != _wasPressed)
            {
                _wasPressed = pressed;
                var clip = pressed ? openClip : slamClip;
                if (clip != null) AudioSource.PlayClipAtPoint(clip, leaf.position, 0.9f);
            }
            _open = Mathf.MoveTowards(_open, pressed ? 1f : 0f, Time.deltaTime / (pressed ? openSeconds : closeSeconds));
            float k = pressed ? Mathf.SmoothStep(0f, 1f, _open) : _open * _open;
            leaf.localPosition = _rest + openOffset * k;
        }
    }
}
