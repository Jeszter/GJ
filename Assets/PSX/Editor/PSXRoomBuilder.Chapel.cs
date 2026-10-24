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
            "They built this chapel when the\n" +
            "mine still had men in it.\n\n" +
            "Four rooms under it, and every\n" +
            "one of them took me more than once.\n\n" +
            "The way up is chained. A round\n" +
            "would do. Or the other thing.\n\n" +
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

            var rig = BuildPlayer(new Vector3(0f, 0f, 1.0f), 0f);
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
            Block("Planks", "Wood", new Vector3(DoorHalfWidth, 1.2f, 0f), new Vector3(1.8f, 2.4f, 0.1f), default, 1f, leaf, true);
            foreach (float y in new[] { 0.4f, 1.2f, 2.0f })
                Block("Strap", "DarkMetal", new Vector3(0.7f, y, -0.06f), new Vector3(1.3f, 0.08f, 0.02f), default, 4f, leaf);
            Block("Ring", "DarkMetal", new Vector3(1.55f, 1.1f, -0.08f), new Vector3(0.1f, 0.1f, 0.02f), default, 4f, leaf);

            // The chain across from frame to frame, and the padlock that holds it.
            var chain = Block("Chain", "Chain", new Vector3(0f, 1.2f, -0.2f), new Vector3(1.9f, 0.04f, 0.03f), new Vector3(0, 0, 4), 6f, door);
            var padlock = Block("Padlock", "Steel", new Vector3(0.05f, 1.14f, -0.22f), new Vector3(0.11f, 0.13f, 0.05f), default, 8f, door);
            Block("Shackle", "Chrome", new Vector3(0.05f, 1.23f, -0.22f), new Vector3(0.07f, 0.06f, 0.015f), default, 8f, padlock.transform)
                .transform.localPosition = new Vector3(0f, 0.09f, 0f);
            padlock.AddComponent<BoxCollider>().size = new Vector3(0.22f, 0.26f, 0.12f);
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
                    Block("Back", "Wood", new Vector3(0, 0.78f, 0.2f), new Vector3(2.4f, 0.55f, 0.05f), new Vector3(-8, 0, 0), 2f, pew);
                    foreach (float x in new[] { -1.17f, 1.17f })
                        Block("End", "Wood", new Vector3(x, 0.48f, 0.05f), new Vector3(0.06f, 0.96f, 0.5f), default, 2f, pew);
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

            var note = Plate("Note_Chapel", "NoteDiary", new Vector3(-0.45f, 1.022f, 14.9f), 0.2f, 0.26f, Vector3.up, chapel, -6f);
            note.AddComponent<Game.ReadableNote>().text = NoteChapel;
            Helmet(new Vector3(-3.2f, 0f, 2.0f), 140f, chapel);
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
