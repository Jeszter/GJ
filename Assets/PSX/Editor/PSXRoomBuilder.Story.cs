using UnityEngine;

namespace GJ.PSX.EditorTools
{
    // The backstory's five little 3D sets, built 60 m under room 1 and filmed by the StoryStage camera:
    // the mine at night, the adit down to the bunker, the vault door sealing, the lighter and the powder,
    // and the body on the floor with the fire still crawling to the door.
    public static partial class PSXRoomBuilder
    {
        static readonly Vector3 StageOrigin = new Vector3(0f, -60f, 0f);

        static Transform NewSet(int index, Transform stage)
        {
            var set = new GameObject("Set_" + index).transform;
            set.SetParent(stage, false);
            set.localPosition = new Vector3(index * 40f, 0f, 0f);
            return set;
        }

        static Transform Shot(Transform set, Vector3 pos, Vector3 lookAt)
        {
            var t = new GameObject("Shot").transform;
            t.SetParent(set, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.LookRotation(lookAt - pos);
            return t;
        }

        static Light SetLight(Transform set, Vector3 pos, Color color, float range, float intensity)
        {
            var l = PointLight("Light", Vector3.zero, color, range, intensity);
            l.transform.SetParent(set, false);
            l.transform.localPosition = pos;
            return l;
        }

        // A crude PS1 person: boots, trousers, coat, arms, head and hard hat, standing on its feet at the group origin,
        // facing +Z. lying: on its back, head toward -X.
        static Transform Figure(string name, Vector3 pos, float yaw, Transform parent, bool lying = false)
        {
            var g = Group(name, pos, yaw, parent);
            var body = new GameObject("Body").transform;
            body.SetParent(g, false);
            if (lying)
            {
                body.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(-90f, 0f, 0f);
                body.localPosition = new Vector3(0.85f, 0.13f, 0f);
            }
            var cloth = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
            {
                cloth.Tube(new Vector3(0.1f * s, 0.1f, 0f), new Vector3(0.1f * s, 0.86f, 0f), 0.075f, 0.085f, 6, Vector3.forward, 2f);
                cloth.Tube(new Vector3(0.22f * s, 1.42f, 0f), new Vector3(0.27f * s, 0.98f, 0.04f), 0.06f, 0.05f, 6, Vector3.forward, 2f);
            }
            cloth.Limb(new Vector3(0, 0.84f, 0), new Vector3(0, 1.48f, 0), 0.36f, 0.22f, 0.44f, 0.24f, Vector3.forward, 2f);
            Place("Clothes", cloth, "Olive", Vector3.zero, default, body);
            var skin = new MeshBuilder();
            skin.Tube(new Vector3(0, 1.5f, 0), new Vector3(0, 1.74f, 0.01f), 0.095f, 0.085f, 7, Vector3.forward, 4f);
            foreach (float s in new[] { -1f, 1f })
                skin.Tube(new Vector3(0.27f * s, 0.98f, 0.04f), new Vector3(0.28f * s, 0.88f, 0.05f), 0.045f, 0.04f, 5, Vector3.forward, 4f);
            Place("Skin", skin, "Skin", Vector3.zero, default, body);
            var boots = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f }) boots.Box(new Vector3(0.1f * s, 0.06f, 0.04f), new Vector3(0.12f, 0.12f, 0.26f), 4f);
            Place("Boots", boots, "Leather", Vector3.zero, default, body);
            var hat = new MeshBuilder();
            hat.Tube(new Vector3(0, 1.7f, 0.01f), new Vector3(0, 1.84f, 0.01f), 0.13f, 0.07f, 8, Vector3.forward, 4f);
            hat.Tube(new Vector3(0, 1.69f, 0.01f), new Vector3(0, 1.71f, 0.01f), 0.16f, 0.16f, 8, Vector3.forward, 4f);
            Place("Helmet", hat, "Helmet", Vector3.zero, default, body);
            return g;
        }

        // A burning flame for the sets: tongue, core and halo with a flickering light, scaled by size.
        static void SetFlame(Transform parent, Vector3 pos, float size, float lightRange, float intensity)
        {
            var f = new GameObject("Flame").transform;
            f.SetParent(parent, false);
            f.localPosition = pos;
            var halo = new MeshBuilder(); halo.Cross(new Vector3(0, -0.3f * size, 0), 1.4f * size, 1.4f * size);
            Place("Halo", halo, "FlameHalo", Vector3.zero, default, f);
            var tongue = new MeshBuilder(); tongue.Cross(Vector3.zero, 0.45f * size, size);
            Place("Tongue", tongue, "Flame", Vector3.zero, default, f);
            var core = new MeshBuilder(); core.Cross(new Vector3(0, 0.05f * size, 0), 0.2f * size, 0.55f * size);
            Place("Core", core, "FlameCore", Vector3.zero, default, f);
            var l = SetLight(parent, pos + Vector3.up * size * 0.5f, new Color(1f, 0.62f, 0.3f), lightRange, intensity);
            var flicker = f.gameObject.AddComponent<FlameFlicker>();
            flicker.flame = f;
            flicker.flameLight = l;
            flicker.baseIntensity = intensity;
            flicker.lean = 6f;
        }

        static Game.StoryStage BuildStoryStage()
        {
            var root = new GameObject("StoryStage").transform;
            root.SetParent(_world, false);
            root.localPosition = StageOrigin;

            var camGo = new GameObject("StoryCamera");
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.025f, 0.05f);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 45f;

            var sets = new GameObject[5];
            var shots = new Transform[5];
            var drift = new Vector3[5];
            var turn = new float[5];

            // ---- 0. 1987: the mine at night.
            {
                var s = NewSet(0, root);
                Block("Ground", "Ash", new Vector3(0, -0.05f, 10f), new Vector3(50f, 0.1f, 40f), default, 0.5f, s);
                var heap = new MeshBuilder();
                heap.Tube(Vector3.zero, new Vector3(0, 5f, 0), 7f, 0.3f, 9, Vector3.forward, 0.5f);
                Place("SpoilHeap", heap, "Ash", new Vector3(10f, 0f, 16f), default, s);
                // Headframe: four raking legs, braces, a platform and the big sheave wheel.
                var frame = new MeshBuilder();
                Vector3 top = new Vector3(0, 7f, 9f);
                foreach (var foot in new[] { new Vector3(-1.6f, 0, 7.6f), new Vector3(1.6f, 0, 7.6f), new Vector3(-1.6f, 0, 10.4f), new Vector3(1.6f, 0, 10.4f) })
                    frame.Limb(foot, top + new Vector3(Mathf.Sign(foot.x) * 0.4f, 0, (foot.z - 9f) * 0.25f), 0.2f, 0.2f, 0.16f, 0.16f, Vector3.forward, 1f);
                for (int k = 1; k <= 3; k++)
                {
                    float y = k * 1.8f, w = 1.6f - k * 0.3f;
                    frame.Box(new Vector3(0, y, 7.6f + k * 0.35f), new Vector3(w * 2f, 0.12f, 0.12f), 1f);
                    frame.Box(new Vector3(0, y, 10.4f - k * 0.35f), new Vector3(w * 2f, 0.12f, 0.12f), 1f);
                }
                frame.Limb(new Vector3(0, 0, 3.5f), new Vector3(0, 6.2f, 8.6f), 0.22f, 0.22f, 0.18f, 0.18f, Vector3.up, 1f);
                frame.Box(top + new Vector3(0, 0.1f, 0), new Vector3(1.4f, 0.15f, 1.4f), 1f);
                Place("Headframe", frame, "Rust", Vector3.zero, default, s);
                var wheel = new GameObject("Sheave").transform;
                wheel.SetParent(s, false);
                wheel.localPosition = top + new Vector3(0, 1.1f, 0);
                wheel.localRotation = Quaternion.Euler(0, 0, 90);
                ValveWheel(wheel, 1.0f, "DarkMetal");
                // Winding house with one lit window, a shed, fences, trees, rocks and a signpost.
                Model("Survival/structure-metal", new Vector3(-5.5f, 0f, 8f), 15f, 3.2f, s);
                Model("Survival/structure-metal-roof", new Vector3(-5.5f, 3.2f, 8f), 15f, 0.9f, s);
                Block("Window", "LanternGlass", new Vector3(-4.6f, 1.6f, 6.25f), new Vector3(0.6f, 0.45f, 0.05f), new Vector3(0, 15, 0), 2f, s);
                SetLight(s, new Vector3(-4.4f, 1.6f, 5.5f), new Color(1f, 0.7f, 0.35f), 6f, 1.6f);
                for (int i = 0; i < 7; i++)
                    Model(i == 3 ? "Survival/fence-doorway" : "Survival/fence", new Vector3(-6f + i * 2f, 0f, 4.5f), 0f, 1.2f, s);
                Model("Survival/tree-tall", new Vector3(-10f, 0f, 13f), 0f, 7f, s);
                Model("Survival/tree", new Vector3(6.5f, 0f, 7f), 40f, 4.5f, s);
                Model("Survival/tree-trunk", new Vector3(3.5f, 0f, 3.5f), 10f, 1.4f, s);
                Model("Survival/rock-a", new Vector3(1.6f, 0f, 2.6f), 30f, 0.6f, s);
                Model("Survival/rock-b", new Vector3(-2.6f, 0f, 3.2f), 70f, 0.5f, s);
                Model("Survival/rock-c", new Vector3(5.5f, 0f, 11f), 0f, 1.2f, s);
                Model("Survival/signpost", new Vector3(-1.2f, 0f, 3f), -20f, 1.7f, s);
                // Moon, stars, cold moonlight.
                var moon = new MeshBuilder(); moon.Cross(Vector3.zero, 4f, 4f);
                Place("Moon", moon, "MoonGlow", new Vector3(-8f, 13f, 30f), default, s);
                for (int i = 0; i < 70; i++)
                {
                    float a = Hash(i, 0, 150) * Mathf.PI - Mathf.PI * 0.5f, e = 0.2f + Hash(i, 1, 150) * 1.1f;
                    var p = new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)) * 38f;
                    var star = new MeshBuilder(); star.Cross(Vector3.zero, 0.25f, 0.25f);
                    Place("Star", star, "Mote", p, default, s);
                }
                SetLight(s, new Vector3(-6f, 12f, 20f), new Color(0.5f, 0.6f, 0.95f), 35f, 1.1f);
                SetLight(s, new Vector3(0f, 3f, 2f), new Color(0.4f, 0.45f, 0.7f), 12f, 0.6f);
                sets[0] = s.gameObject;
                shots[0] = Shot(s, new Vector3(2.2f, 1.5f, -1f), new Vector3(-0.5f, 4.2f, 9f));
                drift[0] = new Vector3(-0.22f, 0.02f, 0.05f);
                turn[0] = 0.5f;
            }

            // ---- 1. The adit down to the bunker.
            {
                var s = NewSet(1, root);
                const int segs = 12;
                var segments = new Transform[segs];
                for (int k = 0; k < segs; k++)
                {
                    var seg = new GameObject("Seg").transform;
                    segments[k] = seg;
                    seg.SetParent(s, false);
                    seg.localPosition = new Vector3(0, -k * 0.32f, k * 1.5f);
                    seg.localRotation = Quaternion.Euler(12f, 0, 0);
                    Block("Floor", "Concrete", new Vector3(0, -0.05f, 0), new Vector3(2.6f, 0.1f, 1.55f), default, 1f, seg);
                    Block("Wall_L", "Stone", new Vector3(-1.35f, 1.2f, 0), new Vector3(0.1f, 2.5f, 1.55f), default, 0.5f, seg);
                    Block("Wall_R", "Stone", new Vector3(1.35f, 1.2f, 0), new Vector3(0.1f, 2.5f, 1.55f), default, 0.5f, seg);
                    Block("Roof", "Ceiling", new Vector3(0, 2.45f, 0), new Vector3(2.8f, 0.1f, 1.55f), default, 1f, seg);
                    foreach (float x in new[] { -1.15f, 1.15f }) Block("Post", "Wood", new Vector3(x, 1.1f, 0.7f), new Vector3(0.16f, 2.2f, 0.16f), default, 1f, seg);
                    Block("Cap", "Wood", new Vector3(0, 2.28f, 0.7f), new Vector3(2.5f, 0.18f, 0.18f), default, 1f, seg);
                    foreach (float x in new[] { -0.45f, 0.45f }) Block("Rail", "Steel", new Vector3(x, 0.03f, 0), new Vector3(0.05f, 0.06f, 1.55f), default, 2f, seg);
                    for (int t = 0; t < 3; t++) Block("Sleeper", "Wood", new Vector3(0, 0.0f, -0.5f + t * 0.5f), new Vector3(1.2f, 0.05f, 0.14f), default, 2f, seg);
                    if (k % 2 == 1)
                    {
                        Block("Bulb", "Bulb", new Vector3(0, 2.18f, 0.7f), new Vector3(0.07f, 0.09f, 0.07f), default, 4f, seg);
                        var l = SetLight(s, Vector3.zero, new Color(1f, 0.78f, 0.45f), 3.2f, 1.3f);
                        l.transform.position = seg.TransformPoint(new Vector3(0, 2.0f, 0.7f));
                    }
                }
                // A mine cart left on the rails, and the bunker wall at the bottom.
                var cartSeg = segments[4];
                var cart = Group("Cart", Vector3.zero, 0f, cartSeg);
                Block("Tub", "Rust", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.5f, 1.2f), default, 1f, cart);
                foreach (float x in new[] { -0.45f, 0.45f })
                    foreach (float z in new[] { -0.4f, 0.4f })
                        Cyl("Wheel", "DarkMetal", new Vector3(x, 0.13f, z), 0.13f, 0.06f, 8, new Vector3(0, 0, 90), 2f, cart);
                Model("Survival/resource-stone-large", new Vector3(0, 0.7f, 0), 0f, 0.35f, cart);
                var end = segments[segs - 1];
                Block("BunkerWall", "Concrete", new Vector3(0, 1.2f, 0.8f), new Vector3(2.7f, 2.5f, 0.2f), default, 1f, end);
                Plate("Sign", "SignSector", end.TransformPoint(new Vector3(0, 1.9f, 0.69f)) - s.position, 1.0f, 0.25f, Vector3.back, s);
                Block("Hatch", "VaultMetal", new Vector3(0, 0.85f, 0.68f), new Vector3(1.0f, 1.5f, 0.06f), default, 0.6f, end);
                var red = SetLight(s, Vector3.zero, new Color(1f, 0.15f, 0.08f), 4f, 1.4f);
                red.transform.position = end.TransformPoint(new Vector3(0.7f, 2.0f, 0.3f));
                var pulse = red.gameObject.AddComponent<FlameFlicker>();
                pulse.flameLight = red; pulse.baseIntensity = 1.4f; pulse.flicker = 0.9f; pulse.speed = 2f;
                Model("Survival/box-large", new Vector3(-0.8f, 0f, 0.1f), 10f, 0.4f, segments[7]);
                Model("Survival/barrel", new Vector3(0.85f, 0f, -0.2f), 0f, 0.8f, segments[9]);
                sets[1] = s.gameObject;
                shots[1] = Shot(s, new Vector3(0.2f, 1.55f, -1.5f), new Vector3(0f, -1.6f, 10f));
                drift[1] = new Vector3(0f, 0f, 0.45f);
                turn[1] = 0f;
            }

            // ---- 2. The vault door seals behind you.
            {
                var s = NewSet(2, root);
                Block("Floor", "Floor", new Vector3(0, -0.05f, 1f), new Vector3(8f, 0.1f, 9f), default, 0.5f, s);
                Block("Wall", "Stone", new Vector3(0, 1.7f, 4.1f), new Vector3(8f, 3.4f, 0.2f), default, 0.5f, s);
                Block("Wall_L", "Stone", new Vector3(-3.6f, 1.7f, 1f), new Vector3(0.2f, 3.4f, 9f), default, 0.5f, s);
                Block("Wall_R", "Stone", new Vector3(3.6f, 1.7f, 1f), new Vector3(0.2f, 3.4f, 9f), default, 0.5f, s);
                Block("Ceiling", "Ceiling", new Vector3(0, 3.45f, 1f), new Vector3(8f, 0.1f, 9f), default, 1f, s);
                foreach (float x in new[] { -1.05f, 1.05f }) Block("Frame", "DarkMetal", new Vector3(x, 1.2f, 3.88f), new Vector3(0.3f, 2.4f, 0.24f), default, 1f, s);
                Block("FrameTop", "DarkMetal", new Vector3(0, 2.55f, 3.88f), new Vector3(2.4f, 0.3f, 0.24f), default, 1f, s);
                Block("Leaf", "VaultMetal", new Vector3(0, 1.2f, 3.92f), new Vector3(1.8f, 2.4f, 0.12f), default, 0.5f, s);
                var wheel = new GameObject("Wheel").transform;
                wheel.SetParent(s, false);
                wheel.localPosition = new Vector3(0, 1.25f, 3.82f);
                wheel.localRotation = Quaternion.Euler(-90, 0, 0);
                ValveWheel(wheel, 0.36f, "Steel");
                var stripes = new MeshBuilder();
                stripes.Quad(new Vector3(-1.2f, 0.006f, 2.9f), new Vector3(2.4f, 0, 0), new Vector3(0, 0, 0.9f), 2f);
                Place("Stripes", stripes, "Hazard", Vector3.zero, default, s);
                Block("RedLamp", "RedLamp", new Vector3(0, 2.95f, 3.95f), new Vector3(0.16f, 0.16f, 0.12f), default, 1f, s);
                var red = SetLight(s, new Vector3(0, 2.8f, 3.4f), new Color(1f, 0.12f, 0.06f), 7f, 2.2f);
                var pulse = red.gameObject.AddComponent<FlameFlicker>();
                pulse.flameLight = red; pulse.baseIntensity = 2.2f; pulse.flicker = 0.95f; pulse.speed = 2.5f;
                // Charges stacked on the left, boxes and a barrel on the right.
                var crate = Group("Charges", new Vector3(-2.3f, 0f, 2.6f), 20f, s);
                Model("Survival/box-large-open", Vector3.zero, 90f, 0.5f, crate);
                for (int i = 0; i < 6; i++)
                    Cyl("Stick", "Dynamite", new Vector3(-0.25f + i * 0.1f, 0.36f, (i % 2) * 0.08f - 0.04f), 0.035f, 0.3f, 6, new Vector3(0, 0, 0), 4f, crate);
                Model("Survival/box-large", new Vector3(2.4f, 0f, 3.1f), -10f, 0.45f, s);
                Model("Survival/box", new Vector3(2.4f, 0.45f, 3.1f), 25f, 0.32f, s);
                Model("Survival/barrel", new Vector3(2.9f, 0f, 2.2f), 0f, 0.85f, s);
                // You, from behind, a lighter low in your right hand.
                Figure("Miner", new Vector3(0.45f, 0f, 1.2f), 8f, s);
                SetFlame(s, new Vector3(0.78f, 0.95f, 1.35f), 0.07f, 3f, 1.2f);
                SetLight(s, new Vector3(0f, 2.5f, -1.5f), new Color(0.45f, 0.55f, 0.85f), 7f, 0.7f);
                sets[2] = s.gameObject;
                shots[2] = Shot(s, new Vector3(1.1f, 1.55f, -2.6f), new Vector3(0f, 1.4f, 4f));
                drift[2] = new Vector3(-0.04f, 0f, 0.14f);
                turn[2] = -0.3f;
            }

            // ---- 3. Ninety seconds of air. A lighter. A trail of powder.
            {
                var s = NewSet(3, root);
                Block("Floor", "Floor", new Vector3(0, -0.05f, 1.5f), new Vector3(8f, 0.1f, 8f), default, 0.5f, s);
                // A Zippo six times life size, open, burning.
                var zippo = Group("Zippo", new Vector3(0f, 0f, 0f), -20f, s);
                var caseMb = new MeshBuilder();
                caseMb.ChamferBox(new Vector3(0, 0.135f, 0), new Vector3(0.23f, 0.27f, 0.08f), 0.024f, 6f);
                Place("Case", caseMb, "Chrome", Vector3.zero, default, zippo);
                var chimney = new MeshBuilder { FaceShade = false };
                chimney.ChamferBox(new Vector3(-0.018f, 0.32f, 0), new Vector3(0.15f, 0.1f, 0.055f), 0.01f, 1f / 0.15f);
                Place("Chimney", chimney, "Chimney", Vector3.zero, default, zippo);
                var lid = new MeshBuilder();
                lid.ChamferBox(new Vector3(0.115f, 0.093f, 0), new Vector3(0.23f, 0.186f, 0.08f), 0.024f, 6f);
                Place("Lid", lid, "Chrome", new Vector3(-0.115f, 0.27f, 0), new Vector3(0, 0, 135), zippo);
                Cyl("Wheel", "Steel", new Vector3(0.075f, 0.33f, -0.03f), 0.027f, 0.05f, 6, new Vector3(90, 0, 0), 4f, zippo);
                SetFlame(zippo, new Vector3(-0.018f, 0.37f, 0), 0.32f, 4f, 2.2f);
                // The powder trail curling away into the dark.
                var trail = Smooth(new[] { new Vector3(0.25f, 0.012f, 0.05f), new Vector3(0.7f, 0.012f, 0.6f), new Vector3(0.5f, 0.012f, 1.4f), new Vector3(1.2f, 0.012f, 2.3f), new Vector3(1.0f, 0.012f, 3.4f), new Vector3(1.8f, 0.012f, 4.6f) }, 6);
                var powder = new MeshBuilder();
                powder.Ribbon(trail, 0.08f, 4f);
                Place("Powder", powder, "Powder", Vector3.zero, default, s);
                // The air counter on a crate behind.
                Model("Survival/box-large", new Vector3(-0.9f, 0f, 1.6f), 80f, 0.42f, s);
                var display = Group("AirCounter", new Vector3(-0.9f, 0.42f, 1.55f), -15f, s);
                Block("Body", "Display", new Vector3(0, 0.12f, 0), new Vector3(0.5f, 0.2f, 0.1f), default, 2f, display);
                var digits = new MeshBuilder { FaceShade = false };
                digits.Quad(new Vector3(-0.24f, 0.03f, -0.052f), new Vector3(0.48f, 0, 0), new Vector3(0, 0.18f, 0), 1f / 0.48f);
                var digitsGo = Place("Digits", digits, "DisplayDigits", Vector3.zero, default, display);
                var timer = digitsGo.AddComponent<Game.SegmentDisplay>();
                timer.target = digitsGo.GetComponent<Renderer>();
                SetLight(s, new Vector3(-0.9f, 0.7f, 1.2f), new Color(1f, 0.15f, 0.08f), 1.5f, 0.6f);
                sets[3] = s.gameObject;
                shots[3] = Shot(s, new Vector3(0.35f, 0.42f, -0.95f), new Vector3(-0.1f, 0.25f, 0.4f));
                drift[3] = new Vector3(0.05f, 0.004f, -0.01f);
                turn[3] = -2f;
                timerDisplay = timer;
            }

            // ---- 4. The room goes on without you.
            {
                var s = NewSet(4, root);
                Block("Floor", "Floor", new Vector3(0, -0.05f, 1.5f), new Vector3(9f, 0.1f, 9f), default, 0.5f, s);
                Block("Wall_A", "Stone", new Vector3(-2.2f, 1.7f, 5f), new Vector3(5.4f, 3.4f, 0.2f), default, 0.5f, s);
                Block("Wall_B", "Stone", new Vector3(3.3f, 1.7f, 5f), new Vector3(2.2f, 3.4f, 0.2f), default, 0.5f, s);
                Block("Lintel", "Stone", new Vector3(1.4f, 2.9f, 5f), new Vector3(1.6f, 1.0f, 0.2f), default, 0.5f, s);
                Block("Ceiling", "Ceiling", new Vector3(0, 3.45f, 1.5f), new Vector3(9f, 0.1f, 9f), default, 1f, s);
                // The door stands open; cold light pours in from the corridor.
                Block("Leaf", "VaultMetal", new Vector3(0.75f, 1.2f, 4.2f), new Vector3(0.12f, 2.4f, 1.6f), new Vector3(0, 20, 0), 0.5f, s);
                Block("ExitGlow", "ExitGlow", new Vector3(1.4f, 1.2f, 7f), new Vector3(1.6f, 2.4f, 0.1f), default, 1f, s);
                Block("CorridorFloor", "Floor", new Vector3(1.4f, -0.05f, 6f), new Vector3(1.6f, 0.1f, 2f), default, 0.5f, s);
                Block("CorridorWall_L", "Stone", new Vector3(0.55f, 1.2f, 6f), new Vector3(0.1f, 2.4f, 2f), default, 0.5f, s);
                Block("CorridorWall_R", "Stone", new Vector3(2.25f, 1.2f, 6f), new Vector3(0.1f, 2.4f, 2f), default, 0.5f, s);
                Block("CorridorRoof", "Ceiling", new Vector3(1.4f, 2.42f, 6f), new Vector3(1.8f, 0.1f, 2f), default, 1f, s);
                SetLight(s, new Vector3(1.4f, 1.4f, 5.6f), new Color(0.75f, 0.85f, 1f), 9f, 3f);
                // You, where the air ran out; the lighter by your hand; the fire crawling on.
                Figure("Body", new Vector3(-1.6f, 0f, 0.6f), -70f, s, true);
                Cyl("DroppedLighter", "Chrome", new Vector3(-1.05f, 0.02f, 0.05f), 0.03f, 0.05f, 6, new Vector3(0, 0, 90), 6f, s);
                var path = Smooth(new[] { new Vector3(-1.0f, 0.012f, 0.1f), new Vector3(-0.2f, 0.012f, 0.4f), new Vector3(0.2f, 0.012f, 1.6f), new Vector3(0.9f, 0.012f, 2.7f), new Vector3(1.3f, 0.012f, 4.6f) }, 6);
                var powder = new MeshBuilder();
                powder.Ribbon(path, 0.08f, 4f);
                Place("Powder", powder, "Powder", Vector3.zero, default, s);
                var spark = new GameObject("Spark").transform;
                spark.SetParent(s, false);
                SetFlame(spark, Vector3.zero, 0.1f, 3f, 2f);
                var crawl = spark.gameObject.AddComponent<Game.Crawl>();
                var world = new Vector3[path.Count];
                for (int i = 0; i < path.Count; i++) world[i] = s.TransformPoint(path[i]);
                crawl.path = world;
                crawl.seconds = 9f;
                crawl.startAt = 0.2f;
                Model("Survival/box-open", new Vector3(-2.8f, 0f, 3.6f), 20f, 0.36f, s);
                Model("Survival/barrel", new Vector3(3.2f, 0f, 3.8f), 0f, 0.85f, s);
                SetLight(s, new Vector3(-2f, 2.6f, -1f), new Color(0.4f, 0.5f, 0.8f), 7f, 0.5f);
                sets[4] = s.gameObject;
                shots[4] = Shot(s, new Vector3(-2.7f, 0.55f, -1.3f), new Vector3(1.1f, 0.7f, 4.6f));
                drift[4] = new Vector3(0.06f, 0.004f, 0.06f);
                turn[4] = 0.4f;
            }

            var stage = root.gameObject.AddComponent<Game.StoryStage>();
            stage.cam = cam;
            stage.sets = sets;
            stage.shots = shots;
            stage.drift = drift;
            stage.turn = turn;
            stage.timer = timerDisplay;
            return stage;
        }

        static Game.SegmentDisplay timerDisplay;
    }
}
