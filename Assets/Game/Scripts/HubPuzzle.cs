using UnityEngine;

namespace GJ.Game
{
    // The Sector 4 corridor: a door for every room, taken in any order, with a lamp over each that turns green once
    // you have walked out of it, and at the far end the vault door out of the bunker, which opens when every room
    // is done. The run starts here without a light: the lighter lies by the lift, and no door is worth opening
    // until it is found.
    public class HubPuzzle : RoomPuzzle
    {
        [System.Serializable]
        public class RoomDoor
        {
            public string scene;
            public string title;          // "4-1  POWDER ROOM"
            public Transform point;       // where you look to go in
            public Transform spawn;       // where you stand coming back out of it
            public Renderer lamp;
            public GameObject haloRed, haloGreen;
        }

        public RoomDoor[] doors;
        public Material lampDone;
        public AudioClip doorClip;
        public GameObject lighter;       // dropped on the way down, lying by the lift until it is picked up
        public Transform lighterPoint;
        public AudioClip pickClip;

        bool _leaving;

        public override bool Working => false;
        public override bool AllowsSelfShot => false;

        static bool Done(RoomDoor d) => GameSession.Cleared.Contains(d.scene);
        static bool Built(RoomDoor d) => Application.CanStreamedLevelBeLoaded(d.scene);

        void Start()
        {
            if (lighter != null) lighter.SetActive(!GameSession.HasLighter);
            foreach (var d in doors)
            {
                bool done = Done(d);
                if (done && lampDone != null) d.lamp.sharedMaterial = lampDone;
                if (d.haloRed != null) d.haloRed.SetActive(!done);
                if (d.haloGreen != null) d.haloGreen.SetActive(done);
            }
        }

        // Back out of a room: stand in front of its door. Say how far along the corridor the run has got, and once
        // every door has opened, open the way out.
        public override void Arrived(string cameFrom)
        {
            int total = 0, done = 0;
            RoomDoor justDone = null;
            foreach (var d in doors)
            {
                if (!Built(d)) continue;
                total++;
                if (Done(d)) done++;
                if (d.scene == GameSession.JustCleared) justDone = d;
                if (d.scene == cameFrom && d.spawn != null) StandAt(d.spawn);
            }
            GameSession.JustCleared = null;

            if (!GameSession.HasLighter)
                Director.startMessage = "SECTOR 4\n<size=10>you dropped your lighter coming down. find it</size>";
            else if (done >= total)
            {
                Director.startMessage = "every door is open\n<size=10>the way out is at the end of the corridor</size>";
                RaiseSolved();
            }
            else if (justDone != null)
                Director.startMessage = Loc.F("{0}  -  DONE", Loc.T(justDone.title)) + "\n<size=10>" + Loc.F("{0} of {1} rooms", done, total) + "</size>";
        }

        void StandAt(Transform spot)
        {
            var body = Director.Controller;
            var cc = body.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            body.transform.position = spot.position;
            body.SetLook(spot.eulerAngles.y, 0f);
            if (cc != null) cc.enabled = true;
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            if (_leaving) return null;
            if (lighter != null && lighter.activeSelf && Aim(eye, forward, lighterPoint.position, 2.2f) > 0.9f)
                return new Option { prompt = "[E] pick up your lighter", act = PickUpLighter };
            RoomDoor best = null;
            float bestAim = 0.88f;
            foreach (var d in doors)
            {
                float aim = Aim(eye, forward, d.point.position, 2.2f);
                if (aim > bestAim) { bestAim = aim; best = d; }
            }
            if (best == null) return null;
            string title = Loc.T(best.title);
            if (Done(best)) return new Option { prompt = Loc.F("{0}  -  done", title) };
            if (!Built(best)) return new Option { prompt = Loc.F("{0}  -  sealed", title) };
            if (!GameSession.HasLighter) return new Option { prompt = Loc.F("{0}  -  pitch dark in there. find your lighter first", title) };
            var door = best;
            return new Option { prompt = Loc.F("[E] go in:  {0}", title), act = () => Enter(door), gesture = Gesture.Push };
        }

        void PickUpLighter()
        {
            lighter.SetActive(false);
            if (pickClip != null) AudioSource.PlayClipAtPoint(pickClip, lighterPoint.position, 0.8f);
            Director.PickUpLighter();
            Say("your lighter\n<size=10>[F] open and shut it</size>");
        }

        void Enter(RoomDoor d)
        {
            _leaving = true;
            if (doorClip != null) AudioSource.PlayClipAtPoint(doorClip, d.point.position, 0.9f);
            Director.flow.EnterRoom(d.scene);
        }
    }
}
