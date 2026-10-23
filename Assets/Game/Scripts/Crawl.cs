using UnityEngine;

namespace GJ.Game
{
    // Moves a transform along a path at a steady speed, over and over (the spark creeping along a fuse in the story).
    public class Crawl : MonoBehaviour
    {
        public Vector3[] path;           // world points
        public float seconds = 8f;
        [Range(0f, 1f)] public float startAt = 0f;

        float _t;

        void OnEnable() => _t = startAt * seconds;

        void Update()
        {
            _t = Mathf.Repeat(_t + Time.unscaledDeltaTime, seconds);
            float total = 0f;
            for (int i = 1; i < path.Length; i++) total += Vector3.Distance(path[i - 1], path[i]);
            float d = _t / seconds * total;
            for (int i = 1; i < path.Length; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (d <= seg) { transform.position = Vector3.Lerp(path[i - 1], path[i], seg > 0f ? d / seg : 0f); return; }
                d -= seg;
            }
        }
    }
}
