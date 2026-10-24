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

            // Built standing (feet at 0, facing +Z), then laid down: up turns into +Z, the face looks at the ceiling.
            var lay = Matrix4x4.identity;
            lay.SetColumn(0, new Vector4(-1f, 0f, 0f, 0f));
            lay.SetColumn(1, new Vector4(0f, 0f, 1f, 0f));
            lay.SetColumn(2, new Vector4(0f, 1f, 0f, 0f));
            lay.SetColumn(3, new Vector4(0f, 0.13f, -1.62f, 1f));

            // Legs a little apart, arms flung out from the shoulders.
            var cloth = new MeshBuilder { M = lay };
            foreach (float s in new[] { -1f, 1f })
            {
                cloth.Tube(new Vector3(0.1f * s, 0.86f, 0f), new Vector3(0.17f * s, 0.12f, 0f), 0.085f, 0.075f, 6, Vector3.forward, 2f);
                cloth.Tube(new Vector3(0.22f * s, 1.42f, 0f), new Vector3(0.48f * s, 1.12f, -0.02f), 0.06f, 0.05f, 6, Vector3.forward, 2f);
            }
            cloth.Limb(new Vector3(0, 0.84f, 0), new Vector3(0, 1.48f, 0), 0.36f, 0.22f, 0.44f, 0.24f, Vector3.forward, 2f);
            Place("Clothes", cloth, "Coverall", Vector3.zero, default, body);
            var skin = new MeshBuilder { M = lay };
            skin.Tube(new Vector3(0, 1.5f, 0), new Vector3(0, 1.74f, 0.01f), 0.095f, 0.085f, 7, Vector3.forward, 4f);
            foreach (float s in new[] { -1f, 1f })
                skin.Tube(new Vector3(0.48f * s, 1.12f, -0.02f), new Vector3(0.56f * s, 1.03f, -0.02f), 0.045f, 0.04f, 5, Vector3.forward, 4f);
            Place("Skin", skin, "Skin", Vector3.zero, default, body);
            var boots = new MeshBuilder { M = lay };
            foreach (float s in new[] { -1f, 1f }) boots.Box(new Vector3(0.17f * s, 0.06f, 0.04f), new Vector3(0.12f, 0.12f, 0.26f), 4f);
            Place("Boots", boots, "Leather", Vector3.zero, default, body);

            // The hard hat knocked off and lying upturned beside the head.
            var hat = new MeshBuilder();
            hat.Tube(new Vector3(0, 0f, 0), new Vector3(0, 0.12f, 0), 0.14f, 0.07f, 8, Vector3.forward, 4f);
            hat.Tube(new Vector3(0, -0.01f, 0), new Vector3(0, 0.01f, 0), 0.17f, 0.17f, 8, Vector3.forward, 4f);
            Place("HardHat", hat, "Helmet", new Vector3(-0.32f, 0.1f, 0.12f), new Vector3(150f, 30f, 0f), body);

            var corpse = body.gameObject.AddComponent<Game.Corpse>();
            body.gameObject.SetActive(false);
            return corpse;
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
