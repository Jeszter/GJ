using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // A tired bulb: every so often it stutters off and on a few times, light and glowing glass together, with a buzz.
    public class BulbFlicker : MonoBehaviour
    {
        public Light bulbLight;
        public GameObject[] glowing;     // bulb and halo meshes that go dark with it
        public AudioClip buzzClip;
        public Vector2 interval = new Vector2(4f, 11f);

        float _intensity;

        void Start()
        {
            if (bulbLight != null) _intensity = bulbLight.intensity;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
                if (buzzClip != null) AudioSource.PlayClipAtPoint(buzzClip, transform.position, 0.35f);
                int blinks = Random.Range(2, 6);
                for (int i = 0; i < blinks; i++)
                {
                    Set(Random.Range(0f, 0.25f));
                    yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
                    Set(Random.Range(0.6f, 1f));
                    yield return new WaitForSeconds(Random.Range(0.03f, 0.2f));
                }
                Set(1f);
            }
        }

        void Set(float k)
        {
            if (bulbLight != null) bulbLight.intensity = _intensity * k;
            foreach (var g in glowing) if (g != null) g.SetActive(k > 0.4f);
        }
    }
}
