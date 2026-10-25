using UnityEngine;

namespace GJ.Game
{
    // Slow drift for a layer of mist: wanders back and forth along an axis and breathes up and down a little.
    public class Drift : MonoBehaviour
    {
        public Vector3 axis = Vector3.right;
        public float distance = 1.5f, period = 30f, rise = 0.08f;

        Vector3 _rest;
        float _phase;

        void Awake()
        {
            _rest = transform.localPosition;
            _phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            float t = Time.time * Mathf.PI * 2f / period + _phase;
            transform.localPosition = _rest + axis * Mathf.Sin(t) * distance + Vector3.up * Mathf.Sin(t * 1.7f) * rise;
        }
    }
}
