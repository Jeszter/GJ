using UnityEngine;

namespace GJ.PSX.EditorTools
{
    // The end of the run, past the cemetery gate: a road through the trees, the mine rescue truck that comes up it at
    // dawn, two of its crew, a few birds; and the boot you kick with in the graveyard fight.
    public static partial class PSXRoomBuilder
    {
        const float EndRoadZ = 54.5f;

        static void GraveyardRoad(Transform yard)
        {
            Block("Road", "Asphalt", new Vector3(0f, 0.006f, EndRoadZ), new Vector3(90f, 0.012f, 6f), default, 0.25f, yard);
            var dashes = new MeshBuilder { FaceShade = false };
            for (int i = 0; i < 30; i++)
                dashes.Quad(new Vector3(-44f + i * 3f, 0.014f, EndRoadZ - 0.06f), new Vector3(1.4f, 0f, 0f), new Vector3(0f, 0f, 0.12f), 1f);
            Place("Dashes", dashes, "Paper", Vector3.zero, default, yard);
            // A trodden path from the gate down to the road.
            Block("Path", "Ash", new Vector3(0f, 0.004f, (RoomMax.z + EndRoadZ - 3f) * 0.5f), new Vector3(1.4f, 0.008f, EndRoadZ - 3f - RoomMax.z), default, 1f, yard);
        }

        static Game.GraveyardEnding GraveyardEnding(Transform world, Game.GraveyardPuzzle puzzle, PlayerRig rig)
        {
            var ending = world.gameObject.AddComponent<Game.GraveyardEnding>();
            ending.truck = RescueTruck(world, out ending.beacon, out ending.beaconLight, out ending.engine);
            ending.truckFrom = new Vector3(-34f, 0f, EndRoadZ);
            ending.truckStop = new Vector3(1.6f, 0f, EndRoadZ);
            ending.crew = new[] { Rescuer("Rescuer_A", world), Rescuer("Rescuer_B", world) };
            ending.crewStart = new[] { new Vector3(-1.4f, 0f, EndRoadZ - 0.7f), new Vector3(-1.8f, 0f, EndRoadZ + 0.2f) };
            ending.birds = Birds(world, 5);
            ending.thudClip = PSXSoundGenerator.Load("clunk");
            ending.doorClip = PSXSoundGenerator.Load("door_open");
            ending.stepClip = PSXSoundGenerator.Load("footstep");
            ending.gaspClip = PSXSoundGenerator.Load("gasp");
            ending.heartClip = PSXSoundGenerator.Load("heartbeat");
            ending.shotClip = PSXSoundGenerator.Load("gunshot");
            puzzle.ending = ending;
            puzzle.kickLeg = KickLeg(rig.head);
            puzzle.kickClip = PSXSoundGenerator.Load("clunk");
            return ending;
        }

        // An ambulance: a white box body on a cab, rounded, a red-and-white chequered band and the word on its sides, a
        // light bar on the cab roof turning blue and red, headlights on. Built facing +Z, turned to drive along the road.
        static Transform RescueTruck(Transform parent, out Transform beacon, out Light beaconLight, out AudioSource engine)
        {
            var g = Group("Ambulance", new Vector3(-34f, 0f, EndRoadZ), 90f, parent);
            Block("Chassis", "DarkMetal", new Vector3(0f, 0.55f, -0.2f), new Vector3(1.85f, 0.3f, 5.4f), default, 2f, g);
            var body = new MeshBuilder();
            body.ChamferBox(new Vector3(0f, 1.32f, 1.72f), new Vector3(2.0f, 1.36f, 1.42f), 0.14f, 1f);     // cab
            body.ChamferBox(new Vector3(0f, 0.98f, 2.52f), new Vector3(1.96f, 0.66f, 0.62f), 0.12f, 1f);    // nose
            body.ChamferBox(new Vector3(0f, 1.66f, -0.95f), new Vector3(2.18f, 2.18f, 3.75f), 0.08f, 1f);   // box
            Place("Body", body, "VanPaint", Vector3.zero, default, g);
            // Dark wheel arches and skirts under the white.
            foreach (float s in new[] { -1f, 1f })
            {
                Block("Skirt", "DarkMetal", new Vector3(1.0f * s, 0.62f, -0.2f), new Vector3(0.06f, 0.22f, 5.2f), default, 2f, g);
                foreach (float z in new[] { 1.85f, -1.95f })
                    Block("Arch", "DarkMetal", new Vector3(1.0f * s, 0.9f, z), new Vector3(0.08f, 0.36f, 0.98f), default, 2f, g);
            }
            Block("BumperF", "DarkMetal", new Vector3(0f, 0.55f, 2.86f), new Vector3(2.0f, 0.26f, 0.14f), default, 2f, g);
            Block("BumperR", "DarkMetal", new Vector3(0f, 0.55f, -2.86f), new Vector3(2.0f, 0.26f, 0.14f), default, 2f, g);
            Block("Step", "Steel", new Vector3(0f, 0.38f, -2.98f), new Vector3(1.4f, 0.05f, 0.25f), default, 4f, g);
            Block("Grille", "DarkMetal", new Vector3(0f, 0.95f, 2.84f), new Vector3(0.95f, 0.32f, 0.02f), default, 2f, g);
            for (int i = 0; i < 4; i++)
                Block("GrilleBar", "Chrome", new Vector3(0f, 0.84f + i * 0.075f, 2.855f), new Vector3(0.9f, 0.015f, 0.01f), default, 4f, g);
            Block("Windshield", "VanGlass", new Vector3(0f, 1.62f, 2.44f), new Vector3(1.72f, 0.66f, 0.03f), new Vector3(-14f, 0f, 0f), 1f, g);
            foreach (float s in new[] { -1f, 1f })
            {
                Block("SideWindow", "VanGlass", new Vector3(1.005f * s, 1.66f, 1.85f), new Vector3(0.02f, 0.52f, 0.95f), default, 1f, g);
                Block("BoxWindow", "VanGlass", new Vector3(1.095f * s, 2.05f, -0.2f), new Vector3(0.02f, 0.36f, 0.75f), default, 1f, g);
                // Mirror on its arm.
                Block("MirrorArm", "DarkMetal", new Vector3(1.12f * s, 1.55f, 2.3f), new Vector3(0.24f, 0.03f, 0.03f), default, 4f, g);
                Block("Mirror", "DarkMetal", new Vector3(1.25f * s, 1.6f, 2.28f), new Vector3(0.05f, 0.28f, 0.16f), default, 4f, g);
                // The chequered band, the word, and a red cross on the box.
                Block("BandRed", "VanRed", new Vector3(1.093f * s, 1.12f, -0.95f), new Vector3(0.02f, 0.26f, 3.72f), default, 1f, g);
                Plate("Chequer", "Chequer", new Vector3(1.105f * s, 1.12f, -0.95f), 3.7f, 0.26f, new Vector3(s, 0f, 0f), g);
                Block("CabBand", "VanRed", new Vector3(1.005f * s, 1.12f, 1.75f), new Vector3(0.02f, 0.2f, 1.3f), default, 1f, g);
                Plate("Lettering", "SignAmbulance", new Vector3(1.1f * s, 1.62f, -0.6f), 2.0f, 0.42f, new Vector3(s, 0f, 0f), g);
                Plate("Cross", "RedCross", new Vector3(1.1f * s, 2.12f, -2.2f), 0.6f, 0.6f, new Vector3(s, 0f, 0f), g);
                Block("TailLamp", "TailLamp", new Vector3(0.9f * s, 1.0f, -2.84f), new Vector3(0.14f, 0.36f, 0.03f), default, 1f, g);
                Block("HeadLamp", "Headlamp", new Vector3(0.68f * s, 1.02f, 2.835f), new Vector3(0.32f, 0.2f, 0.03f), default, 1f, g);
                Block("Indicator", "Beacon", new Vector3(0.92f * s, 1.02f, 2.83f), new Vector3(0.1f, 0.12f, 0.03f), default, 1f, g);
                var halo = new MeshBuilder();
                halo.Cross(Vector3.zero, 0.9f, 0.9f);
                Place("HeadHalo", halo, "HeadlampHalo", new Vector3(0.68f * s, 0.57f, 2.92f), default, g);
                var beam = new GameObject("HeadBeam").AddComponent<Light>();
                beam.transform.SetParent(g, false);
                beam.transform.localPosition = new Vector3(0.68f * s, 1.02f, 2.92f);
                beam.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
                beam.type = LightType.Spot;
                beam.spotAngle = 55f;
                beam.range = 22f;
                beam.intensity = 3.2f;
                beam.color = new Color(1f, 0.92f, 0.78f);
                beam.shadows = LightShadows.None;
                foreach (float z in new[] { 1.85f, -1.95f })
                {
                    Cyl("Wheel", "Rubber", new Vector3(s > 0 ? 1.1f : -0.8f, 0.42f, z), 0.42f, 0.3f, 12, new Vector3(0f, 0f, 90f), 2f, g);
                    Cyl("Hub", "Chrome", new Vector3(s > 0 ? 1.12f : -1.1f, 0.42f, z), 0.17f, 0.02f, 8, new Vector3(0f, 0f, 90f), 4f, g);
                }
            }
            // The rear doors, each with a window, and the cross between them.
            foreach (float s in new[] { -1f, 1f })
                Block("RearWindow", "VanGlass", new Vector3(0.5f * s, 2.0f, -2.835f), new Vector3(0.6f, 0.45f, 0.02f), default, 1f, g);
            Block("RearSeam", "DarkMetal", new Vector3(0f, 1.6f, -2.835f), new Vector3(0.02f, 1.9f, 0.01f), default, 4f, g);
            Plate("Cross", "RedCross", new Vector3(0.5f, 1.45f, -2.85f), 0.4f, 0.4f, Vector3.back, g);
            Plate("Lettering", "SignAmbulance", new Vector3(0f, 2.55f, 2.42f), 1.2f, 0.25f, Vector3.forward, g);
            // The light bar: a dark base along the cab roof, a blue lens each end, red in the middle; inside it a turning
            // arm with a blue and a red lamp on opposite ends.
            Block("BarBase", "DarkMetal", new Vector3(0f, 2.04f, 1.65f), new Vector3(1.6f, 0.06f, 0.3f), default, 4f, g);
            foreach (float s in new[] { -1f, 1f })
                Block("BarBlue", "BlueLamp", new Vector3(0.52f * s, 2.13f, 1.65f), new Vector3(0.5f, 0.13f, 0.26f), default, 2f, g);
            Block("BarRed", "TailLamp", new Vector3(0f, 2.13f, 1.65f), new Vector3(0.5f, 0.13f, 0.26f), default, 2f, g);
            var bh = new MeshBuilder();
            bh.Cross(Vector3.zero, 1.6f, 1.0f);
            Place("BarHalo", bh, "BlueHalo", new Vector3(0f, 1.75f, 1.65f), default, g);
            beacon = new GameObject("BeaconArm").transform;
            beacon.SetParent(g, false);
            beacon.localPosition = new Vector3(0f, 2.22f, 1.65f);
            beaconLight = null;
            foreach (var (z, c) in new[] { (0.3f, new Color(0.25f, 0.45f, 1f)), (-0.3f, new Color(1f, 0.15f, 0.1f)) })
            {
                var l = new GameObject("BarLight").AddComponent<Light>();
                l.transform.SetParent(beacon, false);
                l.transform.localPosition = new Vector3(0f, 0f, z);
                l.transform.localRotation = Quaternion.Euler(18f, z > 0 ? 0f : 180f, 0f);
                l.type = LightType.Spot;
                l.spotAngle = 75f;
                l.range = 16f;
                l.intensity = 2.4f;
                l.color = c;
                l.shadows = LightShadows.None;
                if (beaconLight == null) beaconLight = l;
            }
            engine = g.gameObject.AddComponent<AudioSource>();
            engine.clip = PSXSoundGenerator.Load("rumble");
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 1f;
            engine.minDistance = 5f;
            engine.maxDistance = 60f;
            return g;
        }

        // One of the paramedics, posed four ways (standing, the two halves of a step, kneeling), facing +Z.
        static Game.RescueWorker Rescuer(string name, Transform parent)
        {
            var root = Group(name, Vector3.zero, 0f, parent);
            var w = root.gameObject.AddComponent<Game.RescueWorker>();
            Vector3 sh = new Vector3(0.21f, 1.47f, 0f);
            w.stand = RescuerPose(root, "Stand", 0.95f, 4f,
                new[] { new Vector3(0.1f, 0.95f, 0f), new Vector3(0.11f, 0.52f, 0.03f), new Vector3(0.12f, 0.1f, 0f) },
                new[] { new Vector3(-0.1f, 0.95f, 0f), new Vector3(-0.11f, 0.52f, 0.03f), new Vector3(-0.12f, 0.1f, 0f) },
                new[] { new Vector3(0.25f, 1.16f, 0.02f), new Vector3(0.25f, 0.9f, 0.08f) },
                new[] { new Vector3(-0.25f, 1.16f, 0.02f), new Vector3(-0.25f, 0.9f, 0.08f) });
            w.stepA = RescuerPose(root, "StepA", 0.93f, 9f,
                new[] { new Vector3(0.1f, 0.93f, 0f), new Vector3(0.11f, 0.54f, 0.2f), new Vector3(0.12f, 0.1f, 0.28f) },
                new[] { new Vector3(-0.1f, 0.93f, 0f), new Vector3(-0.11f, 0.5f, -0.08f), new Vector3(-0.12f, 0.16f, -0.3f) },
                new[] { new Vector3(0.25f, 1.17f, -0.1f), new Vector3(0.24f, 0.93f, -0.18f) },
                new[] { new Vector3(-0.25f, 1.17f, 0.12f), new Vector3(-0.23f, 0.95f, 0.26f) });
            w.stepB = RescuerPose(root, "StepB", 0.93f, 9f,
                new[] { new Vector3(0.1f, 0.93f, 0f), new Vector3(0.11f, 0.5f, -0.08f), new Vector3(0.12f, 0.16f, -0.3f) },
                new[] { new Vector3(-0.1f, 0.93f, 0f), new Vector3(-0.11f, 0.54f, 0.2f), new Vector3(-0.12f, 0.1f, 0.28f) },
                new[] { new Vector3(0.25f, 1.17f, 0.12f), new Vector3(0.23f, 0.95f, 0.26f) },
                new[] { new Vector3(-0.25f, 1.17f, -0.1f), new Vector3(-0.24f, 0.93f, -0.18f) });
            // Kneeling over someone on the ground: right foot planted, left knee down, bent forward, both hands out.
            w.kneel = RescuerPose(root, "Kneel", 0.58f, 32f,
                new[] { new Vector3(0.12f, 0.58f, 0f), new Vector3(0.14f, 0.52f, 0.42f), new Vector3(0.14f, 0.1f, 0.44f) },
                new[] { new Vector3(-0.12f, 0.58f, 0f), new Vector3(-0.13f, 0.1f, 0.08f), new Vector3(-0.13f, 0.12f, -0.36f) },
                new[] { new Vector3(0.2f, 0.8f, 0.42f), new Vector3(0.13f, 0.44f, 0.68f) },
                new[] { new Vector3(-0.2f, 0.8f, 0.42f), new Vector3(-0.13f, 0.44f, 0.68f) });
            w.kneel.SetActive(false);
            w.stepA.SetActive(false);
            w.stepB.SetActive(false);

            // The flashlight in the right hand: its beam goes where the ending points it.
            var lamp = new GameObject("Flashlight").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0.25f, 0.84f, 0.22f);
            lamp.type = LightType.Spot;
            lamp.spotAngle = 40f;
            lamp.range = 9f;
            lamp.intensity = 2.6f;
            lamp.color = new Color(1f, 0.95f, 0.85f);
            lamp.shadows = LightShadows.None;
            w.lamp = lamp;
            var kneelLamp = new GameObject("KneelLamp").transform;
            kneelLamp.SetParent(root, false);
            kneelLamp.localPosition = new Vector3(0.13f, 0.4f, 0.8f);
            w.lampKneel = kneelLamp;
            return w;
        }

        // One pose: legs are hip, knee, foot; arms elbow, wrist (from the shoulders of a torso leaning forward by lean).
        // A paramedic: dark green uniform with yellow hi-vis bands and silver tape, rounded limbs, short dark hair, a
        // face; a flashlight in the right hand and, standing or walking, a red medical bag in the left.
        static GameObject RescuerPose(Transform root, string name, float hipY, float lean, Vector3[] legR, Vector3[] legL, Vector3[] armR, Vector3[] armL)
        {
            var g = Group(name, Vector3.zero, 0f, root);
            var tilt = Quaternion.Euler(lean, 0f, 0f);
            Vector3 hip = new Vector3(0f, hipY, 0f);
            Vector3 neck = hip + tilt * new Vector3(0f, 0.58f, 0f);
            Vector3 headC = neck + tilt * new Vector3(0f, 0.16f, 0.02f);
            Vector3 fwd = tilt * Vector3.forward;

            var suit = new MeshBuilder();
            suit.Tube(hip + tilt * new Vector3(0f, -0.05f, 0f), neck, 0.17f, 0.2f, 10, fwd, 2f);
            suit.Tube(neck - tilt * new Vector3(0f, 0.06f, 0f), neck + tilt * new Vector3(0f, 0.02f, 0f), 0.2f, 0.09f, 10, fwd, 2f);
            foreach (var leg in new[] { legR, legL })
            {
                suit.Tube(leg[0], leg[1], 0.085f, 0.07f, 8, UpFor(leg[1] - leg[0]), 2f);
                suit.Tube(leg[1], leg[2] + Vector3.up * 0.07f, 0.065f, 0.058f, 8, UpFor(leg[2] - leg[1]), 2f);
            }
            Vector3 shR = neck + tilt * new Vector3(0.2f, -0.06f, 0f), shL = neck + tilt * new Vector3(-0.2f, -0.06f, 0f);
            foreach (var (sh, arm) in new[] { (shR, armR), (shL, armL) })
            {
                suit.Tube(sh, arm[0], 0.06f, 0.05f, 8, UpFor(arm[0] - sh), 2f);
                suit.Tube(arm[0], arm[1], 0.05f, 0.042f, 8, UpFor(arm[1] - arm[0]), 2f);
            }
            Place("Uniform", suit, "ParamedicSuit", Vector3.zero, default, g);

            // Hi-vis yellow across the chest and the back, round the upper arms; silver tape round the shins.
            var hivis = new MeshBuilder();
            foreach (float a in new[] { 0.5f, 0.74f })
            {
                Vector3 c = Vector3.Lerp(hip, neck, a);
                hivis.Tube(c - tilt * Vector3.up * 0.035f, c + tilt * Vector3.up * 0.035f, 0.188f + a * 0.02f, 0.19f + a * 0.02f, 10, fwd, 4f);
            }
            foreach (var (sh, arm) in new[] { (shR, armR), (shL, armL) })
            {
                Vector3 c = Vector3.Lerp(sh, arm[0], 0.55f), d = (arm[0] - sh).normalized;
                hivis.Tube(c - d * 0.03f, c + d * 0.03f, 0.058f, 0.056f, 8, UpFor(d), 4f);
            }
            Place("HiVis", hivis, "HiVis", Vector3.zero, default, g);
            var tape = new MeshBuilder();
            foreach (var leg in new[] { legR, legL })
            {
                Vector3 c = Vector3.Lerp(leg[1], leg[2], 0.5f), d = (leg[2] - leg[1]).normalized;
                tape.Tube(c - d * 0.02f, c + d * 0.02f, 0.067f, 0.066f, 8, UpFor(d), 4f);
            }
            Place("Tape", tape, "ReflectBand", Vector3.zero, default, g);

            // Head and face, ears, short hair.
            var skin = new MeshBuilder();
            skin.Tube(neck - tilt * new Vector3(0f, 0.02f, 0f), headC - tilt * new Vector3(0f, 0.06f, 0f), 0.05f, 0.05f, 8, fwd, 4f);
            skin.Tube(headC - tilt * new Vector3(0f, 0.1f, 0f), headC + tilt * new Vector3(0f, 0.08f, 0f), 0.075f, 0.088f, 10, fwd, 4f);
            skin.Box(headC + tilt * new Vector3(0f, -0.015f, 0.09f), new Vector3(0.025f, 0.045f, 0.03f), 8f);
            foreach (float s in new[] { -1f, 1f })
                skin.Box(headC + tilt * new Vector3(0.088f * s, 0f, 0f), new Vector3(0.02f, 0.045f, 0.03f), 8f);
            foreach (var arm in new[] { armR, armL })
            {
                Vector3 d = (arm[1] - arm[0]).normalized;
                skin.Limb(arm[1], arm[1] + d * 0.09f, 0.06f, 0.03f, 0.05f, 0.028f, UpFor(d), 4f);
            }
            Place("Skin", skin, "Skin", Vector3.zero, default, g);
            var hair = new MeshBuilder();
            hair.Tube(headC + tilt * new Vector3(0f, 0.04f, -0.01f), headC + tilt * new Vector3(0f, 0.11f, -0.015f), 0.093f, 0.06f, 10, fwd, 4f);
            hair.Box(headC + tilt * new Vector3(0f, 0.0f, -0.07f), new Vector3(0.17f, 0.1f, 0.05f), 4f);
            Place("Hair", hair, "Hair", Vector3.zero, default, g);
            var eyes = new MeshBuilder();
            foreach (float s in new[] { -1f, 1f })
                eyes.Box(headC + tilt * new Vector3(0.032f * s, 0.022f, 0.083f), new Vector3(0.018f, 0.01f, 0.008f), 8f);
            Place("Eyes", eyes, "Void", Vector3.zero, default, g);

            var boots = new MeshBuilder();
            foreach (var leg in new[] { legR, legL })
                boots.ChamferBox(leg[2] + new Vector3(0f, -0.03f, 0.05f), new Vector3(0.11f, 0.11f, 0.26f), 0.025f, 4f);
            Place("Boots", boots, "Rubber", Vector3.zero, default, g);

            // The flashlight in the right fist, pointing on along the forearm.
            Vector3 dR = (armR[1] - armR[0]).normalized, fist = armR[1] + dR * 0.06f;
            var torch = new MeshBuilder();
            torch.Tube(fist - fwd * 0.06f, fist + fwd * 0.12f, 0.02f, 0.026f, 8, Vector3.up, 4f);
            Place("Flashlight", torch, "DarkMetal", Vector3.zero, default, g);
            var lens = new MeshBuilder();
            lens.Cross(fist + fwd * 0.12f + Vector3.down * 0.05f, 0.1f, 0.1f);
            Place("FlashlightGlow", lens, "HeadlampHalo", Vector3.zero, default, g);
            // The bag in the left hand, when not kneeling.
            if (hipY > 0.8f)
            {
                Vector3 bagAt = armL[1] + Vector3.down * 0.2f;
                var bag = new MeshBuilder();
                bag.ChamferBox(bagAt, new Vector3(0.14f, 0.24f, 0.36f), 0.03f, 2f);
                Place("Bag", bag, "VanRed", Vector3.zero, default, g);
                Block("BagHandle", "DarkMetal", bagAt + new Vector3(0f, 0.13f, 0f), new Vector3(0.03f, 0.05f, 0.16f), default, 4f, g);
                Plate("BagCross", "RedCross", bagAt + new Vector3(-0.072f, 0f, 0f), 0.14f, 0.14f, Vector3.left, g);
            }
            return g.gameObject;
        }

        // A spruce: a trunk showing under the lowest boughs, then six tiers of boughs narrowing to the top, each tier a
        // short skirt drooping at its rim under a cone, each a little off the trunk line and turned its own way.
        static void PineTree(MeshBuilder needles, MeshBuilder trunk, Vector3 p, float h, int seed)
        {
            trunk.Tube(p, p + Vector3.up * h * 0.9f, 0.2f + h * 0.01f, 0.04f, 6, Vector3.forward, 1f);
            const int tiers = 6;
            for (int k = 0; k < tiers; k++)
            {
                float t = k / (float)tiers;
                float y = h * (0.16f + t * 0.74f), th = h * (0.24f - t * 0.1f);
                float r = (h * 0.24f + 0.4f) * (1f - t * 0.82f) * (0.85f + Hash(seed, k, 194) * 0.3f);
                var c = p + new Vector3((Hash(seed, k, 195) - 0.5f) * 0.25f, y, (Hash(seed, k, 196) - 0.5f) * 0.25f);
                var turn = Quaternion.Euler(0f, Hash(seed, k, 197) * 60f, 0f) * Vector3.forward;
                int sides = 7 + (k % 2);
                needles.Tube(c + Vector3.down * th * 0.18f, c, r * 0.9f, r, sides, turn, 0.5f);
                needles.Tube(c, c + Vector3.up * th, r, 0.04f, sides, turn, 0.5f);
            }
        }

        // What grows and lies between the trees: low bushes in clumps, fallen trunks, mossy stones. skip(x, z) keeps the
        // yard, the road and the path clear.
        static void ForestFloor(Transform parent, int count, System.Func<float, float, bool> skip)
        {
            var bushes = new MeshBuilder();
            var logs = new MeshBuilder();
            var stones = new MeshBuilder();
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-34f, 34f, Hash(i, 0, 380)), z = Mathf.Lerp(-4f, 82f, Hash(i, 1, 381));
                if (skip(x, z)) continue;
                var p = new Vector3(x, 0f, z);
                float kind = Hash(i, 2, 382);
                if (kind < 0.6f)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        var at = p + new Vector3((Hash(i, b + 3, 383) - 0.5f) * 0.9f, 0f, (Hash(i, b + 6, 384) - 0.5f) * 0.9f);
                        float s = 0.3f + Hash(i, b + 9, 385) * 0.4f;
                        bushes.Tube(at, at + Vector3.up * s * 1.3f, s, 0.05f, 6, Vector3.forward, 1f);
                    }
                }
                else if (kind < 0.8f)
                {
                    float ang = Hash(i, 12, 386) * Mathf.PI, len = 2f + Hash(i, 13, 387) * 2.5f, r = 0.14f + Hash(i, 14, 388) * 0.12f;
                    var d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * len * 0.5f;
                    logs.Tube(p - d + Vector3.up * r, p + d + Vector3.up * r * 0.8f, r, r * 0.8f, 7, Vector3.up, 1f);
                    logs.Tube(p + d * 0.2f + Vector3.up * r, p + d * 0.2f + new Vector3(-d.z, 0.5f, d.x) * 0.25f, 0.05f, 0.02f, 4, Vector3.up, 2f);
                }
                else
                {
                    float s = 0.3f + Hash(i, 15, 389) * 0.6f;
                    stones.M = Matrix4x4.TRS(p + Vector3.up * s * 0.25f, Quaternion.Euler(Hash(i, 16, 390) * 20f, Hash(i, 17, 391) * 180f, 0f), Vector3.one);
                    stones.ChamferBox(Vector3.zero, new Vector3(s, s * 0.6f, s * 0.8f), s * 0.2f, 1f);
                }
            }
            stones.M = Matrix4x4.identity;
            Place("Undergrowth", bushes, "PineLight", Vector3.zero, default, parent);
            Place("FallenLogs", logs, "Wood", Vector3.zero, default, parent);
            Place("Stones", stones, "Stone", Vector3.zero, default, parent);
        }

        // Grass gone to seed in tufts all over the yard and thick along the fence and the road, ferns where it meets the
        // trees, and a few flowers left on the graves (white and yellow); the path up the middle and the road kept bare.
        static void GraveyardPlants(Transform yard)
        {
            var grass = new MeshBuilder();
            var dry = new MeshBuilder();
            var ferns = new MeshBuilder();
            var white = new MeshBuilder();
            var yellow = new MeshBuilder();
            for (int i = 0; i < 1400; i++)
            {
                float x = Mathf.Lerp(-30f, 30f, Hash(i, 0, 440)), z = Mathf.Lerp(-3f, 76f, Hash(i, 1, 441));
                bool path = Mathf.Abs(x) < 1.0f && z < EndRoadZ - 3f;
                bool road = z > EndRoadZ - 3f && z < EndRoadZ + 3f;
                if (path || road) continue;
                var p = new Vector3(x, 0f, z);
                float h = 0.25f + Hash(i, 2, 442) * 0.45f;
                // Taller along the fence lines and the verges.
                if (Mathf.Abs(Mathf.Abs(x) - RoomMax.x) < 1.2f || Mathf.Abs(z - RoomMax.z) < 1.2f || Mathf.Abs(Mathf.Abs(z - EndRoadZ) - 3.5f) < 0.8f) h *= 1.8f;
                Tuft(Hash(i, 3, 443) > 0.7f ? dry : grass, p, h, 5, i);
            }
            for (int i = 0; i < 160; i++)
            {
                float x = Mathf.Lerp(-30f, 30f, Hash(i, 4, 444)), z = Mathf.Lerp(-3f, 76f, Hash(i, 5, 445));
                bool inside = x > RoomMin.x && x < RoomMax.x && z < RoomMax.z;
                bool road = z > EndRoadZ - 4f && z < EndRoadZ + 4f;
                if (inside || road || (Mathf.Abs(x) < 9f && z > RoomMax.z && z < EndRoadZ)) continue;
                var p = new Vector3(x, 0f, z);
                for (int f = 0; f < 6; f++)
                {
                    float a = f / 6f * Mathf.PI * 2f + Hash(i, 6, 446);
                    var tip = p + new Vector3(Mathf.Cos(a) * 0.6f, 0.35f + Hash(i, f, 447) * 0.2f, Mathf.Sin(a) * 0.6f);
                    ferns.Limb(p, tip, 0.03f, 0.01f, 0.16f, 0.01f, Vector3.up, 2f);
                }
            }
            for (int i = 0; i < 90; i++)
            {
                float x = Mathf.Lerp(RoomMin.x + 1f, RoomMax.x - 1f, Hash(i, 7, 448)), z = Mathf.Lerp(6f, RoomMax.z - 1f, Hash(i, 8, 449));
                if (Mathf.Abs(x) < 1.6f) continue;
                var p = new Vector3(x, 0f, z);
                for (int f = 0; f < 3; f++)
                {
                    var at = p + new Vector3((Hash(i, f, 450) - 0.5f) * 0.4f, 0f, (Hash(i, f, 451) - 0.5f) * 0.4f);
                    float h = 0.18f + Hash(i, f, 452) * 0.15f;
                    grass.Tube(at, at + Vector3.up * h, 0.006f, 0.004f, 3, Vector3.forward, 4f);
                    var head = (Hash(i, 9, 453) > 0.5f ? white : yellow);
                    head.Box(at + Vector3.up * h, new Vector3(0.045f, 0.02f, 0.045f), 4f);
                }
            }
            Place("Grass", grass, "GrassBlade", Vector3.zero, default, yard);
            Place("DryGrass", dry, "DryGrass", Vector3.zero, default, yard);
            Place("Ferns", ferns, "PineLight", Vector3.zero, default, yard);
            Place("FlowersWhite", white, "Paper", Vector3.zero, default, yard);
            Place("FlowersYellow", yellow, "Helmet", Vector3.zero, default, yard);
        }

        // A tuft: blades fanned out from one root, each leaning its own way.
        static void Tuft(MeshBuilder mb, Vector3 at, float h, int blades, int seed)
        {
            for (int b = 0; b < blades; b++)
            {
                float a = Hash(seed, b, 454) * Mathf.PI * 2f, lean = 0.1f + Hash(seed, b, 455) * 0.3f;
                var root = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.04f;
                var tip = root + new Vector3(Mathf.Cos(a) * lean * h, h * (0.7f + Hash(seed, b, 456) * 0.3f), Mathf.Sin(a) * lean * h);
                mb.Tube(root, tip, 0.016f, 0.002f, 3, Vector3.forward, 4f);
            }
        }

        // A few dark birds for the dawn sky: two wings in a shallow V, flapped by scaling.
        static Transform[] Birds(Transform parent, int count)
        {
            var list = new Transform[count];
            var group = new GameObject("Birds").transform;
            group.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                var mb = new MeshBuilder { FaceShade = false };
                foreach (float s in new[] { -1f, 1f })
                {
                    mb.Quad(new Vector3(0f, 0f, -0.08f), new Vector3(0.42f * s, 0.12f, 0.02f), new Vector3(0f, 0f, 0.16f), 1f);
                    mb.Quad(new Vector3(0f, 0f, -0.08f), new Vector3(0.42f * s, 0.12f, 0.02f), new Vector3(0f, 0f, 0.16f), 1f, 0f, true);
                }
                mb.Box(Vector3.zero, new Vector3(0.07f, 0.06f, 0.24f), 1f);
                var bird = Place("Bird", mb, "Void", Vector3.zero, default, group);
                bird.SetActive(false);
                list[i] = bird.transform;
            }
            return list;
        }

        // The boot and the shin of the right leg, under the view: the graveyard kick swings it up into sight.
        static Transform KickLeg(Transform head)
        {
            var leg = new GameObject("KickLeg").transform;
            leg.SetParent(head, false);
            leg.localPosition = new Vector3(0.1f, -1.15f, 0.15f);
            var shin = new MeshBuilder();
            shin.Limb(new Vector3(0f, 0.11f, -0.04f), new Vector3(0f, -0.04f, -0.62f), 0.13f, 0.14f, 0.16f, 0.16f, Vector3.up, 2f);
            Place("Shin", shin, "Coverall", Vector3.zero, default, leg);
            var boot = new MeshBuilder();
            boot.ChamferBox(new Vector3(0f, 0.06f, 0.06f), new Vector3(0.13f, 0.15f, 0.32f), 0.03f, 4f);
            boot.Box(new Vector3(0f, -0.025f, 0.06f), new Vector3(0.135f, 0.03f, 0.33f), 4f);
            Place("Boot", boot, "Leather", Vector3.zero, default, leg);
            foreach (var r in leg.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            leg.gameObject.SetActive(false);
            return leg;
        }
    }
}
