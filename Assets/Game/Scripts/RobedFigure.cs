using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // The figure in the robe: a few poses baked from one model (standing, reaching out a hand, an arm raised to strike,
    // fallen), swapped as it moves. Driven from outside for the chapel ambush (Show/Walk), or left to fight in the
    // graveyard: it closes in, winds up and strikes, staggers when hit, and falls when it has taken enough.
    public class RobedFigure : MonoBehaviour
    {
        public GameObject idle, reach, strike, fallen;
        public Transform eyes;                 // the two red points in the hood
        public int health = 7;
        public float walkSpeed = 1.4f;
        public float reachRange = 1.7f;        // close enough to swing
        public AudioClip hissClip, hitClip, swingClip, fallClip;

        public GameObject knifeLow, knifeHigh;      // the blade hanging from the hand, and raised to strike
        public GameObject bloodSplat;               // copied onto the grass under it when it is hit
        public bool Armed { get; set; }

        public System.Action<RobedFigure> Struck;   // a blow that connected with the player
        public System.Action<RobedFigure> Wounded;  // it took a hit and is still standing
        public System.Action Died;

        public bool Dead { get; private set; }
        public bool Fighting { get; set; }

        Transform _target;
        float _stagger, _next;
        bool _busy;

        public void Show(GameObject pose)
        {
            foreach (var p in new[] { idle, reach, strike, fallen }) if (p != null) p.SetActive(p == pose);
            if (knifeLow != null) knifeLow.SetActive(Armed && pose == idle);
            if (knifeHigh != null) knifeHigh.SetActive(Armed && pose == strike);
        }

        void Awake() => Show(idle);

        public void Engage(Transform target)
        {
            _target = target;
            Fighting = true;
            _next = Time.time + 1.5f;
        }

        void Update()
        {
            if (!Fighting || Dead || _target == null || _busy) return;
            Vector3 to = _target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (_stagger > 0f) { _stagger -= Time.deltaTime; return; }

            // Glide in, the robe swaying; at arm's length wind up and swing.
            if (dist > reachRange)
            {
                transform.position += to.normalized * walkSpeed * Time.deltaTime;
                Show(idle);
            }
            else if (Time.time >= _next) StartCoroutine(Swing());
        }

        IEnumerator Swing()
        {
            _busy = true;
            Show(strike);
            Play(hissClip, 0.8f);
            // The wind-up: rearing back with the arm up, a beat to step away.
            Vector3 start = transform.position;
            for (float t = 0f; t < 0.6f && _stagger <= 0f; t += Time.deltaTime)
            {
                transform.position = start - transform.forward * 0.15f * Mathf.Sin(t / 0.6f * Mathf.PI * 0.5f);
                yield return null;
            }
            if (_stagger <= 0f && !Dead)
            {
                Play(swingClip, 0.9f);
                Vector3 back = transform.position;
                for (float t = 0f; t < 0.15f; t += Time.deltaTime)
                {
                    transform.position = back + transform.forward * 0.3f * (t / 0.15f);
                    yield return null;
                }
                Vector3 to = _target.position - transform.position;
                to.y = 0f;
                if (to.magnitude < reachRange + 0.3f && Vector3.Angle(transform.forward, to) < 50f)
                {
                    Play(hitClip, 1f);
                    Struck?.Invoke(this);
                }
            }
            Show(idle);
            _next = Time.time + Random.Range(1.1f, 1.8f);
            _busy = false;
        }

        // A punch (no damage: it only rocks it back a step) or a round, which bleeds it and puts it down for good once
        // it has had enough.
        public void Hit(int damage, Vector3 from)
        {
            if (Dead) return;
            health -= damage;
            Play(hitClip, 0.8f);
            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) transform.position += away.normalized * 0.35f * Mathf.Max(1, damage);
            _stagger = 0.45f;
            if (damage > 0) Bleed(2 + damage * 2);
            if (health > 0) Wounded?.Invoke(this);
            if (health <= 0)
            {
                StopAllCoroutines();
                _busy = false;
                StartCoroutine(Fall());
            }
        }

        // A boot in the chest: no damage, but it is thrown back a long step, its swing broken off, and it needs a moment
        // to come again.
        public void Kick(Vector3 from)
        {
            if (Dead) return;
            Play(hitClip, 1f);
            _stagger = 1.1f;
            Show(reach);
            Vector3 away = transform.position - from;
            away.y = 0f;
            StartCoroutine(Shove(away.sqrMagnitude > 0.0001f ? away.normalized * 1.4f : -transform.forward * 1.4f));
        }

        IEnumerator Shove(Vector3 by)
        {
            Vector3 start = transform.position;
            for (float t = 0f; t < 0.3f && !Dead; t += Time.deltaTime)
            {
                float k = 1f - (1f - t / 0.3f) * (1f - t / 0.3f);
                transform.position = start + by * k;
                yield return null;
            }
        }

        IEnumerator Fall()
        {
            Dead = true;
            Fighting = false;
            Play(fallClip, 1f);
            if (eyes != null) eyes.gameObject.SetActive(false);
            // Sags to its knees, then over onto the grass.
            Vector3 start = transform.position;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                transform.position = start + Vector3.down * 0.5f * (t / 0.8f);
                yield return null;
            }
            transform.position = start;
            Show(fallen);
            Died?.Invoke();
        }

        // Dark blood thrown onto the grass round its feet; it stays there.
        void Bleed(int count)
        {
            if (bloodSplat == null) return;
            for (int i = 0; i < count; i++)
            {
                var splat = Instantiate(bloodSplat, transform.parent);
                Vector2 off = Random.insideUnitCircle * 0.9f;
                splat.transform.position = new Vector3(transform.position.x + off.x, 0.012f + i * 0.001f, transform.position.z + off.y);
                splat.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                splat.transform.localScale = Vector3.one * Random.Range(0.5f, 1.3f);
                splat.SetActive(true);
            }
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position + Vector3.up * 1.4f, volume);
        }
    }
}
