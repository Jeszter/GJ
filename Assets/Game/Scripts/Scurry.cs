using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // A rat that lives in the walls: every so often it darts out of one hole along the skirting, pauses to sniff, and
    // runs into another, squeaking. Hidden between runs.
    public class Scurry : MonoBehaviour
    {
        public Vector3[] path;            // world points along the floor, first and last at the holes
        public float speed = 1.6f;
        public Vector2 interval = new Vector2(12f, 25f);
        public AudioClip squeakClip;
        public Transform tail;

        AudioSource _audio;

        void Start()
        {
            _audio = GetComponent<AudioSource>();
            Show(false);
            StartCoroutine(Run());
        }

        void Show(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(Random.Range(4f, 9f));
            while (true)
            {
                bool reverse = Random.value > 0.5f;
                Show(true);
                if (_audio != null && squeakClip != null) _audio.PlayOneShot(squeakClip, 0.7f);
                int pause = Random.Range(1, path.Length - 1);
                for (int k = 0; k < path.Length - 1; k++)
                {
                    Vector3 a = path[reverse ? path.Length - 1 - k : k], b = path[reverse ? path.Length - 2 - k : k + 1];
                    transform.rotation = Quaternion.LookRotation(b - a);
                    for (float d = 0f; d < Vector3.Distance(a, b); d += speed * Time.deltaTime)
                    {
                        transform.position = Vector3.MoveTowards(a, b, d) + Vector3.up * Mathf.Abs(Mathf.Sin(d * 40f)) * 0.008f;
                        if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 30f) * 15f, 0f);
                        yield return null;
                    }
                    if (k == pause)
                    {
                        // Stop, sniff, look about.
                        Quaternion look = transform.rotation;
                        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                        {
                            transform.rotation = look * Quaternion.Euler(0f, Mathf.Sin(t * 9f) * 25f, 0f);
                            yield return null;
                        }
                        if (_audio != null && squeakClip != null && Random.value > 0.5f) _audio.PlayOneShot(squeakClip, 0.5f);
                    }
                }
                Show(false);
                yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
            }
        }
    }
}
