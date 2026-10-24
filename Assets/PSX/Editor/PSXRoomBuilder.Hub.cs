using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // The Sector 4 corridor the rooms open off: a long concrete passage with a steel door on either side for each
    // room, a lamp beside every door (red, green once the room is done), and the bunker's vault door at the far end,
    // which only opens when every room has been walked out of.
    public static partial class PSXRoomBuilder
    {
        const string HubPath = "Assets/Scenes/Hub.unity";

        // Stencil over each door: material suffix and text.
        static readonly (string, string)[] HubSigns = { ("1", "4-1 POWDER"), ("2", "4-2 PUMPS"), ("3", "4-3 ARMORY"), ("4", "4-4 BOILER") };

        [MenuItem("GJ/Build Hub")]
        public static void BuildHubOnly()
        {
            if (!BeginBuild()) return;
            BuildHub();
            FinishBuild();
        }

        static void BuildHub()
        {
            RoomMin = new Vector3(-1.6f, 0f, 0f);
            RoomMax = new Vector3(1.6f, 3.2f, 14f);
            var scene = NewRoomScene("Hub");

            BuildShell(new[] { 2.5f, 7f, 11.5f });
            BuildDoor();
            if (_door.flash != null) Object.DestroyImmediate(_door.flash.gameObject);
            _door.flash = null;
            _door.blastClip = null;

            var hub = new GameObject("Corridor").transform;
            hub.SetParent(_world, false);
            var puzzle = hub.gameObject.AddComponent<Game.HubPuzzle>();
            puzzle.lampDone = Mats["GreenLamp"];
            puzzle.doorClip = PSXSoundGenerator.Load("door_wheel");
            puzzle.doors = new[]
            {
                HubDoor(hub, -1f, 4.75f, "Room01", "4-1  POWDER ROOM", "1"),
                HubDoor(hub, 1f, 4.75f, "Room02", "4-2  PUMP ROOM", "2"),
                HubDoor(hub, -1f, 9.25f, "Room03", "4-3  ARMORY", "3"),
                HubDoor(hub, 1f, 9.25f, "Room04", "4-4  BOILER ROOM", "4"),
            };

            // The dead end the lift came down into, and the way out at the other end.
            var props = new GameObject("Props").transform;
            props.SetParent(_world, false);
            Plate("Sign_Sector", "SignSector", new Vector3(0f, 2.4f, 0.012f), 1.0f, 0.25f, Vector3.forward, props);
            Plate("Sign_Exit", "SignExit", new Vector3(0f, 2.75f, RoomMax.z - 0.3f), 0.5f, 0.17f, Vector3.back, props);
            Plate("Sign_ExitBeyond", "SignExit", new Vector3(DoorHalfWidth - 0.012f, 2.0f, RoomMax.z + 2.2f), 0.5f, 0.17f, Vector3.left, props);
            Helmet(new Vector3(-1.2f, 0f, 1.2f), 40f, props);
            Extinguisher(new Vector3(RoomMax.x, 0.5f, 1.4f), -90f, props);

            // The lighter, dropped on the way down: a shut Zippo lying on its back near the helmet, a glint on its
            // chrome so it catches the eye under the first light.
            var lighter = Group("DroppedLighter", new Vector3(-0.5f, 0.012f, 2.1f), 25f, props);
            var caseMb = new MeshBuilder();
            caseMb.ChamferBox(new Vector3(0f, 0.01f, 0f), new Vector3(0.038f, 0.014f, 0.058f), 0.004f);
            Place("Case", caseMb, "Chrome", Vector3.zero, default, lighter);
            Block("Seam", "DarkMetal", new Vector3(0f, 0.01f, 0.012f), new Vector3(0.039f, 0.015f, 0.0015f), default, 4f, lighter);
            var glint = new MeshBuilder();
            glint.Cross(Vector3.zero, 0.09f, 0.09f);
            Place("Glint", glint, "Mote", new Vector3(0.01f, 0.03f, 0f), default, lighter);
            lighter.localScale = Vector3.one * 1.6f;
            puzzle.lighter = lighter.gameObject;
            puzzle.lighterPoint = lighter;
            puzzle.pickClip = PSXSoundGenerator.Load("ui_pick");
            PipeRun("PipeRun_Hub", "Rust", new[] { new Vector3(RoomMin.x + 0.12f, 2.9f, 0.3f), new Vector3(RoomMin.x + 0.12f, 2.9f, RoomMax.z - 0.3f) }, 0.07f, props);
            PipeRun("PipeRun_HubCable", "DarkMetal", new[] { new Vector3(RoomMax.x - 0.1f, 3.0f, 0.3f), new Vector3(RoomMax.x - 0.1f, 3.0f, RoomMax.z - 0.3f) }, 0.025f, props);

            // Cold strip lights down the middle of the ceiling, between the beams.
            foreach (float z in new[] { 1.8f, 4.75f, 9.25f, 13.0f })
            {
                Block("Tube", "Fluorescent", new Vector3(0f, RoomMax.y - 0.04f, z), new Vector3(0.08f, 0.04f, 1.1f), default, 4f, props);
                Block("TubeMount", "DarkMetal", new Vector3(0f, RoomMax.y - 0.01f, z), new Vector3(0.14f, 0.02f, 1.2f), default, 4f, props);
                PointLight("HubLight", new Vector3(0f, RoomMax.y - 0.4f, z), new Color(0.78f, 0.9f, 0.85f), 7f, 3.0f);
            }

            AutoColliders(_world);
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            Dust(new Vector3(0f, 1.6f, RoomMax.z * 0.5f), new Vector3(3f, 2.6f, RoomMax.z), 60, life);

            var rig = BuildPlayer(new Vector3(0f, 0f, 1.0f), 0f);
            BuildDirector(rig, puzzle, 0, "SECTOR 4\n<size=10>four sealed rooms. walk out of every one of them</size>", 0, "Chapel");

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.16f, 0.19f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.04f, 0.045f, 0.055f);
            RenderSettings.fogStartDistance = 5f;
            RenderSettings.fogEndDistance = 18f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;

            EditorSceneManager.SaveScene(scene, HubPath);
            Debug.Log("PSX hub built: " + HubPath);
        }

        // A room's door in the side wall (side -1 left, +1 right) at z: frame, steel leaf with a small wheel, the
        // room's stencil above and a status lamp beside it.
        static Game.HubPuzzle.RoomDoor HubDoor(Transform parent, float side, float z, string sceneName, string title, string sign)
        {
            float wallX = side < 0 ? RoomMin.x : RoomMax.x;
            var g = Group("Door_" + sceneName, new Vector3(wallX, 0f, z), side < 0 ? 90f : -90f, parent);
            // Local +Z looks out into the corridor; the leaf sits against the wall.
            Block("Frame_Top", "DarkMetal", new Vector3(0, 2.25f, 0.06f), new Vector3(1.4f, 0.2f, 0.12f), default, 1f, g);
            foreach (float s in new[] { -1f, 1f })
                Block("Frame_Side", "DarkMetal", new Vector3(0.6f * s, 1.075f, 0.06f), new Vector3(0.2f, 2.15f, 0.12f), default, 1f, g);
            Block("Leaf", "VaultMetal", new Vector3(0, 1.075f, 0.03f), new Vector3(1.0f, 2.15f, 0.06f), default, 0.5f, g);
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(g, false);
            wheel.localPosition = new Vector3(0f, 1.2f, 0.07f);
            Cyl("Hub", "Steel", Vector3.zero, 0.06f, 0.06f, 8, new Vector3(90, 0, 0), 1f, wheel);
            for (int i = 0; i < 3; i++)
                Block("Spoke", "Steel", new Vector3(0, 0, 0.03f), new Vector3(0.03f, 0.4f, 0.03f), new Vector3(0, 0, i * 60f), 1f, wheel);
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) / 8f * 360f, rad = a * Mathf.Deg2Rad;
                Block("Rim", "Steel", new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * 0.2f + new Vector3(0, 0, 0.03f),
                      new Vector3(0.04f, 0.17f, 0.04f), new Vector3(0, 0, a), 1f, wheel);
            }
            Plate("Sign", "SignHub" + sign, new Vector3(0f, 2.55f, 0.02f), 0.64f, 0.16f, Vector3.forward, g);
            Block("Hazard", "Hazard", new Vector3(0f, 0.004f, 0.45f), new Vector3(1.2f, 0.008f, 0.5f), default, 2f, g);

            // Status lamp: a caged bulb by the frame with a halo in each colour, one of them switched on at runtime.
            var lamp = Group("Lamp", new Vector3(0.85f, 2.1f, 0.05f), 0f, g);
            Block("Mount", "DarkMetal", new Vector3(0, 0, -0.02f), new Vector3(0.12f, 0.16f, 0.03f), default, 4f, lamp);
            var bulb = Block("Bulb", "RedLamp", new Vector3(0, 0, 0.03f), new Vector3(0.07f, 0.09f, 0.06f), default, 4f, lamp);
            foreach (var cx in new[] { -0.045f, 0.045f })
                Block("Cage", "DarkMetal", new Vector3(cx, 0, 0.04f), new Vector3(0.008f, 0.12f, 0.08f), default, 4f, lamp);
            var redMb = new MeshBuilder();
            redMb.Cross(Vector3.zero, 0.45f, 0.45f);
            var red = Place("HaloRed", redMb, "RedHalo", new Vector3(0, -0.22f, 0.06f), default, lamp);
            var greenMb = new MeshBuilder();
            greenMb.Cross(Vector3.zero, 0.45f, 0.45f);
            var green = Place("HaloGreen", greenMb, "GreenHalo", new Vector3(0, -0.22f, 0.06f), default, lamp);
            green.SetActive(false);

            var point = new GameObject("LookPoint").transform;
            point.SetParent(g, false);
            point.localPosition = new Vector3(0f, 1.3f, 0.1f);
            // Coming back out: a step in front of the door, facing down the corridor toward the way out.
            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(parent, false);
            spawn.position = new Vector3(wallX - side * 0.85f, 0f, z);
            spawn.rotation = Quaternion.identity;

            return new Game.HubPuzzle.RoomDoor
            {
                scene = sceneName, title = title, point = point, spawn = spawn,
                lamp = bulb.GetComponent<Renderer>(), haloRed = red, haloGreen = green
            };
        }
    }
}
