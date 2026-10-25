using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // Builds the rooms in PS1 style: tiny generated pixel textures, flat low-poly meshes, a few warm lights and a
    // first-person player holding a lighter. Room 1 is "powder fuse -> pipe -> vault door", room 2 (in
    // PSXRoomBuilder.Room2.cs) is "cistern -> chute -> counterweight -> bolt". Everything it makes lands in
    // Assets/PSX/Generated and can be swapped later.
    public static partial class PSXRoomBuilder
    {
        const string Root = "Assets/PSX/Generated";
        const string ScenePath = "Assets/Scenes/Room01.unity";
        const string Scene2Path = "Assets/Scenes/Room02.unity";

        // The room being built; each room sets its own size before building.
        static Vector3 RoomMin = new Vector3(-3f, 0f, 0f);
        static Vector3 RoomMax = new Vector3(3f, 3.4f, 8f);

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static Transform _world;
        static int _meshId;
        static string _meshFolder;

        const float DoorHalfWidth = 0.9f;
        const float DoorHeight = 2.4f;

        // Gameplay pieces made while building, wired together at the end.
        static Game.Fuse _fuse;
        static Game.VaultDoor _door;
        static Renderer _displayDigits;
        static List<Vector3> _fusePath;

        // The main menu is set in room 1, so it is rebuilt along with it.
        [MenuItem("GJ/Build PSX Room")]
        public static void Build()
        {
            if (!BeginBuild()) return;
            BuildRoom1();
            BuildMenu();
            FinishBuild();
        }

        [MenuItem("GJ/Build PSX Room 2")]
        public static void BuildSecond()
        {
            if (!BeginBuild()) return;
            BuildRoom2();
            FinishBuild();
        }

        [MenuItem("GJ/Build PSX Room 3")]
        public static void BuildThird()
        {
            if (!BeginBuild()) return;
            BuildRoom3();
            FinishBuild();
        }

        [MenuItem("GJ/Build All Rooms")]
        public static void BuildAll()
        {
            if (!BeginBuild()) return;
            BuildRoom4();
            BuildRoom3();
            BuildRoom2();
            BuildRoom1();
            BuildHub();
            BuildChapel();
            BuildGraveyard();
            BuildMenu();
            FinishBuild();
        }

        const string LastBuildKey = "GJ.Build.FinishedAt";

        static bool BeginBuild()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before building the room."); return false; }
            // A request from the editor bridge that times out is sent again, and a build always outlasts the timeout:
            // one that finished under a minute ago is that same request coming back, not a new one.
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(LastBuildKey, -1000f) < 60f)
            {
                Debug.LogWarning("Built a moment ago, skipping the repeat.");
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            Directory.CreateDirectory(Root + "/Textures");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Meshes");
            // Meshes from before rooms had their own folders.
            foreach (var old in Directory.GetFiles(Root + "/Meshes", "*.asset")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));
            AssetDatabase.Refresh();
            MakeMaterials();
            PSXSoundGenerator.Generate();
            return true;
        }

        // Every room's meshes live in their own folder, so rebuilding one room leaves the other intact.
        static UnityEngine.SceneManagement.Scene NewRoomScene(string roomName)
        {
            _meshFolder = $"{Root}/Meshes/{roomName}";
            AssetDatabase.DeleteAsset(_meshFolder);
            Directory.CreateDirectory(_meshFolder);
            AssetDatabase.Refresh();
            _meshId = 0;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _world = new GameObject(roomName).transform;
            return scene;
        }

        static void BuildRoom1()
        {
            RoomMin = new Vector3(-3f, 0f, 0f);
            RoomMax = new Vector3(3f, 3.4f, 8f);
            var scene = NewRoomScene("Room01");

            BuildShell(new[] { 1.6f, 4.2f, 6.8f });
            BuildDoor();
            BuildPipes();
            BuildFuse();
            BuildProps();
            BuildDetails();
            BuildLights();
            DressRoom1();
            Models1();
            AutoColliders(_world);
            Life1();

            var rig = BuildPlayer(new Vector3(0.7f, 0f, 0.8f), -12f);
            var puzzle = _fuse.gameObject.AddComponent<Game.FusePuzzle>();
            puzzle.fuse = _fuse;
            puzzle.bucket = GameObject.Find("Room01/Props/Bucket")?.transform;
            puzzle.shoveClip = PSXSoundGenerator.Load("clunk");

            // M.'s revolver left on the workbench: two rounds, and they have to last the whole way down.
            var benchGun = Group("BenchRevolver", new Vector3(2.38f, 0.948f, 2.95f), 0f, _world);
            benchGun.localRotation = Quaternion.Euler(0f, 70f, 90f);
            RevolverModel(benchGun);
            var gunPoint = new GameObject("BenchRevolverPoint").transform;
            gunPoint.SetParent(_world, false);
            gunPoint.localPosition = new Vector3(2.38f, 0.98f, 2.95f);
            puzzle.benchGun = benchGun.gameObject;
            puzzle.benchGunPoint = gunPoint;
            puzzle.takeClip = PSXSoundGenerator.Load("rifle_bolt");

            var pipeGas = GameObject.Find("Room01/Props/PipeGas").transform;
            puzzle.gasCylinder = pipeGas.GetComponent<Game.Shootable>();
            puzzle.gasValve = pipeGas.Find("HandWheel");
            puzzle.gasHiss = pipeGas.GetComponent<AudioSource>();
            puzzle.valveClip = PSXSoundGenerator.Load("valve");

            BuildDirector(rig, puzzle, 4, "SECTOR 4 - POWDER ROOM\nyou have four breaths", 1, "Hub");
            UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>().revolver = BuildRevolver(rig);
            SetupRenderSettings();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("PSX room built: " + ScenePath);
        }

        // The main menu first (the game starts there), then the rooms in order. Scenes that no longer exist drop out.
        static void FinishBuild()
        {
            var order = new[] { MenuPath, HubPath, ScenePath, Scene2Path, Scene3Path, Scene4Path, ChapelPath, GraveyardPath };
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => System.Array.IndexOf(order, s.path) >= 0 || !File.Exists(s.path));
            int at = 0;
            foreach (var path in order)
                if (File.Exists(path)) scenes.Insert(at++, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            SessionState.SetFloat(LastBuildKey, (float)EditorApplication.timeSinceStartup);
        }

        // ---------------------------------------------------------------- textures

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }

        static Color Shade(Color c, float d) => new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), c.a);

        static Texture2D MakeTexture(string name, int size, Func<int, int, Color> pixel, bool alpha = false) =>
            MakeTexture(name, size, size, pixel, alpha);

        static Texture2D MakeTexture(string name, int width, int height, Func<int, int, Color> pixel, bool alpha = false)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, pixel(x, y));
            string path = $"{Root}/Textures/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaIsTransparency = alpha;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D StoneBlocks() => MakeTexture("stone_blocks", 64, (x, y) =>
        {
            // 32x16 px blocks, every other row shifted half a block.
            int row = y / 16;
            int bx = (x + (row % 2) * 16) % 64;
            int col = bx / 32;
            int lx = bx % 32, ly = y % 16;
            if (lx == 0 || ly == 0) return new Color(0.13f, 0.13f, 0.14f);
            var c = new Color(0.42f, 0.43f, 0.45f);
            c = Shade(c, (Hash(col, row, 1) - 0.5f) * 0.12f);
            c = Shade(c, (Hash(x, y, 2) - 0.5f) * 0.07f);
            if (ly == 15 || lx == 1) c = Shade(c, 0.06f);
            if (ly == 1 || lx == 31) c = Shade(c, -0.07f);
            if (Hash(x / 3, y / 3, 3) > 0.96f) c = Shade(c, -0.06f);
            return c;
        });

        static Texture2D FloorTiles() => MakeTexture("floor_tiles", 64, (x, y) =>
        {
            int lx = x % 32, ly = y % 32;
            if (lx == 0 || ly == 0) return new Color(0.11f, 0.1f, 0.09f);
            var c = new Color(0.3f, 0.3f, 0.3f);
            c = Shade(c, (Hash(x / 32, y / 32, 4) - 0.5f) * 0.08f);
            c = Shade(c, (Hash(x, y, 5) - 0.5f) * 0.08f);
            if (Hash(x / 4, y / 4, 6) > 0.9f) c = Shade(c, -0.06f);
            // A couple of cracks.
            if (Mathf.Abs((x * 0.7f + 9f) - y) < 0.6f && x > 34 && x < 58) c = Shade(c, -0.15f);
            if (Mathf.Abs((-x * 0.5f + 24f) - y) < 0.6f && x > 4 && x < 22) c = Shade(c, -0.15f);
            return c;
        });

        static Texture2D Ceiling() => MakeTexture("ceiling", 32, (x, y) =>
            Shade(new Color(0.2f, 0.2f, 0.21f), (Hash(x, y, 7) - 0.5f) * 0.08f + (Hash(x / 4, y / 4, 8) - 0.5f) * 0.06f));

        static Texture2D VaultMetal() => MakeTexture("vault_metal", 64, (x, y) =>
        {
            var c = new Color(0.4f, 0.44f, 0.48f);
            c = Shade(c, (Hash(x, y, 9) - 0.5f) * 0.06f);
            if (y % 16 == 0) c = Shade(c, -0.18f);
            if (y % 16 == 1) c = Shade(c, 0.08f);
            if (x % 16 == 3 && y % 16 == 4) c = Shade(c, 0.2f);   // rivets
            if (x % 16 == 3 && y % 16 == 3) c = Shade(c, -0.15f);
            if (Hash(x, y / 2, 10) > 0.985f) c = Shade(c, 0.15f); // scratches
            float rust = Mathf.Clamp01((Hash(x / 2, 0, 11) - 0.72f) * 4f) * Mathf.Clamp01(1f - y / 40f);
            return Color.Lerp(c, new Color(0.42f, 0.22f, 0.1f), rust * 0.8f);
        });

        static Texture2D Rust() => MakeTexture("rust", 32, (x, y) =>
        {
            var c = Color.Lerp(new Color(0.36f, 0.19f, 0.09f), new Color(0.5f, 0.3f, 0.14f), Hash(x / 2, y / 2, 12));
            return Shade(c, (Hash(x, y, 13) - 0.5f) * 0.1f);
        });

        static Texture2D RedPaint() => MakeTexture("red_paint", 32, (x, y) =>
        {
            var c = Shade(new Color(0.6f, 0.09f, 0.07f), (Hash(x, y, 14) - 0.5f) * 0.06f);
            if (Hash(x / 2, y / 2, 15) > 0.92f) c = new Color(0.38f, 0.36f, 0.34f); // chipped paint
            if (y % 32 == 26 || y % 32 == 27) c = new Color(0.85f, 0.82f, 0.75f);  // label band
            return c;
        });

        static Texture2D Wood() => MakeTexture("wood", 32, (x, y) =>
        {
            int plank = x / 8;
            if (x % 8 == 0) return new Color(0.16f, 0.1f, 0.06f);
            var c = Shade(new Color(0.4f, 0.31f, 0.21f), (Hash(plank, 0, 16) - 0.5f) * 0.1f);
            c = Shade(c, Mathf.Sin((y + Hash(plank, 1, 17) * 20f) * 0.9f + x * 0.3f) * 0.03f);
            return Shade(c, (Hash(x, y, 18) - 0.5f) * 0.05f);
        });

        // Crate side: dark frame boards round the edge and a diagonal brace across the planks.
        static Texture2D Crate() => MakeTexture("crate", 32, (x, y) =>
        {
            bool frame = x < 4 || y < 4 || x > 27 || y > 27;
            bool brace = !frame && Mathf.Abs(x - y) < 3;
            if (frame || brace)
            {
                if ((frame && (x == 4 || y == 4 || x == 27 || y == 27)) || (brace && Mathf.Abs(x - y) == 2)) return new Color(0.12f, 0.08f, 0.05f);
                return Shade(new Color(0.33f, 0.24f, 0.15f), (Hash(x, y, 22) - 0.5f) * 0.06f);
            }
            if (y % 8 == 0) return new Color(0.14f, 0.1f, 0.06f);
            var c = Shade(new Color(0.44f, 0.34f, 0.23f), (Hash(0, y / 8, 23) - 0.5f) * 0.1f);
            return Shade(c, (Hash(x, y, 24) - 0.5f) * 0.05f);
        });

        // Red seven-segment "00:00" in the bottom third (that is what the display face maps), unlit segments faint.
        static Texture2D DisplayDigits() => MakeTexture("display_digits", 64, (x, y) =>
        {
            var off = new Color(0.08f, 0.01f, 0.01f, 1f);
            if (y < 3 || y > 19) return Color.black;
            int[] starts = { 6, 18, 36, 48 };
            foreach (int sx in starts)
            {
                int lx = x - sx, ly = y - 3;
                if (lx < 0 || lx > 9) continue;
                bool top = ly >= 14 && ly <= 15 && lx >= 2 && lx <= 7;
                bool bottom = ly <= 1 && lx >= 2 && lx <= 7;
                bool middle = ly >= 7 && ly <= 8 && lx >= 2 && lx <= 7;
                bool vert = (lx <= 1 || lx >= 8) && ly >= 2 && ly <= 13 && ly != 7 && ly != 8;
                if (top || bottom || vert) return Color.white;
                if (middle) return off;
                return Color.black;
            }
            if ((x == 31 || x == 32) && (y == 7 || y == 8 || y == 13 || y == 14)) return Color.white;
            return Color.black;
        });

        static Texture2D Hazard() => MakeTexture("hazard", 32, (x, y) =>
        {
            bool yellow = ((x + y) / 8) % 2 == 0;
            var c = yellow ? new Color(0.78f, 0.6f, 0.08f) : new Color(0.08f, 0.08f, 0.08f);
            return Shade(c, (Hash(x, y, 19) - 0.5f) * 0.1f - (Hash(x / 4, y / 4, 20) > 0.8f ? 0.1f : 0f));
        });

        static Texture2D WarningSign() => MakeTexture("warning_sign", 32, (x, y) =>
        {
            if (x < 1 || y < 1 || x > 30 || y > 30) return new Color(0.1f, 0.1f, 0.1f);
            var bg = new Color(0.8f, 0.62f, 0.1f);
            // Black triangle with a yellow "!" inside.
            float cx = 15.5f;
            bool inTri = y >= 5 && y <= 26 && Mathf.Abs(x - cx) <= (26 - y) * 0.55f;
            bool inInner = y >= 8 && y <= 22 && Mathf.Abs(x - cx) <= (22 - y) * 0.55f - 1f;
            bool bang = (x == 15 || x == 16) && ((y >= 12 && y <= 18) || y == 10);
            var c = inTri && !inInner ? new Color(0.08f, 0.08f, 0.08f) : bg;
            if (bang) c = new Color(0.08f, 0.08f, 0.08f);
            return Shade(c, (Hash(x, y, 21) - 0.5f) * 0.08f);
        });

        static Texture2D Glow() => MakeTexture("glow", 32, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
            float a = Mathf.Clamp01(1f - d);
            a = Mathf.Round(a * a * 6f) / 6f; // banded falloff reads more "PS1"
            return new Color(1f, 1f, 1f, a);
        }, true);

        // Teardrop flame: round at the root, drawn out to a point, in bands from deep red at the rim through orange
        // to a pale centre, with a blue root.
        static Texture2D Flame() => MakeTexture("flame", 32, (x, y) =>
        {
            float u = (x + 0.5f) / 32f - 0.5f;
            float v = (y + 0.5f) / 32f;
            float width = 0.44f * Mathf.Pow(Mathf.Sin(Mathf.Clamp01(v * 1.05f) * Mathf.PI * 0.92f + 0.12f), 0.8f) * Mathf.Pow(1f - v, 0.7f);
            float a = Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(0.01f, width));
            a = Mathf.Clamp01(a * 1.25f - v * 0.15f);
            a = Mathf.Round(a * 7f) / 7f;
            Color c = a > 0.75f ? new Color(1f, 0.97f, 0.85f)
                    : a > 0.5f ? new Color(1f, 0.8f, 0.35f)
                    : a > 0.25f ? new Color(1f, 0.5f, 0.12f)
                    : new Color(0.85f, 0.22f, 0.05f);
            if (v < 0.2f && a > 0.3f) c = Color.Lerp(new Color(0.25f, 0.4f, 1f), c, v / 0.2f); // blue root
            return new Color(c.r, c.g, c.b, a);
        }, true);

        // Tiny 5x7 stencil font for signs painted into textures.
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".####", "#....", "#....", "#.###", "#...#", "#...#", ".###." },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
            ['J'] = new[] { "....#", "....#", "....#", "....#", "#...#", "#...#", ".###." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." },
            ['>'] = new[] { "#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#...." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." },
            ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
            [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
        };

        // A painted plate: background, a thin border, and up to two centred lines of stencil text.
        static Texture2D Sign(string name, int width, int height, Color bg, Color ink, int scale, params string[] lines)
        {
            var pixels = new bool[width, height];
            int lineH = 7 * scale, gap = 3 * scale;
            int blockH = lines.Length * lineH + (lines.Length - 1) * gap;
            for (int l = 0; l < lines.Length; l++)
            {
                string text = lines[l];
                int textW = text.Length * 6 * scale - scale;
                int x0 = (width - textW) / 2;
                int top = (height + blockH) / 2 - l * (lineH + gap);
                for (int c = 0; c < text.Length; c++)
                {
                    if (!Glyphs.TryGetValue(text[c], out var g)) continue;
                    for (int gy = 0; gy < 7; gy++)
                        for (int gx = 0; gx < 5; gx++)
                        {
                            if (g[gy][gx] != '#') continue;
                            for (int sy = 0; sy < scale; sy++)
                                for (int sx = 0; sx < scale; sx++)
                                {
                                    int px = x0 + (c * 6 + gx) * scale + sx, py = top - 1 - gy * scale - sy;
                                    if (px >= 0 && px < width && py >= 0 && py < height) pixels[px, py] = true;
                                }
                        }
                }
            }
            return MakeTexture(name, width, height, (x, y) =>
            {
                bool border = x == 1 || y == 1 || x == width - 2 || y == height - 2;
                var c = pixels[x, y] || border ? ink : bg;
                // Worn paint: a few flecks missing.
                if (Hash(x, y, name.Length) > 0.94f) c = Color.Lerp(c, bg * 0.7f, 0.6f);
                return Shade(c, (Hash(x / 2, y / 2, 50) - 0.5f) * 0.06f);
            });
        }

        // Soft glow across the width of a ribbon (u), constant along it (v).
        static Texture2D GlowStrip() => MakeTexture("glow_strip", 32, (x, y) =>
        {
            float a = Mathf.Clamp01(1f - Mathf.Abs((x + 0.5f) / 16f - 1f));
            a = Mathf.Round(a * a * 5f) / 5f;
            return new Color(1f, 1f, 1f, a);
        }, true);

        static Texture2D Plain(string name, Color color, float noise) =>
            MakeTexture(name, 16, (x, y) => Shade(color, (Hash(x, y, name.Length) - 0.5f) * noise));

        // ---------------------------------------------------------------- materials

        static void MakeMaterials()
        {
            Mats.Clear();
            var lit = Shader.Find("PSX/Lit");
            var add = Shader.Find("PSX/Additive");

            Lit("Stone", lit, StoneBlocks());
            Lit("Floor", lit, FloorTiles());
            Lit("Ceiling", lit, Ceiling());
            Lit("VaultMetal", lit, VaultMetal());
            Lit("Rust", lit, Rust());
            Lit("RedPaint", lit, RedPaint());
            Lit("Wood", lit, Wood());
            Lit("Crate", lit, Crate());
            Additive("DisplayDigits", add, DisplayDigits(), new Color(1f, 0.12f, 0.06f) * 1.6f);
            Lit("Hazard", lit, Hazard());
            Lit("Sign", lit, WarningSign());
            Lit("DarkMetal", lit, Plain("dark_metal", new Color(0.18f, 0.19f, 0.2f), 0.08f));
            Lit("Steel", lit, Plain("steel", new Color(0.55f, 0.57f, 0.6f), 0.08f), new Color(0.05f, 0.05f, 0.06f));
            Lit("Powder", lit, Plain("powder", new Color(0.08f, 0.07f, 0.07f), 0.1f));
            // The hand sits closer to the flame than any light could sensibly reach, so its warm glow is emission.
            // Worn skin: blotchy tone, a few darker creases and grime.
            var skin = MakeTexture("skin", 32, (x, y) =>
            {
                var c = Shade(new Color(0.56f, 0.42f, 0.35f), (Hash(x / 4, y / 4, 43) - 0.5f) * 0.06f + (Hash(x, y, 44) - 0.5f) * 0.04f);
                if (Mathf.Abs(Mathf.Sin(x * 0.35f + y * 0.9f)) < 0.06f) c = Shade(c, -0.06f);
                if (Hash(x / 2, y / 2, 45) > 0.93f) c = Color.Lerp(c, new Color(0.25f, 0.2f, 0.17f), 0.4f);
                return c;
            });
            Lit("Skin", lit, skin, new Color(0.15f, 0.08f, 0.04f));
            Lit("Glove", lit, MakeTexture("glove", 16, (x, y) =>
                Shade(new Color(0.24f, 0.17f, 0.12f), (Hash(x, y, 40) - 0.5f) * 0.08f + (y % 8 == 0 ? -0.05f : 0f))),
                new Color(0.05f, 0.03f, 0.015f));
            // Brushed steel: faint vertical streaks.
            Lit("Chrome", lit, MakeTexture("chrome", 16, (x, y) =>
                Shade(new Color(0.62f, 0.63f, 0.66f), (Hash(x, 0, 41) - 0.5f) * 0.16f + (Hash(x, y, 42) - 0.5f) * 0.05f)),
                new Color(0.2f, 0.16f, 0.12f));
            Lit("Chimney", lit, MakeTexture("chimney", 16, (x, y) =>
                (x % 4 == 1 || x % 4 == 2) && (y % 4 == 1 || y % 4 == 2) ? new Color(0.05f, 0.04f, 0.04f) : new Color(0.62f, 0.63f, 0.66f)),
                new Color(0.12f, 0.09f, 0.06f));
            Lit("Glass", lit, Plain("glass", new Color(0.3f, 0.45f, 0.4f), 0.1f));
            Lit("Bulb", lit, Plain("bulb", Color.white, 0f), new Color(1f, 0.82f, 0.5f) * 2f);
            Lit("RedLamp", lit, Plain("red_lamp", Color.white, 0f), new Color(1f, 0.1f, 0.06f) * 2.5f);
            Lit("Display", lit, Plain("display", new Color(0.06f, 0.06f, 0.06f), 0.04f));
            Lit("Ash", lit, Plain("ash", new Color(0.24f, 0.23f, 0.22f), 0.12f));
            Lit("SignSector", lit, Sign("sign_sector", 64, 16, new Color(0.17f, 0.18f, 0.19f), new Color(0.78f, 0.76f, 0.7f), 1, "SECTOR 4"));
            Lit("SignDanger", lit, Sign("sign_danger", 64, 32, new Color(0.8f, 0.62f, 0.1f), new Color(0.08f, 0.08f, 0.08f), 1, "DANGER", "EXPLOSIVES"));
            Lit("SignAir", lit, Sign("sign_air", 32, 16, new Color(0.15f, 0.15f, 0.16f), new Color(0.85f, 0.82f, 0.75f), 1, "AIR"));
            Lit("SignExit", lit, Sign("sign_exit", 48, 16, new Color(0.08f, 0.38f, 0.16f), new Color(0.9f, 0.95f, 0.9f), 1, "EXIT >"), new Color(0.05f, 0.25f, 0.1f));
            Lit("Paper", lit, MakeTexture("paper", 16, (x, y) =>
                Shade(new Color(0.78f, 0.73f, 0.62f), (Hash(x, y, 51) - 0.5f) * 0.06f + (y % 3 == 0 && x > 2 && x < 13 && y > 2 && y < 14 ? -0.12f : 0f))),
                new Color(0.06f, 0.05f, 0.04f));
            Lit("NoteDiary", lit, NoteTexture("note_diary", 7, false), new Color(0.07f, 0.06f, 0.05f));
            Lit("NoteScrap", lit, NoteTexture("note_scrap", 8, true), new Color(0.07f, 0.06f, 0.05f));
            Lit("Gauge", lit, MakeTexture("gauge", 16, (x, y) =>
            {
                Vector2 d = new Vector2(x - 7.5f, y - 7.5f);
                if (d.magnitude > 7.2f) return new Color(0.1f, 0.1f, 0.1f);
                if (Mathf.Abs(d.y + d.x * 0.9f) < 0.8f && d.x < 0 && d.magnitude < 6f) return new Color(0.75f, 0.1f, 0.08f);
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (d.magnitude > 5.5f && Mathf.Repeat(ang, 30f) < 8f && ang > -40f) return new Color(0.15f, 0.15f, 0.15f);
                return new Color(0.85f, 0.82f, 0.74f);
            }), new Color(0.12f, 0.11f, 0.09f));
            Lit("Water", lit, Plain("water", new Color(0.12f, 0.2f, 0.24f), 0.08f), new Color(0.03f, 0.05f, 0.06f));
            Lit("ExitGlow", lit, Plain("exit_glow", new Color(0.85f, 0.9f, 1f), 0.04f), new Color(0.75f, 0.85f, 1f) * 1.3f);

            // Board-formed concrete for pillars and the pump room: plank imprints, pits, damp toward the floor.
            Lit("Concrete", lit, MakeTexture("concrete", 32, (x, y) =>
            {
                var c = Shade(new Color(0.4f, 0.4f, 0.38f), (Hash(x / 2, y / 6, 70) - 0.5f) * 0.06f + (Hash(x, y, 71) - 0.5f) * 0.06f);
                if (y % 8 == 0) c = Shade(c, -0.07f);
                if (Hash(x, y, 72) > 0.96f) c = Shade(c, -0.12f);
                return Color.Lerp(c, new Color(0.24f, 0.25f, 0.22f), Mathf.Clamp01((6f - y) / 6f) * 0.5f);
            }));
            // The cistern: faded green paint over riveted plates, rust bleeding down from the seams.
            Lit("TankPaint", lit, MakeTexture("tank_paint", 32, (x, y) =>
            {
                var c = Shade(new Color(0.24f, 0.32f, 0.26f), (Hash(x / 3, y / 3, 73) - 0.5f) * 0.06f + (Hash(x, y, 74) - 0.5f) * 0.04f);
                if (y % 16 == 0) c = Shade(c, -0.1f);
                if (y % 16 == 2 && x % 4 == 1) c = Shade(c, 0.12f);
                float rust = Mathf.Clamp01((Hash(x, 0, 75) - 0.7f) * 5f) * Mathf.Clamp01(1f - (y % 16) / 12f);
                if (Hash(x / 2, y / 2, 76) > 0.94f) rust = 1f;
                return Color.Lerp(c, new Color(0.4f, 0.2f, 0.09f), rust * 0.7f);
            }));
            // Chain: dark oval links, every other one seen edge-on.
            Lit("Chain", lit, MakeTexture("chain", 16, (x, y) =>
            {
                int link = y / 8, ly = y % 8;
                bool edge = link % 2 == 1;
                bool metal = edge ? x >= 6 && x <= 9 : (x == 4 || x == 11 || ((ly == 0 || ly == 7) && x > 4 && x < 11));
                return metal ? Shade(new Color(0.34f, 0.33f, 0.31f), (Hash(x, y, 77) - 0.5f) * 0.1f + (ly < 2 ? 0.08f : 0f)) : new Color(0.03f, 0.03f, 0.03f);
            }), new Color(0.02f, 0.02f, 0.02f));
            // Floor drain and vent grates: bars with black gaps.
            Lit("Grate", lit, MakeTexture("grate", 16, (x, y) =>
                x % 4 == 0 || y % 8 == 0 ? Shade(new Color(0.3f, 0.3f, 0.31f), (Hash(x, y, 78) - 0.5f) * 0.1f) : new Color(0.02f, 0.02f, 0.025f)));
            Lit("Blanket", lit, MakeTexture("blanket", 32, (x, y) =>
            {
                var c = (y / 4) % 4 == 0 ? new Color(0.3f, 0.11f, 0.09f) : new Color(0.24f, 0.22f, 0.2f);
                return Shade(c, (Hash(x, y, 79) - 0.5f) * 0.08f + (Hash(x / 4, y / 4, 80) - 0.5f) * 0.06f);
            }));
            Lit("Mattress", lit, MakeTexture("mattress", 16, (x, y) =>
            {
                var c = Shade(new Color(0.55f, 0.52f, 0.44f), (Hash(x, y, 81) - 0.5f) * 0.06f);
                if (x % 8 == 0) c = Shade(c, -0.08f);
                if (Hash(x / 3, y / 3, 82) > 0.85f) c = Color.Lerp(c, new Color(0.4f, 0.32f, 0.2f), 0.5f);
                return c;
            }));
            Lit("Helmet", lit, Plain("helmet", new Color(0.72f, 0.55f, 0.1f), 0.1f));
            Lit("Dynamite", lit, MakeTexture("dynamite", 16, (x, y) =>
                y % 16 > 12 ? new Color(0.75f, 0.7f, 0.6f) : Shade(new Color(0.62f, 0.12f, 0.08f), (Hash(x, y, 83) - 0.5f) * 0.08f)));
            Lit("SightWater", lit, Plain("sight_water", new Color(0.3f, 0.5f, 0.6f), 0.06f), new Color(0.12f, 0.28f, 0.38f));
            Lit("Fluorescent", lit, Plain("fluorescent", Color.white, 0f), new Color(0.75f, 0.95f, 0.85f) * 1.6f);
            Lit("LanternGlass", lit, Plain("lantern_glass", Color.white, 0f), new Color(1f, 0.7f, 0.35f) * 1.4f);
            Lit("SignPump", lit, Sign("sign_pump", 64, 16, new Color(0.17f, 0.18f, 0.19f), new Color(0.78f, 0.76f, 0.7f), 1, "PUMP ROOM"));
            Lit("SignSector2", lit, Sign("sign_sector2", 64, 16, new Color(0.17f, 0.18f, 0.19f), new Color(0.78f, 0.76f, 0.7f), 1, "SECTOR 4-2"));
            Lit("SignDrain", lit, Sign("sign_drain", 64, 32, new Color(0.8f, 0.62f, 0.1f), new Color(0.08f, 0.08f, 0.08f), 1, "KEEP DRAIN", "CLEAR"));
            Lit("SignWeight", lit, Sign("sign_weight", 96, 32, new Color(0.62f, 0.12f, 0.08f), new Color(0.9f, 0.85f, 0.75f), 1, "BOLT", "COUNTERWEIGHT"));
            Lit("Calendar", lit, Sign("calendar", 32, 32, new Color(0.78f, 0.74f, 0.64f), new Color(0.55f, 0.12f, 0.08f), 1, "MAY", "1987"));
            MakePropMaterials(lit);
            MakeLifeMaterials();
            MakeModelMaterials(lit);
            MakeArmsMaterial(lit);
            Lit("Gunmetal", lit, MakeTexture("gunmetal", 16, (x, y) =>
                Shade(new Color(0.16f, 0.17f, 0.2f), (Hash(x, 0, 160) - 0.5f) * 0.06f + (Hash(x, y, 161) - 0.5f) * 0.04f)), new Color(0.03f, 0.03f, 0.035f));
            Lit("GripWood", lit, MakeTexture("grip_wood", 16, (x, y) =>
                (x + y) % 4 == 0 ? new Color(0.22f, 0.08f, 0.06f) : Shade(new Color(0.42f, 0.13f, 0.09f), (Hash(x, y, 162) - 0.5f) * 0.08f)), new Color(0.04f, 0.01f, 0.01f));
            // The revolver in the hand: lighter steel and the skin's warm glow, so it reads in the dark as the fist does.
            Lit("HeldGunmetal", lit, MakeTexture("held_gunmetal", 16, (x, y) =>
                Shade(new Color(0.23f, 0.24f, 0.28f), (Hash(x, 0, 160) - 0.5f) * 0.08f + (Hash(x, y, 161) - 0.5f) * 0.05f)), new Color(0.09f, 0.075f, 0.06f));
            // Black rubber grips, stippled; the one in the hand glows faintly like the skin round it.
            Lit("RubberGrip", lit, MakeTexture("rubber_grip", 16, (x, y) =>
                Shade(new Color(0.09f, 0.085f, 0.085f), (Hash(x, y, 164) - 0.5f) * 0.06f + ((x + y * 3) % 5 == 0 ? -0.03f : 0f))), new Color(0.01f, 0.01f, 0.01f));
            Lit("HeldRubberGrip", lit, MakeTexture("held_rubber_grip", 16, (x, y) =>
                Shade(new Color(0.14f, 0.13f, 0.13f), (Hash(x, y, 164) - 0.5f) * 0.07f + ((x + y * 3) % 5 == 0 ? -0.04f : 0f))), new Color(0.07f, 0.055f, 0.045f));
            Lit("HeldBrass", lit, Plain("held_brass", new Color(0.72f, 0.55f, 0.1f), 0.1f), new Color(0.1f, 0.08f, 0.03f));
            Lit("Sand", lit, MakeTexture("sand", 16, (x, y) =>
                Shade(new Color(0.62f, 0.52f, 0.34f), (Hash(x, y, 163) - 0.5f) * 0.14f)), new Color(0.05f, 0.04f, 0.02f));
            Lit("Target", lit, MakeTexture("target", 32, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 18f));
                var paper = Shade(new Color(0.8f, 0.76f, 0.64f), (Hash(x, y, 164) - 0.5f) * 0.05f);
                if (y < 6) return Shade(paper, -0.02f);
                int ring = (int)(d / 2.6f);
                Color c = ring > 5 ? paper : ring % 2 == 0 ? new Color(0.1f, 0.1f, 0.1f) : paper;
                if (ring == 0) c = new Color(0.65f, 0.12f, 0.08f);
                foreach (var hole in new[] { new Vector2(12, 22), new Vector2(19, 13), new Vector2(22, 24), new Vector2(9, 15) })
                    if (Vector2.Distance(new Vector2(x, y), hole) < 1f) c = new Color(0.03f, 0.03f, 0.03f);
                return c;
            }));
            Lit("SignSand", lit, Sign("sign_sand", 64, 16, new Color(0.55f, 0.45f, 0.2f), new Color(0.08f, 0.08f, 0.08f), 1, "SAND"));
            Lit("SignArmory", lit, Sign("sign_armory", 64, 16, new Color(0.17f, 0.18f, 0.19f), new Color(0.78f, 0.76f, 0.7f), 1, "ARMORY"));
            Lit("SignAmmo", lit, Sign("sign_ammo", 64, 16, new Color(0.62f, 0.12f, 0.08f), new Color(0.9f, 0.85f, 0.75f), 1, "SIGN OUT"));
            Lit("SignHold", lit, Sign("sign_hold", 64, 32, new Color(0.8f, 0.62f, 0.1f), new Color(0.08f, 0.08f, 0.08f), 1, "HOLD DOWN", "CAGE LIFTS"));
            // The corridor: a stencil over every room's door, and the lamp beside it, red until the room is done.
            foreach (var (n, label) in HubSigns)
                Lit("SignHub" + n, lit, Sign("sign_hub" + n, 64, 16, new Color(0.17f, 0.18f, 0.19f), new Color(0.78f, 0.76f, 0.7f), 1, label));
            Lit("GreenLamp", lit, Plain("green_lamp", Color.white, 0f), new Color(0.15f, 1f, 0.3f) * 2.2f);
            // The chapel's coloured glass: small panes in reds, blues and golds between dark lead lines, glowing.
            Lit("StainedGlass", lit, MakeTexture("stained_glass", 16, 32, (x, y) =>
            {
                if (x % 4 == 0 || y % 5 == 0 || x == 15 || y == 31) return new Color(0.06f, 0.06f, 0.07f);
                var panes = new[] { new Color(0.7f, 0.12f, 0.1f), new Color(0.15f, 0.25f, 0.75f), new Color(0.85f, 0.65f, 0.15f), new Color(0.2f, 0.55f, 0.3f) };
                var c = panes[(int)(Hash(x / 4, y / 5, 172) * 4f) % 4];
                return Shade(c, (Hash(x, y, 173) - 0.5f) * 0.1f);
            }), new Color(1.1f, 1f, 1.1f));
            // Graveyard turf: dark grass going to bare earth.
            Lit("Grass", lit, MakeTexture("grass", 32, (x, y) =>
            {
                var c = Color.Lerp(new Color(0.16f, 0.2f, 0.1f), new Color(0.24f, 0.19f, 0.12f), Hash(x / 4, y / 4, 174));
                return Shade(c, (Hash(x, y, 175) - 0.5f) * 0.1f + (Hash(x, y / 3, 176) > 0.8f ? 0.06f : 0f));
            }));
            // The chapel: a red runner with a gold border, banners with a gold cross, and painted icons.
            Lit("Carpet", lit, MakeTexture("carpet", 16, 32, (x, y) =>
            {
                bool border = x < 2 || x > 13;
                bool diamond = Mathf.Abs(x - 7.5f) + Mathf.Abs((y % 16) - 7.5f) < 4f;
                var c = border ? new Color(0.6f, 0.45f, 0.15f) : diamond ? new Color(0.45f, 0.08f, 0.07f) : new Color(0.32f, 0.05f, 0.05f);
                return Shade(c, (Hash(x, y, 177) - 0.5f) * 0.06f);
            }));
            Lit("Banner", lit, MakeTexture("banner", 16, 32, (x, y) =>
            {
                bool cross = (x >= 7 && x <= 8 && y > 8 && y < 28) || (y >= 20 && y <= 21 && x > 3 && x < 12);
                bool hem = y < 3 || x == 0 || x == 15;
                var c = cross || hem ? new Color(0.75f, 0.58f, 0.2f) : new Color(0.38f, 0.06f, 0.06f);
                if (y < 3 && x % 3 == 0) c = new Color(0.1f, 0.03f, 0.03f);
                return Shade(c, (Hash(x, y, 178) - 0.5f) * 0.06f);
            }), new Color(0.04f, 0.02f, 0.01f));
            Lit("Icon", lit, MakeTexture("icon", 24, 32, (x, y) =>
            {
                if (x < 2 || y < 2 || x > 21 || y > 29) return Shade(new Color(0.7f, 0.55f, 0.2f), (Hash(x, y, 179) - 0.5f) * 0.1f);
                float halo = Vector2.Distance(new Vector2(x, y), new Vector2(11.5f, 21f));
                if (halo < 5f && halo > 3.2f) return new Color(0.85f, 0.7f, 0.25f);
                if (halo <= 3.2f) return new Color(0.55f, 0.4f, 0.3f);
                if (Mathf.Abs(x - 11.5f) < 5f - (y - 4) * 0.08f && y < 17) return Shade(new Color(0.18f, 0.2f, 0.38f), (Hash(x, y, 180) - 0.5f) * 0.05f);
                return Shade(new Color(0.5f, 0.38f, 0.15f), (Hash(x / 2, y / 2, 181) - 0.5f) * 0.08f);
            }), new Color(0.05f, 0.04f, 0.02f));
            // The figure: a coarse black robe, and a bone-white mask with hollow eyes and a stitched slit of a mouth.
            // The cultist (after the reference): a dusty maroon-brown robe, a darker seam with bone-pale clasps down the
            // front, faint swirls stitched on the chest, pale hands, and a hood with a lighter rim round a black hollow
            // shaped like a shield where the face should be.
            Lit("Robe", lit, MakeTexture("robe", 16, (x, y) =>
                Shade(new Color(0.3f, 0.16f, 0.14f), (Hash(x, y / 2, 182) - 0.5f) * 0.05f + (x % 5 == 0 ? -0.025f : 0f))),
                new Color(0.03f, 0.015f, 0.012f));
            Lit("Clasps", lit, MakeTexture("clasps", 8, 32, (x, y) =>
            {
                bool clasp = y % 4 == 1 && x > 0 && x < 7;
                bool seam = x == 3 || x == 4;
                if (clasp) return Shade(new Color(0.5f, 0.36f, 0.3f), (Hash(x, y, 197) - 0.5f) * 0.06f);
                return seam ? new Color(0.1f, 0.05f, 0.04f) : Shade(new Color(0.27f, 0.14f, 0.12f), (Hash(x, y, 198) - 0.5f) * 0.05f);
            }), new Color(0.03f, 0.015f, 0.012f));
            Lit("Embroidery", lit, MakeTexture("embroidery", 16, 16, (x, y) =>
            {
                Vector2 c1 = new Vector2(5f, 10f), c2 = new Vector2(10f, 5f);
                float r1 = (new Vector2(x, y) - c1).magnitude, r2 = (new Vector2(x, y) - c2).magnitude;
                bool line = Mathf.Abs(r1 - 3.5f) < 0.6f || Mathf.Abs(r2 - 2.5f) < 0.6f || (x == 8 && y > 2 && y < 13);
                return line ? new Color(0.19f, 0.09f, 0.08f) : Shade(new Color(0.3f, 0.16f, 0.14f), (Hash(x, y, 199) - 0.5f) * 0.05f);
            }), new Color(0.03f, 0.015f, 0.012f));
            Lit("Void", lit, Plain("void", new Color(0f, 0f, 0f), 0f));
            Lit("PaleSkin", lit, Plain("pale_skin", new Color(0.86f, 0.84f, 0.94f), 0.04f), new Color(0.12f, 0.12f, 0.14f));
            Lit("Mask", lit, MakeTexture("mask", 16, 20, (x, y) =>
            {
                // A shield: flat-topped, sides drawn in to a point at the chin.
                float u = Mathf.Abs(x - 7.5f) / 7.5f, v = y / 19f;
                float halfW = v > 0.45f ? 0.72f - (v - 0.45f) * 0.5f : 0.72f * (v / 0.45f);
                float rimW = halfW + 0.2f;
                if (u < halfW && v > 0.04f && v < 0.88f) return new Color(0f, 0f, 0f);
                if (u < rimW && v < 0.97f) return Shade(new Color(0.4f, 0.23f, 0.2f), (Hash(x, y, 196) - 0.5f) * 0.05f);
                return Shade(new Color(0.3f, 0.16f, 0.14f), (Hash(x, y, 195) - 0.5f) * 0.05f);
            }));
            Lit("LanternPane", lit, MakeTexture("lantern_pane", 8, 16, (x, y) =>
                x == 0 || y % 8 == 0 ? new Color(0.08f, 0.07f, 0.06f) : Shade(new Color(0.75f, 0.52f, 0.25f), (Hash(x, y, 185) - 0.5f) * 0.1f)),
                new Color(0.55f, 0.32f, 0.12f));
            // Blood: near-black red, wet highlights; and the blade, steel run with it.
            Lit("Blood", lit, MakeTexture("blood", 16, (x, y) =>
                Shade(new Color(0.3f, 0.02f, 0.02f), (Hash(x, y, 186) - 0.5f) * 0.1f + (Hash(x / 3, y / 3, 187) > 0.85f ? 0.12f : 0f))),
                new Color(0.08f, 0f, 0f));
            Lit("BloodyBlade", lit, MakeTexture("bloody_blade", 8, 32, (x, y) =>
            {
                bool edge = x == 7;
                bool blood = y < 20 && Hash(x, y / 2, 188) > 0.35f - y * 0.02f;
                var c = edge ? new Color(0.85f, 0.86f, 0.88f) : new Color(0.55f, 0.56f, 0.6f);
                return blood ? Shade(new Color(0.35f, 0.02f, 0.02f), (Hash(x, y, 189) - 0.5f) * 0.08f) : Shade(c, (Hash(x, y, 190) - 0.5f) * 0.06f);
            }), new Color(0.22f, 0.18f, 0.18f));
            Lit("Pine", lit, MakeTexture("pine", 16, (x, y) =>
                Shade(new Color(0.06f, 0.1f, 0.07f), (Hash(x, y, 184) - 0.5f) * 0.06f)));
            // Faded blue work overalls for the bodies left on the floor: they have to read against warm brick and tile.
            Lit("Coverall", lit, MakeTexture("coverall", 16, (x, y) =>
                Shade(new Color(0.34f, 0.42f, 0.54f), (Hash(x / 2, y / 2, 171) - 0.5f) * 0.08f + (x % 8 == 0 ? -0.05f : 0f))),
                new Color(0.02f, 0.025f, 0.035f));
            // The boiler room: valve and danger stencils, copper windings, and a pressure dial with no needle painted on.
            Lit("SignBoiler", lit, Sign("sign_boiler", 64, 16, new Color(0.17f, 0.18f, 0.19f), new Color(0.78f, 0.76f, 0.7f), 1, "BOILER ROOM"));
            Lit("SignWater", lit, Sign("sign_water", 48, 16, new Color(0.12f, 0.3f, 0.5f), new Color(0.9f, 0.92f, 0.9f), 1, "WATER"));
            Lit("SignRelief", lit, Sign("sign_relief", 48, 16, new Color(0.62f, 0.12f, 0.08f), new Color(0.9f, 0.85f, 0.75f), 1, "RELIEF"));
            Lit("SignLive", lit, Sign("sign_live", 64, 32, new Color(0.8f, 0.62f, 0.1f), new Color(0.08f, 0.08f, 0.08f), 1, "DANGER", "LIVE LINE"));
            Lit("Copper", lit, MakeTexture("copper", 16, (x, y) =>
                Shade(new Color(0.62f, 0.34f, 0.16f), (y % 2 == 0 ? 0.05f : -0.05f) + (Hash(x, y, 170) - 0.5f) * 0.06f)), new Color(0.06f, 0.03f, 0.01f));
            Lit("Dial", lit, MakeTexture("dial", 16, (x, y) =>
            {
                Vector2 d = new Vector2(x - 7.5f, y - 7.5f);
                if (d.magnitude > 7.2f) return new Color(0.1f, 0.1f, 0.1f);
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (d.magnitude > 5.5f && Mathf.Repeat(ang, 30f) < 8f && ang > -40f) return new Color(0.15f, 0.15f, 0.15f);
                if (d.magnitude > 5.5f && ang > -40f && ang < 10f) return new Color(0.7f, 0.12f, 0.08f);
                return new Color(0.85f, 0.82f, 0.74f);
            }), new Color(0.12f, 0.11f, 0.09f));

            var strip = GlowStrip();
            Additive("WaterStream", add, strip, new Color(0.45f, 0.65f, 0.8f) * 0.9f);
            // Two soft diagonal streaks of reflected light, for the glass over the timer.
            Additive("DisplaySheen", add, MakeTexture("display_sheen", 32, (x, y) =>
            {
                float d = Mathf.Repeat(x - y * 0.6f, 32f);
                float a = d > 4f && d < 8f ? 0.5f : d > 10f && d < 11.5f ? 0.3f : 0f;
                return new Color(1f, 1f, 1f, a);
            }, true), new Color(0.6f, 0.65f, 0.7f) * 0.18f);
            Additive("FuseCore", add, strip, new Color(1f, 0.6f, 0.2f) * 1.8f);
            Additive("FuseGlow", add, strip, new Color(1f, 0.35f, 0.08f) * 0.55f);
            Additive("Halo", add, Glow(), new Color(1f, 0.75f, 0.45f) * 0.5f);
            Additive("RedHalo", add, Glow(), new Color(1f, 0.15f, 0.08f) * 0.6f);
            Additive("GreenHalo", add, Glow(), new Color(0.15f, 1f, 0.3f) * 0.5f);
            Additive("Flame", add, Flame(), Color.white * 1.4f);
            Additive("FlameCore", add, Flame(), new Color(1f, 0.92f, 0.7f) * 1.3f);
            Additive("FlameHalo", add, Glow(), new Color(1f, 0.5f, 0.18f) * 0.3f);
            Additive("Mote", add, Glow(), new Color(1f, 0.9f, 0.75f) * 0.5f);
            Additive("MoonGlow", add, Glow(), new Color(0.85f, 0.88f, 1f) * 1.3f);
            Additive("Steam", add, Glow(), new Color(0.6f, 0.65f, 0.7f) * 0.18f);
            Additive("SparkFlash", add, Glow(), new Color(0.75f, 0.85f, 1f) * 2f);

            // The end: the mine rescue truck that comes up the road at dawn, and the crew in it.
            Lit("VanPaint", lit, MakeTexture("van_paint", 16, (x, y) =>
                Shade(new Color(0.78f, 0.77f, 0.72f), (Hash(x, y, 340) - 0.5f) * 0.05f - (y < 3 ? 0.12f * (3 - y) / 3f : 0f))), new Color(0.06f, 0.06f, 0.055f));
            Lit("VanRed", lit, Plain("van_red", new Color(0.62f, 0.1f, 0.07f), 0.05f), new Color(0.05f, 0.01f, 0.01f));
            Lit("VanGlass", lit, Plain("van_glass", new Color(0.12f, 0.15f, 0.17f), 0.04f), new Color(0.02f, 0.025f, 0.03f));
            Lit("Headlamp", lit, Plain("headlamp", Color.white, 0f), new Color(1f, 0.92f, 0.75f) * 2.4f);
            Lit("TailLamp", lit, Plain("tail_lamp", Color.white, 0f), new Color(1f, 0.08f, 0.04f) * 1.8f);
            Lit("Beacon", lit, Plain("beacon", Color.white, 0f), new Color(1f, 0.55f, 0.08f) * 2.4f);
            Additive("BeaconHalo", add, Glow(), new Color(1f, 0.55f, 0.1f) * 0.55f);
            Additive("HeadlampHalo", add, Glow(), new Color(1f, 0.92f, 0.75f) * 0.5f);
            Lit("SignRescue", lit, Sign("sign_rescue", 64, 16, new Color(0.62f, 0.1f, 0.07f), new Color(0.95f, 0.93f, 0.88f), 1, "MINE RESCUE"));
            // Orange rescue coveralls with a pale reflective band, a white hard hat.
            Lit("RescueSuit", lit, MakeTexture("rescue_suit", 16, (x, y) =>
                Shade(new Color(0.85f, 0.38f, 0.08f), (Hash(x / 2, y / 2, 341) - 0.5f) * 0.08f)), new Color(0.07f, 0.03f, 0.01f));
            Lit("ReflectBand", lit, Plain("reflect_band", new Color(0.8f, 0.82f, 0.8f), 0.04f), new Color(0.2f, 0.2f, 0.2f));
            Lit("HardHat", lit, Plain("hard_hat", new Color(0.88f, 0.87f, 0.82f), 0.05f), new Color(0.05f, 0.05f, 0.05f));
            Lit("Asphalt", lit, MakeTexture("asphalt", 32, (x, y) =>
                Shade(new Color(0.2f, 0.19f, 0.18f), (Hash(x, y, 342) - 0.5f) * 0.1f + (Hash(x / 4, y / 4, 343) - 0.5f) * 0.06f)));
        }

        static Material SaveMaterial(string name, Material m)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.shader = m.shader; existing.CopyPropertiesFromMaterial(m); UnityEngine.Object.DestroyImmediate(m); m = existing; }
            else AssetDatabase.CreateAsset(m, path);
            Mats[name] = m;
            return m;
        }

        static void Lit(string name, Shader shader, Texture2D tex, Color? emission = null)
        {
            var m = new Material(shader);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_EmissionColor", emission ?? Color.black);
            m.SetFloat("_Affine", SteadyTexture(name) ? 0f : 0.25f);
            m.SetFloat("_DepthOffset", Decal(name) ? -2f : 0f);
            m.SetFloat("_SnapResolution", 180f);
            SaveMaterial(name, m);
        }

        // Thin things laid flat on another surface; drawn a touch in front of it (see _DepthOffset in PSX/Lit).
        static bool Decal(string name) =>
            name.StartsWith("Sign") || name.StartsWith("Note") || name == "Water" || name == "Grate" || name == "Hazard" ||
            name == "Paper" || name == "Ash" || name == "Powder" || name == "Notice" || name == "Poster" || name == "Photo" ||
            name == "Calendar";

        // The PS1's affine texture wobble is part of the look, but on big flat panels and anything with writing on it
        // (the door, signs, notes, displays) it makes the picture swim as you walk past; those keep their textures still.
        static bool SteadyTexture(string name) =>
            name.StartsWith("Sign") || name.StartsWith("Note") || name == "VaultMetal" || name == "Display" || name == "Paper" ||
            name == "Calendar" || name == "Notice" || name == "Poster" || name == "Photo" || name == "ClockFace" ||
            name == "FirstAid" || name == "Hazard" || name == "Gauge" || name == "ExitGlow" || name == "TankPaint" ||
            name == "Arms" || name == "Mask" || name == "StainedGlass" || name == "Banner" || name == "Icon" || name == "Carpet" || name == "Floor" || name == "Stone" || name == "Ceiling" || name == "Concrete";

        // A handwritten page: aged paper darkening to the edges, faint rules, lines of ink scrawl with gaps for words,
        // a fold across the middle, a coffee ring and a dog-eared corner. torn: a ragged top edge, a scrap rather than
        // a diary page.
        static Texture2D NoteTexture(string name, int seed, bool torn) => MakeTexture(name, 48, 64, (x, y) =>
        {
            float u = x / 47f, v = y / 63f;
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            var c = new Color(0.8f, 0.74f, 0.6f);
            c = Shade(c, (Hash(x / 3, y / 3, seed) - 0.5f) * 0.05f + (Hash(x, y, seed + 1) - 0.5f) * 0.04f - Mathf.Clamp01(0.12f - edge) * 1.2f);
            if (torn && y > 58 - (int)(Hash(x / 2, 0, seed + 2) * 4f)) return new Color(0.05f, 0.045f, 0.04f);
            if (!torn && x == 5) c = Color.Lerp(c, new Color(0.7f, 0.3f, 0.3f), 0.4f);            // margin rule
            if (y % 5 == 0 && y < 56) c = Shade(c, -0.04f);                                        // faint ruling
            // Ink: one scrawled line just above each rule, broken into words, the first line heavier (a date).
            int row = y / 5, ly = y % 5;
            if (y < 55 && y > 6 && (ly == 1 || ly == 2))
            {
                float word = Hash(x / 5, row, seed + 3);
                float lineEnd = 40f - Hash(0, row, seed + 4) * 22f;
                bool ink = x > 7 && x < lineEnd && word > 0.22f && Hash(x, y + row * 7, seed + 5) > (ly == 1 ? 0.25f : 0.55f);
                if (row == 10) ink = x > 7 && x < 24 && Hash(x, y, seed + 6) > 0.15f;
                if (ink) c = Color.Lerp(c, new Color(0.1f, 0.12f, 0.24f), 0.85f);
            }
            // Fold crease across the middle: a shadow line and a highlight under it.
            if (y == 31) c = Shade(c, -0.09f);
            if (y == 30) c = Shade(c, 0.05f);
            // Coffee ring.
            float ring = Vector2.Distance(new Vector2(x, y), new Vector2(34f, 14f));
            if (Mathf.Abs(ring - 6.5f) < 0.8f) c = Color.Lerp(c, new Color(0.5f, 0.36f, 0.2f), 0.5f);
            else if (ring < 6f) c = Color.Lerp(c, new Color(0.6f, 0.48f, 0.32f), 0.15f);
            // Dog-eared corner, bottom right.
            if ((47 - x) + y < 7) c = (47 - x) + y < 6 ? Shade(new Color(0.7f, 0.64f, 0.52f), -0.05f) : new Color(0.2f, 0.17f, 0.13f);
            return c;
        });

        static void Additive(string name, Shader shader, Texture2D tex, Color color)
        {
            var m = new Material(shader);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_SnapResolution", 180f);
            SaveMaterial(name, m);
        }

        // ---------------------------------------------------------------- mesh building

        // Flat-shaded mesh builder. UVs are in metres times uvScale, so textures keep one texel size everywhere.
        class MeshBuilder
        {
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<Vector3> _n = new List<Vector3>();
            readonly List<Vector2> _uv = new List<Vector2>();
            readonly List<Color> _c = new List<Color>();
            readonly List<int> _t = new List<int>();
            public Color Tint = Color.white;
            // Bakes a fixed top-light into vertex colours (lighter tops, darker undersides), the classic PS1 trick
            // that keeps props readable even where the realtime lights barely reach.
            public bool FaceShade = true;
            // Placement applied to everything added afterwards, so one mesh can hold many posed parts.
            public Matrix4x4 M = Matrix4x4.identity;

            // Quad o, o+u, o+u+w, o+w. Faces cross(w,u); flip turns it inside out.
            public void Quad(Vector3 o, Vector3 u, Vector3 w, float uvScale, float cell = 0f, bool flip = false)
            {
                int su = cell > 0f ? Mathf.Max(1, Mathf.CeilToInt(u.magnitude / cell)) : 1;
                int sw = cell > 0f ? Mathf.Max(1, Mathf.CeilToInt(w.magnitude / cell)) : 1;
                Vector3 n = Vector3.Cross(w, u).normalized * (flip ? -1f : 1f);
                float ul = u.magnitude, wl = w.magnitude;
                for (int i = 0; i < su; i++)
                    for (int j = 0; j < sw; j++)
                    {
                        float a0 = (float)i / su, a1 = (float)(i + 1) / su, b0 = (float)j / sw, b1 = (float)(j + 1) / sw;
                        int start = _v.Count;
                        AddV(o + u * a0 + w * b0, n, new Vector2(a0 * ul, b0 * wl) * uvScale);
                        AddV(o + u * a0 + w * b1, n, new Vector2(a0 * ul, b1 * wl) * uvScale);
                        AddV(o + u * a1 + w * b1, n, new Vector2(a1 * ul, b1 * wl) * uvScale);
                        AddV(o + u * a1 + w * b0, n, new Vector2(a1 * ul, b0 * wl) * uvScale);
                        if (!flip) _t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                        else _t.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                    }
            }

            void AddV(Vector3 p, Vector3 n, Vector2 uv)
            {
                p = M.MultiplyPoint3x4(p);
                n = M.MultiplyVector(n).normalized;
                _v.Add(p); _n.Add(n); _uv.Add(uv);
                float s = FaceShade ? Mathf.Clamp(0.74f + 0.26f * n.y + 0.1f * Mathf.Abs(n.x), 0.45f, 1f) : 1f;
                _c.Add(new Color(Tint.r * s, Tint.g * s, Tint.b * s, Tint.a));
            }

            public void Box(Vector3 center, Vector3 size, float uvScale, float cell = 0f)
            {
                Vector3 mn = center - size / 2f, mx = center + size / 2f;
                Vector3 X = new Vector3(size.x, 0, 0), Y = new Vector3(0, size.y, 0), Z = new Vector3(0, 0, size.z);
                Quad(new Vector3(mx.x, mn.y, mn.z), Z, Y, uvScale, cell);
                Quad(new Vector3(mn.x, mn.y, mx.z), -Z, Y, uvScale, cell);
                Quad(new Vector3(mx.x, mn.y, mx.z), -X, Y, uvScale, cell);
                Quad(new Vector3(mn.x, mn.y, mn.z), X, Y, uvScale, cell);
                Quad(new Vector3(mn.x, mx.y, mn.z), X, Z, uvScale, cell);
                Quad(new Vector3(mn.x, mn.y, mx.z), X, -Z, uvScale, cell);
            }

            // Cylinder along local Y, base at center.y. Flat-shaded sides.
            public void Cylinder(Vector3 baseCenter, float radius, float height, int sides, float uvScale, bool caps = true)
            {
                float circumference = 2f * Mathf.PI * radius;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = (float)i / sides * Mathf.PI * 2f, a1 = (float)(i + 1) / sides * Mathf.PI * 2f;
                    var p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                    var p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                    int start = _v.Count;
                    Vector3 n = Vector3.Cross(Vector3.up, p1 - p0).normalized;
                    float u0 = (float)i / sides * circumference * uvScale, u1 = (float)(i + 1) / sides * circumference * uvScale;
                    AddV(p0, n, new Vector2(u0, 0));
                    AddV(p0 + Vector3.up * height, n, new Vector2(u0, height * uvScale));
                    AddV(p1 + Vector3.up * height, n, new Vector2(u1, height * uvScale));
                    AddV(p1, n, new Vector2(u1, 0));
                    _t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
                if (!caps) return;
                Cap(baseCenter + Vector3.up * height, radius, sides, Vector3.up, uvScale);
                Cap(baseCenter, radius, sides, Vector3.down, uvScale);
            }

            void Cap(Vector3 c, float r, int sides, Vector3 n, float uvScale)
            {
                int center = _v.Count;
                AddV(c, n, Vector2.one * 0.5f);
                for (int i = 0; i < sides; i++)
                {
                    float a = (float)i / sides * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    AddV(c + d * r, n, new Vector2(d.x, d.z) * r * uvScale + Vector2.one * 0.5f);
                }
                for (int i = 0; i < sides; i++)
                {
                    int a = center + 1 + i, b = center + 1 + (i + 1) % sides;
                    if (n.y > 0) _t.AddRange(new[] { center, b, a });
                    else _t.AddRange(new[] { center, a, b });
                }
            }

            // Flat ribbon lying on the floor along a polyline.
            public void Ribbon(IList<Vector3> points, float width, float uvScale)
            {
                float along = 0f;
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector3 a = points[i], b = points[i + 1];
                    Vector3 dir = (b - a).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * width * 0.5f;
                    float len = Vector3.Distance(a, b);
                    int start = _v.Count;
                    AddV(a - side, Vector3.up, new Vector2(0, along * uvScale));
                    AddV(b - side, Vector3.up, new Vector2(0, (along + len) * uvScale));
                    AddV(b + side, Vector3.up, new Vector2(1, (along + len) * uvScale));
                    AddV(a + side, Vector3.up, new Vector2(1, along * uvScale));
                    _t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    along += len;
                }
            }

            // Two crossed vertical quads, so a flame or glow reads from any side without billboarding.
            public void Cross(Vector3 bottom, float width, float height)
            {
                for (int k = 0; k < 2; k++)
                {
                    Vector3 side = (k == 0 ? Vector3.right : Vector3.forward) * width * 0.5f;
                    int start = _v.Count;
                    AddV(bottom - side, Vector3.back, new Vector2(0, 0));
                    AddV(bottom - side + Vector3.up * height, Vector3.back, new Vector2(0, 1));
                    AddV(bottom + side + Vector3.up * height, Vector3.back, new Vector2(1, 1));
                    AddV(bottom + side, Vector3.back, new Vector2(1, 0));
                    _t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }

            // Tapered box from a to b: start cross-section wA x hA, end wB x hB, "up" orients the height axis.
            // Fingers, wrists and sleeves are chains of these.
            public void Limb(Vector3 a, Vector3 b, float wA, float hA, float wB, float hB, Vector3 up, float uvScale = 4f)
            {
                var saved = M;
                float len = Vector3.Distance(a, b);
                M = saved * Matrix4x4.TRS(a, Quaternion.LookRotation(b - a, up), Vector3.one);
                Vector3[] s = { new Vector3(-wA, -hA, 0) / 2, new Vector3(wA, -hA, 0) / 2, new Vector3(wA, hA, 0) / 2, new Vector3(-wA, hA, 0) / 2 };
                Vector3[] e = { new Vector3(-wB, -hB, 0) / 2 + Vector3.forward * len, new Vector3(wB, -hB, 0) / 2 + Vector3.forward * len, new Vector3(wB, hB, 0) / 2 + Vector3.forward * len, new Vector3(-wB, hB, 0) / 2 + Vector3.forward * len };
                Vector3 mid = Vector3.forward * len * 0.5f;
                for (int i = 0; i < 4; i++) Face(s[i], s[(i + 1) % 4], e[(i + 1) % 4], e[i], mid, uvScale);
                Face(s[0], s[1], s[2], s[3], mid, uvScale);
                Face(e[0], e[1], e[2], e[3], mid, uvScale);
                M = saved;
            }

            // Box with its four edges along Z cut off, so seen from the front its corners are rounded (Zippo body).
            public void ChamferBox(Vector3 center, Vector3 size, float chamfer, float uvScale = 4f)
            {
                float hx = size.x / 2, hy = size.y / 2, hz = size.z / 2, c = Mathf.Min(chamfer, Mathf.Min(hx, hy));
                Vector2[] sec =
                {
                    new Vector2(-hx + c, -hy), new Vector2(hx - c, -hy), new Vector2(hx, -hy + c), new Vector2(hx, hy - c),
                    new Vector2(hx - c, hy), new Vector2(-hx + c, hy), new Vector2(-hx, hy - c), new Vector2(-hx, -hy + c)
                };
                var f = new Vector3[8];
                var b = new Vector3[8];
                for (int i = 0; i < 8; i++)
                {
                    f[i] = center + new Vector3(sec[i].x, sec[i].y, -hz);
                    b[i] = center + new Vector3(sec[i].x, sec[i].y, hz);
                }
                for (int i = 0; i < 8; i++) Face(f[i], f[(i + 1) % 8], b[(i + 1) % 8], b[i], center, uvScale);
                for (int i = 1; i < 7; i++)
                {
                    Tri(f[0], f[i], f[i + 1], center);
                    Tri(b[0], b[i], b[i + 1], center);
                }
            }

            // Round-ish tapered tube from a to b (n-gon section, capped). Fingers read far better round than square.
            public void Tube(Vector3 a, Vector3 b, float rA, float rB, int sides, Vector3 up, float uvScale = 4f)
            {
                var saved = M;
                float len = Vector3.Distance(a, b);
                M = saved * Matrix4x4.TRS(a, Quaternion.LookRotation(b - a, up), Vector3.one);
                var s = new Vector3[sides];
                var e = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float ang = (i + 0.5f) / sides * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0);
                    s[i] = d * rA;
                    e[i] = d * rB + Vector3.forward * len;
                }
                Vector3 mid = Vector3.forward * len * 0.5f;
                for (int i = 0; i < sides; i++) Face(s[i], s[(i + 1) % sides], e[(i + 1) % sides], e[i], mid, uvScale);
                for (int i = 1; i < sides - 1; i++)
                {
                    Tri(s[0], s[i], s[i + 1], mid);
                    Tri(e[0], e[i], e[i + 1], mid);
                }
                M = saved;
            }

            void Tri(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 inside)
            {
                Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                if (Vector3.Dot(n, (v0 + v1 + v2) / 3f - inside) < 0f) { (v1, v2) = (v2, v1); n = -n; }
                int start = _v.Count;
                AddV(v0, n, Vector2.zero);
                AddV(v1, n, Vector2.right);
                AddV(v2, n, Vector2.up);
                _t.AddRange(new[] { start, start + 1, start + 2 });
            }

            // Planar quad; winding is fixed up so the face points away from "inside".
            void Face(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 inside, float uvScale)
            {
                Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                if (Vector3.Dot(n, (v0 + v2) * 0.5f - inside) < 0f) { (v1, v3) = (v3, v1); n = -n; }
                int start = _v.Count;
                float w = Vector3.Distance(v0, v1) * uvScale, h = Vector3.Distance(v0, v3) * uvScale;
                AddV(v0, n, new Vector2(0, 0));
                AddV(v1, n, new Vector2(w, 0));
                AddV(v2, n, new Vector2(w, h));
                AddV(v3, n, new Vector2(0, h));
                _t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            // Single quad with the whole texture on it (signs, notes, papers). Faces cross(w, u).
            public void Card(Vector3 o, Vector3 u, Vector3 w)
            {
                Vector3 n = Vector3.Cross(w, u).normalized;
                int start = _v.Count;
                AddV(o, n, new Vector2(0, 0));
                AddV(o + w, n, new Vector2(0, 1));
                AddV(o + u + w, n, new Vector2(1, 1));
                AddV(o + u, n, new Vector2(1, 0));
                _t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            // Horizontal glow card lying on the floor.
            public void FloorCard(Vector3 center, float size)
            {
                Quad(center + new Vector3(-size / 2, 0, -size / 2), new Vector3(size, 0, 0), new Vector3(0, 0, size), 1f / size);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(_v);
                m.SetNormals(_n);
                m.SetUVs(0, _uv);
                m.SetColors(_c);
                m.SetTriangles(_t, 0);
                m.RecalculateBounds();
                // Many parts share a name ("Body", "Leg"), so every mesh asset gets a running number.
                AssetDatabase.CreateAsset(m, $"{_meshFolder}/{_meshId++:000}_{name}.asset");
                return m;
            }
        }

        static GameObject Place(string name, MeshBuilder mb, string material, Vector3 pos, Vector3 euler = default, Transform parent = null, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _world, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            var mesh = mb.ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats[material];
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static GameObject Block(string name, string material, Vector3 pos, Vector3 size, Vector3 euler = default, float uvScale = 1f, Transform parent = null, bool collider = false)
        {
            var mb = new MeshBuilder();
            mb.Box(Vector3.zero, size, uvScale);
            var go = Place(name, mb, material, pos, euler, parent);
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        static GameObject Cyl(string name, string material, Vector3 basePos, float radius, float height, int sides, Vector3 euler = default, float uvScale = 1f, Transform parent = null, bool collider = false)
        {
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, radius, height, sides, uvScale);
            var go = Place(name, mb, material, basePos, euler, parent);
            if (collider)
            {
                var cc = go.AddComponent<CapsuleCollider>();
                cc.radius = radius;
                cc.height = height;
                cc.center = new Vector3(0, height / 2f, 0);
            }
            return go;
        }

        // ---------------------------------------------------------------- pipes and colliders

        static Vector3 UpFor(Vector3 dir) => Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up;

        // A run of pipe through the given points (local to parent): straight lengths, a rounded elbow at every corner,
        // a bolted flange either side of each elbow, collars where the ends go into a wall, and capsule colliders along
        // the straights so the player bumps into it instead of walking through.
        static Transform PipeRun(string name, string material, IList<Vector3> pts, float radius, Transform parent,
                                 bool wallEnds = true, bool collide = true, string flangeMaterial = "DarkMetal")
        {
            var run = new GameObject("PipeRun_" + name).transform;
            run.SetParent(parent, false);
            var pipe = new MeshBuilder();
            var fittings = new MeshBuilder();
            const int sides = 8;
            float bend = radius * 2.2f;

            // Where each straight starts and ends once the elbows have taken their share of the corner.
            int n = pts.Count;
            var a = new Vector3[n - 1];
            var b = new Vector3[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 dir = (pts[i + 1] - pts[i]).normalized;
                float len = Vector3.Distance(pts[i], pts[i + 1]);
                a[i] = i > 0 ? pts[i] + dir * Mathf.Min(bend, len * 0.4f) : pts[i];
                b[i] = i < n - 2 ? pts[i + 1] - dir * Mathf.Min(bend, len * 0.4f) : pts[i + 1];
                pipe.Tube(a[i], b[i], radius, radius, sides, UpFor(dir), 2f);
                if (collide) CapsuleBetween(run, pts[i], pts[i + 1], radius + 0.01f);
            }

            void Flange(Vector3 at, Vector3 dir, float width, float scale)
            {
                fittings.Tube(at - dir * width * 0.5f, at + dir * width * 0.5f, radius * scale, radius * scale, sides, UpFor(dir), 4f);
                // Four bolt heads round the rim.
                var rot = Quaternion.LookRotation(dir, UpFor(dir));
                for (int k = 0; k < 4; k++)
                {
                    float ang = (k + 0.5f) * Mathf.PI * 0.5f;
                    var off = rot * new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius * (scale - 0.12f);
                    var saved = fittings.M;
                    fittings.M = Matrix4x4.TRS(at + off, rot, Vector3.one);
                    fittings.Box(Vector3.zero, new Vector3(0.016f, 0.016f, width + 0.014f), 8f);
                    fittings.M = saved;
                }
            }

            for (int i = 1; i < n - 1; i++)
            {
                // Elbow: a quadratic curve from the end of one straight to the start of the next.
                Vector3 p0 = b[i - 1], p1 = pts[i], p2 = a[i];
                Vector3 prev = p0;
                for (int s = 1; s <= 5; s++)
                {
                    float t = s / 5f;
                    Vector3 q = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                    pipe.Tube(prev, q, radius * 1.04f, radius * 1.04f, sides, UpFor(q - prev), 2f);
                    prev = q;
                }
                Flange(p0, (pts[i] - pts[i - 1]).normalized, 0.03f, 1.38f);
                Flange(p2, (pts[i + 1] - pts[i]).normalized, 0.03f, 1.38f);
            }
            if (wallEnds)
            {
                Flange(pts[0], (pts[1] - pts[0]).normalized, 0.05f, 1.7f);
                Flange(pts[n - 1], (pts[n - 1] - pts[n - 2]).normalized, 0.05f, 1.7f);
            }
            Place("Pipe", pipe, material, Vector3.zero, default, run);
            Place("Fittings", fittings, flangeMaterial, Vector3.zero, default, run);
            return run;
        }

        static void CapsuleBetween(Transform parent, Vector3 a, Vector3 b, float radius)
        {
            var go = new GameObject("Collider");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = radius;
            cap.height = Vector3.Distance(a, b) + radius * 2f;
        }

        // A clamp holding a pipe to a wall: a strap round the pipe and a plate bolted to the wall behind it.
        static void PipeClamp(Vector3 at, Vector3 along, Vector3 toWall, float radius, float wallGap, Transform parent)
        {
            var mb = new MeshBuilder();
            mb.Tube(at - along * 0.025f, at + along * 0.025f, radius * 1.25f, radius * 1.25f, 8, UpFor(along), 4f);
            mb.M = Matrix4x4.TRS(at + toWall * (wallGap * 0.5f), Quaternion.LookRotation(toWall, UpFor(toWall)), Vector3.one);
            mb.Box(Vector3.zero, new Vector3(0.05f, 0.05f, wallGap), 4f);
            mb.Box(new Vector3(0, 0, wallGap * 0.5f - 0.01f), new Vector3(0.14f, 0.1f, 0.02f), 4f);
            mb.M = Matrix4x4.identity;
            Place("Clamp", mb, "DarkMetal", Vector3.zero, default, parent);
        }

        // Every solid-looking thing the builder made without a collider gets a box round its mesh, so the player can't
        // walk through props. Flat cards (paper, signs, puddles), glows, the fuse itself and tiny bits are left alone.
        static void AutoColliders(Transform root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                if (go.GetComponent<Collider>() != null || mf.sharedMesh == null) continue;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader.name == "PSX/Additive") continue;
                if (UnderAny(go.transform, root, "PipeRun_", "Fuse", "Shell")) continue;
                if (go.GetComponentInParent<Rigidbody>() != null) continue;
                Bounds bounds = mf.sharedMesh.bounds;
                Vector3 size = Vector3.Scale(bounds.size, Abs(go.transform.lossyScale));
                float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float min = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                if (max < 0.1f || min < 0.012f) continue;
                var box = go.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static bool UnderAny(Transform t, Transform root, params string[] names)
        {
            for (; t != null && t != root; t = t.parent)
                foreach (var n in names)
                    if (t.name.StartsWith(n)) return true;
            return false;
        }

        // ---------------------------------------------------------------- the room

        // Floor, ceiling, walls with the doorway, the short exit corridor, skirting, ceiling beams and concrete pillars
        // under each beam. beamZ: where the beams cross the room.
        static void BuildShell(float[] beamZ)
        {
            var shell = new GameObject("Shell").transform;
            shell.SetParent(_world, false);
            Vector3 mn = RoomMin, mx = RoomMax, size = mx - mn;
            const float cell = 0.5f;           // per-vertex lighting needs a dense grid
            const float stone = 0.5f;          // one 64px stone texture spans 2 m
            const float floorUv = 0.5f;

            var floor = new MeshBuilder { FaceShade = false };
            floor.Quad(new Vector3(mn.x, 0, mn.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), floorUv, cell);
            Place("Floor", floor, "Floor", Vector3.zero, default, shell, true);

            var ceiling = new MeshBuilder { FaceShade = false };
            ceiling.Quad(new Vector3(mn.x, mx.y, mn.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), 1f, cell, true);
            Place("Ceiling", ceiling, "Ceiling", Vector3.zero, default, shell, true);

            var walls = new MeshBuilder { FaceShade = false };
            walls.Quad(new Vector3(mx.x, 0, mn.z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0), stone, cell, true); // right
            walls.Quad(new Vector3(mn.x, 0, mx.z), new Vector3(0, 0, -size.z), new Vector3(0, size.y, 0), stone, cell, true); // left
            // Door wall in three pieces round the 1.8 x 2.4 m doorway.
            walls.Quad(new Vector3(mx.x, 0, mx.z), new Vector3(-(mx.x - DoorHalfWidth), 0, 0), new Vector3(0, size.y, 0), stone, cell, true);
            walls.Quad(new Vector3(-DoorHalfWidth, 0, mx.z), new Vector3(-(mx.x - DoorHalfWidth), 0, 0), new Vector3(0, size.y, 0), stone, cell, true);
            walls.Quad(new Vector3(DoorHalfWidth, DoorHeight, mx.z), new Vector3(-2f * DoorHalfWidth, 0, 0), new Vector3(0, size.y - DoorHeight, 0), stone, cell, true);
            walls.Quad(new Vector3(mn.x, 0, mn.z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0), stone, cell, true);  // back wall
            Place("Walls", walls, "Stone", Vector3.zero, default, shell, true);

            // Short corridor behind the door, ending in a cold bright light: the way out.
            const float corridorLen = 3.5f;
            var corridor = new MeshBuilder { FaceShade = false };
            float cz = mx.z, cw = DoorHalfWidth * 2f;
            corridor.Quad(new Vector3(-DoorHalfWidth, 0, cz), new Vector3(cw, 0, 0), new Vector3(0, 0, corridorLen), floorUv, cell);
            corridor.Quad(new Vector3(-DoorHalfWidth, DoorHeight, cz), new Vector3(cw, 0, 0), new Vector3(0, 0, corridorLen), 1f, cell, true);
            corridor.Quad(new Vector3(DoorHalfWidth, 0, cz), new Vector3(0, 0, corridorLen), new Vector3(0, DoorHeight, 0), stone, cell, true);
            corridor.Quad(new Vector3(-DoorHalfWidth, 0, cz + corridorLen), new Vector3(0, 0, -corridorLen), new Vector3(0, DoorHeight, 0), stone, cell, true);
            Place("Corridor", corridor, "Stone", Vector3.zero, default, shell, true);
            var exitWall = new MeshBuilder { FaceShade = false };
            exitWall.Quad(new Vector3(DoorHalfWidth, 0, cz + corridorLen), new Vector3(-cw, 0, 0), new Vector3(0, DoorHeight, 0), 1f, cell, true);
            Place("ExitGlow", exitWall, "ExitGlow", Vector3.zero, default, shell, true);
            var exitLight = new GameObject("ExitLight").AddComponent<Light>();
            exitLight.transform.SetParent(shell, false);
            exitLight.transform.localPosition = new Vector3(0, 1.6f, cz + corridorLen - 0.6f);
            exitLight.type = LightType.Point;
            exitLight.color = new Color(0.8f, 0.9f, 1f);
            exitLight.range = 4.5f;
            exitLight.intensity = 2.5f;
            exitLight.shadows = LightShadows.None;

            // Skirting, ceiling beams and the pillars that carry them break up the big flat surfaces.
            float midZ = (mn.z + mx.z) * 0.5f;
            Block("Skirting_L", "DarkMetal", new Vector3(mn.x + 0.04f, 0.08f, midZ), new Vector3(0.08f, 0.16f, size.z), default, 1f, shell);
            Block("Skirting_R", "DarkMetal", new Vector3(mx.x - 0.04f, 0.08f, midZ), new Vector3(0.08f, 0.16f, size.z), default, 1f, shell);
            for (int i = 0; i < beamZ.Length; i++)
            {
                float z = beamZ[i];
                Block("Beam_" + i, "Rust", new Vector3(0, mx.y - 0.12f, z), new Vector3(size.x, 0.24f, 0.22f), default, 1f, shell);
                // Each end rests on a concrete pillar against the wall, riveted through a plate.
                foreach (float side in new[] { -1f, 1f })
                {
                    float wx = side < 0 ? mn.x : mx.x;
                    Block("Pillar", "Concrete", new Vector3(wx - side * 0.07f, mx.y * 0.5f, z), new Vector3(0.14f, mx.y, 0.3f), default, 1f, shell, true);
                    Block("PillarFoot", "Concrete", new Vector3(wx - side * 0.1f, 0.12f, z), new Vector3(0.2f, 0.24f, 0.38f), default, 1f, shell, true);
                    Block("BeamPlate", "DarkMetal", new Vector3(wx - side * 0.145f, mx.y - 0.2f, z), new Vector3(0.02f, 0.32f, 0.32f), default, 1f, shell);
                    for (int k = 0; k < 4; k++)
                        Block("Rivet", "Steel", new Vector3(wx - side * 0.158f, mx.y - 0.1f - (k / 2) * 0.2f, z - 0.1f + (k % 2) * 0.2f),
                              new Vector3(0.012f, 0.025f, 0.025f), default, 4f, shell);
                }
            }
        }

        static void BuildDoor()
        {
            var door = new GameObject("VaultDoor").transform;
            door.SetParent(_world, false);
            door.localPosition = new Vector3(0, 0, RoomMax.z);

            Block("Frame_Top", "DarkMetal", new Vector3(0, 2.55f, -0.12f), new Vector3(2.4f, 0.3f, 0.24f), default, 1f, door, true);
            Block("Frame_L", "DarkMetal", new Vector3(-1.05f, 1.2f, -0.12f), new Vector3(0.3f, 2.4f, 0.24f), default, 1f, door, true);
            Block("Frame_R", "DarkMetal", new Vector3(1.05f, 1.2f, -0.12f), new Vector3(0.3f, 2.4f, 0.24f), default, 1f, door, true);
            // The leaf swings on a hinge at the left edge of the doorway; panel and wheel ride on it.
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(door, false);
            leaf.localPosition = new Vector3(-DoorHalfWidth, 0, -0.08f);
            Block("Panel", "VaultMetal", new Vector3(DoorHalfWidth, 1.2f, 0), new Vector3(1.8f, 2.4f, 0.12f), default, 0.5f, leaf, true);

            // Locking wheel: hub, rim made of 8 segments, 4 spokes.
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(leaf, false);
            wheel.localPosition = new Vector3(DoorHalfWidth, 1.25f, -0.12f);
            Cyl("Hub", "Steel", Vector3.zero, 0.11f, 0.1f, 8, new Vector3(-90, 0, 0), 1f, wheel);
            const int segments = 8;
            const float rimR = 0.36f;
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) / segments * 360f;
                float rad = a * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * rimR;
                float len = 2f * rimR * Mathf.Sin(Mathf.PI / segments) + 0.02f;
                Block("Rim_" + i, "Steel", p + new Vector3(0, 0, -0.05f), new Vector3(0.07f, len, 0.07f), new Vector3(0, 0, a), 1f, wheel);
            }
            for (int i = 0; i < 4; i++)
                Block("Spoke_" + i, "Steel", new Vector3(0, 0, -0.05f), new Vector3(0.05f, rimR * 2f, 0.045f), new Vector3(0, 0, i * 45f), 1f, wheel);

            // Bolts down the frame and heavy hinges.
            for (int i = 0; i < 6; i++)
            {
                Block("BoltL_" + i, "Steel", new Vector3(-1.05f, 0.2f + i * 0.42f, -0.26f), new Vector3(0.07f, 0.07f, 0.04f), new Vector3(0, 0, 45), 1f, door);
                Block("BoltR_" + i, "Steel", new Vector3(1.05f, 0.2f + i * 0.42f, -0.26f), new Vector3(0.07f, 0.07f, 0.04f), new Vector3(0, 0, 45), 1f, door);
            }
            Block("Hinge_Top", "DarkMetal", new Vector3(-0.95f, 2.0f, -0.18f), new Vector3(0.18f, 0.3f, 0.12f), default, 1f, door);
            Block("Hinge_Bottom", "DarkMetal", new Vector3(-0.95f, 0.45f, -0.18f), new Vector3(0.18f, 0.3f, 0.12f), default, 1f, door);

            // Timer display above the door: a steel housing with a bezel, corner screws and a visor, fed by a conduit
            // from the ceiling. Hazard stripes on the floor in front.
            Block("Display", "Display", new Vector3(0, 2.95f, -0.08f), new Vector3(0.9f, 0.3f, 0.12f), default, 1f, door);
            Block("Housing", "DarkMetal", new Vector3(0, 2.95f, -0.04f), new Vector3(1.06f, 0.46f, 0.08f), default, 2f, door);
            Block("Bezel_Top", "Steel", new Vector3(0, 3.13f, -0.15f), new Vector3(1.0f, 0.04f, 0.03f), default, 4f, door);
            Block("Bezel_Bottom", "Steel", new Vector3(0, 2.77f, -0.15f), new Vector3(1.0f, 0.04f, 0.03f), default, 4f, door);
            Block("Bezel_L", "Steel", new Vector3(-0.48f, 2.95f, -0.15f), new Vector3(0.04f, 0.4f, 0.03f), default, 4f, door);
            Block("Bezel_R", "Steel", new Vector3(0.48f, 2.95f, -0.15f), new Vector3(0.04f, 0.4f, 0.03f), default, 4f, door);
            foreach (var sx in new[] { -0.48f, 0.48f })
                foreach (var sy in new[] { 2.77f, 3.13f })
                    Cyl("Screw", "Chrome", new Vector3(sx, sy, -0.165f), 0.012f, 0.01f, 6, new Vector3(-90, 0, 0), 4f, door);
            Block("Visor", "DarkMetal", new Vector3(0, 3.19f, -0.2f), new Vector3(1.04f, 0.025f, 0.14f), new Vector3(-12, 0, 0), 2f, door);
            Block("Bracket_L", "DarkMetal", new Vector3(-0.42f, 2.7f, -0.06f), new Vector3(0.05f, 0.1f, 0.12f), default, 4f, door);
            Block("Bracket_R", "DarkMetal", new Vector3(0.42f, 2.7f, -0.06f), new Vector3(0.05f, 0.1f, 0.12f), default, 4f, door);
            float conduitLen = RoomMax.y - 3.17f;
            if (conduitLen > 0.05f)
                Cyl("DisplayConduit", "DarkMetal", new Vector3(0.36f, 3.17f, -0.05f), 0.025f, conduitLen, 6, default, 2f, door);
            // A faint sheen across the glass in front of the digits.
            var sheen = new MeshBuilder { FaceShade = false };
            sheen.Card(new Vector3(-0.44f, 2.8f, -0.152f), new Vector3(0.88f, 0, 0), new Vector3(0, 0.3f, 0));
            Place("DisplayGlass", sheen, "DisplaySheen", Vector3.zero, default, door);
            var digits = new MeshBuilder { FaceShade = false };
            digits.Quad(new Vector3(-0.45f, 2.8f, -0.145f), new Vector3(0.9f, 0, 0), new Vector3(0, 0.3f, 0), 1f / 0.9f);
            var digitsGo = Place("DisplayDigits", digits, "DisplayDigits", Vector3.zero, default, door);
            _displayDigits = digitsGo.GetComponent<Renderer>();
            var stripes = new MeshBuilder();
            stripes.Quad(new Vector3(-1.2f, 0.006f, -1.0f), new Vector3(2.4f, 0, 0), new Vector3(0, 0, 0.9f), 2f);
            Place("HazardStripes", stripes, "Hazard", Vector3.zero, default, door);

            _door = door.gameObject.AddComponent<Game.VaultDoor>();
            _door.leaf = leaf;
            _door.wheel = wheel;
            // Blast flash where the pipe from the fuse inlet meets the mechanism behind the door.
            var flash = new GameObject("BlastFlash").AddComponent<Light>();
            flash.transform.SetParent(door, false);
            flash.transform.localPosition = new Vector3(-1.4f, 2.4f, -0.5f);
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.6f, 0.25f);
            flash.range = 7f;
            flash.intensity = 0f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;
            _door.flash = flash;
            _door.blastClip = PSXSoundGenerator.Load("explosion");
            _door.wheelClip = PSXSoundGenerator.Load("door_wheel");
            _door.openClip = PSXSoundGenerator.Load("door_open");
        }

        static void BuildPipes()
        {
            var pipes = new GameObject("Pipes").transform;
            pipes.SetParent(_world, false);

            // The fuse duct: the powder trail ends in a funnel on the floor by the door; from there the pipe climbs the
            // wall, turns along the door wall and disappears into the lock mechanism's housing beside the frame.
            var funnel = new MeshBuilder();
            funnel.Tube(new Vector3(0, 0.005f, 0), new Vector3(0, 0.16f, 0), 0.19f, 0.085f, 10, Vector3.forward, 2f);
            funnel.Tube(new Vector3(0, 0.0f, 0), new Vector3(0, 0.03f, 0), 0.21f, 0.21f, 10, Vector3.forward, 2f);
            Place("Pipe_Inlet", funnel, "DarkMetal", new Vector3(-2.72f, 0f, 7.5f), default, pipes, true);
            var duct = PipeRun("FuseDuct", "Rust", new[]
            {
                new Vector3(-2.72f, 0.16f, 7.5f), new Vector3(-2.72f, 2.55f, 7.5f),
                new Vector3(-2.72f, 2.55f, 7.8f), new Vector3(-1.58f, 2.55f, 7.8f)
            }, 0.075f, pipes);
            PipeClamp(new Vector3(-2.72f, 0.75f, 7.5f), Vector3.up, Vector3.left, 0.075f, 0.28f, duct);
            PipeClamp(new Vector3(-2.72f, 1.95f, 7.5f), Vector3.up, Vector3.left, 0.075f, 0.28f, duct);
            PipeClamp(new Vector3(-2.1f, 2.55f, 7.8f), Vector3.right, Vector3.forward, 0.075f, 0.2f, duct);
            // The housing the duct feeds: a riveted box with a hazard band, where the blast trips the lock.
            var housing = new GameObject("LockHousing").transform;
            housing.SetParent(pipes, false);
            Block("Box", "DarkMetal", new Vector3(-1.42f, 2.55f, 7.86f), new Vector3(0.34f, 0.4f, 0.26f), default, 2f, housing);
            Block("Band", "Hazard", new Vector3(-1.42f, 2.42f, 7.725f), new Vector3(0.34f, 0.08f, 0.01f), default, 4f, housing);
            Block("Lid", "Steel", new Vector3(-1.42f, 2.77f, 7.86f), new Vector3(0.38f, 0.04f, 0.3f), default, 2f, housing);
            for (int k = 0; k < 4; k++)
                Block("Rivet", "Steel", new Vector3(-1.56f + (k % 2) * 0.28f, 2.39f + (k / 2) * 0.3f, 7.725f), new Vector3(0.02f, 0.02f, 0.01f), default, 4f, housing);
            Block("Rod_ToFrame", "Steel", new Vector3(-1.2f, 2.5f, 7.86f), new Vector3(0.12f, 0.03f, 0.03f), default, 4f, housing);

            // Valve with a red wheel and a pressure gauge on the climbing part.
            Cyl("Valve_Body", "DarkMetal", new Vector3(-2.72f, 1.25f, 7.5f), 0.115f, 0.2f, 8, default, 1f, pipes);
            Cyl("Valve_Stem", "Steel", new Vector3(-2.62f, 1.35f, 7.5f), 0.02f, 0.12f, 6, new Vector3(0, 0, -90), 1f, pipes);
            var valveWheel = new GameObject("Valve_Wheel").transform;
            valveWheel.SetParent(pipes, false);
            valveWheel.localPosition = new Vector3(-2.49f, 1.35f, 7.5f);
            valveWheel.localRotation = Quaternion.Euler(0, 0, -90);
            ValveWheel(valveWheel, 0.12f, "RedPaint");
            Cyl("Gauge_Stem", "Steel", new Vector3(-2.65f, 1.75f, 7.5f), 0.015f, 0.06f, 6, new Vector3(0, 0, -90), 1f, pipes);
            Cyl("Gauge_Body", "DarkMetal", new Vector3(-2.59f, 1.75f, 7.5f), 0.06f, 0.035f, 10, new Vector3(0, 0, -90), 1f, pipes);
            Cyl("Gauge_Face", "Gauge", new Vector3(-2.555f, 1.75f, 7.5f), 0.052f, 0.004f, 10, new Vector3(0, 0, -90), 1f / 0.1f, pipes);

            // A water main along the top of the left wall, in from the back wall and out through the door wall.
            var main = PipeRun("LeftMain", "Rust", new[] { new Vector3(-2.7f, 3.0f, -0.05f), new Vector3(-2.7f, 3.0f, 8.05f) }, 0.07f, pipes);
            foreach (float z in new[] { 0.8f, 2.9f, 5.5f, 7.6f }) PipeClamp(new Vector3(-2.7f, 3.0f, z), Vector3.forward, Vector3.left, 0.07f, 0.3f, main);

            // Two pipes under the ceiling, hung on rods between the beams.
            var ceilA = PipeRun("CeilingA", "Rust", new[] { new Vector3(1.3f, 2.98f, -0.05f), new Vector3(1.3f, 2.98f, 8.05f) }, 0.06f, pipes);
            var ceilB = PipeRun("CeilingB", "DarkMetal", new[] { new Vector3(1.56f, 2.96f, -0.05f), new Vector3(1.56f, 2.96f, 8.05f) }, 0.04f, pipes, true, true, "Steel");
            foreach (float z in new[] { 0.8f, 2.9f, 5.5f, 7.5f })
            {
                Block("Hanger_Rod", "DarkMetal", new Vector3(1.43f, 3.2f, z), new Vector3(0.015f, 0.4f, 0.015f), default, 4f, ceilA);
                Block("Hanger_Bar", "DarkMetal", new Vector3(1.43f, 2.91f, z), new Vector3(0.42f, 0.02f, 0.04f), default, 4f, ceilA);
            }

            // A standpipe in the corner by the crates with its own shut-off, and a drain pipe by the shelving.
            var stand = PipeRun("Standpipe", "Rust", new[] { new Vector3(2.8f, 0f, 7.82f), new Vector3(2.8f, 3.4f, 7.82f) }, 0.07f, pipes);
            PipeClamp(new Vector3(2.8f, 1.0f, 7.82f), Vector3.up, Vector3.right, 0.07f, 0.2f, stand);
            PipeClamp(new Vector3(2.8f, 2.5f, 7.82f), Vector3.up, Vector3.right, 0.07f, 0.2f, stand);
            var standWheel = new GameObject("Standpipe_Wheel").transform;
            standWheel.SetParent(pipes, false);
            standWheel.localPosition = new Vector3(2.64f, 1.55f, 7.82f);
            standWheel.localRotation = Quaternion.Euler(0, 0, 90);
            ValveWheel(standWheel, 0.09f, "Steel");
            Cyl("Standpipe_Valve", "DarkMetal", new Vector3(2.8f, 1.46f, 7.82f), 0.1f, 0.18f, 8, default, 1f, pipes);
            var drain = PipeRun("DrainPipe", "DarkMetal", new[] { new Vector3(2.84f, 3.4f, 0.9f), new Vector3(2.84f, 0.3f, 0.9f), new Vector3(2.55f, 0.04f, 0.9f) }, 0.05f, pipes);
            PipeClamp(new Vector3(2.84f, 1.8f, 0.9f), Vector3.up, Vector3.right, 0.05f, 0.16f, drain);
            Block("DrainGrate", "Grate", new Vector3(2.45f, 0.005f, 0.9f), new Vector3(0.28f, 0.01f, 0.28f), default, 1f / 0.28f, pipes);

            // Two thin cables along the right wall, in front of the pillars, sagging a little between staples and
            // ducking into the wall at both ends.
            for (int c = 0; c < 2; c++)
            {
                float y = 2.52f - c * 0.08f;
                const float z0 = 1.1f, z1 = 7.55f;
                var cable = new MeshBuilder();
                cable.Tube(new Vector3(3.0f, y, z0), new Vector3(2.83f, y, z0), 0.014f, 0.014f, 5, Vector3.up, 4f);
                cable.Tube(new Vector3(2.83f, y, z1), new Vector3(3.0f, y, z1), 0.014f, 0.014f, 5, Vector3.up, 4f);
                const int pieces = 26;
                for (int i = 0; i < pieces; i++)
                {
                    float za = Mathf.Lerp(z0, z1, (float)i / pieces), zb = Mathf.Lerp(z0, z1, (float)(i + 1) / pieces);
                    float sa = Mathf.Abs(Mathf.Sin((za - z0) / 1.3f * Mathf.PI)) * 0.04f, sb = Mathf.Abs(Mathf.Sin((zb - z0) / 1.3f * Mathf.PI)) * 0.04f;
                    cable.Tube(new Vector3(2.83f, y - sa, za), new Vector3(2.83f, y - sb, zb), 0.014f, 0.014f, 5, Vector3.up, 4f);
                }
                Place("Cable_" + c, cable, "DarkMetal", Vector3.zero, default, pipes);
            }
        }

        // Spoked hand wheel lying in its local XZ plane (turns about local Y): rim of short bars, four spokes, a hub.
        static void ValveWheel(Transform wheel, float radius, string material)
        {
            var mb = new MeshBuilder();
            const int segs = 10;
            for (int i = 0; i < segs; i++)
            {
                float a0 = (float)i / segs * Mathf.PI * 2f, a1 = (float)(i + 1) / segs * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                mb.Tube(p0, p1, radius * 0.13f, radius * 0.13f, 5, Vector3.up, 4f);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                mb.Tube(Vector3.zero, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, radius * 0.08f, radius * 0.08f, 4, Vector3.up, 4f);
            }
            mb.Cylinder(new Vector3(0, -radius * 0.12f, 0), radius * 0.22f, radius * 0.24f, 8, 4f);
            Place("Wheel", mb, material, Vector3.zero, default, wheel);
        }

        static readonly Vector3[] FusePath =
        {
            new Vector3(-1.45f, 0.012f, 1.75f), new Vector3(-0.9f, 0.012f, 2.3f), new Vector3(-0.55f, 0.012f, 3.0f),
            new Vector3(-0.85f, 0.012f, 3.8f), new Vector3(-0.4f, 0.012f, 4.6f), new Vector3(-0.7f, 0.012f, 5.5f),
            new Vector3(-1.4f, 0.012f, 6.2f), new Vector3(-2.2f, 0.012f, 6.9f), new Vector3(-2.7f, 0.012f, 7.45f)
        };

        // Catmull-Rom through the control points so the fuse curves instead of zig-zagging.
        static List<Vector3> Smooth(IList<Vector3> pts, int perSegment)
        {
            var result = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];
                for (int s = 0; s < perSegment; s++)
                {
                    float t = (float)s / perSegment, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }
            result.Add(pts[pts.Count - 1]);
            return result;
        }

        static void BuildFuse()
        {
            var fuse = new GameObject("Fuse").transform;
            fuse.SetParent(_world, false);

            var path = Smooth(FusePath, 6);
            _fusePath = path;
            var powder = new MeshBuilder();
            powder.Ribbon(path, 0.07f, 4f);
            Place("Powder", powder, "Powder", Vector3.zero, default, fuse);

            // Unlit at the start; the Fuse component draws ash, hot core and glow as it burns.
            var spark = new MeshBuilder();
            spark.Cross(Vector3.zero, 0.12f, 0.16f);
            var sparkGo = Place("Spark", spark, "Flame", path[0], default, fuse);
            var light = new GameObject("SparkLight").AddComponent<Light>();
            light.transform.SetParent(sparkGo.transform, false);
            light.transform.localPosition = Vector3.up * 0.15f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 2.6f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            var flicker = sparkGo.AddComponent<PSX.FlameFlicker>();
            flicker.flameLight = light;
            flicker.flame = sparkGo.transform;
            flicker.baseIntensity = 1.6f;
            flicker.flicker = 0.5f;
            flicker.speed = 14f;

            var tailLight = new GameObject("EmberLight").AddComponent<Light>();
            tailLight.transform.SetParent(fuse, false);
            tailLight.transform.localPosition = path[0] + Vector3.up * 0.25f;
            tailLight.type = LightType.Point;
            tailLight.color = new Color(1f, 0.45f, 0.15f);
            tailLight.range = 2.2f;
            tailLight.intensity = 0.8f;
            tailLight.shadows = LightShadows.None;

            _fuse = fuse.gameObject.AddComponent<Game.Fuse>();
            _fuse.path = path.ToArray();
            _fuse.coreMaterial = Mats["FuseCore"];
            _fuse.glowMaterial = Mats["FuseGlow"];
            _fuse.ashMaterial = Mats["Ash"];
            _fuse.spark = sparkGo.transform;
            _fuse.sparkLight = light;
            _fuse.emberLight = tailLight;
            _fuse.hissClip = PSXSoundGenerator.Load("fuse_hiss");
            _fuse.igniteClip = PSXSoundGenerator.Load("ignite");
            _fuse.fizzleClip = PSXSoundGenerator.Load("fizzle");
        }

        static float PathLength(IList<Vector3> path)
        {
            float len = 0f;
            for (int i = 1; i < path.Count; i++) len += Vector3.Distance(path[i - 1], path[i]);
            return len;
        }

        static Vector3 PathPoint(IList<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= seg) return Vector3.Lerp(path[i - 1], path[i], distance / seg);
                distance -= seg;
            }
            return path[path.Count - 1];
        }

        static void BuildProps()
        {
            var props = new GameObject("Props").transform;
            props.SetParent(_world, false);

            // Powder barrel where the fuse starts.
            var barrel = new GameObject("PowderBarrel").transform;
            barrel.SetParent(props, false);
            barrel.localPosition = new Vector3(-1.9f, 0, 1.35f);
            Cyl("Body", "Rust", Vector3.zero, 0.34f, 0.92f, 10, default, 1f, barrel, true);
            Cyl("Band_Low", "DarkMetal", new Vector3(0, 0.18f, 0), 0.355f, 0.05f, 10, default, 1f, barrel);
            Cyl("Band_High", "DarkMetal", new Vector3(0, 0.72f, 0), 0.355f, 0.05f, 10, default, 1f, barrel);
            Block("Label", "Hazard", new Vector3(0.24f, 0.45f, 0.24f), new Vector3(0.22f, 0.18f, 0.01f), new Vector3(0, 45, 0), 2f, barrel);
            Cyl("SpilledPowder", "Powder", new Vector3(0.3f, 0, 0.35f), 0.22f, 0.012f, 7, default, 1f, barrel);

            // Gas cylinders chained to the left wall.
            for (int i = 0; i < 2; i++) GasCylinder(new Vector3(-2.66f, 0, 3.1f + i * 0.34f), -90f + i * 25f, props);
            GasBracket(new Vector3(RoomMin.x, 0.95f, 3.27f), 0.68f, props);

            // One more by the pipe, its hose plumbed into it: the other ways through the door. Shoot it, or open its
            // valve and let the room fill.
            var pipeGas = GasCylinder(new Vector3(-2.66f, 0, 6.0f), -90f, props, "PipeGas");
            var hose = new MeshBuilder();
            Vector3 outlet = new Vector3(-2.66f, 1.3f, 6.03f), sag = new Vector3(-2.74f, 0.95f, 6.8f), inlet = new Vector3(-2.74f, 1.05f, 7.6f);
            hose.Tube(outlet, sag, 0.014f, 0.014f, 6, Vector3.right, 4f);
            hose.Tube(sag, inlet, 0.014f, 0.014f, 6, Vector3.right, 4f);
            Place("GasHose", hose, "Rubber", Vector3.zero, default, props);
            Cyl("HoseClamp", "Steel", inlet + new Vector3(-0.02f, 0, 0), 0.025f, 0.04f, 8, new Vector3(0, 0, 90), 4f, props);
            pipeGas.gameObject.AddComponent<Game.Shootable>();
            var hiss = pipeGas.gameObject.AddComponent<AudioSource>();
            hiss.clip = PSXSoundGenerator.Load("steam");
            hiss.loop = true;
            hiss.playOnAwake = false;
            hiss.spatialBlend = 1f;
            hiss.rolloffMode = AudioRolloffMode.Linear;
            hiss.minDistance = 0.6f;
            hiss.maxDistance = 9f;
            hiss.volume = 0f;

            // Fire extinguisher and a warning sign.
            Extinguisher(new Vector3(RoomMin.x, 0.5f, 5.1f), 90f, props);
            var sign = new MeshBuilder();
            sign.Quad(new Vector3(0, 0, -0.25f), new Vector3(0, 0, 0.5f), new Vector3(0, 0.5f, 0), 2f);
            Place("WarningSign", sign, "Sign", new Vector3(-2.97f, 1.55f, 2.2f), default, props);

            // Workbench on the right wall with a toolbox and a jar shelf above.
            var bench = new GameObject("Workbench").transform;
            bench.SetParent(props, false);
            bench.localPosition = new Vector3(2.55f, 0, 3.05f);
            Block("Top", "Wood", new Vector3(0, 0.9f, 0), new Vector3(0.8f, 0.07f, 1.9f), default, 1f, bench, true);
            Block("Shelf", "Wood", new Vector3(0, 0.25f, 0), new Vector3(0.72f, 0.04f, 1.8f), default, 1f, bench);
            foreach (var leg in new[] { new Vector3(-0.34f, 0, -0.88f), new Vector3(0.34f, 0, -0.88f), new Vector3(-0.34f, 0, 0.88f), new Vector3(0.34f, 0, 0.88f) })
                Block("Leg", "DarkMetal", leg + new Vector3(0, 0.44f, 0), new Vector3(0.06f, 0.88f, 0.06f), default, 1f, bench);
            Block("Toolbox", "RedPaint", new Vector3(0.05f, 1.02f, -0.45f), new Vector3(0.28f, 0.17f, 0.5f), new Vector3(0, 12, 0), 2f, bench);
            Block("ToolboxHandle", "DarkMetal", new Vector3(0.05f, 1.13f, -0.45f), new Vector3(0.03f, 0.04f, 0.26f), new Vector3(0, 12, 0), 1f, bench);
            Block("Lighter_Box", "Wood", new Vector3(-0.1f, 0.97f, 0.35f), new Vector3(0.18f, 0.08f, 0.12f), new Vector3(0, -20, 0), 2f, bench);
            Block("WallShelf", "Wood", new Vector3(0.3f, 1.7f, 0), new Vector3(0.24f, 0.04f, 1.4f), default, 1f, bench);
            for (int i = 0; i < 5; i++)
                Cyl("Jar_" + i, "Glass", new Vector3(0.3f, 1.72f, -0.55f + i * 0.27f), 0.05f + Hash(i, 0, 30) * 0.02f, 0.12f + Hash(i, 1, 30) * 0.1f, 6, default, 1f, bench);

            // Crates near the door.
            Model("Survival/box", new Vector3(2.3f, 0f, 6.25f), 8f, 0f, props, 0f, null, Vector3.one * 0.8f);
            Model("Survival/box", new Vector3(2.25f, 0.8f, 6.2f), -17f, 0f, props, 0f, null, Vector3.one * 0.5f);
            Model("Survival/box-large", new Vector3(2.4f, 0f, 7.3f), 30f, 0f, props, 0f, null, new Vector3(0.6f, 0.6f, 0.6f));

            // An open crate of the old charges you were sent down to clear: straw and red sticks.
            var charges = new GameObject("ChargeCrate").transform;
            charges.SetParent(props, false);
            charges.localPosition = new Vector3(-0.95f, 0f, 0.4f);
            charges.localRotation = Quaternion.Euler(0, 12, 0);
            Block("Base", "Crate", new Vector3(0, 0.02f, 0), new Vector3(0.56f, 0.04f, 0.36f), default, 2f, charges, true);
            Block("Side_F", "Crate", new Vector3(0, 0.15f, -0.17f), new Vector3(0.56f, 0.26f, 0.025f), default, 2f, charges);
            Block("Side_B", "Crate", new Vector3(0, 0.15f, 0.17f), new Vector3(0.56f, 0.26f, 0.025f), default, 2f, charges);
            Block("Side_L", "Crate", new Vector3(-0.27f, 0.15f, 0), new Vector3(0.025f, 0.26f, 0.32f), default, 2f, charges);
            Block("Side_R", "Crate", new Vector3(0.27f, 0.15f, 0), new Vector3(0.025f, 0.26f, 0.32f), default, 2f, charges);
            Block("Straw", "Wood", new Vector3(0, 0.2f, 0), new Vector3(0.5f, 0.02f, 0.3f), default, 6f, charges);
            for (int i = 0; i < 7; i++)
                Cyl("Stick", "Dynamite", new Vector3(-0.2f + i * 0.065f, 0.21f + (i % 2) * 0.02f, -0.1f), 0.022f, 0.2f, 6, new Vector3(90, Hash(i, 0, 90) * 8f - 4f, 0), 4f, charges);
            Block("Lid", "Crate", new Vector3(0.05f, 0.03f, -0.42f), new Vector3(0.58f, 0.025f, 0.38f), new Vector3(0, -25, 4), 2f, charges);

            // A bucket in the way of the fuse: part of the puzzle (moving it is a step).
            // A bucket of water standing on the trail: the flame dies when it gets there unless it is shoved aside.
            Vector3 bucketPos = PathPoint(_fusePath, PathLength(_fusePath) * 0.78f);
            var bucket = Cyl("Bucket", "Rust", new Vector3(bucketPos.x, 0, bucketPos.z), 0.15f, 0.28f, 8, default, 1f, props, true);
            var water = new MeshBuilder { FaceShade = false };
            water.Cylinder(Vector3.zero, 0.13f, 0.005f, 8, 1f);
            Place("Water", water, "Water", new Vector3(0, 0.23f, 0), default, bucket.transform);
            var body = bucket.AddComponent<Rigidbody>();
            body.mass = 4f;
            body.linearDamping = 2f;
            body.angularDamping = 2f;
            bucket.AddComponent<Game.FuseBlocker>();
        }

        // A flat card on a wall, floor or table, facing along one main axis.
        static GameObject Plate(string name, string material, Vector3 center, float width, float height, Vector3 facing, Transform parent, float spin = 0f)
        {
            Vector3 u, w;
            if (facing == Vector3.back) { u = Vector3.right; w = Vector3.up; }
            else if (facing == Vector3.forward) { u = Vector3.left; w = Vector3.up; }
            else if (facing == Vector3.right) { u = Vector3.forward; w = Vector3.up; }
            else if (facing == Vector3.left) { u = Vector3.back; w = Vector3.up; }
            else { u = Vector3.right; w = Vector3.forward; }
            Quaternion r = Quaternion.AngleAxis(spin, facing);
            u = r * u * width;
            w = r * w * height;
            var mb = new MeshBuilder { FaceShade = false };
            mb.Card(-u / 2 - w / 2, u, w);
            return Place(name, mb, material, center, default, parent);
        }

        const string NoteDay3 =
            "DAY 3\n\n" +
            "The air runs out. Every time.\n" +
            "I wake up on the floor and the\n" +
            "room has moved on without me.\n\n" +
            "The fire kept burning while\n" +
            "I was gone.\n\n" +
            "USE IT.";

        const string NoteDoor =
            "The lock is on the other side.\n\n" +
            "The pipe on the left wall runs\n" +
            "straight to it.\n\n" +
            "Something has to reach it\n" +
            "when you can't.\n\n" +
            "                        - M.";

        // Everything that makes the room feel lived in (and died in): signs, notes, shelving, a control panel,
        // ceiling pipes, lamps, rubble, scattered papers, a puddle, a hanging chain.
        static void BuildDetails()
        {
            var details = new GameObject("Details").transform;
            details.SetParent(_world, false);
            float wallX = RoomMax.x - 0.012f, doorZ = RoomMax.z - 0.012f;

            // Signs.
            Plate("Sign_Sector", "SignSector", new Vector3(-1.2f, 2.35f, 0.012f), 1.2f, 0.3f, Vector3.forward, details);
            Plate("Sign_Danger", "SignDanger", new Vector3(-wallX, 1.75f, 1.05f), 0.6f, 0.3f, Vector3.right, details);
            Plate("Sign_Air", "SignAir", new Vector3(-0.72f, 2.95f, doorZ), 0.36f, 0.18f, Vector3.back, details);
            Plate("Sign_Exit", "SignExit", new Vector3(DoorHalfWidth - 0.012f, 2.0f, RoomMax.z + 2.2f), 0.5f, 0.17f, Vector3.left, details);

            // Notes: backstory, and the puzzle's hint for anyone who reads.
            var note1 = Plate("Note_Day3", "NoteDiary", new Vector3(2.38f, 0.945f, 3.55f), 0.2f, 0.26f, Vector3.up, details, 12f);
            note1.AddComponent<Game.ReadableNote>().text = NoteDay3;
            var note2 = Plate("Note_Door", "NoteScrap", new Vector3(-2.3f, 1.55f, doorZ - 0.004f), 0.22f, 0.28f, Vector3.back, details, -4f);
            note2.AddComponent<Game.ReadableNote>().text = NoteDoor;

            // Control panel left of the door, wired to the frame.
            var panel = new GameObject("ControlPanel").transform;
            panel.SetParent(details, false);
            Block("Box", "DarkMetal", new Vector3(-1.75f, 1.45f, RoomMax.z - 0.07f), new Vector3(0.5f, 0.7f, 0.12f), default, 2f, panel);
            Cyl("Gauge_L", "Gauge", new Vector3(-1.88f, 1.62f, RoomMax.z - 0.13f), 0.075f, 0.02f, 10, new Vector3(-90, 0, 0), 1f / 0.15f, panel);
            Cyl("Gauge_R", "Gauge", new Vector3(-1.62f, 1.62f, RoomMax.z - 0.13f), 0.075f, 0.02f, 10, new Vector3(-90, 0, 0), 1f / 0.15f, panel);
            Block("Lever_Base", "Steel", new Vector3(-1.75f, 1.25f, RoomMax.z - 0.14f), new Vector3(0.12f, 0.06f, 0.04f), default, 4f, panel);
            Block("Lever", "RedPaint", new Vector3(-1.75f, 1.32f, RoomMax.z - 0.17f), new Vector3(0.025f, 0.16f, 0.025f), new Vector3(-30, 0, 0), 4f, panel);
            Block("Conduit", "DarkMetal", new Vector3(-1.75f, 2.5f, RoomMax.z - 0.04f), new Vector3(0.05f, 1.4f, 0.05f), default, 1f, panel);
            Block("Cable_ToDoor", "DarkMetal", new Vector3(-1.37f, 1.5f, RoomMax.z - 0.05f), new Vector3(0.26f, 0.035f, 0.035f), default, 1f, panel);

            // Vent grille on the back wall.
            Block("Vent_Frame", "DarkMetal", new Vector3(1.4f, 2.65f, 0.05f), new Vector3(0.62f, 0.42f, 0.08f), default, 2f, details);
            for (int i = 0; i < 5; i++)
                Block("Vent_Slat_" + i, "Steel", new Vector3(1.4f, 2.5f + i * 0.075f, 0.1f), new Vector3(0.54f, 0.02f, 0.03f), new Vector3(30, 0, 0), 2f, details);

            // Steel shelving against the back wall, stocked.
            var shelf = new GameObject("Shelving").transform;
            shelf.SetParent(details, false);
            shelf.localPosition = new Vector3(2.35f, 0f, 0.3f);
            foreach (var px in new[] { -0.45f, 0.45f })
                foreach (var pz in new[] { -0.18f, 0.18f })
                    Block("Post", "DarkMetal", new Vector3(px, 0.9f, pz), new Vector3(0.04f, 1.8f, 0.04f), default, 1f, shelf);
            for (int i = 0; i < 3; i++)
                Block("Shelf_" + i, "DarkMetal", new Vector3(0, 0.35f + i * 0.6f, 0), new Vector3(0.94f, 0.03f, 0.4f), default, 1f, shelf, true);
            Block("Box_A", "Crate", new Vector3(-0.22f, 0.5f, 0), new Vector3(0.3f, 0.28f, 0.3f), new Vector3(0, 8, 0), 1f / 0.3f, shelf);
            Block("Box_B", "Crate", new Vector3(0.2f, 0.47f, 0.02f), new Vector3(0.26f, 0.22f, 0.26f), new Vector3(0, -14, 0), 1f / 0.26f, shelf);
            Block("Toolbox", "RedPaint", new Vector3(-0.15f, 1.03f, 0), new Vector3(0.36f, 0.14f, 0.18f), new Vector3(0, 5, 0), 2f, shelf);
            Cyl("Rope", "Wood", new Vector3(0.25f, 0.965f, 0), 0.11f, 0.08f, 8, default, 4f, shelf);
            for (int i = 0; i < 4; i++)
                Cyl("Jar_" + i, "Glass", new Vector3(-0.32f + i * 0.2f, 1.565f, 0.02f), 0.045f, 0.1f + Hash(i, 3, 52) * 0.08f, 6, default, 4f, shelf);

            // On the workbench: a tape recorder, a tin mug, loose sheets.
            Block("Recorder", "DarkMetal", new Vector3(2.5f, 0.98f, 2.75f), new Vector3(0.28f, 0.08f, 0.18f), new Vector3(0, -8, 0), 4f, details);
            Cyl("Reel_L", "Steel", new Vector3(2.44f, 1.02f, 2.73f), 0.045f, 0.012f, 8, default, 4f, details);
            Cyl("Reel_R", "Steel", new Vector3(2.56f, 1.02f, 2.77f), 0.045f, 0.012f, 8, default, 4f, details);
            Cyl("Mug", "Steel", new Vector3(2.32f, 0.935f, 3.95f), 0.04f, 0.09f, 7, default, 4f, details);
            Plate("Sheet_Bench", "Paper", new Vector3(2.62f, 0.945f, 3.3f), 0.18f, 0.24f, Vector3.up, details, -25f);

            // A chair knocked over by the bench.
            var chair = new GameObject("Chair").transform;
            chair.SetParent(details, false);
            chair.localPosition = new Vector3(1.5f, 0.22f, 4.4f);
            Model("Furniture/chair", Vector3.zero, 25f, 0f, chair, -88f, null, new Vector3(0.42f, 0.9f, 0.42f));

            // Rubble where the ceiling has given way.
            foreach (var pile in new[] { new Vector3(-2.45f, 0f, 0.5f), new Vector3(1.15f, 0f, 7.6f) })
                for (int i = 0; i < 10; i++)
                {
                    float s = 0.07f + Hash(i, (int)(pile.z * 10), 53) * 0.15f;
                    var p = pile + new Vector3((Hash(i, 1, 54) - 0.5f) * 0.55f, s * 0.4f, (Hash(i, 2, 55) - 0.5f) * 0.45f);
                    Block("Rubble", i % 3 == 0 ? "Ceiling" : "Stone", p, new Vector3(s, s * 0.7f, s * 0.9f),
                          new Vector3(Hash(i, 3, 56) * 40f, Hash(i, 4, 57) * 360f, Hash(i, 5, 58) * 30f), 2f, details);
                }

            // Papers blown across the floor.
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(0.4f + Hash(i, 6, 59) * 2f, 0.01f + i * 0.002f, 1.5f + Hash(i, 7, 60) * 4f);
                Plate("FloorSheet", "Paper", p, 0.18f, 0.24f, Vector3.up, details, Hash(i, 8, 61) * 360f);
            }

            // A puddle by the left wall: the bucket has been used before.
            var puddle = new MeshBuilder { FaceShade = false };
            puddle.Cylinder(Vector3.zero, 0.38f, 0.003f, 9, 1f);
            Place("Puddle", puddle, "Water", new Vector3(-2.35f, 0.002f, 5.9f), new Vector3(0, 17, 0), details);

            // Caged wall lamps on the pillars, burnt down to a glow.
            WallLamp(new Vector3(RoomMin.x + 0.2f, 2.3f, 4.2f), Vector3.right, details);
            WallLamp(new Vector3(RoomMax.x - 0.2f, 2.3f, 1.6f), Vector3.left, details);

            // A miner's helmet dropped by the door, its lamp long dead.
            Helmet(new Vector3(0.72f, 0.1f, 7.15f), 35f, details);

            // Floor drain in the middle of the room.
            Block("FloorDrain", "Grate", new Vector3(0.95f, 0.004f, 5.6f), new Vector3(0.34f, 0.008f, 0.34f), new Vector3(0, 10, 0), 1f / 0.34f, details);

            // A chain hanging from the middle beam.
            for (int i = 0; i < 9; i++)
                Block("ChainLink", "DarkMetal", new Vector3(-1.1f, 3.12f - i * 0.065f, 4.2f), new Vector3(0.022f, 0.06f, 0.008f),
                      new Vector3(0, i % 2 == 0 ? 0 : 90, 0), 4f, details);
        }

        // A welding-gas cylinder: rounded shoulder, neck, a valve with its hand wheel inside a protective collar,
        // a white band with a hazard diamond, and a foot ring.
        static Transform GasCylinder(Vector3 pos, float yaw, Transform parent, string name = "GasCylinder")
        {
            var g = Group(name, pos, yaw, parent);
            const float r = 0.14f, h = 1.08f;
            Cyl("Body", "RedPaint", Vector3.zero, r, h, 14, default, 1f, g, true);
            var dome = new MeshBuilder();
            dome.Tube(new Vector3(0, h, 0), new Vector3(0, h + 0.07f, 0), r, r * 0.8f, 14, Vector3.forward, 2f);
            dome.Tube(new Vector3(0, h + 0.07f, 0), new Vector3(0, h + 0.12f, 0), r * 0.8f, r * 0.42f, 14, Vector3.forward, 2f);
            Place("Shoulder", dome, "RedPaint", Vector3.zero, default, g);
            Cyl("Neck", "Steel", new Vector3(0, h + 0.12f, 0), 0.035f, 0.05f, 8, default, 4f, g);
            Cyl("Valve", "Chrome", new Vector3(0, h + 0.17f, 0), 0.03f, 0.06f, 8, default, 4f, g);
            Cyl("Outlet", "Chrome", new Vector3(0.025f, h + 0.195f, 0), 0.012f, 0.05f, 6, new Vector3(0, 0, -90), 6f, g);
            var wheel = new GameObject("HandWheel").transform;
            wheel.SetParent(g, false);
            wheel.localPosition = new Vector3(0, h + 0.235f, 0);
            ValveWheel(wheel, 0.04f, "DarkMetal");
            var collar = new MeshBuilder();
            OpenDrum(collar, new Vector3(0, h + 0.1f, 0), 0.075f, 0.16f, 10, 0.008f, 4f);
            Place("Collar", collar, "DarkMetal", Vector3.zero, default, g);
            Cyl("Band", "Enamel", new Vector3(0, 0.82f, 0), r + 0.003f, 0.07f, 14, default, 2f, g);
            Plate("Diamond", "Hazard", new Vector3(0, 0.62f, -r - 0.004f), 0.1f, 0.1f, Vector3.back, g, 45f);
            var foot = new MeshBuilder();
            OpenDrum(foot, Vector3.zero, r + 0.012f, 0.06f, 14, 0.01f, 2f);
            Place("FootRing", foot, "DarkMetal", Vector3.zero, default, g);
            return g;
        }

        // A fire extinguisher on its wall bracket (the group sits on the wall, facing +Z into the room): red body with
        // a domed top, the valve head with its lever, a hose down to the nozzle in a clip, a label and a strap.
        static void Extinguisher(Vector3 wallPoint, float yaw, Transform parent)
        {
            var g = Group("Extinguisher", wallPoint, yaw, parent);
            const float r = 0.075f, h = 0.46f, gap = 0.012f;
            float z = r + gap + 0.02f;
            Block("BackPlate", "DarkMetal", new Vector3(0, 0.36f, 0.01f), new Vector3(0.1f, 0.22f, 0.02f), default, 4f, g);
            Block("Hook", "DarkMetal", new Vector3(0, 0.46f, z - r - 0.005f), new Vector3(0.05f, 0.03f, 0.04f), default, 4f, g);
            Cyl("Body", "RedPaint", new Vector3(0, 0f, z), r, h, 12, default, 1f, g);
            var dome = new MeshBuilder();
            dome.Tube(new Vector3(0, h, z), new Vector3(0, h + 0.05f, z), r, 0.025f, 12, Vector3.forward, 2f);
            Place("Dome", dome, "RedPaint", Vector3.zero, default, g);
            Cyl("Strap", "DarkMetal", new Vector3(0, 0.33f, z), r + 0.004f, 0.025f, 12, default, 2f, g);
            Cyl("Neck", "Chrome", new Vector3(0, h + 0.05f, z), 0.018f, 0.03f, 8, default, 4f, g);
            Block("ValveHead", "Chrome", new Vector3(0, h + 0.09f, z), new Vector3(0.04f, 0.035f, 0.05f), default, 6f, g);
            Block("Lever", "Chrome", new Vector3(0, h + 0.115f, z + 0.03f), new Vector3(0.025f, 0.008f, 0.09f), new Vector3(-12, 0, 0), 6f, g);
            Block("Handle", "DarkMetal", new Vector3(0, h + 0.075f, z + 0.035f), new Vector3(0.025f, 0.008f, 0.07f), default, 6f, g);
            Cyl("Gauge", "Gauge", new Vector3(0.022f, h + 0.09f, z), 0.012f, 0.006f, 8, new Vector3(0, 0, -90), 1f / 0.024f, g);
            var hose = new MeshBuilder();
            Vector3 prev = new Vector3(0.025f, h + 0.08f, z + 0.02f);
            foreach (var p in new[] { new Vector3(0.06f, h + 0.02f, z + 0.03f), new Vector3(0.085f, h - 0.15f, z + 0.02f), new Vector3(0.085f, h - 0.3f, z + 0.005f) })
            {
                hose.Tube(prev, p, 0.009f, 0.009f, 6, Vector3.forward, 4f);
                prev = p;
            }
            Place("Hose", hose, "Rubber", Vector3.zero, default, g);
            Cyl("Nozzle", "Rubber", new Vector3(0.085f, h - 0.36f, z + 0.005f), 0.013f, 0.06f, 6, default, 4f, g);
            Block("NozzleClip", "DarkMetal", new Vector3(0.07f, h - 0.33f, z), new Vector3(0.03f, 0.02f, 0.02f), default, 6f, g);
            Plate("Label", "Notice", new Vector3(0, 0.2f, z + r + 0.003f), 0.09f, 0.12f, Vector3.forward, g);
        }

        // Wall bracket holding the cylinders: two arms out from the wall and a sagging chain across them.
        static void GasBracket(Vector3 wallPoint, float span, Transform parent)
        {
            var g = Group("GasBracket", wallPoint, 0f, parent);
            foreach (float z in new[] { -span * 0.5f, span * 0.5f })
            {
                Block("Arm", "DarkMetal", new Vector3(0.17f, 0, z), new Vector3(0.34f, 0.04f, 0.03f), default, 4f, g);
                Block("Plate", "DarkMetal", new Vector3(0.01f, 0, z), new Vector3(0.02f, 0.1f, 0.08f), default, 4f, g);
            }
            var chain = new MeshBuilder();
            const int links = 14;
            for (int i = 0; i < links; i++)
            {
                float k = (i + 0.5f) / links;
                float z = Mathf.Lerp(-span * 0.5f, span * 0.5f, k);
                float sag = Mathf.Sin(k * Mathf.PI) * 0.03f;
                var c = new Vector3(0.48f, -sag, z);
                chain.M = Matrix4x4.TRS(c, Quaternion.identity, Vector3.one);
                chain.Box(Vector3.zero, i % 2 == 0 ? new Vector3(0.008f, 0.025f, 0.045f) : new Vector3(0.025f, 0.008f, 0.045f), 8f);
                chain.M = Matrix4x4.identity;
            }
            Place("Chain", chain, "Steel", Vector3.zero, default, g);
        }

        static void WallLamp(Vector3 pos, Vector3 facing, Transform parent)
        {
            var lamp = new GameObject("WallLamp").transform;
            lamp.SetParent(parent, false);
            lamp.localPosition = pos;
            lamp.localRotation = Quaternion.LookRotation(facing);
            Block("Mount", "DarkMetal", new Vector3(0, 0, -0.04f), new Vector3(0.1f, 0.14f, 0.04f), default, 4f, lamp);
            Block("Bulb", "Bulb", new Vector3(0, 0, 0.03f), new Vector3(0.05f, 0.07f, 0.05f), default, 4f, lamp);
            foreach (var cx in new[] { -0.045f, 0.045f })
                Block("Cage", "DarkMetal", new Vector3(cx, 0, 0.03f), new Vector3(0.008f, 0.12f, 0.09f), default, 4f, lamp);
            Block("CageTop", "DarkMetal", new Vector3(0, 0.06f, 0.03f), new Vector3(0.098f, 0.008f, 0.09f), default, 4f, lamp);
            var halo = new MeshBuilder();
            halo.Cross(Vector3.zero, 0.35f, 0.35f);
            Place("Halo", halo, "Halo", new Vector3(0, -0.17f, 0.05f), default, lamp);
        }

        // Hard hat lying on its side: dome, brim, and the lamp on the front.
        static void Helmet(Vector3 pos, float yaw, Transform parent)
        {
            // Dropped and settled crown-up on the floor, tipped a little onto its brim, the dead lamp to the front.
            var helmet = new GameObject("Helmet").transform;
            helmet.SetParent(parent, false);
            helmet.localPosition = new Vector3(pos.x, 0f, pos.z);
            helmet.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(-7f, 0, 4f);
            var shell = new MeshBuilder();
            shell.Tube(new Vector3(0, 0.02f, 0), new Vector3(0, 0.11f, 0), 0.13f, 0.11f, 12, Vector3.forward, 4f);
            shell.Tube(new Vector3(0, 0.11f, 0), new Vector3(0, 0.165f, 0), 0.11f, 0.045f, 12, Vector3.forward, 4f);
            shell.Tube(new Vector3(0, 0.165f, 0), new Vector3(0, 0.18f, 0), 0.045f, 0.01f, 12, Vector3.forward, 4f);
            shell.Box(new Vector3(0, 0.13f, 0f), new Vector3(0.025f, 0.08f, 0.24f), 4f);
            shell.Tube(new Vector3(0, 0.0f, 0), new Vector3(0, 0.02f, 0), 0.165f, 0.165f, 12, Vector3.forward, 4f);
            Place("Shell", shell, "Helmet", Vector3.zero, default, helmet);
            Cyl("Lamp", "Steel", new Vector3(0, 0.1f, 0.115f), 0.032f, 0.045f, 8, new Vector3(90, 0, 0), 4f, helmet);
            Cyl("LampGlass", "Glass", new Vector3(0, 0.1f, 0.158f), 0.025f, 0.006f, 8, new Vector3(90, 0, 0), 4f, helmet);
        }

        static Light PointLight(string name, Vector3 pos, Color color, float range, float intensity)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(_world, false);
            l.transform.localPosition = pos;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        static void BuildLights()
        {
            // Caged bulb hanging in the middle of the room.
            var lamp = new GameObject("CeilingLamp").transform;
            lamp.SetParent(_world, false);
            lamp.localPosition = new Vector3(0, RoomMax.y, 3.6f);
            Block("Cord", "DarkMetal", new Vector3(0, -0.25f, 0), new Vector3(0.02f, 0.5f, 0.02f), default, 1f, lamp);
            Cyl("Shade", "DarkMetal", new Vector3(0, -0.6f, 0), 0.22f, 0.1f, 8, default, 1f, lamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.66f, 0), new Vector3(0.08f, 0.1f, 0.08f), default, 1f, lamp);
            var halo = new MeshBuilder();
            halo.Cross(Vector3.zero, 0.7f, 0.7f);
            Place("BulbHalo", halo, "Halo", new Vector3(0, -1.0f, 0), default, lamp);
            PointLight("CeilingLight", new Vector3(0, RoomMax.y - 0.8f, 3.6f), new Color(1f, 0.88f, 0.7f), 8f, 3.2f);

            // Caged lamp right in front of the vault door: the door is the goal, so it gets the best light.
            var doorLamp = new GameObject("DoorLamp").transform;
            doorLamp.SetParent(_world, false);
            doorLamp.localPosition = new Vector3(0, RoomMax.y, RoomMax.z - 0.7f);
            Block("Cord", "DarkMetal", new Vector3(0, -0.12f, 0), new Vector3(0.02f, 0.24f, 0.02f), default, 1f, doorLamp);
            Cyl("Shade", "DarkMetal", new Vector3(0, -0.34f, 0), 0.18f, 0.08f, 8, default, 1f, doorLamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.39f, 0), new Vector3(0.07f, 0.08f, 0.07f), default, 1f, doorLamp);
            var doorHalo = new MeshBuilder();
            doorHalo.Cross(Vector3.zero, 0.5f, 0.5f);
            Place("BulbHalo", doorHalo, "Halo", new Vector3(0, -0.65f, 0), default, doorLamp);
            PointLight("DoorLight", new Vector3(0, RoomMax.y - 0.6f, RoomMax.z - 0.9f), new Color(1f, 0.85f, 0.62f), 5f, 2.4f);

            // Red warning lamp above the door.
            Block("RedLamp", "RedLamp", new Vector3(1.4f, 2.75f, RoomMax.z - 0.06f), new Vector3(0.14f, 0.14f, 0.1f), default, 1f, _world);
            var redHalo = new MeshBuilder();
            redHalo.Cross(Vector3.zero, 0.6f, 0.6f);
            Place("RedHalo", redHalo, "RedHalo", new Vector3(1.4f, 2.45f, RoomMax.z - 0.15f), default);
            PointLight("RedLight", new Vector3(1.4f, 2.6f, RoomMax.z - 0.3f), new Color(1f, 0.12f, 0.08f), 2.2f, 0.6f);

            // Cold fill so stone reads blue-grey against the warm lamps instead of going black.
            PointLight("FillLight", new Vector3(-0.5f, 2.8f, 1.6f), new Color(0.5f, 0.62f, 0.9f), 9f, 1.8f);
            PointLight("FillLight_Right", new Vector3(2f, 2.4f, 5f), new Color(0.45f, 0.58f, 0.85f), 5f, 0.9f);
        }

        // The player as built: what the room's director and flow need to be wired to.
        struct PlayerRig
        {
            public PSX.FirstPersonController controller;
            public Transform head, hand;
            public Game.HandAnimator animator;
            public Light lighterLight;
        }

        static PlayerRig BuildPlayer(Vector3 position, float yaw)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(null);
            player.transform.position = position;
            player.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.3f;
            cc.stepOffset = 0.15f;   // low enough that the water bucket gets pushed, not climbed
            cc.center = new Vector3(0, 0.85f, 0);

            var head = new GameObject("Head").transform;
            head.SetParent(player.transform, false);
            head.localPosition = new Vector3(0, 1.55f, 0);
            head.localRotation = Quaternion.Euler(12f, 0, 0);
            MakeEye(head.gameObject);

            var fpc = player.AddComponent<PSX.FirstPersonController>();
            fpc.head = head;
            fpc.stepClip = PSXSoundGenerator.Load("footstep");

            // Hand holding a lighter in the lower right of the view, the lighter standing upright facing the camera.
            // A small yaw shows a little of the back of the hand.
            var hand = new GameObject("LighterHand").transform;
            hand.SetParent(head, false);
            hand.localPosition = new Vector3(0.085f, -0.105f, 0.3f);
            hand.localRotation = Quaternion.Euler(8f, -6f, 0f);

            // Open right palm toward the camera, lighter pinched in front of it: fingers point left and curl toward
            // the camera round the lighter's left side (nails showing), the thumb comes up on the right to the wheel,
            // the forearm runs down-right out of view. Same pose as the team's reference frame.
            var arm = Arm(LighterGrip, hand);

            // Zippo standing in the curled fingers, turned a little to show its face: rounded-corner case, inner insert,
            // perforated chimney, wheel, and the lid swung open on its left-hand hinge.
            var zippo = new GameObject("Zippo").transform;
            zippo.SetParent(hand, false);
            zippo.localPosition = new Vector3(0.02f, 0f, -0.02f);
            zippo.localRotation = Quaternion.Euler(0f, -25f, 0f);
            // A tenth bigger than life, so it still reads at PS1 resolution; LighterGrip is fitted round this size.
            zippo.localScale = Vector3.one * 1.1f;
            var caseMb = new MeshBuilder();
            caseMb.ChamferBox(new Vector3(0, 0.0125f, 0), new Vector3(0.038f, 0.045f, 0.013f), 0.004f);
            caseMb.ChamferBox(new Vector3(0, 0.0395f, 0), new Vector3(0.034f, 0.01f, 0.011f), 0.002f);
            Place("Case", caseMb, "Chrome", Vector3.zero, default, zippo);
            Block("Seam", "DarkMetal", new Vector3(0, 0.0352f, 0), new Vector3(0.0385f, 0.0012f, 0.0135f), default, 4f, zippo);
            var chimneyMb = new MeshBuilder { FaceShade = false };
            chimneyMb.ChamferBox(new Vector3(-0.003f, 0.0424f, 0), new Vector3(0.024f, 0.0136f, 0.009f), 0.002f, 1f / 0.024f);
            Place("Chimney", chimneyMb, "Chimney", Vector3.zero, default, zippo);
            var wheelGo = Cyl("Wheel", "Steel", new Vector3(0.0125f, 0.047f, -0.004f), 0.0045f, 0.008f, 6, new Vector3(90, 0, 0), 4f, zippo);
            Cyl("Hinge", "Chrome", new Vector3(-0.019f, 0.035f, -0.006f), 0.0018f, 0.012f, 6, new Vector3(90, 0, 0), 4f, zippo);
            // Closed, the lid (a third of the height, as on the real thing) sits on top of the case and swallows the
            // whole chimney; the hand animator swings it about the hinge to open, to stand up beside the chimney.
            var lidMb = new MeshBuilder();
            lidMb.ChamferBox(new Vector3(0.019f, 0.0101f, 0), new Vector3(0.038f, 0.0202f, 0.013f), 0.004f);
            lidMb.ChamferBox(new Vector3(0.019f, 0.0012f, 0), new Vector3(0.0385f, 0.0024f, 0.0135f), 0.004f);
            var lidGo = Place("Lid", lidMb, "Chrome", new Vector3(-0.019f, 0.035f, 0), new Vector3(0, 0, 105), zippo);
            // Flint spark seen for a frame when the wheel is struck.
            var sparkMb = new MeshBuilder();
            sparkMb.Cross(Vector3.zero, 0.03f, 0.03f);
            var flintSpark = Place("FlintSpark", sparkMb, "Flame", new Vector3(0.004f, 0.05f, -0.004f), default, zippo);
            flintSpark.SetActive(false);

            // The flame in layers: a soft halo, the orange tongue, and a bright core low in it. They scale and lean
            // together under one pivot.
            var flame = new GameObject("Flame");
            flame.transform.SetParent(zippo, false);
            flame.transform.localPosition = new Vector3(-0.003f, 0.052f, 0f);
            var haloMb = new MeshBuilder();
            haloMb.Cross(new Vector3(0, -0.02f, 0), 0.085f, 0.085f);
            Place("Halo", haloMb, "FlameHalo", Vector3.zero, default, flame.transform);
            var tongueMb = new MeshBuilder();
            tongueMb.Cross(Vector3.zero, 0.03f, 0.068f);
            Place("Tongue", tongueMb, "Flame", Vector3.zero, default, flame.transform);
            var coreMb = new MeshBuilder();
            coreMb.Cross(new Vector3(0, 0.003f, 0), 0.014f, 0.036f);
            Place("Core", coreMb, "FlameCore", Vector3.zero, default, flame.transform);
            var lighterLight = new GameObject("LighterLight").AddComponent<Light>();
            // Hung off the camera ahead of the hand: at the flame itself inverse-square falloff would burn the hand
            // white (the hand gets its warmth from emission instead).
            lighterLight.transform.SetParent(head, false);
            lighterLight.transform.localPosition = new Vector3(0.12f, -0.02f, 0.85f);
            lighterLight.type = LightType.Point;
            lighterLight.color = new Color(1f, 0.68f, 0.38f);
            lighterLight.range = 5f;
            lighterLight.intensity = 1.1f;
            lighterLight.shadows = LightShadows.None;
            var flicker = flame.AddComponent<PSX.FlameFlicker>();
            flicker.flameLight = lighterLight;
            flicker.flame = flame.transform;
            flicker.baseIntensity = 1.1f;
            flicker.lean = 7f;

            // Sway, bob, breath, tremble, and lighting the Zippo by hand.
            var handAudio = hand.gameObject.AddComponent<AudioSource>();
            handAudio.playOnAwake = false;
            handAudio.spatialBlend = 0f;
            var anim = hand.gameObject.AddComponent<Game.HandAnimator>();
            anim.body = cc;
            anim.fingers = arm.fingers;
            anim.thumb = arm.thumb;
            anim.lid = lidGo.transform;
            anim.wheel = wheelGo.transform;
            anim.spark = flintSpark.transform;
            anim.flame = flicker;
            anim.audioSource = handAudio;
            anim.lidClip = PSXSoundGenerator.Load("zippo_lid");
            anim.closeClip = PSXSoundGenerator.Load("zippo_close");
            anim.strikeClip = PSXSoundGenerator.Load("zippo_strike");

            return new PlayerRig { controller = fpc, head = head, hand = hand, animator = anim, lighterLight = lighterLight };
        }

        // The eye: the main camera, drawn through the low-res PSX screen, and the ears.
        static Camera MakeEye(GameObject go)
        {
            var cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.AddComponent<AudioListener>();
            var screen = go.AddComponent<PSX.PSXScreen>();
            screen.screenMaterial = ScreenMaterial();
            screen.enabled = false; // OnEnable already ran without the material; rebuild the output with it
            screen.enabled = true;
            return cam;
        }

        // The TV and its menus, music and sounds, looking through the given eye.
        static Game.GameFlow BuildFlow(Transform head)
        {
            var flow = new GameObject("GameFlow").AddComponent<Game.GameFlow>();
            flow.head = head;
            flow.humClip = PSXSoundGenerator.Load("bunker_hum");
            flow.moveClip = PSXSoundGenerator.Load("ui_move");
            flow.pickClip = PSXSoundGenerator.Load("ui_pick");
            flow.typeClip = PSXSoundGenerator.Load("type_tick");
            flow.staticClip = PSXSoundGenerator.Load("tv_static");
            flow.titleMusic = PSXSoundGenerator.Load("music_title");
            flow.roomMusic = PSXSoundGenerator.Load("music_room");
            return flow;
        }

        // The room's rules (breaths of air, death, revival, restart) and everything round it (pause, settings, end card).
        static void BuildDirector(PlayerRig rig, Game.RoomPuzzle puzzle, int breaths, string startMessage, int room, string nextScene)
        {
            var director = new GameObject("RoomDirector").AddComponent<Game.RoomDirector>();
            director.breaths = breaths;
            director.startMessage = startMessage;
            director.controller = rig.controller;
            director.head = rig.head;
            director.hand = rig.hand;
            director.handAnimator = rig.animator;
            director.lighterLight = rig.lighterLight;
            director.puzzle = puzzle;
            director.door = _door;
            var display = _displayDigits.gameObject.AddComponent<Game.SegmentDisplay>();
            display.target = _displayDigits;
            director.display = display;
            director.exitZ = RoomMax.z + 1.6f;
            director.heartbeatClip = PSXSoundGenerator.Load("heartbeat");
            director.paperClip = PSXSoundGenerator.Load("paper");
            director.gaspClip = PSXSoundGenerator.Load("gasp");
            director.ambientClips = new[] { PSXSoundGenerator.Load("drip"), PSXSoundGenerator.Load("drip"), PSXSoundGenerator.Load("rumble") };
            director.corpseTemplate = CorpseTemplate();

            var flow = BuildFlow(rig.head);
            flow.director = director;
            flow.controller = rig.controller;
            flow.hand = rig.hand.gameObject;
            flow.room = room;
            flow.nextScene = nextScene;
            director.flow = flow;
        }

        static Material ScreenMaterial()
        {
            var m = new Material(Shader.Find("PSX/Screen"));
            return SaveMaterial("PSXScreen", m);
        }

        static void SetupRenderSettings()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.045f, 0.05f, 0.065f);
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 16f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
        }
    }
}
