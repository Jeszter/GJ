using UnityEngine;

namespace GJ.PSX.EditorTools
{
    // Set dressing: small hand-made props (tools, tins, sacks, a radio, boots, a coat on a hook, a breaker panel, a
    // clock, notices...) and where they go in each room. None of it is part of a puzzle; it is there so the rooms look
    // used and lived in. Everything sits in a "Dressing" group and gets colliders from AutoColliders like the rest.
    public static partial class PSXRoomBuilder
    {
        static void MakePropMaterials(Shader lit)
        {
            Lit("Burlap", lit, MakeTexture("burlap", 16, (x, y) =>
                Shade(new Color(0.42f, 0.34f, 0.22f), ((x + y) % 2 == 0 ? 0.03f : -0.03f) + (Hash(x, y, 100) - 0.5f) * 0.08f)));
            Lit("Leather", lit, Plain("leather", new Color(0.2f, 0.13f, 0.08f), 0.08f));
            Lit("Olive", lit, MakeTexture("olive", 16, (x, y) =>
                Shade(new Color(0.25f, 0.27f, 0.17f), (Hash(x, y, 101) - 0.5f) * 0.06f + (y % 8 == 0 ? -0.04f : 0f))));
            Lit("Bakelite", lit, Plain("bakelite", new Color(0.26f, 0.14f, 0.07f), 0.05f), new Color(0.02f, 0.01f, 0f));
            Lit("Enamel", lit, Plain("enamel", new Color(0.82f, 0.8f, 0.74f), 0.06f));
            Lit("PaintBlue", lit, Plain("paint_blue", new Color(0.18f, 0.27f, 0.42f), 0.08f));
            Lit("PaintGreen", lit, Plain("paint_green", new Color(0.2f, 0.34f, 0.2f), 0.08f));
            Lit("Rubber", lit, Plain("rubber", new Color(0.07f, 0.07f, 0.07f), 0.05f));
            Lit("Mop", lit, MakeTexture("mop", 16, (x, y) =>
                x % 3 == 0 ? new Color(0.3f, 0.28f, 0.24f) : Shade(new Color(0.55f, 0.52f, 0.45f), (Hash(x, y, 102) - 0.5f) * 0.1f)));
            // White box with a red cross.
            Lit("FirstAid", lit, MakeTexture("first_aid", 16, (x, y) =>
            {
                bool cross = (x >= 6 && x <= 9 && y >= 3 && y <= 12) || (y >= 6 && y <= 9 && x >= 3 && x <= 12);
                return cross ? new Color(0.7f, 0.08f, 0.06f) : Shade(new Color(0.82f, 0.8f, 0.74f), (Hash(x, y, 103) - 0.5f) * 0.06f);
            }));
            // Clock face: twelve hour ticks; the hands are separate meshes.
            Lit("ClockFace", lit, MakeTexture("clock_face", 32, (x, y) =>
            {
                var d = new Vector2(x - 15.5f, y - 15.5f);
                float r = d.magnitude;
                if (r > 14.2f) return new Color(0.12f, 0.11f, 0.1f);
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                bool quarter = Mathf.Repeat(ang + 6f, 90f) < 12f;
                if (r > (quarter ? 9.5f : 11f) && r < 13.5f && Mathf.Repeat(ang + 4f, 30f) < (quarter ? 8f : 6f)) return new Color(0.06f, 0.06f, 0.06f);
                return Shade(new Color(0.85f, 0.82f, 0.72f), (Hash(x, y, 104) - 0.5f) * 0.05f - r * 0.004f);
            }));
            // A notice: red heading bar, rows of type, a signature.
            Lit("Notice", lit, MakeTexture("notice", 32, (x, y) =>
            {
                var c = Shade(new Color(0.8f, 0.76f, 0.64f), (Hash(x, y, 105) - 0.5f) * 0.05f);
                if (y >= 26 && y <= 29 && x > 3 && x < 28) return new Color(0.6f, 0.12f, 0.08f);
                if (y > 4 && y < 23 && y % 3 == 0 && x > 3 && x < 28 - (int)(Hash(0, y, 106) * 8f)) return Shade(c, -0.35f);
                if (y == 3 && x > 18 && x < 27) return Shade(c, -0.3f);
                return c;
            }));
            // Safety poster: yellow, a black triangle with a flame in it, a black band of text under it.
            Lit("Poster", lit, MakeTexture("poster", 32, (x, y) =>
            {
                var bg = Shade(new Color(0.82f, 0.66f, 0.14f), (Hash(x, y, 107) - 0.5f) * 0.06f);
                if (x < 1 || x > 30 || y < 1 || y > 30) return new Color(0.1f, 0.1f, 0.1f);
                if (y < 9 && y > 2) return y % 2 == 0 && x > 4 && x < 27 ? new Color(0.85f, 0.8f, 0.7f) : new Color(0.08f, 0.08f, 0.08f);
                float cx = 15.5f;
                bool tri = y >= 11 && y <= 28 && Mathf.Abs(x - cx) <= (28 - y) * 0.62f;
                bool inner = y >= 13 && y <= 25 && Mathf.Abs(x - cx) <= (25 - y) * 0.62f - 1f;
                float fv = (y - 13f) / 9f;
                bool flame = fv >= 0f && fv <= 1f && Mathf.Abs(x - cx - Mathf.Sin(fv * 5f) * 0.6f) < 3.2f * Mathf.Sin(fv * Mathf.PI * 0.9f + 0.15f) * (1f - fv * 0.5f);
                if (tri && !inner) return new Color(0.08f, 0.08f, 0.08f);
                if (flame) return fv < 0.4f ? new Color(0.85f, 0.25f, 0.05f) : new Color(0.08f, 0.08f, 0.08f);
                return bg;
            }));
            Lit("Photo", lit, MakeTexture("photo", 16, (x, y) =>
            {
                if (x < 1 || y < 1 || x > 14 || y > 14) return new Color(0.85f, 0.82f, 0.75f);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(8f, 8f));
                var sepia = Color.Lerp(new Color(0.5f, 0.4f, 0.28f), new Color(0.2f, 0.15f, 0.1f), y / 14f);
                if (d < 3f || (y < 6 && Mathf.Abs(x - 8f) < 4f)) sepia = new Color(0.15f, 0.11f, 0.08f);
                return Shade(sepia, (Hash(x, y, 108) - 0.5f) * 0.08f);
            }));
        }

        // ---------------------------------------------------------------- small props

        // An open-topped drum (bucket, pail): outer wall, inner wall, a rim between them and a floor inside, so
        // whatever is in it can be seen from above.
        static void OpenDrum(MeshBuilder mb, Vector3 baseCenter, float radius, float height, int sides, float wall = 0.015f, float uvScale = 2f)
        {
            float inner = radius - wall;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2f, a1 = (float)(i + 1) / sides * Mathf.PI * 2f;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 up = Vector3.up * height;
                mb.Quad(baseCenter + d0 * radius, (d1 - d0) * radius, up, uvScale);
                mb.Quad(baseCenter + d0 * inner, (d1 - d0) * inner, up, uvScale, 0f, true);
                mb.Quad(baseCenter + up + d0 * inner, d0 * wall, (d1 - d0) * inner, uvScale);
            }
            mb.Cylinder(baseCenter, inner, 0.012f, sides, uvScale);
            mb.Cylinder(baseCenter - Vector3.up * 0.001f, radius, 0.002f, sides, uvScale);
        }

        static Transform Group(string name, Vector3 pos, float yaw, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static void Hammer(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Hammer", pos, yaw, parent);
            Block("Handle", "Wood", new Vector3(0, 0.016f, 0), new Vector3(0.028f, 0.024f, 0.3f), default, 6f, g);
            Block("Head", "DarkMetal", new Vector3(0, 0.022f, 0.15f), new Vector3(0.11f, 0.036f, 0.036f), default, 6f, g);
            Block("Claw", "DarkMetal", new Vector3(-0.07f, 0.022f, 0.15f), new Vector3(0.04f, 0.026f, 0.02f), new Vector3(0, 0, 0), 6f, g);
        }

        static void Wrench(Vector3 pos, float yaw, Transform parent, float length = 0.24f)
        {
            var g = Group("Wrench", pos, yaw, parent);
            Block("Shaft", "Chrome", new Vector3(0, 0.006f, 0), new Vector3(0.026f, 0.01f, length), default, 8f, g);
            Block("Jaw_A", "Chrome", new Vector3(-0.012f, 0.007f, length * 0.5f + 0.02f), new Vector3(0.012f, 0.012f, 0.04f), default, 8f, g);
            Block("Jaw_B", "Chrome", new Vector3(0.012f, 0.007f, length * 0.5f + 0.02f), new Vector3(0.012f, 0.012f, 0.04f), default, 8f, g);
            Cyl("Ring", "Chrome", new Vector3(0, 0, -length * 0.5f - 0.015f), 0.022f, 0.012f, 8, default, 8f, g);
        }

        static void Screwdriver(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Screwdriver", pos, yaw, parent);
            Cyl("Handle", "RedPaint", new Vector3(0, 0.014f, -0.06f), 0.014f, 0.1f, 6, new Vector3(90, 0, 0), 6f, g);
            Cyl("Shaft", "Steel", new Vector3(0, 0.014f, 0.04f), 0.004f, 0.1f, 5, new Vector3(90, 0, 0), 6f, g);
        }

        static void OilCan(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("OilCan", pos, yaw, parent);
            Cyl("Body", "PaintGreen", Vector3.zero, 0.06f, 0.09f, 8, default, 6f, g);
            var mb = new MeshBuilder();
            mb.Tube(new Vector3(0, 0.09f, 0), new Vector3(0, 0.13f, 0), 0.05f, 0.012f, 8, Vector3.forward, 6f);
            mb.Tube(new Vector3(0, 0.13f, 0), new Vector3(0.0f, 0.2f, 0.09f), 0.007f, 0.003f, 5, Vector3.up, 6f);
            Place("Spout", mb, "Steel", Vector3.zero, default, g);
        }

        static void PaintCan(Vector3 pos, float yaw, string paint, Transform parent, float r = 0.08f, float h = 0.12f)
        {
            var g = Group("PaintCan", pos, yaw, parent);
            Cyl("Body", paint, Vector3.zero, r, h, 9, default, 4f, g);
            Cyl("Rim", "Steel", new Vector3(0, h, 0), r * 0.98f, 0.008f, 9, default, 4f, g);
            Block("Label", "Paper", new Vector3(0, h * 0.5f, -r * 0.96f), new Vector3(r * 1.1f, h * 0.5f, 0.004f), default, 6f, g);
            var bail = new MeshBuilder();
            bail.Tube(new Vector3(-r, h * 0.8f, 0), new Vector3(0, h + 0.05f, 0), 0.003f, 0.003f, 4, Vector3.forward, 6f);
            bail.Tube(new Vector3(0, h + 0.05f, 0), new Vector3(r, h * 0.8f, 0), 0.003f, 0.003f, 4, Vector3.forward, 6f);
            Place("Bail", bail, "Steel", Vector3.zero, default, g);
        }

        // A filled sack, sagging: a chamfered lump with a tied neck.
        static void Sack(Vector3 pos, float yaw, Transform parent, float tilt = 0f)
        {
            var g = Group("Sack", pos, yaw, parent);
            g.localRotation *= Quaternion.Euler(0, 0, tilt);
            var mb = new MeshBuilder();
            mb.ChamferBox(new Vector3(0, 0.12f, 0), new Vector3(0.5f, 0.24f, 0.32f), 0.08f, 2f);
            mb.Tube(new Vector3(0.25f, 0.12f, 0), new Vector3(0.33f, 0.13f, 0), 0.07f, 0.03f, 6, Vector3.up, 4f);
            Place("Bag", mb, "Burlap", Vector3.zero, default, g);
            Cyl("Tie", "Wood", new Vector3(0.31f, 0.13f, 0), 0.034f, 0.02f, 6, new Vector3(0, 0, 90), 6f, g);
        }

        static void Pallet(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Pallet", pos, yaw, parent);
            for (int i = 0; i < 3; i++)
                Block("Runner", "Wood", new Vector3(-0.35f + i * 0.35f, 0.045f, 0), new Vector3(0.09f, 0.09f, 1.0f), default, 2f, g);
            for (int i = 0; i < 6; i++)
                Block("Slat", "Wood", new Vector3(0, 0.1f, -0.44f + i * 0.176f), new Vector3(0.8f, 0.022f, 0.11f), new Vector3(0, Hash(i, 0, 110) * 3f - 1.5f, 0), 2f, g);
        }

        static void Radio(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Radio", pos, yaw, parent);
            Block("Case", "Bakelite", new Vector3(0, 0.07f, 0), new Vector3(0.24f, 0.14f, 0.12f), default, 6f, g);
            Block("Grille", "Grate", new Vector3(-0.05f, 0.075f, -0.061f), new Vector3(0.1f, 0.09f, 0.004f), default, 10f, g);
            Cyl("Dial", "Gauge", new Vector3(0.065f, 0.09f, -0.06f), 0.03f, 0.004f, 10, new Vector3(-90, 0, 0), 1f / 0.06f, g);
            Cyl("Knob_A", "DarkMetal", new Vector3(0.045f, 0.035f, -0.06f), 0.012f, 0.012f, 6, new Vector3(-90, 0, 0), 6f, g);
            Cyl("Knob_B", "DarkMetal", new Vector3(0.085f, 0.035f, -0.06f), 0.012f, 0.012f, 6, new Vector3(-90, 0, 0), 6f, g);
            Block("Handle", "Leather", new Vector3(0, 0.15f, 0), new Vector3(0.16f, 0.012f, 0.03f), default, 6f, g);
            var mb = new MeshBuilder();
            mb.Tube(new Vector3(0.1f, 0.14f, 0.03f), new Vector3(0.18f, 0.42f, 0.06f), 0.004f, 0.002f, 4, Vector3.forward, 6f);
            Place("Antenna", mb, "Chrome", Vector3.zero, default, g);
        }

        static void Books(Vector3 pos, float yaw, Transform parent, int count = 3)
        {
            var g = Group("Books", pos, yaw, parent);
            string[] covers = { "RedPaint", "PaintBlue", "PaintGreen", "Leather" };
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                float h = 0.025f + Hash(i, 0, 111) * 0.02f;
                Block("Book", covers[i % covers.Length], new Vector3(0, y + h * 0.5f, 0), new Vector3(0.16f, h, 0.22f), new Vector3(0, Hash(i, 1, 111) * 16f - 8f, 0), 6f, g);
                Block("Pages", "Paper", new Vector3(0.004f, y + h * 0.5f, 0), new Vector3(0.155f, h * 0.7f, 0.212f), new Vector3(0, Hash(i, 1, 111) * 16f - 8f, 0), 6f, g);
                y += h;
            }
        }

        static void Boots(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Boots", pos, yaw, parent);
            foreach (float side in new[] { -0.07f, 0.07f })
            {
                Block("Sole", "Rubber", new Vector3(side, 0.012f, 0.03f), new Vector3(0.1f, 0.024f, 0.28f), default, 6f, g);
                Block("Foot", "Leather", new Vector3(side, 0.06f, 0.05f), new Vector3(0.095f, 0.08f, 0.22f), default, 6f, g);
                Cyl("Shaft", "Leather", new Vector3(side, 0.05f, -0.04f), 0.05f, 0.24f, 7, new Vector3(side > 0 ? 6 : -4, 0, 0), 6f, g);
            }
        }

        // A coat hung on a wall hook: shoulders, a long body and two sleeves hanging down. Faces +Z of the group.
        static void Coat(Vector3 pos, float yaw, string cloth, Transform parent)
        {
            var g = Group("Coat", pos, yaw, parent);
            Block("Hook", "DarkMetal", new Vector3(0, 0.02f, -0.02f), new Vector3(0.03f, 0.05f, 0.06f), default, 6f, g);
            var mb = new MeshBuilder();
            mb.Limb(new Vector3(0, 0f, 0.05f), new Vector3(0, -0.85f, 0.07f), 0.38f, 0.1f, 0.46f, 0.08f, Vector3.forward, 2f);
            mb.Tube(new Vector3(-0.17f, -0.04f, 0.06f), new Vector3(-0.22f, -0.6f, 0.1f), 0.05f, 0.045f, 6, Vector3.forward, 2f);
            mb.Tube(new Vector3(0.17f, -0.04f, 0.06f), new Vector3(0.2f, -0.6f, 0.1f), 0.05f, 0.045f, 6, Vector3.forward, 2f);
            mb.Limb(new Vector3(0, 0.0f, 0.06f), new Vector3(0, -0.12f, 0.1f), 0.2f, 0.05f, 0.16f, 0.04f, Vector3.forward, 2f);
            Place("Body", mb, cloth, Vector3.zero, default, g);
            for (int i = 0; i < 4; i++)
                Cyl("Button", "Rubber", new Vector3(0, -0.2f - i * 0.15f, 0.125f), 0.01f, 0.006f, 5, new Vector3(-90, 0, 0), 6f, g);
        }

        // Wall clock with real hands (WallClock turns them). Faces +Z of the group.
        static void Clock(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Clock", pos, yaw, parent);
            Cyl("Case", "DarkMetal", Vector3.zero, 0.17f, 0.05f, 12, new Vector3(90, 0, 0), 4f, g);
            Cyl("Bezel", "Chrome", new Vector3(0, 0, 0.045f), 0.168f, 0.012f, 12, new Vector3(90, 0, 0), 4f, g);
            Cyl("Face", "ClockFace", new Vector3(0, 0, 0.05f), 0.15f, 0.003f, 12, new Vector3(90, 0, 0), 1f / 0.3f, g);
            Transform Hand(string name, float length, float width, float z, string mat)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(g, false);
                pivot.localPosition = new Vector3(0, 0, z);
                Block("Blade", mat, new Vector3(0, length * 0.5f - 0.015f, 0), new Vector3(width, length, 0.003f), default, 8f, pivot);
                return pivot;
            }
            var clock = g.gameObject.AddComponent<Game.WallClock>();
            clock.hour = Hand("Hour", 0.08f, 0.012f, 0.056f, "Rubber");
            clock.minute = Hand("Minute", 0.12f, 0.008f, 0.059f, "Rubber");
            clock.second = Hand("Second", 0.13f, 0.003f, 0.062f, "RedPaint");
            Cyl("Cap", "Rubber", new Vector3(0, 0, 0.063f), 0.008f, 0.004f, 6, new Vector3(90, 0, 0), 8f, g);
            var tick = g.gameObject.AddComponent<AudioSource>();
            tick.spatialBlend = 1f;
            tick.rolloffMode = AudioRolloffMode.Linear;
            tick.minDistance = 0.3f;
            tick.maxDistance = 4f;
            tick.playOnAwake = false;
            clock.tickClip = PSXSoundGenerator.Load("clock_tick");
        }

        // Breaker panel: a steel box with a lipped frame, two DIN rails of black breakers with white levers (one
        // tripped, its lever red and down), a label strip, the door standing open on its hinge, a conduit up top.
        static void BreakerPanel(Vector3 pos, float yaw, Transform parent, float ceiling)
        {
            var g = Group("BreakerPanel", pos, yaw, parent);
            const float w = 0.42f, h = 0.56f, d = 0.12f;
            Block("Back", "PaintGreen", new Vector3(0, 0, 0.005f), new Vector3(w, h, 0.01f), default, 3f, g);
            Block("Side_L", "PaintGreen", new Vector3(-w * 0.5f + 0.01f, 0, d * 0.5f), new Vector3(0.02f, h, d), default, 3f, g);
            Block("Side_R", "PaintGreen", new Vector3(w * 0.5f - 0.01f, 0, d * 0.5f), new Vector3(0.02f, h, d), default, 3f, g);
            Block("Top", "PaintGreen", new Vector3(0, h * 0.5f - 0.01f, d * 0.5f), new Vector3(w, 0.02f, d), default, 3f, g);
            Block("Bottom", "PaintGreen", new Vector3(0, -h * 0.5f + 0.01f, d * 0.5f), new Vector3(w, 0.02f, d), default, 3f, g);
            Block("Mount", "DarkMetal", new Vector3(0, 0, 0.012f), new Vector3(w - 0.05f, h - 0.05f, 0.004f), default, 3f, g);
            for (int row = 0; row < 2; row++)
            {
                float y = 0.1f - row * 0.2f;
                Block("Rail", "Steel", new Vector3(0, y, 0.02f), new Vector3(w - 0.08f, 0.035f, 0.01f), default, 8f, g);
                for (int i = 0; i < 4; i++)
                {
                    float x = -0.12f + i * 0.08f;
                    bool tripped = row == 1 && i == 2;
                    Block("Breaker", "Rubber", new Vector3(x, y, 0.05f), new Vector3(0.06f, 0.11f, 0.05f), default, 8f, g);
                    Block("Tag", "Paper", new Vector3(x, y + 0.04f, 0.0755f), new Vector3(0.04f, 0.015f, 0.002f), default, 8f, g);
                    Block("Lever", tripped ? "RedPaint" : "Enamel", new Vector3(x, y + (tripped ? -0.018f : 0.012f), 0.083f),
                          new Vector3(0.016f, 0.028f, 0.018f), new Vector3(tripped ? 25f : -25f, 0, 0), 8f, g);
                }
            }
            Block("Label", "Notice", new Vector3(0, 0.235f, 0.07f), new Vector3(0.3f, 0.04f, 0.004f), default, 1f / 0.3f, g);
            // Door on a hinge down the left edge, swung most of the way open.
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(g, false);
            hinge.localPosition = new Vector3(-w * 0.5f, 0, d);
            hinge.localRotation = Quaternion.Euler(0, -115f, 0);
            Block("Door", "PaintGreen", new Vector3(w * 0.5f, 0, 0.008f), new Vector3(w, h, 0.016f), default, 3f, hinge);
            Block("DoorHazard", "Hazard", new Vector3(w * 0.5f, 0.12f, 0.018f), new Vector3(0.12f, 0.08f, 0.004f), default, 6f, hinge);
            Block("Handle", "Chrome", new Vector3(w - 0.04f, 0, 0.022f), new Vector3(0.015f, 0.06f, 0.012f), default, 8f, hinge);
            foreach (float hy in new[] { -0.2f, 0.2f })
                Cyl("Knuckle", "Steel", new Vector3(-w * 0.5f, hy - 0.03f, d), 0.008f, 0.06f, 6, default, 8f, g);
            float up = ceiling - pos.y - h * 0.5f;
            if (up > 0.05f) Cyl("Conduit", "DarkMetal", new Vector3(0.12f, h * 0.5f, 0.05f), 0.025f, up, 6, default, 2f, g);
        }

        static void FirstAid(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("FirstAid", pos, yaw, parent);
            Block("Box", "Enamel", new Vector3(0, 0, 0.05f), new Vector3(0.3f, 0.24f, 0.1f), default, 4f, g);
            Block("Front", "FirstAid", new Vector3(0, 0, 0.101f), new Vector3(0.28f, 0.22f, 0.003f), default, 1f / 0.28f, g);
            Block("Latch", "Steel", new Vector3(0.12f, 0, 0.105f), new Vector3(0.015f, 0.04f, 0.01f), default, 8f, g);
        }

        // Plate on the wall: a notice, a poster, a photo. Faces +Z of the group.
        static void WallCard(string name, string material, Vector3 pos, float yaw, float w, float h, Transform parent, float spin = 0f)
        {
            var g = Group(name, pos, yaw, parent);
            Plate("Card", material, Vector3.zero, w, h, Vector3.forward, g, spin);
            Cyl("Pin", "RedPaint", new Vector3(0, h * 0.42f, 0.004f), 0.008f, 0.008f, 5, new Vector3(90, 0, 0), 8f, g);
        }

        static void OilDrum(Vector3 pos, float yaw, Transform parent, string paint = "PaintBlue")
        {
            var g = Group("Drum", pos, yaw, parent);
            Cyl("Body", paint, Vector3.zero, 0.29f, 0.88f, 12, default, 1f, g, true);
            foreach (float y in new[] { 0.02f, 0.3f, 0.58f, 0.86f })
                Cyl("Rib", "DarkMetal", new Vector3(0, y, 0), 0.298f, 0.025f, 12, default, 1f, g);
            Cyl("Bung", "Steel", new Vector3(0.15f, 0.88f, 0.08f), 0.03f, 0.015f, 6, default, 4f, g);
            Block("Hazard", "Hazard", new Vector3(0, 0.45f, -0.285f), new Vector3(0.2f, 0.14f, 0.01f), default, 2f, g);
        }

        static void Mop(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Mop", pos, yaw, parent);
            var mb = new MeshBuilder();
            mb.Tube(new Vector3(0, 0.08f, 0), new Vector3(0, 1.35f, 0.25f), 0.014f, 0.014f, 6, Vector3.forward, 4f);
            Place("Handle", mb, "Wood", Vector3.zero, default, g);
            var head = new MeshBuilder();
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                head.Tube(new Vector3(0, 0.1f, 0), new Vector3(Mathf.Cos(a) * 0.12f, 0.005f, Mathf.Sin(a) * 0.12f), 0.02f, 0.012f, 4, Vector3.up, 4f);
            }
            Place("Head", head, "Mop", Vector3.zero, default, g);
        }

        static void Bucket(Vector3 pos, float yaw, Transform parent, string material = "Steel")
        {
            var g = Group("Pail", pos, yaw, parent);
            var mb = new MeshBuilder();
            OpenDrum(mb, Vector3.zero, 0.14f, 0.26f, 10, 0.012f, 4f);
            Place("Body", mb, material, Vector3.zero, default, g);
            Cyl("Water", "Water", new Vector3(0, 0.18f, 0), 0.126f, 0.004f, 10, default, 4f, g);
        }

        static void Mug(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Mug", pos, yaw, parent);
            Cyl("Body", "Enamel", Vector3.zero, 0.04f, 0.09f, 8, default, 6f, g);
            Cyl("Rim", "PaintBlue", new Vector3(0, 0.085f, 0), 0.041f, 0.006f, 8, default, 6f, g);
            Block("Handle", "Enamel", new Vector3(0.05f, 0.045f, 0), new Vector3(0.015f, 0.055f, 0.012f), default, 6f, g);
        }

        static void PlateAndSpoon(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("Plate", pos, yaw, parent);
            Cyl("Plate", "Enamel", Vector3.zero, 0.11f, 0.012f, 10, default, 6f, g);
            Cyl("Food", "Leather", new Vector3(0.02f, 0.012f, 0), 0.05f, 0.006f, 7, default, 6f, g);
            Block("Spoon", "Chrome", new Vector3(-0.03f, 0.016f, 0.05f), new Vector3(0.012f, 0.004f, 0.15f), new Vector3(0, 30, 0), 8f, g);
        }

        static void Bottle(Vector3 pos, float yaw, Transform parent, bool lying = false)
        {
            var g = Group("Bottle", pos, yaw, parent);
            if (lying) g.localRotation *= Quaternion.Euler(90, 0, 0);
            Cyl("Body", "Glass", lying ? new Vector3(0, 0, -0.035f) : Vector3.zero, 0.035f, 0.2f, 6, default, 4f, g);
            Cyl("Neck", "Glass", lying ? new Vector3(0, 0.2f, -0.035f) : new Vector3(0, 0.2f, 0), 0.014f, 0.07f, 6, default, 4f, g);
        }

        static void TinCan(Vector3 pos, float yaw, Transform parent, bool lying = false)
        {
            var g = Group("Tin", pos, yaw, parent);
            if (lying) { g.localRotation *= Quaternion.Euler(0, 0, 90); g.localPosition += Vector3.up * 0.04f; }
            Cyl("Body", "Steel", lying ? new Vector3(0, -0.05f, 0) : Vector3.zero, 0.04f, 0.1f, 8, default, 6f, g);
            Block("Label", "PaintGreen", lying ? new Vector3(0, 0f, -0.039f) : new Vector3(0, 0.05f, -0.039f), new Vector3(0.05f, 0.06f, 0.003f), default, 6f, g);
        }

        static void CoiledWire(Vector3 pos, Transform parent, float radius = 0.13f, string material = "RedPaint")
        {
            var mb = new MeshBuilder();
            for (int loop = 0; loop < 3; loop++)
                for (int i = 0; i < 10; i++)
                {
                    float a0 = i / 10f * Mathf.PI * 2f, a1 = (i + 1) / 10f * Mathf.PI * 2f, r = radius - loop * 0.012f;
                    float y = 0.012f + loop * 0.014f;
                    mb.Tube(new Vector3(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), new Vector3(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), 0.008f, 0.008f, 4, Vector3.up, 6f);
                }
            Place("Coil", mb, material, pos, default, parent);
        }

        // A loose cable hanging from the ceiling and curling on itself at the end.
        static void DanglingCable(Vector3 top, float length, Transform parent)
        {
            var mb = new MeshBuilder();
            Vector3 prev = top;
            for (int i = 1; i <= 10; i++)
            {
                float k = i / 10f;
                var p = top + new Vector3(Mathf.Sin(k * 2.3f) * 0.08f, -length * k, Mathf.Sin(k * 1.4f) * 0.05f);
                mb.Tube(prev, p, 0.012f, 0.012f, 5, Vector3.forward, 4f);
                prev = p;
            }
            mb.Tube(prev, prev + new Vector3(0.03f, 0.02f, 0.02f), 0.018f, 0.006f, 5, Vector3.forward, 4f);
            Place("DanglingCable", mb, "DarkMetal", Vector3.zero, default, parent);
        }

        static void Litter(Vector3 center, float spread, int seed, Transform parent)
        {
            for (int i = 0; i < 6; i++)
            {
                var p = center + new Vector3((Hash(i, 0, seed) - 0.5f) * spread, 0.006f, (Hash(i, 1, seed) - 0.5f) * spread);
                Cyl("Butt", "Paper", p, 0.006f, 0.03f, 4, new Vector3(90, Hash(i, 2, seed) * 360f, 0), 8f, parent);
            }
            for (int i = 0; i < 5; i++)
            {
                var p = center + new Vector3((Hash(i, 3, seed) - 0.5f) * spread, 0.003f, (Hash(i, 4, seed) - 0.5f) * spread);
                Block("Shard", "Glass", p, new Vector3(0.03f + Hash(i, 5, seed) * 0.03f, 0.004f, 0.02f + Hash(i, 6, seed) * 0.03f), new Vector3(0, Hash(i, 7, seed) * 360f, 0), 8f, parent);
            }
            for (int i = 0; i < 4; i++)
            {
                var p = center + new Vector3((Hash(i, 8, seed) - 0.5f) * spread, 0.008f, (Hash(i, 9, seed) - 0.5f) * spread);
                Cyl("Bolt", "Steel", p, 0.008f, 0.03f, 6, new Vector3(90, Hash(i, 10, seed) * 360f, 0), 8f, parent);
            }
        }

        static void SparePipes(Vector3 pos, float yaw, Transform parent)
        {
            var g = Group("SparePipes", pos, yaw, parent);
            for (int i = 0; i < 3; i++)
            {
                float r = 0.05f - i * 0.006f;
                Cyl("Pipe", i == 1 ? "DarkMetal" : "Rust", new Vector3(-0.8f + Hash(i, 0, 112) * 0.1f, r + (i == 2 ? 0.09f : 0f), -0.12f + i * 0.11f - (i == 2 ? 0.06f : 0f)), r, 1.4f + Hash(i, 1, 112) * 0.3f, 8, new Vector3(0, 0, -90), 2f, g);
            }
            Block("Chock", "Wood", new Vector3(-0.4f, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.4f), default, 4f, g);
        }

        // ---------------------------------------------------------------- where it all goes

        static void DressRoom1()
        {
            var d = new GameObject("Dressing").transform;
            d.SetParent(_world, false);

            // Workbench: tools out, a vise on the corner, cans and wire on the lower shelf.
            var vise = Group("Vise", new Vector3(2.32f, 0.935f, 2.2f), 0f, d);
            Block("Base", "PaintBlue", new Vector3(0, 0.02f, 0), new Vector3(0.14f, 0.04f, 0.12f), default, 6f, vise);
            Block("JawFixed", "PaintBlue", new Vector3(0, 0.08f, 0.04f), new Vector3(0.14f, 0.08f, 0.04f), default, 6f, vise);
            Block("JawMoving", "PaintBlue", new Vector3(0, 0.08f, -0.06f), new Vector3(0.14f, 0.08f, 0.04f), default, 6f, vise);
            Cyl("Screw", "Steel", new Vector3(0, 0.07f, -0.08f), 0.01f, 0.12f, 6, new Vector3(-90, 0, 0), 6f, vise);
            Block("Tommy", "Steel", new Vector3(0, 0.07f, -0.2f), new Vector3(0.16f, 0.01f, 0.01f), default, 6f, vise);
            Hammer(new Vector3(2.28f, 0.935f, 3.0f), 75f, d);
            Wrench(new Vector3(2.8f, 0.935f, 3.72f), -20f, d);
            Screwdriver(new Vector3(2.27f, 0.935f, 3.25f), 100f, d);
            OilCan(new Vector3(2.82f, 0.935f, 2.3f), 30f, d);
            PaintCan(new Vector3(2.35f, 0.27f, 2.4f), 10f, "PaintBlue", d);
            PaintCan(new Vector3(2.55f, 0.27f, 2.55f), -30f, "RedPaint", d, 0.07f, 0.1f);
            CoiledWire(new Vector3(2.5f, 0.27f, 3.4f), d);
            TinCan(new Vector3(2.75f, 0.27f, 3.8f), 0f, d);

            // Back wall: a coat on a hook, a stopped clock, a notice board's worth of paper.
            Coat(new Vector3(-0.35f, 1.85f, 0.0f), 0f, "Olive", d);
            Clock(new Vector3(0.35f, 2.3f, 0.0f), 0f, d);
            WallCard("Notice_A", "Notice", new Vector3(-1.95f, 1.55f, 0.005f), 0f, 0.26f, 0.34f, d, 3f);
            WallCard("Notice_B", "Notice", new Vector3(-1.6f, 1.45f, 0.005f), 0f, 0.22f, 0.3f, d, -5f);
            WallCard("Photo", "Photo", new Vector3(-1.78f, 1.85f, 0.005f), 0f, 0.12f, 0.12f, d, 8f);

            // Right wall between the bench and the crates: breaker panel over a pallet of sacks.
            BreakerPanel(new Vector3(RoomMax.x, 1.65f, 5.3f), -90f, d, RoomMax.y);
            Pallet(new Vector3(2.45f, 0f, 5.15f), 90f, d);
            Sack(new Vector3(2.35f, 0.11f, 4.95f), 85f, d);
            Sack(new Vector3(2.35f, 0.11f, 5.4f), 95f, d);
            Sack(new Vector3(2.42f, 0.35f, 5.17f), 92f, d, 4f);

            // Left wall: a safety poster; door wall: first aid box.
            WallCard("Poster", "Poster", new Vector3(RoomMin.x + 0.005f, 1.6f, 5.95f), 90f, 0.4f, 0.4f, d);
            FirstAid(new Vector3(1.95f, 1.55f, RoomMax.z), 180f, d);
            Mop(new Vector3(-2.75f, 0f, 6.25f), -90f, d);
            Model("Survival/bucket", new Vector3(-2.45f, 0f, 6.35f), 0f, 0.32f, d);

            // Floor: litter round the knocked-over chair and by the door, a bottle that rolled under the bench,
            // cans by the rubble, a wrench dropped by the crates.
            Litter(new Vector3(1.6f, 0f, 4.2f), 0.7f, 120, d);
            Litter(new Vector3(-0.3f, 0f, 6.9f), 0.9f, 121, d);
            Model("Survival/bottle", new Vector3(2.2f, 0.04f, 3.6f), 60f, 0.28f, d, 90f);
            TinCan(new Vector3(-1.5f, 0f, 0.85f), 20f, d, true);
            TinCan(new Vector3(-1.75f, 0f, 0.7f), 0f, d);
            Wrench(new Vector3(1.75f, 0f, 6.6f), 40f, d, 0.3f);
            DanglingCable(new Vector3(-0.6f, RoomMax.y, 2.6f), 0.9f, d);
            DanglingCable(new Vector3(0.9f, RoomMax.y, 6.0f), 0.6f, d);
        }

        static void DressRoom2()
        {
            var d = new GameObject("Dressing").transform;
            d.SetParent(_world, false);

            // The table: a radio, a plate and a mug, books; boots under the cot and a coat above it.
            var tableTop = new Vector3(2.75f, 0.785f, 4.95f);
            Model("Furniture/radio", tableTop + new Vector3(-0.2f, 0f, -0.3f), 160f, 0.16f, d).name = "Radio";
            PlateAndSpoon(tableTop + new Vector3(0.12f, 0f, 0.05f), 0f, d);
            Mug(tableTop + new Vector3(0.3f, 0f, -0.08f), 40f, d);
            Model("Furniture/books", tableTop + new Vector3(-0.25f, 0f, 0.08f), 15f, 0.12f, d);
            Boots(new Vector3(2.55f, 0f, 1.35f), 170f, d);
            Coat(new Vector3(RoomMax.x, 1.75f, 3.1f), -90f, "Leather", d);
            WallCard("Photo", "Photo", new Vector3(RoomMax.x - 0.005f, 1.35f, 2.2f), -90f, 0.13f, 0.13f, d, -6f);
            Model("Survival/bottle-large", new Vector3(2.2f, 0.09f, 2.9f), 20f, 0.3f, d, 90f);

            // Back wall: breaker panel, a notice and a safety poster; tools and spare pipe by the pump.
            BreakerPanel(new Vector3(-1.45f, 1.55f, 0f), 0f, d, RoomMax.y);
            WallCard("Poster", "Poster", new Vector3(1.55f, 1.55f, 0.005f), 0f, 0.42f, 0.42f, d);
            WallCard("Notice", "Notice", new Vector3(0.95f, 1.45f, 0.005f), 0f, 0.24f, 0.32f, d, 4f);
            OilDrum(new Vector3(1.55f, 0f, 0.45f), 30f, d);
            SparePipes(new Vector3(1.2f, 0f, 1.6f), 90f, d);
            OilCan(new Vector3(2.2f, 0.16f, 0.82f), 120f, d);
            Wrench(new Vector3(2.75f, 0.16f, 0.86f), 90f, d, 0.3f);

            // By the drain: a mop and pail; sandbags against the door wall; a clock over the crates.
            Mop(new Vector3(-3.22f, 0f, 3.55f), -90f, d);
            Model("Survival/bucket", new Vector3(-2.95f, 0f, 3.75f), 30f, 0.32f, d);
            Sack(new Vector3(-2.65f, 0f, 7.15f), 5f, d);
            Sack(new Vector3(-2.75f, 0f, 6.75f), -10f, d);
            Sack(new Vector3(-2.68f, 0.21f, 6.95f), 2f, d, -3f);
            Clock(new Vector3(2.3f, 2.4f, RoomMax.z), 180f, d);
            FirstAid(new Vector3(2.95f, 1.6f, RoomMax.z), 180f, d);
            PaintCan(new Vector3(-3.2f, 0.38f, 5.2f), 0f, "PaintGreen", d);

            Litter(new Vector3(0.9f, 0f, 5.6f), 1.0f, 130, d);
            Litter(new Vector3(-0.6f, 0f, 2.0f), 0.8f, 131, d);
            TinCan(new Vector3(0.9f, 0f, 2.8f), 45f, d, true);
            CoiledWire(new Vector3(-0.4f, 0f, 0.95f), d, 0.16f, "DarkMetal");
            DanglingCable(new Vector3(-0.3f, RoomMax.y, 2.2f), 1.0f, d);
            DanglingCable(new Vector3(1.9f, RoomMax.y, 5.9f), 0.7f, d);
        }
    }
}
