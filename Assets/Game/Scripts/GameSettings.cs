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

    // What survives a room reload: whether to skip the title, and the run's stats.
    public static class GameSession
    {
        public static bool SkipTitle;
        public static int Deaths;
        public static float Time;

        public static void Reset()
        {
            Deaths = 0;
            Time = 0f;
        }
    }
}
