using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // A leak in the ceiling: a drop swells, falls, and lands in the puddle below with a plip and a spreading ring.
    public class Dripper : MonoBehaviour
    {
        public Transform drop;           // the drop mesh, starts at this transform
        public Transform ripple;         // flat ring on the floor below, scaled up and faded by shrinking
        public float floorY = 0.005f;
        public AudioClip dripClip;
        public Vector2 interval = new Vector2(1.4f, 3.2f);

        void Start()
        {
            drop.gameObject.SetActive(false);
            ripple.gameObject.SetActive(false);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Vector3 top = drop.position;
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
                drop.gameObject.SetActive(true);
                // Swell at the leak.
                for (float t = 0f; t < 0.5f; t += Time.deltaTime)
                {
                    drop.position = top;
                    drop.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, t / 0.5f);
                    yield return null;
                }
                // Fall.
                float v = 0f;
                Vector3 p = top;
                while (p.y > floorY)
                {
                    v += 9.8f * Time.deltaTime;
                    p.y -= v * Time.deltaTime;
                    drop.position = p;
                    drop.localScale = new Vector3(0.8f, 1.4f, 0.8f);
                    yield return null;
                }
                drop.gameObject.SetActive(false);
                if (dripClip != null) AudioSource.PlayClipAtPoint(dripClip, new Vector3(top.x, floorY, top.z), 0.6f);
                ripple.gameObject.SetActive(true);
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    float k = t / 0.6f;
                    ripple.localScale = new Vector3(0.05f + k * 0.3f, 1f, 0.05f + k * 0.3f);
                    yield return null;
                }
                ripple.gameObject.SetActive(false);
            }
        }
    }
}
