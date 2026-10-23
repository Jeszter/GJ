using System;
using UnityEngine;

namespace GJ.Game
{
    // A wall clock that keeps the player's own time, ticking quietly. Hands turn about the clock's local Z (it faces +Z).
    public class WallClock : MonoBehaviour
    {
        public Transform hour, minute, second;
        public AudioClip tickClip;

        AudioSource _audio;
        int _lastSecond = -1;

        void Awake() => _audio = GetComponent<AudioSource>();

        void Update()
        {
            var now = DateTime.Now;
            float s = now.Second, m = now.Minute + s / 60f, h = now.Hour % 12 + m / 60f;
            // Seen from the front, clockwise is a negative turn about +Z.
            if (hour != null) hour.localRotation = Quaternion.Euler(0f, 0f, -h * 30f);
            if (minute != null) minute.localRotation = Quaternion.Euler(0f, 0f, -m * 6f);
            if (second != null) second.localRotation = Quaternion.Euler(0f, 0f, -s * 6f);
            if (now.Second == _lastSecond) return;
            _lastSecond = now.Second;
            if (_audio != null && tickClip != null) _audio.PlayOneShot(tickClip, 0.5f);
        }
    }
}
