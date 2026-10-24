using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // Room 3, the industrial yard. For now a copy of the armory (PSXRoomBuilder.Room3.cs, kept as it was, with its own
    // scene, as the fallback), to be reshaped into the yard: the same revolver, two hoppers and a light worth not
    // shooting, but the right hopper holds a container on a crane over a broken bridge instead of a door's bolt.
    public static partial class PSXRoomBuilder
    {
        const string YardPath = "Assets/Scenes/Yard.unity";

        [MenuItem("GJ/Build Yard (room 3)")]
        public static void BuildYardRoom()
        {
            if (!BeginBuild()) return;
            BuildYard();
            FinishBuild();
        }

        const string YardNoteDay7 =
            "DAY 7\n\n" +
            "The armory. The bolt on the door\n" +
            "is held by a hopper of sand up\n" +
            "by the ceiling. Empty it and the\n" +
            "iron on the other side pulls the\n" +
            "bolt back.\n\n" +
            "Sand runs slower than I breathe.";

        const string YardNoteHopper =
            "Two hoppers. Only one is on the\n" +
            "bolt's chain - follow the chain.\n" +
            "The other just works the vent.\n\n" +
            "The revolver in the locker has\n" +
            "two rounds. Don't waste them\n" +
            "on the light.\n\n" +
            "                        - M.";

        static void BuildYard()
        {
            RoomMin = new Vector3(-3f, 0f, 0f);
            RoomMax = new Vector3(3f, 3.4f, 7.5f);
            var scene = NewRoomScene("Yard");

            BuildShell(new[] { 2.0f, 4.8f });
            BuildDoor();
            if (_door.flash != null) Object.DestroyImmediate(_door.flash.gameObject);
            _door.flash = null;
            _door.blastClip = null;

            var puzzle = BuildYardArmory();
            BuildYardProps();
            BuildYardLights();
            AutoColliders(_world);
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            Dust(new Vector3(0f, 1.8f, 3.7f), new Vector3(5.5f, 2.8f, 7f), 70, life);
            FlickeringBulb("DoorLamp", "DoorLight");

            var rig = BuildPlayer(new Vector3(0.4f, 0f, 0.9f), 0f);
            var revolver = BuildRevolver(rig);
            puzzle.revolver = revolver;
            BuildDirector(rig, puzzle, 60f, "SECTOR 4 - ARMORY\nyou have 60 seconds of air", 3, "");
            UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>().revolver = revolver;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.17f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.04f, 0.04f, 0.05f);
            RenderSettings.fogStartDistance = 3f;
            RenderSettings.fogEndDistance = 14f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            EditorSceneManager.SaveScene(scene, YardPath);
            Debug.Log("PSX room built: " + YardPath);
        }

        static Game.HopperPuzzle BuildYardArmory()
        {
            var works = new GameObject("Armory").transform;
            works.SetParent(_world, false);
            float ceil = RoomMax.y;

            // The bolt hopper by the door's left, on a chain over two pulleys to an iron weight on the wall.
            var boltHopper = new GameObject("BoltHopper").transform;
            boltHopper.SetParent(works, false);
            var boltBody = Hopper("Hopper", new Vector3(-1.5f, 2.35f, 6.3f), boltHopper, "SignSand");
            Pulley(new Vector3(-1.5f, ceil - 0.15f, 6.3f), Vector3.forward, works);
            Pulley(new Vector3(-1.5f, ceil - 0.15f, 7.2f), Vector3.forward, works);
            var span = new MeshBuilder();
            span.Limb(new Vector3(-1.5f, ceil - 0.07f, 6.3f), new Vector3(-1.5f, ceil - 0.07f, 7.2f), 0.025f, 0.025f, 0.025f, 0.025f, Vector3.up, 6f);
            Place("ChainSpan", span, "Chain", Vector3.zero, default, works);
            var hopperChain = HangingChain("HopperChain", new Vector3(-1.5f, ceil - 0.23f, 6.3f - PulleyRadius), 0.5f, boltBody);
            hopperChain.localPosition = new Vector3(0, 0.45f, 0);
            hopperChain.localScale = new Vector3(1f, ceil - 0.23f - 2.8f, 1f);
            hopperChain.localRotation = Quaternion.Euler(180f, 0f, 0f);
            var weight = new GameObject("IronWeight").transform;
            weight.SetParent(works, false);
            weight.localPosition = new Vector3(-1.5f, 2.0f, 7.33f);
            Block("Iron", "DarkMetal", Vector3.zero, new Vector3(0.22f, 0.3f, 0.14f), default, 2f, weight);
            Block("Ring", "Steel", new Vector3(0, 0.17f, 0), new Vector3(0.05f, 0.05f, 0.02f), default, 4f, weight);
            var weightChain = HangingChain("WeightChain", new Vector3(0, ceil - 0.23f - 2.0f, -0.13f), ceil - 0.23f - 2.17f, weight);
            weightChain.localPosition = new Vector3(0, ceil - 0.23f - 2.0f, -0.13f + PulleyRadius);
            // From the weight a rod down to a bell crank, and the bolt through the frame into the door's edge.
            Block("Guide", "DarkMetal", new Vector3(-1.5f, 1.4f, 7.42f), new Vector3(0.06f, 1.6f, 0.06f), default, 2f, works);
            Block("Crank", "Steel", new Vector3(-1.32f, 1.25f, 7.4f), new Vector3(0.36f, 0.04f, 0.04f), new Vector3(0, 0, 20), 4f, works);
            var bolt = new GameObject("Bolt").transform;
            bolt.SetParent(works, false);
            bolt.localPosition = new Vector3(-0.95f, 1.2f, 7.3f);
            Block("Bar", "Steel", Vector3.zero, new Vector3(0.45f, 0.06f, 0.06f), default, 4f, bolt);
            Block("Knob", "Chrome", new Vector3(-0.2f, 0.05f, 0), new Vector3(0.03f, 0.06f, 0.03f), default, 4f, bolt);
            Block("Keeper", "DarkMetal", new Vector3(-1.2f, 1.2f, 7.33f), new Vector3(0.08f, 0.14f, 0.12f), default, 4f, works);

            // The vent hopper on the right, its chain to a shutter over a vent in the right wall.
            var ventHopper = new GameObject("VentHopper").transform;
            ventHopper.SetParent(works, false);
            var ventBody = Hopper("Hopper", new Vector3(1.5f, 2.35f, 6.3f), ventHopper, "SignSand");
            Pulley(new Vector3(1.5f, ceil - 0.15f, 6.3f), Vector3.right, works);
            var ventSpan = new MeshBuilder();
            ventSpan.Limb(new Vector3(1.5f, ceil - 0.07f, 6.3f), new Vector3(2.9f, ceil - 0.07f, 6.3f), 0.025f, 0.025f, 0.025f, 0.025f, Vector3.up, 6f);
            Place("ChainSpan", ventSpan, "Chain", Vector3.zero, default, works);
            var ventChain = HangingChain("HopperChain", Vector3.zero, 0.5f, ventBody);
            ventChain.localPosition = new Vector3(0, 0.45f, 0);
            ventChain.localScale = new Vector3(1f, ceil - 0.23f - 2.8f, 1f);
            ventChain.localRotation = Quaternion.Euler(180f, 0f, 0f);
            Block("VentFrame", "DarkMetal", new Vector3(RoomMax.x - 0.03f, 2.75f, 6.3f), new Vector3(0.06f, 0.5f, 0.6f), default, 2f, works);
            Block("VentHole", "Grate", new Vector3(RoomMax.x - 0.065f, 2.75f, 6.3f), new Vector3(0.01f, 0.42f, 0.52f), default, 1f / 0.5f, works);
            var shutter = new GameObject("VentShutter").transform;
            shutter.SetParent(works, false);
            shutter.localPosition = new Vector3(RoomMax.x - 0.08f, 3.0f, 6.3f);
            shutter.localRotation = Quaternion.Euler(0, 90, 0);
            Block("Flap", "Steel", new Vector3(0, -0.24f, 0.01f), new Vector3(0.56f, 0.48f, 0.02f), default, 2f, shutter);

            // Sand streams and the heaps they will make.
            var boltStream = SandStream("BoltStream", boltBody.localPosition + new Vector3(0, -0.47f, 0), 1.85f, works);
            var ventStream = SandStream("VentStream", ventBody.localPosition + new Vector3(0, -0.47f, 0), 1.85f, works);
            var boltPile = SandPile("BoltPile", new Vector3(-1.5f, 0f, 6.3f), works);
            var ventPile = SandPile("VentPile", new Vector3(1.5f, 0f, 6.3f), works);
            AudioSource PourSound(Transform at)
            {
                var a = at.gameObject.AddComponent<AudioSource>();
                a.clip = PSXSoundGenerator.Load("sand_pour");
                a.loop = true;
                a.playOnAwake = false;
                a.volume = 0.6f;
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 0.5f;
                a.maxDistance = 9f;
                return a;
            }

            // The lamp you shouldn't shoot.
            var lamp = new GameObject("CeilingLamp").transform;
            lamp.SetParent(works, false);
            lamp.localPosition = new Vector3(0.3f, ceil, 3.8f);
            Block("Cord", "DarkMetal", new Vector3(0, -0.3f, 0), new Vector3(0.02f, 0.6f, 0.02f), default, 1f, lamp);
            Cyl("Shade", "DarkMetal", new Vector3(0, -0.7f, 0), 0.24f, 0.1f, 8, default, 1f, lamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.76f, 0), new Vector3(0.09f, 0.11f, 0.09f), default, 1f, lamp);
            var lampHalo = new MeshBuilder();
            lampHalo.Cross(Vector3.zero, 0.7f, 0.7f);
            var lampGlow = Place("BulbHalo", lampHalo, "Halo", new Vector3(0, -1.1f, 0), default, lamp);
            var lampLight = PointLight("LampLight", new Vector3(0.3f, ceil - 0.9f, 3.8f), new Color(1f, 0.85f, 0.65f), 9.5f, 3.6f);
            lampLight.transform.SetParent(lamp, true);

            // The locker with the revolver.
            var locker = Group("Locker", new Vector3(RoomMin.x + 0.27f, 0f, 2.7f), 90f, works);
            const float lw = 0.5f, lh = 1.8f, ld = 0.45f;
            Block("Back", "PaintGreen", new Vector3(0, lh * 0.5f, -ld * 0.5f + 0.01f), new Vector3(lw, lh, 0.02f), default, 2f, locker);
            Block("Side_L", "PaintGreen", new Vector3(-lw * 0.5f + 0.01f, lh * 0.5f, 0), new Vector3(0.02f, lh, ld), default, 2f, locker);
            Block("Side_R", "PaintGreen", new Vector3(lw * 0.5f - 0.01f, lh * 0.5f, 0), new Vector3(0.02f, lh, ld), default, 2f, locker);
            Block("Top", "PaintGreen", new Vector3(0, lh - 0.01f, 0), new Vector3(lw, 0.02f, ld), default, 2f, locker);
            Block("Bottom", "PaintGreen", new Vector3(0, 0.05f, 0), new Vector3(lw, 0.1f, ld), default, 2f, locker);
            Block("Shelf", "PaintGreen", new Vector3(0, 1.25f, 0), new Vector3(lw - 0.04f, 0.02f, ld - 0.02f), default, 2f, locker);
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(locker, false);
            hinge.localPosition = new Vector3(-lw * 0.5f, 0, ld * 0.5f);
            Block("Door", "PaintGreen", new Vector3(lw * 0.5f, lh * 0.5f, 0.01f), new Vector3(lw, lh, 0.02f), default, 2f, hinge);
            for (int i = 0; i < 4; i++)
                Block("Slot", "Rubber", new Vector3(lw * 0.5f, lh - 0.2f - i * 0.04f, 0.021f), new Vector3(0.3f, 0.015f, 0.003f), default, 8f, hinge);
            Block("Handle", "Chrome", new Vector3(lw - 0.05f, lh * 0.5f, 0.03f), new Vector3(0.02f, 0.1f, 0.02f), default, 8f, hinge);
            Plate("Label", "SignArmory", new Vector3(lw * 0.5f, lh - 0.08f, 0.022f), 0.3f, 0.07f, Vector3.forward, hinge);
            var shelfGun = Group("Revolver", new Vector3(0.02f, 1.262f, 0.02f), 0f, locker);
            shelfGun.localRotation = Quaternion.Euler(0, 70, 90);
            RevolverModel(shelfGun);
            Model("../Kkryy/Box/Box", new Vector3(-0.12f, 1.262f, -0.05f), 10f, 0.08f, locker, 0f, null, null, null, true);
            var lockerPoint = new GameObject("LockerPoint").transform;
            lockerPoint.SetParent(locker, false);
            lockerPoint.localPosition = new Vector3(0, 1.2f, 0.2f);

            var puzzle = works.gameObject.AddComponent<Game.HopperPuzzle>();
            puzzle.lockerDoor = hinge;
            puzzle.lockerPoint = lockerPoint;
            puzzle.revolverInLocker = shelfGun.gameObject;
            puzzle.boltHopper = boltHopper.gameObject.AddComponent<Game.Shootable>();
            puzzle.boltHopperBody = boltBody;
            puzzle.boltWeight = weight;
            puzzle.bolt = bolt;
            puzzle.ventHopper = ventHopper.gameObject.AddComponent<Game.Shootable>();
            puzzle.ventHopperBody = ventBody;
            puzzle.ventShutter = shutter;
            puzzle.lamp = lamp.gameObject.AddComponent<Game.Shootable>();
            puzzle.lampLight = lampLight;
            puzzle.lampGlow = lampGlow;
            puzzle.boltStream = boltStream.gameObject;
            puzzle.ventStream = ventStream.gameObject;
            puzzle.boltPile = boltPile;
            puzzle.ventPile = ventPile;
            puzzle.boltPour = PourSound(boltStream);
            puzzle.ventPour = PourSound(ventStream);
            puzzle.lockerClip = PSXSoundGenerator.Load("locker");
            puzzle.takeClip = PSXSoundGenerator.Load("rifle_bolt");
            puzzle.hitClip = PSXSoundGenerator.Load("clunk");
            puzzle.glassClip = PSXSoundGenerator.Load("glass_break");
            puzzle.clunkClip = PSXSoundGenerator.Load("door_wheel");
            return puzzle;
        }

        static void BuildYardProps()
        {
            var props = new GameObject("Props").transform;
            props.SetParent(_world, false);
            float wallX = RoomMax.x - 0.012f, doorZ = RoomMax.z - 0.012f;

            Plate("Sign_Armory", "SignArmory", new Vector3(-0.6f, 2.6f, 0.012f), 1.2f, 0.3f, Vector3.forward, props);
            Plate("Sign_Exit", "SignExit", new Vector3(DoorHalfWidth - 0.012f, 2.0f, RoomMax.z + 2.2f), 0.5f, 0.17f, Vector3.left, props);
            Plate("Target_A", "Target", new Vector3(1.1f, 1.6f, 0.012f), 0.5f, 0.6f, Vector3.forward, props, 2f);
            Plate("Target_B", "Target", new Vector3(1.75f, 1.55f, 0.012f), 0.5f, 0.6f, Vector3.forward, props, -3f);
            var hopperNote = Plate("Note_Hopper", "NoteScrap", new Vector3(-2.3f, 1.5f, doorZ - 0.004f), 0.22f, 0.28f, Vector3.back, props, 4f);
            hopperNote.AddComponent<Game.ReadableNote>().text = YardNoteHopper;

            // A table with the diary page, a gas mask, a flashlight, bottles and a floppy disk; a chair.
            var table = Group("Table", new Vector3(-2.2f, 0f, 5.2f), 90f, props);
            Model("Furniture/table", Vector3.zero, 0f, 0f, table, 0f, null, new Vector3(1.1f, 0.78f, 0.7f));
            var note = Plate("Note_Day7", "NoteDiary", new Vector3(0.15f, 0.792f, 0.05f), 0.2f, 0.26f, Vector3.up, table, 8f);
            note.AddComponent<Game.ReadableNote>().text = YardNoteDay7;
            Model("../Kkryy/GasMask/GasMask", new Vector3(-0.3f, 0.78f, 0.05f), 30f, 0.16f, table, 0f, null, null, null, true);
            Model("../Kkryy/Flashlight/Flashlight", new Vector3(-0.05f, 0.78f, -0.2f), 80f, 0.06f, table, 0f, null, null, null, true);
            Model("../Kkryy/Bottles/Bottles", new Vector3(0.4f, 0.78f, -0.15f), 0f, 0.22f, table, 0f, null, null, null, true);
            Model("../Kkryy/Floppy/Floppy", new Vector3(0.35f, 0.78f, 0.2f), 25f, 0.01f, table, 0f, null, null, null, true);
            Model("Furniture/chair", new Vector3(-1.55f, 0f, 5.0f), -100f, 0f, props, 0f, null, new Vector3(0.42f, 0.92f, 0.42f));

            // An empty rifle rack on the left wall, the crowbar that won't reach, a barrel, bins and bags.
            var rack = Group("GunRack", new Vector3(RoomMin.x + 0.05f, 0f, 3.9f), 90f, props);
            Block("Board", "Wood", new Vector3(0, 1.35f, 0), new Vector3(1.1f, 0.9f, 0.04f), default, 1f, rack);
            Block("Rail_Low", "Wood", new Vector3(0, 0.95f, 0.06f), new Vector3(1.1f, 0.06f, 0.1f), default, 1f, rack);
            Block("Rail_High", "Wood", new Vector3(0, 1.6f, 0.06f), new Vector3(1.1f, 0.06f, 0.1f), default, 1f, rack);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.4f + i * 0.27f;
                Block("Notch", "Rubber", new Vector3(x, 0.985f, 0.1f), new Vector3(0.05f, 0.02f, 0.03f), default, 4f, rack);
                Block("Peg", "DarkMetal", new Vector3(x, 1.63f, 0.11f), new Vector3(0.03f, 0.06f, 0.06f), default, 4f, rack);
            }
            Plate("RackSign", "SignAmmo", new Vector3(0, 1.85f, 0.025f), 0.5f, 0.12f, Vector3.forward, rack);
            Model("../Kkryy/Crowbar/Crowbar", new Vector3(0.6f, 0.02f, 5.8f), 60f, 0.05f, props, 0f, null, null, null, true);
            Model("../Kkryy/Barrel/Barrel", new Vector3(2.45f, 0f, 6.85f), 20f, 0.85f, props, 0f, null, null, null, true);
            Model("../Kkryy/Barrel/Barrel", new Vector3(2.5f, 0f, 1.2f), 70f, 0.85f, props, 0f, null, null, null, true);
            Model("../Kkryy/TrashCan/TrashCan", new Vector3(1.45f, 0f, 7.0f), 0f, 0.6f, props, 0f, null, null, null, true);
            Model("../Kkryy/GarbageBag/GarbageBag", new Vector3(1.95f, 0f, 7.05f), 40f, 0.45f, props, 0f, null, null, null, true);
            Model("../Kkryy/GarbageBag/GarbageBag", new Vector3(-2.5f, 0f, 0.5f), -20f, 0.4f, props, 0f, null, null, null, true);
            Model("../Kkryy/Box/Box", new Vector3(2.45f, 0f, 3.0f), 10f, 0.5f, props, 0f, null, null, null, true);
            Model("../Kkryy/Box/Box", new Vector3(2.4f, 0.5f, 3.05f), -15f, 0.4f, props, 0f, null, null, null, true);
            Model("../Kkryy/Cardboard/Cardboard", new Vector3(2.45f, 0f, 3.8f), 5f, 0.35f, props, 0f, null, null, null, true);
            Model("Survival/box-large", new Vector3(2.4f, 0f, 5.3f), 0f, 0f, props, 0f, null, new Vector3(0.5f, 0.45f, 0.9f));
            Model("Survival/box", new Vector3(2.4f, 0.45f, 5.3f), 20f, 0f, props, 0f, null, Vector3.one * 0.4f);
            for (int i = 0; i < 3; i++) Sack(new Vector3(-0.9f + i * 0.5f, 0f, 0.35f), 5f * i, props);
            Sack(new Vector3(-0.65f, 0.21f, 0.37f), -4f, props, 3f);
            Helmet(new Vector3(0.9f, 0.1f, 1.4f), 120f, props);
            Clock(new Vector3(2.2f, 2.5f, RoomMax.z), 180f, props);
            PipeRun("RightMain", "Rust", new[] { new Vector3(2.75f, 3.0f, -0.05f), new Vector3(2.75f, 3.0f, 5.7f), new Vector3(2.75f, 3.45f, 5.7f) }, 0.07f, props);
        }

        static void BuildYardLights()
        {
            var doorLamp = new GameObject("DoorLamp").transform;
            doorLamp.SetParent(_world, false);
            doorLamp.localPosition = new Vector3(0, RoomMax.y, RoomMax.z - 0.9f);
            Block("Cord", "DarkMetal", new Vector3(0, -0.15f, 0), new Vector3(0.02f, 0.3f, 0.02f), default, 1f, doorLamp);
            Cyl("Shade", "DarkMetal", new Vector3(0, -0.4f, 0), 0.18f, 0.08f, 8, default, 1f, doorLamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.45f, 0), new Vector3(0.07f, 0.08f, 0.07f), default, 1f, doorLamp);
            var halo = new MeshBuilder();
            halo.Cross(Vector3.zero, 0.5f, 0.5f);
            Place("BulbHalo", halo, "Halo", new Vector3(0, -0.7f, 0), default, doorLamp);
            PointLight("DoorLight", new Vector3(0, RoomMax.y - 0.7f, RoomMax.z - 1.1f), new Color(1f, 0.85f, 0.62f), 4.5f, 1.8f);
            Block("RedLamp", "RedLamp", new Vector3(1.4f, 2.9f, RoomMax.z - 0.06f), new Vector3(0.14f, 0.14f, 0.1f), default, 1f, _world);
            PointLight("RedLight", new Vector3(1.4f, 2.75f, RoomMax.z - 0.3f), new Color(1f, 0.12f, 0.08f), 2.4f, 0.7f);
            PointLight("Fill", new Vector3(-1.5f, 2.6f, 2.2f), new Color(0.45f, 0.55f, 0.85f), 6f, 1.0f);
            // Work lights clamped to the beam by the door, so both hoppers can be seen and aimed at in the dark.
            PointLight("HopperLight_L", new Vector3(-1.5f, 1.6f, 5.4f), new Color(1f, 0.8f, 0.55f), 3.2f, 1.4f);
            PointLight("HopperLight_R", new Vector3(1.5f, 1.6f, 5.4f), new Color(1f, 0.8f, 0.55f), 3.2f, 1.4f);
        }
    }
}
