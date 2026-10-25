using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // Room 4, the boiler room: a pot on a stand feeds steam to a piston that draws the door's bolt back; the bolt's
    // magnetic coil has its own line, cut, its live end hanging over two tanks joined by a pipe.
    public static partial class PSXRoomBuilder
    {
        const string Scene4Path = "Assets/Scenes/Room04.unity";
        static readonly Vector3 PotAt = new Vector3(-2.2f, 0f, 3.6f);
        const float PotRadius = 0.32f;

        const string NoteDay9 =
            "DAY 9\n\n" +
            "The boiler. The bolt runs on steam:\n" +
            "water in the pot, the relief valve\n" +
            "shut, my lighter under it. It boils\n" +
            "slower than I can hold my breath.\n\n" +
            "I've stopped counting the times.\n" +
            "I leave these for whoever comes\n" +
            "down next. I think it's me.";

        const string NoteLine =
            "The lock's coil has its own line.\n" +
            "Somebody cut it over the tanks.\n\n" +
            "Water carries current.\n" +
            "Don't be the one holding the\n" +
            "other end.\n\n" +
            "                        - M.";

        [MenuItem("GJ/Build PSX Room 4")]
        public static void BuildFourth()
        {
            if (!BeginBuild()) return;
            BuildRoom4();
            FinishBuild();
        }

        static void BuildRoom4()
        {
            RoomMin = new Vector3(-3f, 0f, 0f);
            RoomMax = new Vector3(3f, 3.4f, 7.5f);
            var scene = NewRoomScene("Room04");

            BuildShell(new[] { 2.0f, 5.2f });
            BuildDoor();
            if (_door.flash != null) Object.DestroyImmediate(_door.flash.gameObject);
            _door.flash = null;
            _door.blastClip = null;

            var works = new GameObject("Boiler").transform;
            works.SetParent(_world, false);
            var puzzle = works.gameObject.AddComponent<Game.BoilerPuzzle>();
            BuildBoilerPot(puzzle, works);
            BuildBoilerDoorWorks(puzzle, works);
            BuildBoilerLine(puzzle, works);
            puzzle.valveClip = PSXSoundGenerator.Load("valve");
            puzzle.clunkClip = PSXSoundGenerator.Load("clunk");
            puzzle.plugClip = PSXSoundGenerator.Load("ui_pick");
            puzzle.zapClip = PSXSoundGenerator.Load("buzz");
            puzzle.takeClip = PSXSoundGenerator.Load("zippo_close");
            BuildRoom4Props();

            // One lamp hanging in the middle of the room.
            var lamp = Group("Lamp", new Vector3(0.2f, RoomMax.y, 3.6f), 0f, _world);
            Block("Cord", "DarkMetal", new Vector3(0, -0.3f, 0), new Vector3(0.02f, 0.6f, 0.02f), default, 1f, lamp);
            Cyl("Shade", "DarkMetal", new Vector3(0, -0.7f, 0), 0.24f, 0.1f, 8, default, 1f, lamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.76f, 0), new Vector3(0.09f, 0.11f, 0.09f), default, 1f, lamp);
            var halo = new MeshBuilder();
            halo.Cross(Vector3.zero, 0.6f, 0.6f);
            Place("BulbHalo", halo, "Halo", new Vector3(0, -1.1f, 0), default, lamp);
            PointLight("LampLight", new Vector3(0.2f, RoomMax.y - 0.9f, 3.6f), new Color(1f, 0.82f, 0.6f), 10f, 3.4f);
            PointLight("DoorLight", new Vector3(0f, 2.6f, RoomMax.z - 1.2f), new Color(0.75f, 0.8f, 0.9f), 4.5f, 0.9f);
            // A caged bulb on the wall by the boiler, so the valves can be found with the lighter under the pot.
            WallLamp(new Vector3(RoomMin.x + 0.06f, 2.35f, 4.55f), Vector3.right, _world);
            PointLight("BoilerLamp", new Vector3(RoomMin.x + 0.4f, 2.2f, 4.55f), new Color(1f, 0.8f, 0.55f), 5f, 1.5f);

            AutoColliders(_world);
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            Dust(new Vector3(0f, 1.8f, 3.7f), new Vector3(5.5f, 2.8f, 7f), 60, life);
            SteamLeak(new Vector3(-2.2f, 2.95f, 6.2f), new Vector3(0.3f, -0.2f, -1f), life);

            var rig = BuildPlayer(new Vector3(0.4f, 0f, 0.9f), 0f);
            BuildDirector(rig, puzzle, 3, "SECTOR 4 - BOILER ROOM\nyou have three breaths", 4, "Hub");
            UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>().revolver = BuildRevolver(rig);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.13f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.045f, 0.04f);
            RenderSettings.fogStartDistance = 3f;
            RenderSettings.fogEndDistance = 14f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            EditorSceneManager.SaveScene(scene, Scene4Path);
            Debug.Log("PSX room built: " + Scene4Path);
        }

        // The pot on its stand, its water pipe and valve, the sight glass, the burner grate, the relief valve.
        static void BuildBoilerPot(Game.BoilerPuzzle puzzle, Transform works)
        {
            var pot = Group("Pot", PotAt, 0f, works);
            foreach (float lx in new[] { -0.26f, 0.26f })
                foreach (float lz in new[] { -0.26f, 0.26f })
                    Block("Leg", "DarkMetal", new Vector3(lx, 0.39f, lz), new Vector3(0.05f, 0.78f, 0.05f), default, 2f, pot);
            Cyl("Ring", "DarkMetal", new Vector3(0, 0.74f, 0), PotRadius + 0.03f, 0.04f, 10, default, 1f, pot);
            Block("Grate", "Grate", new Vector3(0, 0.33f, 0), new Vector3(0.46f, 0.02f, 0.46f), default, 1f / 0.46f, pot);
            Cyl("Body", "DarkMetal", new Vector3(0, 0.78f, 0), PotRadius, 0.6f, 12, default, 1f, pot);
            foreach (float y in new[] { 0.9f, 1.25f })
                Cyl("Band", "Steel", new Vector3(0, y, 0), PotRadius + 0.012f, 0.035f, 12, default, 2f, pot);
            var lid = new MeshBuilder();
            lid.Tube(new Vector3(0, 1.38f, 0), new Vector3(0, 1.48f, 0), PotRadius + 0.01f, 0.1f, 12, Vector3.forward, 2f);
            Place("Lid", lid, "DarkMetal", Vector3.zero, default, pot);

            // Sight glass on the side toward the room: a water column in a cage between two caps.
            var glass = new Vector3(PotRadius + 0.06f, 0f, 0.14f);
            Block("GlassCap_Low", "DarkMetal", glass + new Vector3(0, 0.82f, 0), new Vector3(0.07f, 0.03f, 0.07f), default, 4f, pot);
            Block("GlassCap_High", "DarkMetal", glass + new Vector3(0, 1.34f, 0), new Vector3(0.07f, 0.03f, 0.07f), default, 4f, pot);
            Block("GlassGuard", "Steel", glass + new Vector3(0.025f, 1.08f, 0), new Vector3(0.008f, 0.52f, 0.008f), default, 4f, pot);
            Block("GlassStub_Low", "Steel", new Vector3(PotRadius + 0.02f, 0.82f, 0.14f), new Vector3(0.06f, 0.02f, 0.02f), default, 4f, pot);
            Block("GlassStub_High", "Steel", new Vector3(PotRadius + 0.02f, 1.34f, 0.14f), new Vector3(0.06f, 0.02f, 0.02f), default, 4f, pot);
            var level = new GameObject("PotLevel").transform;
            level.SetParent(pot, false);
            level.localPosition = glass + new Vector3(0, 0.835f, 0);
            Block("Column", "SightWater", new Vector3(0, 0.245f, 0), new Vector3(0.03f, 0.49f, 0.03f), default, 4f, level);
            puzzle.potLevel = level;

            // The burner grate, and the lighter that can be left standing on it.
            var burner = new GameObject("BurnerPoint").transform;
            burner.SetParent(pot, false);
            burner.localPosition = new Vector3(0, 0.38f, 0);
            puzzle.burnerPoint = burner;
            puzzle.leftLighter = BurningLighter(pot, new Vector3(0.02f, 0.34f, 0.04f));
            var potSteam = SteamLeak(PotAt + new Vector3(0.12f, 1.47f, 0.05f), Vector3.up, works);
            puzzle.potSteam = Quiet(potSteam, out puzzle.potHiss);

            // Water in from the wall, through a valve, over the rim of the lid.
            PipeRun("Feed", "Rust", new[]
            {
                new Vector3(-2.95f, 3.0f, 3.0f), new Vector3(-2.95f, 1.6f, 3.0f), new Vector3(-2.45f, 1.6f, 3.0f),
                new Vector3(-2.45f, 1.6f, 3.45f), new Vector3(-2.45f, 1.44f, 3.45f)
            }, 0.035f, works);
            Cyl("FeedValve_Body", "DarkMetal", new Vector3(-2.95f, 1.98f, 3.0f), 0.06f, 0.18f, 8, default, 2f, works);
            Cyl("FeedValve_Stem", "Steel", new Vector3(-2.92f, 2.07f, 3.0f), 0.015f, 0.12f, 6, new Vector3(0, 0, -90), 2f, works);
            var feed = new GameObject("FeedValve").transform;
            feed.SetParent(works, false);
            feed.localPosition = new Vector3(-2.8f, 2.07f, 3.0f);
            feed.localRotation = Quaternion.Euler(0, 0, -90);
            ValveWheel(feed, 0.1f, "Steel");
            puzzle.feedValve = feed;
            Plate("Sign_Water", "SignWater", new Vector3(RoomMin.x + 0.012f, 2.4f, 3.0f), 0.3f, 0.1f, Vector3.right, works);

            // Steam out of the lid, up and along the ceiling to the piston by the door, with the relief valve on the
            // way up: a red wheel, and a short stack that screams when it's open.
            PipeRun("Steam", "DarkMetal", new[]
            {
                PotAt + new Vector3(0, 1.46f, 0), PotAt + new Vector3(0, 3.0f, 0), new Vector3(-2.2f, 3.0f, 7.25f),
                new Vector3(-2.6f, 3.0f, 7.25f), new Vector3(-2.6f, 1.2f, 7.25f), new Vector3(-2.5f, 1.2f, 7.25f)
            }, 0.04f, works, false);
            Block("Relief_Body", "RedPaint", new Vector3(-2.12f, 2.0f, 3.6f), new Vector3(0.1f, 0.12f, 0.1f), default, 4f, works);
            Cyl("Relief_Stack", "DarkMetal", new Vector3(-2.1f, 2.06f, 3.6f), 0.025f, 0.22f, 6, default, 2f, works);
            var relief = new GameObject("ReliefValve").transform;
            relief.SetParent(works, false);
            relief.localPosition = new Vector3(-1.99f, 2.0f, 3.6f);
            relief.localRotation = Quaternion.Euler(0, 0, -90);
            ValveWheel(relief, 0.08f, "RedPaint");
            puzzle.reliefValve = relief;
            Plate("Sign_Relief", "SignRelief", new Vector3(-2.15f, 2.32f, 3.6f), 0.18f, 0.06f, Vector3.right, works);
            var reliefSteam = SteamLeak(new Vector3(-2.1f, 2.3f, 3.6f), Vector3.up, works);
            puzzle.reliefSteam = Quiet(reliefSteam, out puzzle.reliefHiss);
        }

        // A steam jet kept off until the puzzle opens it: no prewarmed puff and no hiss at load.
        static ParticleSystem Quiet(ParticleSystem ps, out AudioSource hiss)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.prewarm = false;
            hiss = ps.GetComponent<AudioSource>();
            hiss.playOnAwake = false;
            hiss.volume = 0f;
            return ps;
        }

        // The Zippo left standing open on the grate, burning.
        static GameObject BurningLighter(Transform parent, Vector3 at)
        {
            var g = Group("LeftLighter", at, 30f, parent);
            var caseMb = new MeshBuilder();
            caseMb.ChamferBox(new Vector3(0, 0.0225f, 0), new Vector3(0.038f, 0.045f, 0.013f), 0.004f);
            Place("Case", caseMb, "Chrome", Vector3.zero, default, g);
            Block("Lid", "Chrome", new Vector3(-0.026f, 0.06f, 0), new Vector3(0.012f, 0.032f, 0.013f), new Vector3(0, 0, 20), 4f, g);
            Block("Chimney", "Chimney", new Vector3(0, 0.052f, 0), new Vector3(0.022f, 0.016f, 0.009f), default, 1f / 0.022f, g);
            SetFlame(g, new Vector3(0f, 0.068f, 0f), 0.07f, 2.6f, 1.3f);
            // Bigger than life, like the one in the hand, so it still reads down there under the pot.
            g.localScale = Vector3.one * 1.6f;
            return g.gameObject;
        }

        // By the door: the piston and its rod, the bolt, the coil round it with its lead, and the pressure gauge.
        static void BuildBoilerDoorWorks(Game.BoilerPuzzle puzzle, Transform works)
        {
            var cyl = new MeshBuilder();
            cyl.Tube(new Vector3(-2.5f, 1.2f, 7.25f), new Vector3(-1.62f, 1.2f, 7.25f), 0.09f, 0.09f, 10, Vector3.up, 2f);
            Place("PistonCylinder", cyl, "Rust", Vector3.zero, default, works);
            foreach (float x in new[] { -2.5f, -1.62f })
                Block("PistonCap", "DarkMetal", new Vector3(x, 1.2f, 7.25f), new Vector3(0.04f, 0.22f, 0.22f), default, 4f, works);
            foreach (float x in new[] { -2.35f, -1.8f })
                Block("PistonMount", "DarkMetal", new Vector3(x, 1.2f, 7.4f), new Vector3(0.06f, 0.06f, 0.2f), default, 4f, works);
            var rod = new GameObject("PistonRod").transform;
            rod.SetParent(works, false);
            rod.localPosition = new Vector3(-1.62f, 1.2f, 7.25f);
            var rodMb = new MeshBuilder();
            rodMb.Tube(Vector3.zero, new Vector3(0.45f, 0f, 0f), 0.022f, 0.022f, 6, Vector3.up, 4f);
            Place("Rod", rodMb, "Chrome", Vector3.zero, default, rod);
            puzzle.pistonRod = rod;

            var bolt = new GameObject("Bolt").transform;
            bolt.SetParent(works, false);
            bolt.localPosition = new Vector3(-0.95f, 1.2f, 7.3f);
            Block("Bar", "Steel", Vector3.zero, new Vector3(0.45f, 0.06f, 0.06f), default, 4f, bolt);
            Block("Knob", "Chrome", new Vector3(-0.2f, 0.05f, 0), new Vector3(0.03f, 0.06f, 0.03f), default, 4f, bolt);
            puzzle.bolt = bolt;
            Block("Keeper", "DarkMetal", new Vector3(-1.25f, 1.2f, 7.33f), new Vector3(0.08f, 0.14f, 0.12f), default, 4f, works);

            // The coil: copper windings round the bolt's guide, and its lead up over the door and down into tank A.
            Block("CoilCore", "DarkMetal", new Vector3(-1.08f, 1.2f, 7.27f), new Vector3(0.16f, 0.15f, 0.15f), default, 4f, works);
            foreach (float x in new[] { -1.13f, -1.08f, -1.03f })
                Block("Winding", "Copper", new Vector3(x, 1.2f, 7.27f), new Vector3(0.035f, 0.17f, 0.17f), default, 8f, works);
            var glowMb = new MeshBuilder();
            glowMb.Cross(Vector3.zero, 0.35f, 0.35f);
            var glow = Place("CoilGlow", glowMb, "SparkFlash", new Vector3(-1.08f, 1.05f, 7.16f), default, works);
            glow.SetActive(false);
            puzzle.coilGlow = glow;
            PullWire("CoilLead", new[]
            {
                new Vector3(-1.08f, 1.29f, 7.24f), new Vector3(-1.08f, 3.3f, 7.24f), new Vector3(1.5f, 3.3f, 7.24f),
                new Vector3(1.5f, 1.0f, 7.24f), new Vector3(1.5f, 1.0f, 6.95f), new Vector3(1.5f, 0.3f, 6.95f)
            }, works, "Rubber", false, 0.012f);

            // The pressure gauge on the steam drop, its needle swinging over as the pressure climbs.
            Cyl("Gauge_Body", "DarkMetal", new Vector3(-2.6f, 1.75f, 7.17f), 0.09f, 0.05f, 10, new Vector3(90, 0, 0), 2f, works);
            Plate("Gauge_Dial", "Dial", new Vector3(-2.6f, 1.75f, 7.165f), 0.15f, 0.15f, Vector3.back, works);
            var needle = new GameObject("GaugeNeedle").transform;
            needle.SetParent(works, false);
            needle.localPosition = new Vector3(-2.6f, 1.75f, 7.16f);
            Block("Needle", "RedPaint", new Vector3(0f, 0.033f, 0f), new Vector3(0.008f, 0.066f, 0.004f), default, 8f, needle);
            puzzle.gaugeNeedle = needle;
        }

        // The other way out: two tanks joined by a pipe, a tap over them, and the coil's line cut in two, its live
        // end hanging over tank B and its plug lying by the socket.
        static void BuildBoilerLine(Game.BoilerPuzzle puzzle, Transform works)
        {
            var tankA = Group("TankA", new Vector3(1.5f, 0f, 6.95f), 0f, works);
            var tankB = Group("TankB", new Vector3(2.45f, 0f, 6.5f), 0f, works);
            foreach (var (tank, radius) in new[] { (tankA, 0.24f), (tankB, 0.3f) })
            {
                var drum = new MeshBuilder();
                OpenDrum(drum, Vector3.zero, radius, 0.6f, 12, 0.015f, 2f);
                Place("Body", drum, "Rust", Vector3.zero, default, tank);
                var band = new MeshBuilder();
                OpenDrum(band, new Vector3(0, 0.42f, 0), radius + 0.01f, 0.04f, 12, 0.012f, 2f);
                Place("Band", band, "DarkMetal", Vector3.zero, default, tank);
                var water = new GameObject("Water").transform;
                water.SetParent(tank, false);
                water.localPosition = new Vector3(0f, 0.05f, 0f);
                var waterMb = new MeshBuilder { FaceShade = false };
                waterMb.Cylinder(Vector3.zero, radius - 0.02f, 0.004f, 12, 1f);
                Place("Surface", waterMb, "SightWater", Vector3.zero, default, water);
                if (tank == tankA) puzzle.tankWaterA = water;
                else puzzle.tankWaterB = water;
            }
            PipeRun("TankJoin", "Rust", new[] { new Vector3(1.72f, 0.1f, 6.88f), new Vector3(2.17f, 0.1f, 6.6f) }, 0.03f, works, false);

            // The tap over tank B, straight down from the ceiling.
            PipeRun("TankFeed", "Rust", new[] { new Vector3(2.45f, RoomMax.y, 6.35f), new Vector3(2.45f, 1.0f, 6.35f) }, 0.03f, works);
            Block("Spout", "DarkMetal", new Vector3(2.45f, 0.99f, 6.35f), new Vector3(0.08f, 0.03f, 0.08f), default, 4f, works);
            Cyl("Tap_Body", "DarkMetal", new Vector3(2.45f, 1.42f, 6.35f), 0.05f, 0.16f, 8, default, 2f, works);
            var tap = new GameObject("Tap").transform;
            tap.SetParent(works, false);
            tap.localPosition = new Vector3(2.34f, 1.5f, 6.35f);
            tap.localRotation = Quaternion.Euler(0, 0, 90);
            ValveWheel(tap, 0.08f, "Steel");
            puzzle.tankTap = tap;
            puzzle.tankStream = Stream("TankStream", new Vector3(2.45f, 0.3f, 6.35f), 0.68f, 0.04f, works);
            puzzle.tankStream.SetActive(false);

            // The socket low on the right wall, the plug either lying by it or pushed in.
            Block("Socket", "DarkMetal", new Vector3(RoomMax.x - 0.015f, 0.45f, 4.3f), new Vector3(0.03f, 0.13f, 0.1f), default, 4f, works);
            Block("SocketFace", "Rubber", new Vector3(RoomMax.x - 0.032f, 0.45f, 4.3f), new Vector3(0.005f, 0.06f, 0.05f), default, 8f, works);
            Plate("Sign_Live", "SignLive", new Vector3(RoomMax.x - 0.012f, 0.85f, 4.3f), 0.3f, 0.15f, Vector3.left, works);
            var socket = new GameObject("SocketPoint").transform;
            socket.SetParent(works, false);
            socket.localPosition = new Vector3(RoomMax.x - 0.05f, 0.45f, 4.3f);
            puzzle.socketPoint = socket;
            var plugIn = new GameObject("PlugIn").transform;
            plugIn.SetParent(works, false);
            Block("Plug", "Rubber", new Vector3(RoomMax.x - 0.06f, 0.45f, 4.3f), new Vector3(0.05f, 0.05f, 0.04f), default, 8f, plugIn);
            PullWire("Lead", new[] { new Vector3(RoomMax.x - 0.09f, 0.45f, 4.3f), new Vector3(2.88f, 0.02f, 4.45f), new Vector3(2.7f, 0.02f, 4.5f) }, plugIn, "Rubber", false, 0.01f);
            puzzle.plugIn = plugIn.gameObject;
            var plugLoose = new GameObject("PlugLoose").transform;
            plugLoose.SetParent(works, false);
            Block("Plug", "Rubber", new Vector3(2.55f, 0.025f, 4.2f), new Vector3(0.05f, 0.04f, 0.05f), new Vector3(0, 35, 0), 8f, plugLoose);
            PullWire("Lead", new[] { new Vector3(2.55f, 0.02f, 4.2f), new Vector3(2.7f, 0.02f, 4.5f) }, plugLoose, "Rubber", false, 0.01f);
            puzzle.plugLoose = plugLoose.gameObject;

            // The line: along the floor, up the wall past the pillar, out on a bracket over tank B.
            PullWire("CutLine", new[]
            {
                new Vector3(2.7f, 0.015f, 4.5f), new Vector3(2.7f, 0.015f, 5.9f), new Vector3(2.97f, 0.015f, 5.95f),
                new Vector3(2.97f, 1.3f, 5.95f), new Vector3(2.97f, 1.3f, 6.75f), new Vector3(2.62f, 1.3f, 6.75f)
            }, works, "Rubber", false, 0.012f);
            Block("Bracket", "DarkMetal", new Vector3(2.85f, 1.28f, 6.75f), new Vector3(0.26f, 0.03f, 0.03f), default, 4f, works);
            var tip = new Vector3(2.62f, 0.85f, 6.75f);
            var live = Group("LiveEnd", tip, 0f, works);
            PullWire("Hanging", new[] { new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0.03f, 0f) }, live, "Rubber", false, 0.016f);
            Block("BareEnd", "Copper", Vector3.zero, new Vector3(0.026f, 0.07f, 0.026f), default, 8f, live);
            puzzle.liveEnd = live;
            var dropped = Group("LiveEndDropped", Vector3.zero, 0f, works);
            PullWire("Hanging", new[] { new Vector3(2.62f, 1.3f, 6.75f), new Vector3(2.56f, 0.75f, 6.62f), new Vector3(2.5f, 0.3f, 6.5f) }, dropped, "Rubber", false, 0.016f);
            dropped.gameObject.SetActive(false);
            puzzle.liveEndDropped = dropped.gameObject;
            var waterTip = new GameObject("WaterTip").transform;
            waterTip.SetParent(works, false);
            waterTip.localPosition = new Vector3(2.5f, 0.52f, 6.5f);
            puzzle.waterTip = waterTip;
            var sparkOffset = new Vector3(Mathf.Sin(2.3f) * 0.08f, 0f, Mathf.Sin(1.4f) * 0.05f);
            var sparks = SparkingEnd(tip - sparkOffset, 0f, works);
            sparks.GetComponent<Game.Sparker>().interval = new Vector2(0.5f, 1.8f);
            sparks.SetActive(false);
            puzzle.liveSparks = sparks;
        }

        static void BuildRoom4Props()
        {
            var props = new GameObject("Props").transform;
            props.SetParent(_world, false);
            Plate("Sign_Boiler", "SignBoiler", new Vector3(-0.6f, 2.6f, 0.012f), 1.2f, 0.3f, Vector3.forward, props);
            Plate("Sign_Exit", "SignExit", new Vector3(DoorHalfWidth - 0.012f, 2.0f, RoomMax.z + 2.2f), 0.5f, 0.17f, Vector3.left, props);
            var diary = Plate("Note_Day9", "NoteDiary", new Vector3(RoomMin.x + 0.012f, 1.5f, 1.3f), 0.22f, 0.28f, Vector3.right, props, -3f);
            diary.AddComponent<Game.ReadableNote>().text = NoteDay9;
            var scrap = Plate("Note_Line", "NoteScrap", new Vector3(RoomMax.x - 0.012f, 1.45f, 3.85f), 0.22f, 0.28f, Vector3.left, props, 4f);
            scrap.AddComponent<Game.ReadableNote>().text = NoteLine;

            // Coal for a boiler long gone cold, a shovel's worth of it spilled; drums, boxes, a helmet.
            var heap = new MeshBuilder();
            heap.Tube(Vector3.zero, new Vector3(0f, 0.45f, 0f), 0.75f, 0.08f, 9, Vector3.forward, 2f);
            Place("CoalHeap", heap, "Rubber", new Vector3(-2.2f, 0f, 1.2f), default, props);
            Model("../Kkryy/Barrel/Barrel", new Vector3(2.5f, 0f, 1.1f), 30f, 0.85f, props, 0f, null, null, null, true);
            Model("../Kkryy/Barrel/Barrel", new Vector3(2.45f, 0f, 2.6f), -20f, 0.85f, props, 0f, null, null, null, true);
            Model("../Kkryy/Box/Box", new Vector3(-2.4f, 0f, 6.5f), 15f, 0.5f, props, 0f, null, null, null, true);
            Helmet(new Vector3(1.2f, 0.1f, 1.6f), 70f, props);
            Extinguisher(new Vector3(RoomMin.x, 0.5f, 5.9f), 90f, props);
        }
    }
}
