using UnityEngine;

namespace GJ.Game
{
    // Slow pendulum sway for something hanging (a lamp on its cord), with a second, slower wobble so it never quite
    // repeats. Anything parented under it (the light) swings along.
    public class Swing : MonoBehaviour
    {
        public float angle = 3f;
        public float period = 4.5f;

        Quaternion _rest;
        float _seed;

        void Awake()
        {
            _rest = transform.localRotation;
            _seed = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time + _seed;
            float x = Mathf.Sin(t * Mathf.PI * 2f / period) * angle;
            float z = Mathf.Sin(t * Mathf.PI * 2f / (period * 1.7f)) * angle * 0.6f;
            transform.localRotation = _rest * Quaternion.Euler(x, 0f, z);
        }
    }
}
