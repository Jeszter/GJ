using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // The bare end of a hanging cable: now and then it spits a few blue-white sparks, lighting the walls for a moment.
    public class Sparker : MonoBehaviour
    {
        public GameObject flash;          // additive glow at the cable end
        public Light sparkLight;
        public Transform[] bits;          // tiny sparks thrown off and falling
        public AudioClip sparkClip;
        public Vector2 interval = new Vector2(3f, 9f);

        // Restarted every time it is switched on (a line plugged in again).
        void OnEnable()
        {
            flash.SetActive(false);
            sparkLight.enabled = false;
            foreach (var b in bits) b.gameObject.SetActive(false);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
                if (sparkClip != null) AudioSource.PlayClipAtPoint(sparkClip, transform.position, 0.6f);
                var vel = new Vector3[bits.Length];
                for (int i = 0; i < bits.Length; i++)
                {
                    bits[i].localPosition = Vector3.zero;
                    bits[i].gameObject.SetActive(true);
                    vel[i] = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0f, 0.6f), Random.Range(-0.8f, 0.8f));
                }
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    bool on = t < 0.25f && Random.value > 0.3f;
                    flash.SetActive(on);
                    sparkLight.enabled = on;
                    sparkLight.intensity = Random.Range(1f, 2.5f);
                    for (int i = 0; i < bits.Length; i++)
                    {
                        vel[i] += Vector3.down * 6f * Time.deltaTime;
                        bits[i].position += vel[i] * Time.deltaTime;
                        bits[i].gameObject.SetActive(t < 0.6f && bits[i].position.y > 0.02f);
                    }
                    yield return null;
                }
                flash.SetActive(false);
                sparkLight.enabled = false;
                foreach (var b in bits) b.gameObject.SetActive(false);
            }
        }
    }
}
