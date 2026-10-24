using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // The figure in the robe and mask, and the graveyard behind the chapel where it fights you: a great sprawl of
    // leaning stones and crosses inside a broken iron fence, black pines all round it, fog lying in the hollows, the
    // chapel's back wall at one end and the cemetery gate far off at the other, which opens once the figure stays down.
    public static partial class PSXRoomBuilder
    {
        const string GraveyardPath = "Assets/Scenes/Graveyard.unity";

        [MenuItem("GJ/Build Graveyard")]
        public static void BuildGraveyardOnly()
        {
            if (!BeginBuild()) return;
            BuildGraveyard();
            FinishBuild();
        }

        // A tall thin body under a black robe that falls to the ground, a deep hood, and a bone-white mask with hollow
        // eyes, two red points burning in them. Baked in four poses under one root that faces +Z.
        static Game.RobedFigure RobedFigure(Transform parent, Vector3 at, float yaw)
        {
            var root = Group("RobedFigure", at, yaw, parent);
            GameObject Pose(string name, System.Action<Transform> pose, bool lying = false)
            {
                var holder = Group(name, Vector3.zero, 0f, root);
                var model = Model("../LOWHO/Vampire", Vector3.zero, 0f, 1.95f, holder, 0f, null, null, pose, false);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = Mats["Robe"];
                    r.sharedMaterials = mats;
                }
                Shroud(holder);
                if (lying)
                {
                    holder.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    holder.localPosition = new Vector3(0f, 0.22f, 0f);
                }
                return holder.gameObject;
            }
            var fig = root.gameObject.AddComponent<Game.RobedFigure>();
            fig.idle = Pose("Idle", PoseStanding);
            fig.reach = Pose("Reach", r => { PoseStanding(r); Bend(r, "upper_arm.R", Vector3.right, -80f); Bend(r, "forearm.R", Vector3.right, 50f); });
            fig.strike = Pose("Strike", r => { PoseStanding(r); Bend(r, "upper_arm.R", Vector3.right, -150f); Bend(r, "forearm.R", Vector3.right, 20f); });
            fig.fallen = Pose("Fallen", PoseFallen, true);
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root, false);
            eyes.localPosition = new Vector3(0f, 1.756f, 0.285f);
            foreach (float s in new[] { -1f, 1f })
            {
                var eye = new MeshBuilder();
                eye.Cross(Vector3.zero, 0.035f, 0.035f);
                Place("Eye", eye, "RedHalo", new Vector3(0.0375f * s, 0f, 0f), default, eyes);
            }
            fig.eyes = eyes;
            var body = root.gameObject.AddComponent<CapsuleCollider>();
            body.height = 1.9f;
            body.radius = 0.35f;
            body.center = new Vector3(0f, 0.95f, 0f);
            root.gameObject.AddComponent<Game.Shootable>();
            fig.hissClip = PSXSoundGenerator.Load("steam");
            fig.hitClip = PSXSoundGenerator.Load("clunk");
            fig.swingClip = PSXSoundGenerator.Load("chain");
            fig.fallClip = PSXSoundGenerator.Load("rumble");
            return fig;
        }

        // A cast-iron lamp post: a stepped foot, a fluted post with a collar, a scrolled arm out over the path and a
        // glazed lantern hanging from it under a little pyramid roof, lit from inside.
        static void Lantern(Vector3 at, Transform parent)
        {
            var g = Group("LampPost", at, 0f, parent);
            Cyl("Foot", "DarkMetal", Vector3.zero, 0.16f, 0.12f, 8, default, 2f, g, true);
            Cyl("Foot2", "DarkMetal", new Vector3(0f, 0.12f, 0f), 0.11f, 0.1f, 8, default, 2f, g);
            Cyl("Post", "DarkMetal", new Vector3(0f, 0.22f, 0f), 0.045f, 2.45f, 8, default, 2f, g);
            Cyl("Collar", "DarkMetal", new Vector3(0f, 1.2f, 0f), 0.065f, 0.06f, 8, default, 2f, g);
            Cyl("Finial", "DarkMetal", new Vector3(0f, 2.67f, 0f), 0.03f, 0.12f, 6, default, 2f, g);
            var arm = new MeshBuilder();
            arm.Tube(new Vector3(0f, 2.55f, 0f), new Vector3(0.42f, 2.6f, 0f), 0.022f, 0.022f, 5, Vector3.forward, 4f);
            arm.Tube(new Vector3(0f, 2.3f, 0f), new Vector3(0.22f, 2.57f, 0f), 0.015f, 0.015f, 5, Vector3.forward, 4f);
            Place("Arm", arm, "DarkMetal", Vector3.zero, default, g);
            var lamp = Group("Lantern", new Vector3(0.42f, 2.26f, 0f), 0f, g);
            Block("Glass", "LanternGlass", new Vector3(0f, 0.16f, 0f), new Vector3(0.16f, 0.24f, 0.16f), default, 4f, lamp);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Block("Frame", "DarkMetal", new Vector3(0.085f * sx, 0.16f, 0.085f * sz), new Vector3(0.02f, 0.26f, 0.02f), default, 4f, lamp);
            Block("Base", "DarkMetal", new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.04f, 0.2f), default, 4f, lamp);
            var roof = new MeshBuilder();
            roof.Tube(new Vector3(0f, 0.28f, 0f), new Vector3(0f, 0.42f, 0f), 0.15f, 0.02f, 4, Vector3.forward, 4f);
            Place("Roof", roof, "DarkMetal", Vector3.zero, new Vector3(0f, 45f, 0f), lamp);
            Block("Hook", "DarkMetal", new Vector3(0f, 0.38f, 0f), new Vector3(0.015f, 0.08f, 0.015f), default, 4f, lamp);
            var halo = new MeshBuilder();
            halo.Cross(Vector3.zero, 1.1f, 1.1f);
            Place("Halo", halo, "Halo", new Vector3(0f, 0.16f, 0f), default, lamp);
        }

        // The robe to the ground, the hood, and the mask in the hood's mouth.
        static void Shroud(Transform pose)
        {
            var robe = new MeshBuilder();
            robe.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 1.48f, 0f), 0.48f, 0.24f, 10, Vector3.forward, 1f);
            robe.Tube(new Vector3(0f, 1.35f, -0.03f), new Vector3(0f, 2.12f, -0.08f), 0.37f, 0.08f, 9, Vector3.forward, 1f);
            Place("Robe", robe, "Robe", Vector3.zero, default, pose);
            var mask = new MeshBuilder { FaceShade = false };
            mask.Quad(new Vector3(0.1f, 1.6f, 0.27f), new Vector3(-0.2f, 0f, 0f), new Vector3(0f, 0.26f, 0f), 1f / 0.2f);
            Place("Mask", mask, "Mask", Vector3.zero, default, pose);
        }

        static void BuildGraveyard()
        {
            RoomMin = new Vector3(-18f, 0f, 0f);
            RoomMax = new Vector3(18f, 6f, 44f);
            var scene = NewRoomScene("Graveyard");
            var yard = new GameObject("Yard").transform;
            yard.SetParent(_world, false);

            // Ground far out under the trees, dense enough for the vertex lights.
            var ground = new MeshBuilder { FaceShade = false };
            ground.Quad(new Vector3(-40f, 0f, -6f), new Vector3(80f, 0f, 0f), new Vector3(0f, 0f, 70f), 0.4f, 2f);
            Place("Ground", ground, "Grass", Vector3.zero, default, yard, true);

            // The chapel's back wall, and a broken iron fence round the plot with the gate in the far side.
            Block("ChapelWall", "Stone", new Vector3(0f, 3f, -0.2f), new Vector3(16.4f, 6f, 0.4f), default, 0.5f, yard, true);
            Block("ChapelDoor", "Wood", new Vector3(0f, 1.2f, 0.02f), new Vector3(1.8f, 2.4f, 0.06f), default, 1f, yard);
            Block("ChapelRoof", "Rust", new Vector3(0f, 6.2f, -1.2f), new Vector3(17f, 0.3f, 2.4f), new Vector3(-20, 0, 0), 0.5f, yard);
            Block("BellTower", "Stone", new Vector3(0f, 7.5f, -0.6f), new Vector3(2f, 3f, 1.2f), default, 0.5f, yard);
            var fence = new GameObject("Fence").transform;
            fence.SetParent(yard, false);
            void FenceRun(Vector3 a, Vector3 b, int seed)
            {
                Vector3 d = b - a;
                int n = Mathf.CeilToInt(d.magnitude / 0.3f);
                var bars = new MeshBuilder();
                for (int i = 0; i <= n; i++)
                {
                    if (Hash(i, seed, 185) > 0.88f) continue;   // bent away or gone
                    var p = a + d * (i / (float)n);
                    float h = 1.5f + (i % 2) * 0.12f;
                    bars.Box(p + Vector3.up * h * 0.5f, new Vector3(0.025f, h, 0.025f), 8f);
                }
                bars.Box((a + b) * 0.5f + Vector3.up * 1.35f, new Vector3(Mathf.Max(Mathf.Abs(d.x), 0.04f), 0.035f, Mathf.Max(Mathf.Abs(d.z), 0.04f)), 8f);
                Place("Bars", bars, "DarkMetal", Vector3.zero, default, fence);
                var wall = new GameObject("FenceWall").AddComponent<BoxCollider>();
                wall.transform.SetParent(fence, false);
                wall.center = (a + b) * 0.5f + Vector3.up * 1f;
                wall.size = new Vector3(Mathf.Max(Mathf.Abs(d.x), 0.3f), 2f, Mathf.Max(Mathf.Abs(d.z), 0.3f));
            }
            FenceRun(new Vector3(RoomMin.x, 0f, 0f), new Vector3(RoomMin.x, 0f, RoomMax.z), 1);
            FenceRun(new Vector3(RoomMax.x, 0f, 0f), new Vector3(RoomMax.x, 0f, RoomMax.z), 2);
            FenceRun(new Vector3(RoomMin.x, 0f, 0f), new Vector3(-8.2f, 0f, 0f), 3);
            FenceRun(new Vector3(8.2f, 0f, 0f), new Vector3(RoomMax.x, 0f, 0f), 4);
            FenceRun(new Vector3(RoomMin.x, 0f, RoomMax.z), new Vector3(-1.2f, 0f, RoomMax.z), 5);
            FenceRun(new Vector3(1.2f, 0f, RoomMax.z), new Vector3(RoomMax.x, 0f, RoomMax.z), 6);
            foreach (float x in new[] { -1.2f, 1.2f })
            {
                Block("GatePost", "Concrete", new Vector3(x, 1.3f, RoomMax.z), new Vector3(0.45f, 2.6f, 0.45f), default, 1f, yard, true);
                Block("PostCap", "Concrete", new Vector3(x, 2.68f, RoomMax.z), new Vector3(0.6f, 0.15f, 0.6f), default, 1f, yard);
            }
            var gate = new GameObject("Gate").transform;
            gate.SetParent(_world, false);
            gate.localPosition = new Vector3(0f, 0f, RoomMax.z);
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(gate, false);
            leaf.localPosition = new Vector3(-1.0f, 0f, 0f);
            var bars2 = new MeshBuilder();
            for (int i = 0; i <= 12; i++) bars2.Box(new Vector3(0.08f + i * 0.152f, 1.1f, 0f), new Vector3(0.025f, 2.2f, 0.025f), 8f);
            foreach (float y in new[] { 0.15f, 1.0f, 2.1f }) bars2.Box(new Vector3(1.0f, y, 0f), new Vector3(1.95f, 0.04f, 0.03f), 8f);
            Place("Bars", bars2, "DarkMetal", Vector3.zero, default, leaf).AddComponent<BoxCollider>();
            _door = gate.gameObject.AddComponent<Game.VaultDoor>();
            _door.leaf = leaf;
            _door.openAngle = -100f;
            _door.openClip = PSXSoundGenerator.Load("door_open");

            // Graves in long uneven rows across the whole plot, a path up the middle, two open graves and a vault.
            for (int row = 0; row < 11; row++)
                for (int col = 0; col < 13; col++)
                {
                    float x = -16f + col * 2.65f + (Hash(row, col, 180) - 0.5f) * 0.8f;
                    float z = 6f + row * 3.4f + (Hash(row, col, 181) - 0.5f) * 0.9f;
                    if (Mathf.Abs(x) < 1.6f || Hash(row, col, 186) > 0.86f) continue;   // the path, and gaps
                    if (z < 14f && Mathf.Abs(x) < 6f) continue;                              // room to fight by the chapel
                    var g = Group("Grave", new Vector3(x, 0f, z), (Hash(row, col, 182) - 0.5f) * 20f, yard);
                    float lean = (Hash(row, col, 183) - 0.5f) * 22f;
                    float kind = Hash(row, col, 184);
                    if (kind > 0.55f)
                    {
                        Block("Stone", "Concrete", new Vector3(0f, 0.45f, 0f), new Vector3(0.62f, 0.9f, 0.13f), new Vector3(lean, 0, 0), 1f, g, true);
                        Cyl("Top", "Concrete", new Vector3(-0.31f, 0.9f, 0f), 0.31f, 0.13f, 8, new Vector3(lean, 0, -90), 1f, g);
                    }
                    else if (kind > 0.2f)
                    {
                        Block("Cross", "Wood", new Vector3(0f, 0.65f, 0f), new Vector3(0.1f, 1.3f, 0.1f), new Vector3(lean, 0, 0), 2f, g, true);
                        Block("CrossBar", "Wood", new Vector3(0f, 1.0f, 0f), new Vector3(0.58f, 0.09f, 0.09f), new Vector3(lean, 0, 0), 2f, g);
                    }
                    else
                    {
                        Block("Obelisk", "Concrete", new Vector3(0f, 0.9f, 0f), new Vector3(0.32f, 1.8f, 0.32f), new Vector3(lean * 0.3f, 0, 0), 1f, g, true);
                        Block("Plinth", "Concrete", new Vector3(0f, 0.12f, 0f), new Vector3(0.6f, 0.24f, 0.6f), default, 1f, g);
                    }
                    Block("Mound", "Grass", new Vector3(0f, 0.05f, -0.8f), new Vector3(0.85f, 0.12f, 1.55f), default, 1f, g);
                }
            foreach (var (x, z) in new[] { (-4.6f, 18.4f), (5.2f, 15.8f) })
            {
                Block("Pit", "Rubber", new Vector3(x, 0.005f, z), new Vector3(0.9f, 0.01f, 1.9f), default, 1f, yard);
                Block("Spoil", "Grass", new Vector3(x + 0.8f, 0.2f, z), new Vector3(0.5f, 0.4f, 1.6f), new Vector3(0, 0, 15), 1f, yard);
            }
            var crypt = Group("Crypt", new Vector3(-11f, 0f, 30f), 90f, yard);
            Block("Walls", "Stone", new Vector3(0f, 1.4f, 0f), new Vector3(3f, 2.8f, 4f), default, 0.5f, crypt, true);
            Block("Roof", "Concrete", new Vector3(0f, 3.0f, 0f), new Vector3(3.4f, 0.3f, 4.4f), default, 0.5f, crypt);
            Block("CryptDoor", "Rust", new Vector3(0f, 1.0f, 2.01f), new Vector3(1.0f, 2.0f, 0.04f), default, 1f, crypt);

            // Lamp posts up the path, and the moon behind the fog.
            foreach (var z in new[] { 6f, 16f, 28f, 40f })
            {
                Lantern(new Vector3(-1.5f, 0f, z), yard);
                PointLight("LampLight", new Vector3(-1.1f, 2.45f, z), new Color(1f, 0.72f, 0.42f), 10f, 2.6f);
            }
            var moon = new MeshBuilder();
            moon.Card(new Vector3(-14f, 22f, 90f), new Vector3(10f, 0f, 0f), new Vector3(0f, 10f, 0f));
            Place("Moon", moon, "MoonGlow", Vector3.zero, default, yard);
            var sun = new GameObject("Moonlight").AddComponent<Light>();
            sun.transform.SetParent(_world, false);
            sun.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            sun.type = LightType.Directional;
            sun.color = new Color(0.55f, 0.62f, 0.85f);
            sun.intensity = 0.4f;
            sun.shadows = LightShadows.None;

            // The revolver where it landed, a glint on it.
            var puzzle = yard.gameObject.AddComponent<Game.GraveyardPuzzle>();
            var gun = Group("LostRevolver", new Vector3(3.4f, 0.03f, 7.2f), 35f, yard);
            var model = Group("Model", Vector3.zero, 0f, gun);
            model.localRotation = Quaternion.Euler(0f, 0f, 90f);
            RevolverModel(model);
            var glint = new MeshBuilder();
            glint.Cross(Vector3.zero, 0.12f, 0.12f);
            Place("Glint", glint, "Mote", new Vector3(0f, 0.08f, 0f), default, gun);
            puzzle.lostGun = gun.gameObject;
            puzzle.lostGunPoint = gun;
            puzzle.punchClip = PSXSoundGenerator.Load("clunk");
            puzzle.swishClip = PSXSoundGenerator.Load("ui_move");
            puzzle.takeClip = PSXSoundGenerator.Load("rifle_bolt");

            // The director wants a display; out here it is a dead clock face in the bell tower.
            var clock = new MeshBuilder { FaceShade = false };
            clock.Quad(new Vector3(0.4f, 7.4f, 0.02f), new Vector3(-0.8f, 0f, 0f), new Vector3(0f, 0.27f, 0f), 1f / 0.8f);
            _displayDigits = Place("DisplayDigits", clock, "DisplayDigits", Vector3.zero, default, yard).GetComponent<Renderer>();

            AutoColliders(_world);

            // After the auto colliders (none of this needs one): the forest, the mist, and the figure.
            var forest = new MeshBuilder();
            var dead = new MeshBuilder();
            for (int i = 0; i < 170; i++)
            {
                float a = Hash(i, 0, 187) * Mathf.PI * 2f;
                float x = Mathf.Lerp(-34f, 34f, Hash(i, 1, 188)), z = Mathf.Lerp(-4f, 62f, Hash(i, 2, 189));
                bool inside = x > RoomMin.x - 1.5f && x < RoomMax.x + 1.5f && z > -2f && z < RoomMax.z + 1.5f;
                if (inside) continue;
                float h = 6f + Hash(i, 3, 190) * 7f;
                var p = new Vector3(x, 0f, z);
                if (Hash(i, 4, 191) > 0.25f)
                {
                    forest.Tube(p, p + Vector3.up * h * 0.25f, 0.18f, 0.14f, 5, Vector3.forward, 1f);
                    for (int k = 0; k < 3; k++)
                        forest.Tube(p + Vector3.up * h * (0.2f + k * 0.25f), p + Vector3.up * h * (0.55f + k * 0.22f), h * (0.24f - k * 0.06f), 0.02f, 7, Vector3.forward, 0.5f);
                }
                else
                {
                    dead.Tube(p, p + new Vector3(0.3f, h * 0.7f, 0f), 0.22f, 0.05f, 6, Vector3.forward, 1f);
                    for (int b = 0; b < 4; b++)
                    {
                        float ang = a + b * 1.6f, y = h * (0.3f + b * 0.1f);
                        var from = p + new Vector3(0.1f, y, 0f);
                        dead.Tube(from, from + new Vector3(Mathf.Cos(ang), 0.6f, Mathf.Sin(ang)) * (2f - b * 0.3f), 0.07f, 0.015f, 5, Vector3.up, 1f);
                    }
                }
            }
            Place("Forest", forest, "Pine", Vector3.zero, default, yard);
            Place("DeadTrees", dead, "Wood", Vector3.zero, default, yard);
            var mist = new MeshBuilder { FaceShade = false };
            for (int i = 0; i < 70; i++)
            {
                var c = new Vector3(Mathf.Lerp(-20f, 20f, Hash(i, 5, 192)), 0.35f + Hash(i, 6, 193) * 0.5f, Mathf.Lerp(2f, 48f, Hash(i, 7, 194)));
                float w = 4f + Hash(i, 8, 195) * 5f;
                float yaw = Hash(i, 9, 196) * 180f;
                var u = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * w;
                mist.Card(c - u * 0.5f, u, Vector3.up * 1.3f);
            }
            Place("GroundMist", mist, "Steam", Vector3.zero, default, yard);
            puzzle.figure = RobedFigure(_world, new Vector3(0f, 0f, 10f), 180f);
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            Dust(new Vector3(0f, 1.5f, 20f), new Vector3(30f, 3f, 40f), 120, life);

            var rig = BuildPlayer(new Vector3(0f, 0f, 2.2f), 0f);
            BuildDirector(rig, puzzle, 4, "", 6, "");
            UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>().revolver = BuildRevolver(rig);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.18f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.1f, 0.11f, 0.14f);
            RenderSettings.fogStartDistance = 3f;
            RenderSettings.fogEndDistance = 24f;
            RenderSettings.skybox = null;
            RenderSettings.sun = sun;
            EditorSceneManager.SaveScene(scene, GraveyardPath);
            Debug.Log("PSX graveyard built: " + GraveyardPath);
        }
    }
}
