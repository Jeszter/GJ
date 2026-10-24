using System;
using System.Collections.Generic;
using UnityEngine;

namespace GJ.Game
{
    // A powder trail that burns along a path at a fixed speed. Behind the flame it leaves ash, at the front a hot core
    // and a soft glow. Anything carrying a FuseBlocker (the water bucket) under the flame puts it out; it can be lit
    // again from where it stopped. Reaching the end fires Finished.
    public class Fuse : MonoBehaviour
    {
        public enum State { Idle, Burning, Fizzled, Done }

        public Vector3[] path;          // local space
        public float speed = 0.075f;    // m/s: a full burn outlasts all four breaths, so the player is dead before it lands
        public Material coreMaterial;
        public Material glowMaterial;
        public Material ashMaterial;
        public Transform spark;
        public Light sparkLight;
        public Light emberLight;
        public float blockRadius = 0.2f;
        public AudioClip hissClip;
        public AudioClip igniteClip;
        public AudioClip fizzleClip;

        public event Action Finished;
        public event Action Fizzled;

        public State Current { get; private set; } = State.Idle;
        public float Burnt { get; private set; }
        public float Length => _total;
        public Vector3 FrontWorld => transform.TransformPoint(PointAt(Burnt));

        float[] _cum;
        float _total;
        Mesh _ash, _core, _glow;
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Color> _c = new List<Color>();
        readonly List<int> _t = new List<int>();
        readonly Collider[] _hits = new Collider[8];
        AudioSource _hiss;

        void Awake()
        {
            _cum = new float[path.Length];
            for (int i = 1; i < path.Length; i++) _cum[i] = _cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            _total = _cum[path.Length - 1];

            _ash = MakeLayer("Ash", ashMaterial);
            _glow = MakeLayer("Glow", glowMaterial);
            _core = MakeLayer("Core", coreMaterial);

            // The hiss rides along with the flame, so you can hear where it is even from the floor.
            if (spark != null && hissClip != null)
            {
                _hiss = spark.gameObject.AddComponent<AudioSource>();
                _hiss.clip = hissClip;
                _hiss.loop = true;
                _hiss.playOnAwake = false;
                _hiss.spatialBlend = 1f;
                _hiss.rolloffMode = AudioRolloffMode.Linear;
                _hiss.minDistance = 0.5f;
                _hiss.maxDistance = 9f;
                _hiss.volume = 0.7f;
            }
            SetFlameVisible(false);
        }

        void PlayAt(AudioClip clip, float volume)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.TransformPoint(PointAt(Burnt)), volume);
        }

        Mesh MakeLayer(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Fuse " + name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mesh;
        }

        public bool Ignite()
        {
            if (Current != State.Idle && Current != State.Fizzled) return false;
            Current = State.Burning;
            SetFlameVisible(true);
            PlayAt(igniteClip, 0.8f);
            return true;
        }

        void SetFlameVisible(bool on)
        {
            if (spark != null) spark.gameObject.SetActive(on);
            if (sparkLight != null) sparkLight.enabled = on;
            if (emberLight != null) emberLight.enabled = on;
            if (_hiss != null && on)
            {
                _hiss.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                _hiss.Play();
            }
        }

        void Update()
        {
            if (Current != State.Burning) return;

            Burnt = Mathf.Min(_total, Burnt + speed * Time.deltaTime);
            Vector3 front = PointAt(Burnt);
            if (spark != null) spark.localPosition = front;
            if (emberLight != null) emberLight.transform.localPosition = PointAt(Mathf.Max(0f, Burnt - 0.4f)) + Vector3.up * 0.25f;

            RebuildMeshes();

            if (Burnt >= _total)
            {
                Current = State.Done;
                SetFlameVisible(false);
                Finished?.Invoke();
                return;
            }

            int n = Physics.OverlapSphereNonAlloc(transform.TransformPoint(front), blockRadius, _hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].GetComponentInParent<FuseBlocker>() == null) continue;
                Current = State.Fizzled;
                SetFlameVisible(false);
                PlayAt(fizzleClip, 0.8f);
                Fizzled?.Invoke();
                return;
            }
        }

        public Vector3 PointAt(float distance)
        {
            if (distance <= 0f) return path[0];
            for (int i = 1; i < path.Length; i++)
            {
                if (_cum[i] < distance) continue;
                float seg = _cum[i] - _cum[i - 1];
                return Vector3.Lerp(path[i - 1], path[i], seg > 0f ? (distance - _cum[i - 1]) / seg : 0f);
            }
            return path[path.Length - 1];
        }

        void RebuildMeshes()
        {
            Ribbon(_ash, 0f, Mathf.Max(0f, Burnt - 0.12f), 0.08f, 0.003f, false);
            Ribbon(_core, Mathf.Max(0f, Burnt - 0.9f), Burnt, 0.06f, 0.006f, true);
            Ribbon(_glow, Mathf.Max(0f, Burnt - 1.6f), Burnt, 0.5f, 0.005f, true);
        }

        // Flat strip lying on the floor between two distances along the path; "fade" ramps vertex alpha up toward
        // the flame so the hot part cools off behind it.
        void Ribbon(Mesh mesh, float from, float to, float width, float lift, bool fade)
        {
            _v.Clear(); _uv.Clear(); _c.Clear(); _t.Clear();
            if (to - from > 0.01f)
            {
                var pts = new List<float> { from };
                for (int i = 1; i < path.Length - 1; i++)
                    if (_cum[i] > from && _cum[i] < to) pts.Add(_cum[i]);
                pts.Add(to);

                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 p = PointAt(pts[i]) + Vector3.up * lift;
                    Vector3 ahead = PointAt(Mathf.Min(_total, pts[i] + 0.05f)) - PointAt(Mathf.Max(0f, pts[i] - 0.05f));
                    Vector3 side = Vector3.Cross(Vector3.up, ahead.normalized) * width * 0.5f;
                    float a = fade ? Mathf.Clamp01((pts[i] - from) / Mathf.Max(0.01f, to - from)) : 1f;
                    a *= a;
                    _v.Add(p - side); _uv.Add(new Vector2(0f, pts[i])); _c.Add(new Color(1, 1, 1, a));
                    _v.Add(p + side); _uv.Add(new Vector2(1f, pts[i])); _c.Add(new Color(1, 1, 1, a));
                    if (i == 0) continue;
                    int b = _v.Count - 4;
                    _t.AddRange(new[] { b, b + 2, b + 3, b, b + 3, b + 1 });
                }
            }
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _uv);
            mesh.SetColors(_c);
            mesh.SetTriangles(_t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
