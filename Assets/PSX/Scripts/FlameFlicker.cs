using UnityEngine;

namespace GJ.PSX
{
    // Noise-driven flicker for a flame: light intensity and flame height wobble together, and the flame leans a
    // little this way and that.
    public class FlameFlicker : MonoBehaviour
    {
        public Light flameLight;
        public Transform flame;
        public float baseIntensity = 1.2f;
        [Range(0f, 1f)] public float flicker = 0.35f;
        public float speed = 9f;
        public float lean = 0f;           // degrees either way
        // 0 = out, 1 = full flame; animated by whoever lights or snuffs it.
        [Range(0f, 1f)] public float strength = 1f;

        Vector3 _baseScale;
        Quaternion _baseRot;
        float _seed;

        void Awake()
        {
            _seed = Random.value * 100f;
            if (flame != null)
            {
                _baseScale = flame.localScale;
                _baseRot = flame.localRotation;
            }
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * speed);
            if (flameLight != null) flameLight.intensity = baseIntensity * (1f + (n - 0.5f) * 2f * flicker) * strength;
            if (flame == null) return;
            float w = 0.9f + Mathf.PerlinNoise(_seed + 5f, Time.time * speed * 1.3f) * 0.2f;
            flame.localScale = new Vector3(_baseScale.x * strength * w, _baseScale.y * (0.8f + n * 0.4f) * strength, _baseScale.z * strength * w);
            if (lean > 0f)
            {
                float tilt = (Mathf.PerlinNoise(_seed + 9f, Time.time * speed * 0.35f) - 0.5f) * 2f * lean;
                flame.localRotation = _baseRot * Quaternion.Euler(0f, 0f, tilt);
            }
        }
    }
}
