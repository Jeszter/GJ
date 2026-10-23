using UnityEngine;

namespace GJ.Game
{
    // Little 3D sets for the backstory cards, built well below the room. One camera films whichever set the current
    // card needs into a tiny point-filtered texture (the PS1 look comes for free), drifting slowly from the shot's
    // start pose along its drift.
    public class StoryStage : MonoBehaviour
    {
        public const int W = 180, H = 90;

        public Camera cam;
        public GameObject[] sets;        // one per card, only the current one is switched on
        public Transform[] shots;        // camera start pose per card
        public Vector3[] drift;          // camera movement per second, in the shot's own axes
        public float[] turn;             // camera yaw per second
        public SegmentDisplay timer;     // the "AIR" counter in the lighter shot, counts down from 90

        RenderTexture _rt;
        int _shot = -1;
        float _t;

        public Texture Texture => _rt;

        void Awake()
        {
            _rt = new RenderTexture(W, H, 16) { filterMode = FilterMode.Point, name = "Story Frame" };
            _rt.Create();
            cam.targetTexture = _rt;
            cam.enabled = false;
            foreach (var s in sets) s.SetActive(false);
        }

        void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }

        public void Show(int shot)
        {
            if (_shot >= 0 && _shot < sets.Length) sets[_shot].SetActive(false);
            _shot = Mathf.Clamp(shot, 0, sets.Length - 1);
            sets[_shot].SetActive(true);
            cam.enabled = true;
            _t = 0f;
            Tick(0f);
        }

        public void Hide()
        {
            if (_shot >= 0 && _shot < sets.Length) sets[_shot].SetActive(false);
            _shot = -1;
            cam.enabled = false;
        }

        public void Tick(float dt)
        {
            if (_shot < 0) return;
            _t += dt;
            var pose = shots[_shot];
            Quaternion rot = pose.rotation * Quaternion.Euler(0f, turn[_shot] * _t, 0f);
            cam.transform.SetPositionAndRotation(pose.position + pose.rotation * drift[_shot] * _t, rot);
            if (timer != null && timer.gameObject.activeInHierarchy) timer.ShowTime(90f - _t, Mathf.Repeat(_t, 1f) < 0.5f);
        }
    }
}
