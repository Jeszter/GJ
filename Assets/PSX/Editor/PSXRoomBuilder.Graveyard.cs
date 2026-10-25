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

        // The cultist from the reference, built piece by piece so its arms can be posed: a long robe split at the hem
        // over dark boots, a hip-length tunic over it with bone clasps down the front and swirls on the chest, a rope
        // belt, wide sleeves, pale hands, and a rounded hood with a black shield of nothing where the face should be.
        // Four poses (standing with the blade low, reaching out, blade raised, fallen) under one root facing +Z.
        static Game.RobedFigure RobedFigure(Transform parent, Vector3 at, float yaw)
        {
            var root = Group("RobedFigure", at, yaw, parent);
            var fig = root.gameObject.AddComponent<Game.RobedFigure>();
            Vector3 shR = new Vector3(0.27f, 1.47f, 0f), shL = new Vector3(-0.27f, 1.47f, 0f);
            Vector3[] downL = { shL, new Vector3(-0.34f, 1.17f, 0.02f), new Vector3(-0.37f, 0.91f, 0.06f) };
            Vector3[] downR = { shR, new Vector3(0.33f, 1.17f, 0.04f), new Vector3(0.35f, 0.93f, 0.14f) };
            Vector3[] outR = { shR, new Vector3(0.3f, 1.42f, 0.3f), new Vector3(0.26f, 1.4f, 0.6f) };
            Vector3[] upR = { shR, new Vector3(0.36f, 1.78f, 0.06f), new Vector3(0.33f, 2.05f, -0.04f) };
            Vector3[] slashR = { shR, new Vector3(0.29f, 1.3f, 0.33f), new Vector3(0.2f, 1.0f, 0.6f) };
            fig.idle = CultistPose(root, "Idle", downR, downL);
            fig.reach = CultistPose(root, "Reach", outR, downL);
            fig.strike = CultistPose(root, "Strike", upR, downL);
            fig.slash = CultistPose(root, "Slash", slashR, new[] { shL, new Vector3(-0.36f, 1.2f, -0.08f), new Vector3(-0.4f, 0.98f, -0.2f) });
            fig.fallen = CultistPose(root, "Fallen", outR, new[] { shL, new Vector3(-0.55f, 1.4f, 0.05f), new Vector3(-0.82f, 1.38f, 0.1f) });
            fig.fallen.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            fig.fallen.transform.localPosition = new Vector3(0f, 0.3f, 0f);

            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root, false);
            eyes.localPosition = new Vector3(0f, 1.72f, 0.175f);
            foreach (float s in new[] { -1f, 1f })
            {
                var eye = new MeshBuilder();
                eye.Cross(Vector3.zero, 0.03f, 0.03f);
                Place("Eye", eye, "RedHalo", new Vector3(0.04f * s, 0f, 0f), default, eyes);
            }
            fig.eyes = eyes;

            // The blade in the right fist, the grip across the palm: hanging low and pointing ahead, raised with the blade
            // laid back over the shoulder, and brought down and forward at the end of the blow.
            fig.knifeLow = Machete(root, Fist(downR), new Vector3(0.05f, -0.25f, 0.97f));
            fig.knifeHigh = Machete(root, Fist(upR), new Vector3(0.05f, 0.3f, -0.95f));
            fig.knifeSlash = Machete(root, Fist(slashR), new Vector3(-0.12f, -0.85f, 0.5f));
            fig.trail = SlashTrail(root, shR, Fist(upR) + new Vector3(0.05f, 0.3f, -0.95f).normalized * 0.98f, Fist(slashR) + new Vector3(-0.12f, -0.85f, 0.5f).normalized * 0.98f);
            var drop = new MeshBuilder { FaceShade = false };
            drop.Cross(new Vector3(0f, -0.02f, 0f), 0.05f, 0.05f);
            var dropGo = Place("BloodDrop", drop, "Blood", Vector3.zero, default, parent);
            dropGo.SetActive(false);
            fig.bloodDrop = dropGo;
            var splat = new MeshBuilder { FaceShade = false };
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f, r = 0.05f + Hash(i, 0, 191) * 0.12f;
                splat.Quad(new Vector3(Mathf.Cos(a) * r - 0.06f, 0f, Mathf.Sin(a) * r - 0.06f), new Vector3(0.12f + Hash(i, 1, 192) * 0.08f, 0f, 0f), new Vector3(0f, 0f, 0.12f), 4f);
            }
            var splatGo = Place("BloodSplat", splat, "Blood", Vector3.zero, default, parent);
            splatGo.SetActive(false);
            fig.bloodSplat = splatGo;

            var body = root.gameObject.AddComponent<CapsuleCollider>();
            body.height = 2.05f;
            body.radius = 0.36f;
            body.center = new Vector3(0f, 1.02f, 0f);
            root.gameObject.AddComponent<Game.Shootable>();
            fig.hissClip = PSXSoundGenerator.Load("steam");
            fig.hitClip = PSXSoundGenerator.Load("clunk");
            fig.swingClip = PSXSoundGenerator.Load("chain");
            fig.fallClip = PSXSoundGenerator.Load("rumble");
            return fig;
        }

        // One pose of the cultist; each arm is shoulder, elbow, wrist.
        static GameObject CultistPose(Transform root, string name, Vector3[] armR, Vector3[] armL)
        {
            var g = Group(name, Vector3.zero, 0f, root);
            var robe = new MeshBuilder();
            // The long skirt to the ankles, and the tunic over it to the hips, a touch wider at its hem.
            robe.Tube(new Vector3(0f, 0.04f, 0f), new Vector3(0f, 1.0f, 0f), 0.31f, 0.21f, 10, Vector3.forward, 1f);
            robe.Tube(new Vector3(0f, 0.86f, 0f), new Vector3(0f, 1.42f, 0f), 0.27f, 0.23f, 10, Vector3.forward, 1f);
            robe.Limb(new Vector3(-0.3f, 1.44f, 0f), new Vector3(0.3f, 1.44f, 0f), 0.16f, 0.2f, 0.16f, 0.2f, Vector3.up, 1f);
            // The hood: round, falling back behind the head, its front cut open for the face.
            robe.Tube(new Vector3(0f, 1.42f, -0.02f), new Vector3(0f, 1.62f, -0.02f), 0.19f, 0.2f, 10, Vector3.forward, 1f);
            robe.Tube(new Vector3(0f, 1.62f, -0.02f), new Vector3(0f, 1.86f, -0.05f), 0.2f, 0.16f, 10, Vector3.forward, 1f);
            robe.Tube(new Vector3(0f, 1.86f, -0.05f), new Vector3(0f, 1.96f, -0.1f), 0.16f, 0.06f, 10, Vector3.forward, 1f);
            // Wide sleeves, flaring at the cuff.
            foreach (var arm in new[] { armR, armL })
            {
                robe.Tube(arm[0], arm[1], 0.075f, 0.085f, 7, UpFor(arm[1] - arm[0]), 2f);
                robe.Tube(arm[1], arm[2], 0.085f, 0.13f, 7, UpFor(arm[2] - arm[1]), 2f);
            }
            Place("Robe", robe, "Robe", Vector3.zero, default, g);

            // Clasps down the front of tunic and skirt; swirls on the chest either side of them.
            var clasps = new MeshBuilder();
            clasps.Quad(new Vector3(0.04f, 0.12f, 0.302f), new Vector3(-0.08f, 0f, 0f), new Vector3(0f, 0.8f, -0.09f), 1f / 0.08f);
            clasps.Quad(new Vector3(0.04f, 0.9f, 0.27f), new Vector3(-0.08f, 0f, 0f), new Vector3(0f, 0.5f, -0.035f), 1f / 0.08f);
            Place("Clasps", clasps, "Clasps", Vector3.zero, default, g);
            var swirls = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
                swirls.Quad(new Vector3(0.05f * s + (s > 0 ? 0.13f : 0f), 1.12f, 0.255f), new Vector3(-0.13f, 0f, 0f), new Vector3(0f, 0.22f, -0.015f), 1f / 0.13f);
            Place("Swirls", swirls, "Embroidery", Vector3.zero, default, g);
            // A thin rope belt.
            var belt = new MeshBuilder();
            belt.Tube(new Vector3(0f, 0.96f, 0f), new Vector3(0f, 0.99f, 0f), 0.255f, 0.255f, 10, Vector3.forward, 4f);
            Place("Belt", belt, "Leather", Vector3.zero, default, g);
            // Deep shadow in the hood, narrow at the chin, broad at the brow, and in it a face that has been taken
            // apart: grey skin, the eyes gouged out, cut across and across, the mouth slit to the ears and stitched.
            var face = new MeshBuilder { FaceShade = false };
            face.Limb(new Vector3(0f, 1.5f, 0.17f), new Vector3(0f, 1.83f, 0.135f), 0.09f, 0.01f, 0.22f, 0.01f, Vector3.forward, 4f);
            Place("Void", face, "Void", Vector3.zero, default, g);
            var skin = new MeshBuilder { FaceShade = false };
            skin.Quad(new Vector3(0.12f, 1.56f, 0.19f), new Vector3(-0.24f, 0f, 0f), new Vector3(0f, 0.24f, -0.03f), 1f / 0.24f);
            Place("Face", skin, "MutilatedFace", Vector3.zero, default, g);

            // Pale hands out of the cuffs, thumbs up the side.
            var hands = new MeshBuilder();
            foreach (var arm in new[] { armR, armL })
            {
                Vector3 d = (arm[2] - arm[1]).normalized;
                Vector3 side = Vector3.Cross(d, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(d, Vector3.up).normalized : Vector3.right;
                hands.Limb(arm[2] - d * 0.02f, arm[2] + d * 0.15f, 0.085f, 0.035f, 0.075f, 0.03f, UpFor(d), 4f);
                hands.Limb(arm[2] + d * 0.02f + side * 0.045f, arm[2] + d * 0.08f + side * 0.08f, 0.025f, 0.025f, 0.02f, 0.02f, UpFor(d), 4f);
            }
            Place("Hands", hands, "PaleSkin", Vector3.zero, default, g);
            // Only the toes of dark boots under the hem.
            var boots = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
                boots.ChamferBox(new Vector3(0.1f * s, 0.045f, 0.25f), new Vector3(0.12f, 0.09f, 0.16f), 0.02f, 4f);
            Place("Boots", boots, "Leather", Vector3.zero, default, g);
            return g.gameObject;
        }

        // Where the fist closes, from an arm's shoulder, elbow and wrist: a little past the wrist, inside the hand.
        static Vector3 Fist(Vector3[] arm) => arm[2] + (arm[2] - arm[1]).normalized * 0.07f;

        // A machete as long as an arm, its grip through the fist and its blade along blade, the flat of it facing the
        // figure's sides so the edge leads the blow. Wrapped grip, a crossbar, a broad blade widening to its tip,
        // bloody; hidden until the figure draws it.
        static GameObject Machete(Transform parent, Vector3 fist, Vector3 blade)
        {
            blade = blade.normalized;
            Vector3 flat = Vector3.right - blade * Vector3.Dot(Vector3.right, blade);
            var g = Group("Machete", fist + blade * 0.02f, 0f, parent);
            g.localRotation = Quaternion.LookRotation(flat.normalized, blade);
            Cyl("Grip", "Leather", new Vector3(0f, -0.12f, 0f), 0.022f, 0.2f, 6, default, 6f, g);
            Block("Guard", "DarkMetal", new Vector3(0f, 0.09f, 0f), new Vector3(0.1f, 0.025f, 0.035f), default, 6f, g);
            var steel = new MeshBuilder();
            steel.Quad(new Vector3(-0.05f, 0.1f, 0.005f), new Vector3(0.1f, 0f, 0f), new Vector3(0.035f, 0.88f, 0f), 1f / 0.1f);
            steel.Quad(new Vector3(0.05f, 0.1f, -0.005f), new Vector3(-0.1f, 0f, 0f), new Vector3(0.035f, 0.88f, 0f), 1f / 0.1f);
            steel.Box(new Vector3(-0.05f, 0.54f, 0f), new Vector3(0.012f, 0.88f, 0.012f), 8f);
            Place("Blade", steel, "BloodyBlade", Vector3.zero, default, g);
            g.gameObject.SetActive(false);
            return g.gameObject;
        }

        // The smear of light along the blade's path for the instant of a blow: a band swept over the shoulder from the
        // raised tip to the lowered one, inner edge two thirds of the way out.
        static GameObject SlashTrail(Transform root, Vector3 shoulder, Vector3 from, Vector3 to)
        {
            Vector3 a = from - shoulder, b = to - shoulder;
            float angA = Mathf.Atan2(a.y, a.z), angB = Mathf.Atan2(b.y, b.z);
            if (angB > angA) angB -= Mathf.PI * 2f;   // round over the top, not under
            var mb = new MeshBuilder { FaceShade = false };
            const int steps = 10;
            Vector3 Point(float t, float r)
            {
                float ang = Mathf.Lerp(angA, angB, t);
                float len = Mathf.Lerp(new Vector2(a.y, a.z).magnitude, new Vector2(b.y, b.z).magnitude, t) * r;
                return shoulder + new Vector3(Mathf.Lerp(a.x, b.x, t) * r, Mathf.Sin(ang) * len, Mathf.Cos(ang) * len);
            }
            // Only the stroke itself, from over the head down the front; the glow runs across the band, so the band
            // reads as one continuous smear along the blade's path.
            for (int i = 0; i < steps; i++)
            {
                float t0 = Mathf.Lerp(0.35f, 1f, i / (float)steps), t1 = Mathf.Lerp(0.35f, 1f, (i + 1) / (float)steps);
                Vector3 o = Point(t0, 0.82f), w = Point(t0, 1f) - o, u = Point(t1, 0.82f) - o;
                mb.Quad(o, w, u, 1f);
                mb.Quad(o, w, u, 1f, 0f, true);
            }
            var go = Place("SlashTrail", mb, "SlashTrail", Vector3.zero, default, root);
            go.SetActive(false);
            return go;
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
            Block("Glass", "LanternPane", new Vector3(0f, 0.16f, 0f), new Vector3(0.16f, 0.24f, 0.16f), default, 1f / 0.16f, lamp);
            var flame = new MeshBuilder();
            flame.Cross(Vector3.zero, 0.04f, 0.08f);
            Place("Flame", flame, "Flame", new Vector3(0f, 0.13f, 0f), default, lamp);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Block("Frame", "DarkMetal", new Vector3(0.085f * sx, 0.16f, 0.085f * sz), new Vector3(0.02f, 0.26f, 0.02f), default, 4f, lamp);
            Block("Base", "DarkMetal", new Vector3(0f, 0.02f, 0f), new Vector3(0.2f, 0.04f, 0.2f), default, 4f, lamp);
            var roof = new MeshBuilder();
            roof.Tube(new Vector3(0f, 0.28f, 0f), new Vector3(0f, 0.42f, 0f), 0.15f, 0.02f, 4, Vector3.forward, 4f);
            Place("Roof", roof, "DarkMetal", Vector3.zero, new Vector3(0f, 45f, 0f), lamp);
            Block("Hook", "DarkMetal", new Vector3(0f, 0.38f, 0f), new Vector3(0.015f, 0.08f, 0.015f), default, 4f, lamp);
            var halo = new MeshBuilder();
            halo.Cross(Vector3.zero, 0.55f, 0.55f);
            Place("Halo", halo, "FlameHalo", new Vector3(0f, 0.16f, 0f), default, lamp);
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
            ground.Quad(new Vector3(-40f, 0f, -6f), new Vector3(80f, 0f, 0f), new Vector3(0f, 0f, 96f), 0.4f, 2f);
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
            sun.intensity = 0.75f;
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
            // A little warm light on it, so the revolver can be found in the grass at night.
            var gunLight = new GameObject("GunLight").AddComponent<Light>();
            gunLight.transform.SetParent(gun, false);
            gunLight.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            gunLight.type = LightType.Point;
            gunLight.range = 3f;
            gunLight.intensity = 2.6f;
            gunLight.color = new Color(1f, 0.85f, 0.6f);
            gunLight.shadows = LightShadows.None;
            puzzle.lostGun = gun.gameObject;
            puzzle.lostGunPoint = gun;
            puzzle.punchClip = PSXSoundGenerator.Load("clunk");
            puzzle.swishClip = PSXSoundGenerator.Load("ui_move");
            puzzle.takeClip = PSXSoundGenerator.Load("rifle_bolt");
            puzzle.drawClip = PSXSoundGenerator.Load("ratchet");

            // The director wants a display; out here it is a dead clock face in the bell tower.
            var clock = new MeshBuilder { FaceShade = false };
            clock.Quad(new Vector3(0.4f, 7.4f, 0.02f), new Vector3(-0.8f, 0f, 0f), new Vector3(0f, 0.27f, 0f), 1f / 0.8f);
            _displayDigits = Place("DisplayDigits", clock, "DisplayDigits", Vector3.zero, default, yard).GetComponent<Renderer>();

            GraveyardRoad(yard);
            AutoColliders(_world);

            // After the auto colliders (none of this needs one): the forest, the mist, and the figure.
            var forest = new MeshBuilder();
            var forestLight = new MeshBuilder();
            var trunks = new MeshBuilder();
            var dead = new MeshBuilder();
            for (int i = 0; i < 260; i++)
            {
                float a = Hash(i, 0, 187) * Mathf.PI * 2f;
                float x = Mathf.Lerp(-34f, 34f, Hash(i, 1, 188)), z = Mathf.Lerp(-4f, 82f, Hash(i, 2, 189));
                bool inside = x > RoomMin.x - 1.5f && x < RoomMax.x + 1.5f && z > -2f && z < RoomMax.z + 1.5f;
                // The road beyond the gate and the grass down to it are clear.
                bool road = z > EndRoadZ - 5f && z < EndRoadZ + 4.5f;
                bool clearing = Mathf.Abs(x) < 9f && z > RoomMax.z && z < EndRoadZ;
                if (inside || road || clearing) continue;
                float h = 6f + Hash(i, 3, 190) * 7f;
                var p = new Vector3(x, 0f, z);
                if (Hash(i, 4, 191) > 0.18f)
                    PineTree(Hash(i, 5, 193) > 0.6f ? forestLight : forest, trunks, p, h, i);
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
            Place("ForestLight", forestLight, "PineLight", Vector3.zero, default, yard);
            Place("Trunks", trunks, "Wood", Vector3.zero, default, yard);
            Place("DeadTrees", dead, "Wood", Vector3.zero, default, yard);
            ForestFloor(yard, 190, (x, z) =>
                (x > RoomMin.x - 1.5f && x < RoomMax.x + 1.5f && z > -2f && z < RoomMax.z + 1.5f)
                || (z > EndRoadZ - 4f && z < EndRoadZ + 4f) || (Mathf.Abs(x) < 2f && z > RoomMax.z && z < EndRoadZ));
            // Mist in three layers that drift each their own way: a thin one hugging the grass, a deeper one at the
            // knee, and banks of it out between the trees.
            for (int layer = 0; layer < 3; layer++)
            {
                var mist = new MeshBuilder { FaceShade = false };
                int count = layer == 2 ? 40 : 55;
                for (int i = 0; i < count; i++)
                {
                    int k = i + layer * 100;
                    float x = layer == 2 ? Mathf.Lerp(-34f, 34f, Hash(k, 5, 192)) : Mathf.Lerp(-20f, 20f, Hash(k, 5, 192));
                    float z = layer == 2 ? Mathf.Lerp(-4f, 80f, Hash(k, 7, 194)) : Mathf.Lerp(2f, 70f, Hash(k, 7, 194));
                    if (layer == 2 && x > RoomMin.x && x < RoomMax.x && z < RoomMax.z) x += Mathf.Sign(x) * 18f;
                    float y = layer == 0 ? 0.05f : layer == 1 ? 0.3f + Hash(k, 6, 193) * 0.4f : 0.5f + Hash(k, 6, 193) * 1.5f;
                    float w = (layer == 2 ? 8f : 4f) + Hash(k, 8, 195) * 5f;
                    float h = layer == 0 ? 0.7f : layer == 1 ? 1.3f : 3f;
                    var u = Quaternion.Euler(0f, Hash(k, 9, 196) * 180f, 0f) * Vector3.right * w;
                    mist.Card(new Vector3(x, y, z) - u * 0.5f, u, Vector3.up * h);
                }
                var layerGo = Place("GroundMist", mist, layer == 2 ? "FogBank" : "Steam", Vector3.zero, default, yard);
                var drift = layerGo.AddComponent<Game.Drift>();
                drift.axis = layer == 1 ? new Vector3(0.6f, 0f, 0.8f) : layer == 2 ? Vector3.forward : Vector3.right;
                drift.distance = 1f + layer;
                drift.period = 26f + layer * 9f;
            }
            GraveyardPlants(yard);
            puzzle.figure = RobedFigure(_world, new Vector3(0f, 0f, 10f), 180f);
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            Dust(new Vector3(0f, 1.5f, 20f), new Vector3(30f, 3f, 40f), 120, life);

            var rig = BuildPlayer(new Vector3(0f, 0f, 2.2f), 0f);
            BuildDirector(rig, puzzle, 4, "", 6, "");
            var director = UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>();
            director.revolver = BuildRevolver(rig);
            director.exitZ = 10000f;   // out through the gate is the ending (GraveyardEnding), not the director's exit
            GraveyardEnding(_world, puzzle, rig).moonlight = sun;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.75f, 0.77f, 0.9f);   // PSX/Lit reads the probe linearly: this is still night
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.24f, 0.25f, 0.3f);
            RenderSettings.fogStartDistance = 5f;
            RenderSettings.fogEndDistance = 32f;
            RenderSettings.skybox = null;
            RenderSettings.sun = sun;
            EditorSceneManager.SaveScene(scene, GraveyardPath);
            Debug.Log("PSX graveyard built: " + GraveyardPath);
        }
    }
}
