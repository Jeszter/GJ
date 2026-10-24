using System.Collections.Generic;
using UnityEngine;

namespace GJ.PSX.EditorTools
{
    // What dying leaves behind: the body from a failed try (copied in at runtime wherever it fell), and the spring
    // plates in the floor that a body is heavy enough to hold down.
    public static partial class PSXRoomBuilder
    {
        // The miner from the story cards, lying on its back, head along +Z and the torso about half a metre behind
        // the origin; kept switched off for the director to copy.
        static Game.Corpse CorpseTemplate()
        {
            var holder = new GameObject("PastBodies").transform;
            holder.SetParent(_world, false);
            var body = new GameObject("PastBody").transform;
            body.SetParent(holder, false);

            // The miner from the story cards, fallen on his back with an arm flung out: tipped back so his front faces
            // the ceiling, turned so the head points along +Z, feet 1.6 m behind the origin.
            var fallen = Model("../LOWHO/Zombie", Vector3.zero, 0f, 1.72f, body, 0f, null, null, PoseFallen, true);
            if (fallen != null)
            {
                fallen.transform.localPosition = new Vector3(0f, 0.14f, -1.62f);
                fallen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
                AsMiner(fallen);
            }

            // The hard hat knocked off and lying upturned beside the head.
            var hat = new MeshBuilder();
            hat.Tube(new Vector3(0, 0f, 0), new Vector3(0, 0.12f, 0), 0.14f, 0.07f, 8, Vector3.forward, 4f);
            hat.Tube(new Vector3(0, -0.01f, 0), new Vector3(0, 0.01f, 0), 0.17f, 0.17f, 8, Vector3.forward, 4f);
            Place("HardHat", hat, "Helmet", new Vector3(-0.32f, 0.1f, 0.12f), new Vector3(150f, 30f, 0f), body);

            var corpse = body.gameObject.AddComponent<Game.Corpse>();
            body.gameObject.SetActive(false);
            return corpse;
        }

        // The model is painted as a zombie: the same body repainted as a man in his work blues.
        static void AsMiner(GameObject model)
        {
            if (!Mats.ContainsKey("MinerBody")) Lit("MinerBody", Shader.Find("PSX/Lit"), MinerBodyTexture());
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Mats["MinerBody"];
                r.sharedMaterials = mats;
            }
        }

        // The LOWHO zombie's texture with the green skin turned to a living (well, recently living) colour and the
        // ragged shirt to faded blue overalls; everything darker left alone.
        static Texture2D MinerBodyTexture()
        {
            var src = new Texture2D(2, 2);
            src.LoadImage(System.IO.File.ReadAllBytes("Assets/ThirdParty/LOWHO/Texture/Texture.png"));
            var tex = MakeTexture("miner_body", src.width, src.height, (x, y) =>
            {
                var c = src.GetPixel(x, y);
                Color.RGBToHSV(c, out float h, out float sat, out float val);
                if (sat > 0.18f && h > 0.15f && h < 0.48f) return Color.HSVToRGB(0.06f, 0.42f, Mathf.Clamp01(val * 1.05f));
                if (sat > 0.25f && (h < 0.12f || h > 0.9f)) return Color.HSVToRGB(0.6f, 0.35f, Mathf.Clamp01(val * 0.9f));
                return c;
            });
            Object.DestroyImmediate(src);
            return tex;
        }

        // A spring plate let into the floor: a sunken steel frame round a striped tread that rides down under weight.
        static Game.PressurePlate PressurePlateAt(string name, Vector3 center, float yaw, Transform parent)
        {
            const float half = 0.4f;
            var g = Group(name, center, yaw, parent);
            foreach (float s in new[] { -1f, 1f })
            {
                Block("Frame", "DarkMetal", new Vector3(0, 0.012f, (half + 0.04f) * s), new Vector3(half * 2f + 0.16f, 0.024f, 0.08f), default, 4f, g);
                Block("Frame", "DarkMetal", new Vector3((half + 0.04f) * s, 0.012f, 0), new Vector3(0.08f, 0.024f, half * 2f), default, 4f, g);
            }
            var top = new GameObject("Tread").transform;
            top.SetParent(g, false);
            top.localPosition = new Vector3(0, 0.04f, 0);
            Block("Plate", "Steel", new Vector3(0, -0.02f, 0), new Vector3(half * 2f - 0.02f, 0.04f, half * 2f - 0.02f), default, 4f, top);
            foreach (float s in new[] { -1f, 1f })
            {
                Block("Stripe", "Hazard", new Vector3(0, 0.002f, (half - 0.06f) * s), new Vector3(half * 2f - 0.04f, 0.004f, 0.08f), default, 2f, top);
                Block("Stripe", "Hazard", new Vector3((half - 0.06f) * s, 0.002f, 0), new Vector3(0.08f, 0.004f, half * 2f - 0.2f), default, 2f, top);
            }
            var plate = g.gameObject.AddComponent<Game.PressurePlate>();
            plate.size = new Vector2(half * 2f, half * 2f);
            plate.top = top;
            plate.downClip = PSXSoundGenerator.Load("clunk");
            plate.upClip = PSXSoundGenerator.Load("ratchet");
            return plate;
        }

        // A taut wire through a run of points, with a little pulley at every bend: how a plate pulls on what it holds.
        // Without pulleys and in rubber it is a cable. One mesh per straight run, so the auto colliders stay as thin as
        // the wire instead of boxing in the room.
        static void PullWire(string name, IList<Vector3> points, Transform parent, string material = "Steel", bool pulleys = true, float radius = 0.008f)
        {
            for (int i = 1; i < points.Count; i++)
            {
                var mb = new MeshBuilder();
                mb.Tube(points[i - 1], points[i], radius, radius, 4, UpFor(points[i] - points[i - 1]), 8f);
                Place(name, mb, material, Vector3.zero, default, parent);
            }
            // Each pulley a block of its own: one mesh for all of them would get one room-sized collider.
            if (!pulleys) return;
            for (int i = 1; i < points.Count - 1; i++)
                Block(name + "_Pulley", "DarkMetal", points[i], new Vector3(0.07f, 0.07f, 0.07f), default, 8f, parent);
        }
    }
}
