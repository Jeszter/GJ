using UnityEngine;

namespace GJ.PSX
{
    // Noise-driven flicker for a flame: light intensity and flame height wobble together.
    public class FlameFlicker : MonoBehaviour
    {
        public Light flameLight;
        public Transform flame;
        public float baseIntensity = 1.2f;
        [Range(0f, 1f)] public float flicker = 0.35f;
        public float speed = 9f;
        // 0 = out, 1 = full flame; animated by whoever lights or snuffs it.
        [Range(0f, 1f)] public float strength = 1f;

        Vector3 _baseScale;
        float _seed;

        void Awake()
        {
            _seed = Random.value * 100f;
            if (flame != null) _baseScale = flame.localScale;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * speed);
            if (flameLight != null) flameLight.intensity = baseIntensity * (1f + (n - 0.5f) * 2f * flicker) * strength;
            if (flame != null) flame.localScale = new Vector3(_baseScale.x * strength, _baseScale.y * (0.8f + n * 0.4f) * strength, _baseScale.z * strength);
        }
    }
}
