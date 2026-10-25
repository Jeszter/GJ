using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // The miners' chapel past the corridor's last door: a stone nave with pews, an altar under a cross, coloured glass
    // lit from somewhere above, a hymn board, and at the far end the wooden door up to the surface, chained shut.
    // Boxes of rounds lie about, and a revolver on the altar for whoever came down without one.
    public static partial class PSXRoomBuilder
    {
        const string ChapelPath = "Assets/Scenes/Chapel.unity";

        const string NoteChapel =
            "The ones who gave up down here\n" +
            "never left. They put on the robes\n" +
            "and wait past that door for the\n" +
            "next one to give up too.\n\n" +
            "The way up is chained. A round\n" +
            "would do. Or the other thing.\n" +
            "Don't. That's how they began.\n\n" +
            "                        - M.";

        [MenuItem("GJ/Build Chapel")]
        public static void BuildChapelOnly()
        {
            if (!BeginBuild()) return;
            BuildChapel();
            FinishBuild();
        }

        static void BuildChapel()
        {
            RoomMin = new Vector3(-4f, 0f, 0f);
            RoomMax = new Vector3(4f, 5f, 18f);
            var scene = NewRoomScene("Chapel");
            BuildShell(new[] { 4.5f, 9f, 13.5f });
            // No corridor of white light behind this door: it opens on the night (ChapelOutside).
            foreach (var n in new[] { "Corridor", "ExitGlow", "ExitLight" })
            {
                var stale = GameObject.Find(n);
                if (stale != null) Object.DestroyImmediate(stale);
            }

            var chapel = new GameObject("Nave").transform;
            chapel.SetParent(_world, false);
            var puzzle = chapel.gameObject.AddComponent<Game.ChapelPuzzle>();
            ChapelDoor(puzzle);
            ChapelFurniture(chapel);
            puzzle.loot = ChapelLoot(chapel);
            puzzle.takeClip = PSXSoundGenerator.Load("rifle_bolt");
            puzzle.snapClip = PSXSoundGenerator.Load("glass_break");
            puzzle.bellClip = PSXSoundGenerator.Load("rumble");

            // The hymn board by the altar: its numbers (the time spent down here) are the director's display.
            var board = Group("HymnBoard", new Vector3(RoomMin.x + 0.06f, 2.0f, 13.0f), -90f, chapel);
            Block("Board", "Wood", new Vector3(0, 0, 0.03f), new Vector3(0.8f, 0.45f, 0.04f), default, 2f, board);
            var digits = new MeshBuilder { FaceShade = false };
            digits.Quad(new Vector3(-0.33f, -0.1f, 0f), new Vector3(0.66f, 0f, 0f), new Vector3(0f, 0.22f, 0f), 1f / 0.66f);
            _displayDigits = Place("DisplayDigits", digits, "DisplayDigits", Vector3.zero, default, board).GetComponent<Renderer>();

            AutoColliders(_world);
            ChapelOutside(chapel);
            // Beyond the door, waiting: the figure in the robe (after the auto colliders: it has its own capsule).
            puzzle.figure = RobedFigure(_world, new Vector3(0f, 0f, RoomMax.z + 2.6f), 180f);
            var doorway = new GameObject("Doorway").transform;
            doorway.SetParent(_world, false);
            doorway.localPosition = new Vector3(0f, 0f, RoomMax.z + 0.4f);
            puzzle.doorway = doorway;
            var dropped = Group("DroppedRevolver", Vector3.zero, 0f, _world);
            RevolverModel(dropped);
            puzzle.droppedGun = dropped.gameObject;
            puzzle.grabClip = PSXSoundGenerator.Load("chain");
            puzzle.clatterClip = PSXSoundGenerator.Load("clunk");
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            Dust(new Vector3(0f, 2.4f, 9f), new Vector3(7f, 4f, 17f), 90, life);

            var rig = BuildPlayer(new Vector3(0f, 0f, 2.0f), 0f);
            BuildDirector(rig, puzzle, 0, "THE MINERS' CHAPEL", 0, "");
            UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>().revolver = BuildRevolver(rig);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.24f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.045f, 0.06f);
            RenderSettings.fogStartDistance = 6f;
            RenderSettings.fogEndDistance = 32f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            EditorSceneManager.SaveScene(scene, ChapelPath);
            Debug.Log("PSX chapel built: " + ChapelPath);
        }

        // The way up: a plank door with iron straps on its hinge, chained across with a padlock in the middle.
        static void ChapelDoor(Game.ChapelPuzzle puzzle)
        {
            var door = new GameObject("ChapelDoor").transform;
            door.SetParent(_world, false);
            door.localPosition = new Vector3(0f, 0f, RoomMax.z);
            Block("Frame_Top", "Concrete", new Vector3(0, 2.55f, -0.12f), new Vector3(2.4f, 0.3f, 0.24f), default, 1f, door, true);
            Block("Frame_L", "Concrete", new Vector3(-1.05f, 1.2f, -0.12f), new Vector3(0.3f, 2.4f, 0.24f), default, 1f, door, true);
            Block("Frame_R", "Concrete", new Vector3(1.05f, 1.2f, -0.12f), new Vector3(0.3f, 2.4f, 0.24f), default, 1f, door, true);
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(door, false);
            leaf.localPosition = new Vector3(-DoorHalfWidth, 0f, -0.08f);
            var planks = Block("Planks", "Wood", new Vector3(DoorHalfWidth, 1.2f, 0f), new Vector3(1.8f, 2.4f, 0.1f), default, 1f, leaf, true);
            // A round anywhere into the door splinters the hasp: at PS1 resolution the lock alone is a few pixels.
            puzzle.doorTarget = planks.AddComponent<Game.Shootable>();
            foreach (float y in new[] { 0.4f, 1.2f, 2.0f })
                Block("Strap", "DarkMetal", new Vector3(0.7f, y, -0.06f), new Vector3(1.3f, 0.08f, 0.02f), default, 4f, leaf);
            Block("Ring", "DarkMetal", new Vector3(1.55f, 1.1f, -0.08f), new Vector3(0.1f, 0.1f, 0.02f), default, 4f, leaf);

            // The chain across from frame to frame, and the padlock that holds it.
            var chain = Block("Chain", "Chain", new Vector3(0f, 1.2f, -0.2f), new Vector3(1.9f, 0.04f, 0.03f), new Vector3(0, 0, 4), 6f, door);
            var padlock = Block("Padlock", "Steel", new Vector3(0.05f, 1.14f, -0.22f), new Vector3(0.11f, 0.13f, 0.05f), default, 8f, door);
            Block("Shackle", "Chrome", new Vector3(0.05f, 1.23f, -0.22f), new Vector3(0.07f, 0.06f, 0.015f), default, 8f, padlock.transform)
                .transform.localPosition = new Vector3(0f, 0.09f, 0f);
            padlock.AddComponent<BoxCollider>().size = new Vector3(0.32f, 0.34f, 0.16f);
            // The chain takes a round as well as the lock does.
            chain.AddComponent<BoxCollider>().size = new Vector3(1.9f, 0.1f, 0.1f);
            puzzle.chainTarget = chain.AddComponent<Game.Shootable>();
            puzzle.padlock = padlock.AddComponent<Game.Shootable>();
            puzzle.padlockPiece = padlock;
            puzzle.chain = chain;

            _door = door.gameObject.AddComponent<Game.VaultDoor>();
            _door.leaf = leaf;
            _door.openAngle = -105f;
            _door.openClip = PSXSoundGenerator.Load("door_open");
        }

        // Pews down both sides of the aisle, the altar under a cross, coloured windows, candles.
        static void ChapelFurniture(Transform chapel)
        {
            for (int row = 0; row < 6; row++)
            {
                float z = 4f + row * 1.35f;
                foreach (float side in new[] { -1f, 1f })
                {
                    var pew = Group("Pew", new Vector3(2.2f * side, 0f, z), 0f, chapel);
                    Block("Seat", "Wood", new Vector3(0, 0.45f, 0), new Vector3(2.4f, 0.06f, 0.42f), default, 2f, pew);
                    // Facing the altar: the back rest behind whoever sits, toward the way in.
                    Block("Back", "Wood", new Vector3(0, 0.78f, -0.2f), new Vector3(2.4f, 0.55f, 0.05f), new Vector3(-8, 0, 0), 2f, pew);
                    foreach (float x in new[] { -1.17f, 1.17f })
                        Block("End", "Wood", new Vector3(x, 0.48f, -0.05f), new Vector3(0.06f, 0.96f, 0.5f), default, 2f, pew);
                }
            }

            // The altar: a stone block with a red cloth, a cross on the wall above the door behind it.
            Block("Altar", "Concrete", new Vector3(0f, 0.5f, 15f), new Vector3(2.2f, 1.0f, 0.9f), default, 1f, chapel, true);
            Block("AltarCloth", "Blanket", new Vector3(0f, 1.01f, 15f), new Vector3(2.3f, 0.02f, 0.95f), default, 2f, chapel);
            Block("AltarDrape", "Blanket", new Vector3(0f, 0.7f, 14.535f), new Vector3(1.4f, 0.6f, 0.01f), default, 2f, chapel);
            Block("Cross_Upright", "Wood", new Vector3(0f, 3.6f, RoomMax.z - 0.05f), new Vector3(0.14f, 1.6f, 0.08f), default, 2f, chapel);
            Block("Cross_Bar", "Wood", new Vector3(0f, 3.95f, RoomMax.z - 0.06f), new Vector3(0.9f, 0.14f, 0.08f), default, 2f, chapel);

            // Candles on the altar; only one of them gets a light, the other burns as a card.
            foreach (float x in new[] { -0.8f, 0.8f })
                Cyl("Candle", "Paper", new Vector3(x, 1.02f, 15.1f), 0.035f, 0.22f, 6, default, 8f, chapel);
            SetFlame(chapel, new Vector3(-0.8f, 1.27f, 15.1f), 0.06f, 8f, 2.6f);
            var card = new MeshBuilder();
            card.Cross(Vector3.zero, 0.03f, 0.06f);
            Place("Flame", card, "Flame", new Vector3(0.8f, 1.27f, 15.1f), default, chapel);
            // A candle stand at the head of the aisle, by the way in.
            Cyl("StandFoot", "DarkMetal", new Vector3(1.0f, 0f, 2.2f), 0.18f, 0.04f, 8, default, 2f, chapel);
            Cyl("StandPost", "DarkMetal", new Vector3(1.0f, 0f, 2.2f), 0.025f, 1.2f, 6, default, 2f, chapel);
            Cyl("StandCandle", "Paper", new Vector3(1.0f, 1.2f, 2.2f), 0.04f, 0.25f, 6, default, 8f, chapel);
            SetFlame(chapel, new Vector3(1.0f, 1.48f, 2.2f), 0.07f, 9f, 2.8f);

            // Coloured glass high in the far wall either side of the door, lit as if by a day far above.
            foreach (float x in new[] { -2.5f, 2.5f })
            {
                var glass = new MeshBuilder { FaceShade = false };
                glass.Quad(new Vector3(x + 0.45f, 2.2f, RoomMax.z - 0.02f), new Vector3(-0.9f, 0f, 0f), new Vector3(0f, 2.0f, 0f), 1f / 0.9f);
                Place("StainedGlass", glass, "StainedGlass", Vector3.zero, default, chapel);
                Block("GlassFrame", "DarkMetal", new Vector3(x, 4.25f, RoomMax.z - 0.03f), new Vector3(1.0f, 0.1f, 0.04f), default, 4f, chapel);
                Block("GlassFrame", "DarkMetal", new Vector3(x, 2.15f, RoomMax.z - 0.03f), new Vector3(1.0f, 0.1f, 0.04f), default, 4f, chapel);
            }
            PointLight("GlassLight", new Vector3(0f, 3.4f, RoomMax.z - 1.2f), new Color(0.55f, 0.6f, 1f), 14f, 3.2f);

            // Two iron candle rings hung over the aisle on chains.
            foreach (float z in new[] { 6.6f, 11.2f })
            {
                var ring = Group("CandleRing", new Vector3(0f, 3.6f, z), 0f, chapel);
                Block("HangChain", "Chain", new Vector3(0f, (RoomMax.y - 3.6f) * 0.5f, 0f), new Vector3(0.03f, RoomMax.y - 3.6f, 0.03f), default, 6f, ring);
                var hoop = new MeshBuilder();
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i / 8f * Mathf.PI * 2f, a1 = (i + 1) / 8f * Mathf.PI * 2f;
                    hoop.Tube(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.45f, new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.45f, 0.02f, 0.02f, 4, Vector3.up, 4f);
                }
                Place("Hoop", hoop, "DarkMetal", Vector3.zero, default, ring);
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    var at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.45f;
                    Cyl("RingCandle", "Paper", at, 0.025f, 0.14f, 6, default, 8f, ring);
                    var flame = new MeshBuilder();
                    flame.Cross(Vector3.zero, 0.025f, 0.05f);
                    Place("Flame", flame, "Flame", at + Vector3.up * 0.17f, default, ring);
                }
                var halo = new MeshBuilder();
                halo.Cross(Vector3.zero, 1.2f, 1.2f);
                Place("RingHalo", halo, "FlameHalo", new Vector3(0f, 0.15f, 0f), default, ring);
                PointLight("RingLight", new Vector3(0f, 3.55f, z), new Color(1f, 0.72f, 0.42f), 8f, 2.4f);
            }

            ChapelDressing(chapel);
            ChapelEntrance(chapel);

            var note = Plate("Note_Chapel", "NoteDiary", new Vector3(-0.45f, 1.022f, 14.9f), 0.2f, 0.26f, Vector3.up, chapel, -6f);
            note.AddComponent<Game.ReadableNote>().text = NoteChapel;
            Helmet(new Vector3(-3.2f, 0f, 2.0f), 140f, chapel);
        }

        // The way you came in, behind you at the start: a pair of plank doors in a stone frame with the corridor's stencil
        // over them, buried to the lintel: the roof came down behind you, rocks and a slab and two beams across them.
        static void ChapelEntrance(Transform chapel)
        {
            var g = Group("Entrance", new Vector3(0f, 0f, 0f), 0f, chapel);
            Block("Frame_Top", "Concrete", new Vector3(0f, 2.55f, 0.12f), new Vector3(2.4f, 0.3f, 0.24f), default, 1f, g, true);
            foreach (float s in new[] { -1f, 1f })
            {
                Block("Frame_Side", "Concrete", new Vector3(1.05f * s, 1.2f, 0.12f), new Vector3(0.3f, 2.4f, 0.24f), default, 1f, g, true);
                Block("Leaf", "Wood", new Vector3(0.45f * s, 1.2f, 0.05f), new Vector3(0.88f, 2.38f, 0.08f), default, 1f, g, true);
                foreach (float y in new[] { 0.45f, 1.95f })
                    Block("Strap", "DarkMetal", new Vector3(0.45f * s, y, 0.1f), new Vector3(0.8f, 0.07f, 0.015f), default, 4f, g);
                Cyl("Ring", "DarkMetal", new Vector3(0.12f * s, 1.15f, 0.1f), 0.05f, 0.015f, 8, new Vector3(90, 0, 0), 4f, g);
            }
            Block("Seam", "Rubber", new Vector3(0f, 1.2f, 0.095f), new Vector3(0.02f, 2.38f, 0.005f), default, 4f, g);
            Plate("Sign_Sector", "SignSector", new Vector3(0f, 2.95f, 0.012f), 1.0f, 0.25f, Vector3.forward, g, 4f);

            // The heap: rocks piled highest against the doors and spilling out onto the floor.
            var rocks = new MeshBuilder();
            for (int i = 0; i < 95; i++)
            {
                float x = (Hash(i, 0, 301) - 0.5f) * 3.4f;
                float z = 0.08f + Hash(i, 1, 302) * 1.0f;
                float top = Mathf.Max(0.15f, 2.75f * (1f - z / 1.15f) * (1f - Mathf.Abs(x) / 2.3f));
                float y = Hash(i, 2, 303) * top;
                float s = 0.18f + Hash(i, 3, 304) * 0.3f;
                rocks.M = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(Hash(i, 4, 305) * 90f, Hash(i, 5, 306) * 180f, Hash(i, 6, 307) * 90f), Vector3.one);
                rocks.ChamferBox(Vector3.zero, new Vector3(s, s * (0.6f + Hash(i, 7, 308) * 0.5f), s * (0.7f + Hash(i, 8, 309) * 0.4f)), s * 0.2f, 2f);
            }
            rocks.M = Matrix4x4.identity;
            var heap = Place("Rubble", rocks, "Stone", Vector3.zero, default, g);
            var block = heap.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, 1.0f, 0.35f);
            block.size = new Vector3(3.4f, 2.0f, 0.7f);
            Block("FallenSlab", "Concrete", new Vector3(0.5f, 1.3f, 0.55f), new Vector3(1.6f, 0.16f, 1.0f), new Vector3(-48f, 12f, 8f), 1f, g);
            Block("Beam", "Wood", new Vector3(-0.2f, 1.35f, 0.45f), new Vector3(0.16f, 0.16f, 3.0f), new Vector3(0f, 78f, 38f), 2f, g);
            Block("Beam", "Wood", new Vector3(0.4f, 0.9f, 0.7f), new Vector3(0.14f, 0.14f, 2.4f), new Vector3(-12f, -62f, -25f), 2f, g);
            // Grit and pebbles spread out a step further, and a dark hole in the ceiling above where it came from.
            var grit = new MeshBuilder();
            for (int i = 0; i < 40; i++)
            {
                var at = new Vector3((Hash(i, 0, 311) - 0.5f) * 4f, 0.02f, 1.0f + Hash(i, 1, 312) * 1.0f);
                float s = 0.04f + Hash(i, 2, 313) * 0.08f;
                grit.Box(at, new Vector3(s, s * 0.6f, s), 4f);
            }
            Place("Grit", grit, "Stone", Vector3.zero, default, g);
            var hole = new MeshBuilder { FaceShade = false };
            hole.Quad(new Vector3(-1.4f, RoomMax.y - 0.01f, 0.05f), new Vector3(0f, 0f, 1.4f), new Vector3(2.8f, 0f, 0f), 1f);
            Place("CeilingHole", hole, "Rubber", Vector3.zero, default, g);
            Dust(new Vector3(0f, 1.6f, 0.9f), new Vector3(3f, 2.8f, 1.2f), 25, g);
        }

        // What the far door opens on: the night behind the chapel. Wet grass, a flagged path out to a lamp post, the
        // first crooked graves of the yard, ground mist, pines black against a fogged moon. Built after the auto
        // colliders; only the ground under the door can be stood on.
        static void ChapelOutside(Transform chapel)
        {
            var g = Group("Outside", Vector3.zero, 0f, chapel);
            var ground = new MeshBuilder { FaceShade = false };
            ground.Quad(new Vector3(-20f, -0.01f, RoomMax.z), new Vector3(40f, 0f, 0f), new Vector3(0f, 0f, 40f), 0.4f, 2f);
            Place("Ground", ground, "Grass", Vector3.zero, default, g, true);
            for (int i = 0; i < 8; i++)
                Block("Flag", "Concrete", new Vector3((Hash(i, 0, 321) - 0.5f) * 0.25f, 0.01f, RoomMax.z + 0.8f + i * 1.1f), new Vector3(0.9f, 0.03f, 0.75f), new Vector3(0f, (Hash(i, 1, 322) - 0.5f) * 14f, 0f), 1f, g);
            for (int i = 0; i < 26; i++)
            {
                float x = (Hash(i, 0, 323) - 0.5f) * 26f;
                float z = RoomMax.z + 4f + Hash(i, 1, 324) * 22f;
                if (Mathf.Abs(x) < 1.8f) continue;
                var grave = Group("Grave", new Vector3(x, 0f, z), (Hash(i, 2, 325) - 0.5f) * 20f, g);
                float lean = (Hash(i, 3, 326) - 0.5f) * 24f;
                if (Hash(i, 4, 327) > 0.45f)
                {
                    Block("Stone", "Concrete", new Vector3(0f, 0.45f, 0f), new Vector3(0.62f, 0.9f, 0.13f), new Vector3(lean, 0, 0), 1f, grave);
                    Cyl("Top", "Concrete", new Vector3(-0.31f, 0.9f, 0f), 0.31f, 0.13f, 8, new Vector3(lean, 0, -90), 1f, grave);
                }
                else
                {
                    Block("Cross", "Wood", new Vector3(0f, 0.65f, 0f), new Vector3(0.1f, 1.3f, 0.1f), new Vector3(lean, 0, 0), 2f, grave);
                    Block("CrossBar", "Wood", new Vector3(0f, 1.0f, 0f), new Vector3(0.58f, 0.09f, 0.09f), new Vector3(lean, 0, 0), 2f, grave);
                }
                Block("Mound", "Grass", new Vector3(0f, 0.05f, -0.8f), new Vector3(0.85f, 0.12f, 1.55f), default, 1f, grave);
            }
            Lantern(new Vector3(-1.5f, 0f, RoomMax.z + 5f), g);
            PointLight("LampLight", new Vector3(-1.1f, 2.45f, RoomMax.z + 5f), new Color(1f, 0.72f, 0.42f), 9f, 2.4f);
            PointLight("Moonlight", new Vector3(3f, 7f, RoomMax.z + 12f), new Color(0.5f, 0.6f, 0.9f), 24f, 3f);
            // Moonlight on the step outside, so the open door shows the night and not a black hole.
            PointLight("DoorMoon", new Vector3(0.5f, 3.5f, RoomMax.z + 3.5f), new Color(0.55f, 0.65f, 0.95f), 10f, 2.2f);
            var pines = new MeshBuilder();
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3((Hash(i, 0, 328) - 0.5f) * 60f, 0f, RoomMax.z + 26f + Hash(i, 1, 329) * 20f);
                float h = 7f + Hash(i, 2, 330) * 7f;
                pines.Tube(p, p + Vector3.up * h * 0.25f, 0.18f, 0.14f, 5, Vector3.forward, 1f);
                for (int k = 0; k < 3; k++)
                    pines.Tube(p + Vector3.up * h * (0.2f + k * 0.25f), p + Vector3.up * h * (0.55f + k * 0.22f), h * (0.24f - k * 0.06f), 0.02f, 7, Vector3.forward, 0.5f);
            }
            Place("Pines", pines, "Pine", Vector3.zero, default, g);
            var moon = new MeshBuilder();
            moon.Card(new Vector3(-12f, 13f, RoomMax.z + 60f), new Vector3(8f, 0f, 0f), new Vector3(0f, 8f, 0f));
            Place("Moon", moon, "MoonGlow", Vector3.zero, default, g);
            var mist = new MeshBuilder { FaceShade = false };
            for (int i = 0; i < 16; i++)
            {
                var c = new Vector3((Hash(i, 0, 331) - 0.5f) * 20f, 0.3f + Hash(i, 1, 332) * 0.4f, RoomMax.z + 3f + Hash(i, 2, 333) * 22f);
                var u = Quaternion.Euler(0f, Hash(i, 3, 334) * 180f, 0f) * Vector3.right * (4f + Hash(i, 4, 335) * 4f);
                mist.Card(c - u * 0.5f, u, Vector3.up * 1.2f);
            }
            Place("GroundMist", mist, "Steam", Vector3.zero, default, g);
        }

        // The finery: a runner up the aisle, a rail before the altar, banners and icons on the walls, a rack of votive
        // candles, candles on every pillar, and a stone font by the way in.
        static void ChapelDressing(Transform chapel)
        {
            Block("Runner", "Carpet", new Vector3(0f, 0.006f, 7.0f), new Vector3(1.3f, 0.012f, 12.6f), default, 1f / 1.3f, chapel);

            // The altar rail: turned posts and a top rail either side of the gap in the middle.
            foreach (float side in new[] { -1f, 1f })
            {
                Block("RailTop", "Wood", new Vector3(side * 2.2f, 0.9f, 13.7f), new Vector3(2.9f, 0.08f, 0.12f), default, 2f, chapel, true);
                Block("RailFoot", "Wood", new Vector3(side * 2.2f, 0.05f, 13.7f), new Vector3(2.9f, 0.1f, 0.18f), default, 2f, chapel);
                for (int i = 0; i < 6; i++)
                    Cyl("RailPost", "Wood", new Vector3(side * (0.85f + i * 0.55f), 0.1f, 13.7f), 0.035f, 0.8f, 6, default, 4f, chapel);
            }

            // Banners hung between the pillars down both walls, icons either side of the altar.
            foreach (float side in new[] { -1f, 1f })
            {
                float wall = side < 0 ? RoomMin.x + 0.015f : RoomMax.x - 0.015f;
                var facing = side < 0 ? Vector3.right : Vector3.left;
                foreach (float z in new[] { 2.3f, 6.8f, 11.3f })
                {
                    Plate("Banner", "Banner", new Vector3(wall, 3.1f, z), 0.9f, 1.8f, facing, chapel);
                    Block("BannerRod", "DarkMetal", new Vector3(wall + side * -0.05f, 4.05f, z), new Vector3(0.04f, 0.04f, 1.1f), default, 4f, chapel);
                }
                Plate("Icon", "Icon", new Vector3(wall, 2.0f, 15.6f), 0.6f, 0.8f, facing, chapel);
            }

            // Candles in iron cups on every pillar, flames as cards (the rings and the altar give the light).
            foreach (float side in new[] { -1f, 1f })
                foreach (float z in new[] { 4.5f, 9f, 13.5f })
                {
                    float x = side * (RoomMax.x - 0.2f);
                    Block("Sconce", "DarkMetal", new Vector3(x, 2.2f, z), new Vector3(0.12f, 0.04f, 0.12f), default, 4f, chapel);
                    Cyl("SconceCandle", "Paper", new Vector3(x, 2.22f, z), 0.03f, 0.16f, 6, default, 8f, chapel);
                    var flame = new MeshBuilder();
                    flame.Cross(Vector3.zero, 0.03f, 0.06f);
                    Place("Flame", flame, "Flame", new Vector3(x, 2.42f, z), default, chapel);
                    var halo = new MeshBuilder();
                    halo.Cross(Vector3.zero, 0.5f, 0.5f);
                    Place("Halo", halo, "FlameHalo", new Vector3(x, 2.4f, z), default, chapel);
                }

            // A rack of votive candles by the left wall near the altar, three steps of them.
            var rack = Group("VotiveRack", new Vector3(RoomMin.x + 0.45f, 0f, 12.2f), 90f, chapel);
            for (int step = 0; step < 3; step++)
            {
                Block("Step", "DarkMetal", new Vector3(0f, 0.75f + step * 0.12f, -0.12f + step * 0.12f), new Vector3(1.0f, 0.03f, 0.14f), default, 4f, rack);
                for (int i = 0; i < 6; i++)
                {
                    var at = new Vector3(-0.42f + i * 0.17f, 0.78f + step * 0.12f, -0.12f + step * 0.12f);
                    Cyl("Votive", "Paper", at, 0.022f, 0.05f, 6, default, 8f, rack);
                    if ((i + step) % 4 == 3) continue;   // a few gone out
                    var flame = new MeshBuilder();
                    flame.Cross(Vector3.zero, 0.02f, 0.04f);
                    Place("Flame", flame, "Flame", at + Vector3.up * 0.08f, default, rack);
                }
            }
            foreach (float x in new[] { -0.45f, 0.45f })
                Block("RackLeg", "DarkMetal", new Vector3(x, 0.38f, 0f), new Vector3(0.03f, 0.76f, 0.03f), default, 4f, rack);
            var votiveHalo = new MeshBuilder();
            votiveHalo.Cross(Vector3.zero, 1.2f, 0.8f);
            Place("Halo", votiveHalo, "FlameHalo", new Vector3(0f, 1.0f, 0f), default, rack);

            // The font by the way in: a stone bowl on a pillar.
            Cyl("FontFoot", "Concrete", new Vector3(-1.4f, 0f, 1.6f), 0.2f, 0.85f, 8, default, 2f, chapel, true);
            var bowl = new MeshBuilder();
            OpenDrum(bowl, new Vector3(-1.4f, 0.85f, 1.6f), 0.32f, 0.16f, 10, 0.04f, 2f);
            Place("FontBowl", bowl, "Concrete", Vector3.zero, default, chapel);
            var water = new MeshBuilder { FaceShade = false };
            water.Cylinder(new Vector3(-1.4f, 0.96f, 1.6f), 0.27f, 0.004f, 10, 1f);
            Place("FontWater", water, "SightWater", Vector3.zero, default, chapel);
        }

        // What lies about: two boxes of rounds and a revolver on the altar.
        static Game.ChapelPuzzle.Loot[] ChapelLoot(Transform chapel)
        {
            Game.ChapelPuzzle.Loot Box(string name, Vector3 at, float yaw, int rounds, string line)
            {
                var box = Group(name, at, yaw, chapel);
                Block("Box", "Gunmetal", new Vector3(0, 0.03f, 0), new Vector3(0.14f, 0.06f, 0.09f), default, 8f, box);
                Block("Label", "Paper", new Vector3(0, 0.061f, 0), new Vector3(0.1f, 0.002f, 0.06f), default, 8f, box);
                box.localScale = Vector3.one * 1.4f;
                return new Game.ChapelPuzzle.Loot { item = box.gameObject, point = box, prompt = "[E] take the box of rounds", rounds = rounds, line = line };
            }
            var gun = Group("AltarRevolver", new Vector3(0.35f, 1.04f, 14.95f), 0f, chapel);
            var model = Group("Model", Vector3.zero, 0f, gun);
            model.localRotation = Quaternion.Euler(0f, 70f, 90f);
            RevolverModel(model);
            // A shelf by the right wall near the altar for the second box.
            Block("Shelf", "Wood", new Vector3(RoomMax.x - 0.2f, 0.9f, 12.0f), new Vector3(0.35f, 0.04f, 0.6f), default, 2f, chapel);
            return new[]
            {
                Box("RoundsPew", new Vector3(-2.0f, 0.48f, 6.7f), 20f, 2, "a box of shells. two rounds in it"),
                Box("RoundsShelf", new Vector3(RoomMax.x - 0.2f, 0.92f, 12.0f), -10f, 3, "a box of shells. three rounds in it"),
                new Game.ChapelPuzzle.Loot
                {
                    item = gun.gameObject, point = gun, prompt = "[E] take the revolver from the altar", rounds = 3, revolver = true,
                    line = "a revolver someone left on the altar. three rounds\n[Q] draw   [LMB] fire   [X] the other way"
                }
            };
        }
    }
}
