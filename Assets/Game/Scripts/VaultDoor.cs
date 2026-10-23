using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // The vault door: a blast flash, the locking wheel spins, then the leaf swings open on its hinge.
    public class VaultDoor : MonoBehaviour
    {
        public Transform leaf;
        public Transform wheel;
        public Light flash;
        public float openAngle = -100f;
        public float wheelTurns = 1.5f;
        public AudioClip blastClip;
        public AudioClip wheelClip;
        public AudioClip openClip;

        public bool IsOpen { get; private set; }
        public bool IsOpening { get; private set; }

        public void Open()
        {
            if (!IsOpening) StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            IsOpening = true;

            var source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.6f;
            source.minDistance = 2f;
            source.maxDistance = 20f;
            if (blastClip != null) AudioSource.PlayClipAtPoint(blastClip, flash != null ? flash.transform.position : transform.position, 1f);

            if (flash != null)
            {
                flash.enabled = true;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    flash.intensity = Mathf.Lerp(6f, 0f, t / 0.6f);
                    yield return null;
                }
                flash.enabled = false;
            }

            Quaternion wheelStart = wheel != null ? wheel.localRotation : Quaternion.identity;
            if (wheelClip != null) source.PlayOneShot(wheelClip, 0.9f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                if (wheel != null) wheel.localRotation = wheelStart * Quaternion.Euler(0f, 0f, 360f * wheelTurns * Ease(t / 1.2f));
                yield return null;
            }

            Quaternion closed = leaf.localRotation;
            Quaternion open = closed * Quaternion.Euler(0f, openAngle, 0f);
            if (openClip != null) source.PlayOneShot(openClip, 1f);
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                leaf.localRotation = Quaternion.Slerp(closed, open, Ease(t / 2.4f));
                yield return null;
            }
            leaf.localRotation = open;
            IsOpen = true;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
