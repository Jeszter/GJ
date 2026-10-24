using GJ.PSX;
using UnityEngine;

namespace GJ.Game
{
    // Player options, kept in PlayerPrefs and pushed into the running scene by Apply().
    public static class GameSettings
    {
        public static int Sensitivity = 5;     // 1..10
        public static bool InvertY;
        public static int Volume = 8;          // 0..10
        public static int Pixels = 0;          // index into PixelRows
        public static bool Fullscreen = true;

        public static readonly int[] PixelRows = { 240, 360, 480 };
        public static readonly string[] PixelNames = { "CHUNKY", "CLASSIC", "SHARP" };

        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            Sensitivity = PlayerPrefs.GetInt("gj.sensitivity", Sensitivity);
            InvertY = PlayerPrefs.GetInt("gj.invertY", 0) == 1;
            Volume = PlayerPrefs.GetInt("gj.volume", Volume);
            Pixels = Mathf.Clamp(PlayerPrefs.GetInt("gj.pixels", Pixels), 0, PixelRows.Length - 1);
            Fullscreen = PlayerPrefs.GetInt("gj.fullscreen", 1) == 1;
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("gj.sensitivity", Sensitivity);
            PlayerPrefs.SetInt("gj.invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt("gj.volume", Volume);
            PlayerPrefs.SetInt("gj.pixels", Pixels);
            PlayerPrefs.SetInt("gj.fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            AudioListener.volume = Volume / 10f;
            foreach (var c in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            {
                c.lookSensitivity = 0.02f + Sensitivity * 0.012f;
                c.invertY = InvertY;
            }
            foreach (var s in Object.FindObjectsByType<PSXScreen>(FindObjectsSortMode.None))
                s.SetResolution(PixelRows[Pixels]);
            if (!Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        }
    }

    // What survives a scene load: whether the room is opening straight after the story, the run's stats, and which
    // attempt at the current room this is.
    public static class GameSession
    {
        public static bool FromStory;
        public static int Deaths;
        public static float Time;
        public static string Room;       // the room the last attempt was made in
        public static int Attempt;       // this attempt at it, from 1
        public static string LastFail;   // why the attempt before ran out ("one more time"), if it did

        // The revolver and its rounds go with you from room to room; a round is spent for good.
        public static bool HasRevolver;
        public static int Rounds;
        static bool _roomHadRevolver;
        static int _roomRounds;

        // The lighter: not in hand at the start of a run, it lies in the corridor to be found.
        public static bool HasLighter;

        // Rooms walked out of, by scene name: the corridor shows them done and opens the way out once every one is.
        public static readonly System.Collections.Generic.HashSet<string> Cleared = new System.Collections.Generic.HashSet<string>();
        public static string JustCleared;   // the room just walked out of, for the corridor to say so

        // Where you fell on the failed tries at this room (feet position and facing): the bodies stay where they dropped.
        public struct Body
        {
            public Vector3 position;
            public float yaw;
        }
        public static readonly System.Collections.Generic.List<Body> Bodies = new System.Collections.Generic.List<Body>();
        const int MaxBodies = 8;

        public static void AddBody(Vector3 position, float yaw)
        {
            if (Bodies.Count >= MaxBodies) Bodies.RemoveAt(0);
            Bodies.Add(new Body { position = position, yaw = yaw });
        }

        // A room starts: one more attempt at the same room, or the first at a new one. Another attempt gets back what
        // you came in with, so rounds fired on a failed try aren't lost.
        public static void BeginAttempt(string room)
        {
            if (room == Room)
            {
                Attempt++;
                HasRevolver = _roomHadRevolver;
                Rounds = _roomRounds;
            }
            else
            {
                Attempt = 1;
                _roomHadRevolver = HasRevolver;
                _roomRounds = Rounds;
                Bodies.Clear();
            }
            Room = room;
        }

        public static void Reset()
        {
            Deaths = 0;
            Time = 0f;
            Room = null;
            Attempt = 0;
            LastFail = null;
            HasRevolver = false;
            Rounds = 0;
            Bodies.Clear();
            Cleared.Clear();
            JustCleared = null;
            HasLighter = false;
        }
    }
}
