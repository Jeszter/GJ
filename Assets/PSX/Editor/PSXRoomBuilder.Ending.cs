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
            puzzle.ending = ending;
            puzzle.kickLeg = KickLeg(rig.head);
            puzzle.kickClip = PSXSoundGenerator.Load("clunk");
            return ending;
        }

        // A white mine rescue truck with a red band and lettering, its headlights on and an amber beacon on the cab.
        // Built facing +Z, then turned to drive along the road (+X).
        static Transform RescueTruck(Transform parent, out Transform beacon, out Light beaconLight, out AudioSource engine)
        {
            var g = Group("RescueTruck", new Vector3(-34f, 0f, EndRoadZ), 90f, parent);
            Block("Chassis", "DarkMetal", new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 0.3f, 5.2f), default, 2f, g);
            Block("Bumper", "Chrome", new Vector3(0f, 0.55f, 2.62f), new Vector3(2.0f, 0.22f, 0.1f), default, 4f, g);
            Block("Cab", "VanPaint", new Vector3(0f, 1.35f, 1.75f), new Vector3(1.95f, 1.35f, 1.5f), default, 1f, g);
            Block("Hood", "VanPaint", new Vector3(0f, 1.0f, 2.35f), new Vector3(1.9f, 0.6f, 0.6f), default, 1f, g);
            Block("Windshield", "VanGlass", new Vector3(0f, 1.62f, 2.51f), new Vector3(1.7f, 0.62f, 0.03f), new Vector3(-12f, 0f, 0f), 1f, g);
            foreach (float s in new[] { -1f, 1f })
                Block("SideWindow", "VanGlass", new Vector3(0.98f * s, 1.65f, 1.85f), new Vector3(0.02f, 0.5f, 0.9f), default, 1f, g);
            Block("Box", "VanPaint", new Vector3(0f, 1.55f, -0.95f), new Vector3(2.05f, 2.0f, 3.5f), default, 1f, g);
            Block("Band", "VanRed", new Vector3(0f, 1.2f, -0.95f), new Vector3(2.07f, 0.2f, 3.52f), default, 1f, g);
            Block("CabBand", "VanRed", new Vector3(0f, 1.0f, 1.75f), new Vector3(1.97f, 0.16f, 1.52f), default, 1f, g);
            foreach (float s in new[] { -1f, 1f })
                Plate("Lettering", "SignRescue", new Vector3(1.035f * s, 1.85f, -0.95f), 2.4f, 0.6f, new Vector3(s, 0f, 0f), g);
            Plate("Lettering", "SignRescue", new Vector3(0f, 1.95f, -2.715f), 1.6f, 0.4f, Vector3.back, g);
            Block("RearDoorSeam", "DarkMetal", new Vector3(0f, 1.5f, -2.71f), new Vector3(0.02f, 1.8f, 0.01f), default, 4f, g);
            foreach (float s in new[] { -1f, 1f })
            {
                Block("TailLamp", "TailLamp", new Vector3(0.85f * s, 0.85f, -2.72f), new Vector3(0.18f, 0.24f, 0.03f), default, 1f, g);
                Block("HeadLamp", "Headlamp", new Vector3(0.68f * s, 1.0f, 2.66f), new Vector3(0.34f, 0.2f, 0.03f), default, 1f, g);
                var halo = new MeshBuilder();
                halo.Cross(Vector3.zero, 0.9f, 0.9f);
                Place("HeadHalo", halo, "HeadlampHalo", new Vector3(0.68f * s, 0.55f, 2.75f), default, g);
                var beam = new GameObject("HeadBeam").AddComponent<Light>();
                beam.transform.SetParent(g, false);
                beam.transform.localPosition = new Vector3(0.68f * s, 1.0f, 2.75f);
                beam.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
                beam.type = LightType.Spot;
                beam.spotAngle = 55f;
                beam.range = 22f;
                beam.intensity = 3.2f;
                beam.color = new Color(1f, 0.92f, 0.78f);
                beam.shadows = LightShadows.None;
                foreach (float z in new[] { 1.65f, -1.75f })
                    Cyl("Wheel", "Rubber", new Vector3(s > 0 ? 1.12f : -0.82f, 0.42f, z), 0.42f, 0.3f, 10, new Vector3(0f, 0f, 90f), 2f, g);
            }
            // The beacon: an amber dome on the cab roof, a light on a turning arm inside it.
            Cyl("BeaconBase", "DarkMetal", new Vector3(0f, 2.03f, 1.6f), 0.14f, 0.06f, 8, default, 4f, g);
            Cyl("Beacon", "Beacon", new Vector3(0f, 2.09f, 1.6f), 0.11f, 0.16f, 8, default, 4f, g);
            var bh = new MeshBuilder();
            bh.Cross(Vector3.zero, 1.0f, 1.0f);
            Place("BeaconHalo", bh, "BeaconHalo", new Vector3(0f, 1.67f, 1.6f), default, g);
            beacon = new GameObject("BeaconArm").transform;
            beacon.SetParent(g, false);
            beacon.localPosition = new Vector3(0f, 2.17f, 1.6f);
            beaconLight = new GameObject("BeaconLight").AddComponent<Light>();
            beaconLight.transform.SetParent(beacon, false);
            beaconLight.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            beaconLight.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            beaconLight.type = LightType.Spot;
            beaconLight.spotAngle = 70f;
            beaconLight.range = 14f;
            beaconLight.intensity = 2f;
            beaconLight.color = new Color(1f, 0.55f, 0.1f);
            beaconLight.shadows = LightShadows.None;
            engine = g.gameObject.AddComponent<AudioSource>();
            engine.clip = PSXSoundGenerator.Load("rumble");
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 1f;
            engine.minDistance = 5f;
            engine.maxDistance = 60f;
            return g;
        }

        // One of the rescue crew, posed four ways (standing, the two halves of a step, kneeling), facing +Z.
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

            var lamp = new GameObject("HatLamp").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, 1.86f, 0.14f);
            lamp.type = LightType.Spot;
            lamp.spotAngle = 40f;
            lamp.range = 9f;
            lamp.intensity = 2.6f;
            lamp.color = new Color(1f, 0.95f, 0.85f);
            lamp.shadows = LightShadows.None;
            w.lamp = lamp;
            var kneelLamp = new GameObject("KneelLamp").transform;
            kneelLamp.SetParent(root, false);
            kneelLamp.localPosition = new Vector3(0f, 1.32f, 0.5f);
            w.lampKneel = kneelLamp;
            return w;
        }

        // One pose: legs are hip, knee, foot; arms elbow, wrist (from the shoulders of a torso leaning forward by lean).
        static GameObject RescuerPose(Transform root, string name, float hipY, float lean, Vector3[] legR, Vector3[] legL, Vector3[] armR, Vector3[] armL)
        {
            var g = Group(name, Vector3.zero, 0f, root);
            var tilt = Quaternion.Euler(lean, 0f, 0f);
            Vector3 hip = new Vector3(0f, hipY, 0f);
            Vector3 neck = hip + tilt * new Vector3(0f, 0.58f, 0f);
            Vector3 headC = neck + tilt * new Vector3(0f, 0.17f, 0.02f);

            var suit = new MeshBuilder();
            suit.Limb(hip, neck, 0.36f, 0.22f, 0.4f, 0.24f, tilt * Vector3.forward, 2f);
            suit.Box(hip + Vector3.down * 0.04f, new Vector3(0.34f, 0.16f, 0.2f), 2f);
            foreach (var leg in new[] { legR, legL })
            {
                suit.Limb(leg[0], leg[1], 0.15f, 0.15f, 0.13f, 0.13f, UpFor(leg[1] - leg[0]), 2f);
                suit.Limb(leg[1], leg[2] + Vector3.up * 0.06f, 0.12f, 0.12f, 0.11f, 0.11f, UpFor(leg[2] - leg[1]), 2f);
            }
            Vector3 shR = neck + tilt * new Vector3(0.21f, -0.05f, 0f), shL = neck + tilt * new Vector3(-0.21f, -0.05f, 0f);
            suit.Limb(shR, armR[0], 0.1f, 0.1f, 0.09f, 0.09f, UpFor(armR[0] - shR), 2f);
            suit.Limb(armR[0], armR[1], 0.09f, 0.09f, 0.08f, 0.08f, UpFor(armR[1] - armR[0]), 2f);
            suit.Limb(shL, armL[0], 0.1f, 0.1f, 0.09f, 0.09f, UpFor(armL[0] - shL), 2f);
            suit.Limb(armL[0], armL[1], 0.09f, 0.09f, 0.08f, 0.08f, UpFor(armL[1] - armL[0]), 2f);
            Place("Suit", suit, "RescueSuit", Vector3.zero, default, g);

            // Reflective bands round the chest and the forearms and shins.
            var bands = new MeshBuilder();
            foreach (float a in new[] { 0.55f, 0.78f })
            {
                Vector3 c = Vector3.Lerp(hip, neck, a);
                bands.Limb(c - tilt * Vector3.up * 0.025f, c + tilt * Vector3.up * 0.025f, 0.39f, 0.25f, 0.39f, 0.25f, tilt * Vector3.forward, 4f);
            }
            foreach (var arm in new[] { armR, armL })
            {
                Vector3 c = Vector3.Lerp(arm[0], arm[1], 0.6f), d = (arm[1] - arm[0]).normalized;
                bands.Limb(c - d * 0.02f, c + d * 0.02f, 0.095f, 0.095f, 0.095f, 0.095f, UpFor(d), 4f);
            }
            foreach (var leg in new[] { legR, legL })
            {
                Vector3 c = Vector3.Lerp(leg[1], leg[2], 0.55f), d = (leg[2] - leg[1]).normalized;
                bands.Limb(c - d * 0.02f, c + d * 0.02f, 0.125f, 0.125f, 0.125f, 0.125f, UpFor(d), 4f);
            }
            Place("Bands", bands, "ReflectBand", Vector3.zero, default, g);

            var skin = new MeshBuilder();
            skin.Tube(headC + Vector3.down * 0.1f, headC + Vector3.up * 0.08f, 0.085f, 0.09f, 8, tilt * Vector3.forward, 4f);
            Place("Head", skin, "Skin", Vector3.zero, default, g);
            var gloves = new MeshBuilder();
            foreach (var arm in new[] { armR, armL })
            {
                Vector3 d = (arm[1] - arm[0]).normalized;
                gloves.Limb(arm[1], arm[1] + d * 0.09f, 0.065f, 0.035f, 0.055f, 0.03f, UpFor(d), 4f);
            }
            Place("Gloves", gloves, "Glove", Vector3.zero, default, g);
            var boots = new MeshBuilder();
            foreach (var leg in new[] { legR, legL })
                boots.Box(leg[2] + new Vector3(0f, -0.04f, 0.05f), new Vector3(0.12f, 0.12f, 0.27f), 4f);
            Place("Boots", boots, "Rubber", Vector3.zero, default, g);

            // The hard hat with its brim and the lamp on the front.
            var hat = new MeshBuilder();
            Vector3 crown = headC + tilt * new Vector3(0f, 0.05f, 0f);
            hat.Tube(crown, crown + tilt * new Vector3(0f, 0.1f, 0f), 0.125f, 0.06f, 10, tilt * Vector3.forward, 4f);
            hat.Tube(crown - tilt * new Vector3(0f, 0.008f, 0f), crown + tilt * new Vector3(0f, 0.008f, 0f), 0.15f, 0.15f, 10, tilt * Vector3.forward, 4f);
            Place("HardHat", hat, "HardHat", Vector3.zero, default, g);
            Vector3 lampAt = crown + tilt * new Vector3(0f, 0.05f, 0.125f);
            var lampMb = new MeshBuilder();
            lampMb.Box(lampAt, new Vector3(0.06f, 0.045f, 0.03f), 4f);
            Place("HatLampBody", lampMb, "Headlamp", Vector3.zero, default, g);
            var halo = new MeshBuilder();
            halo.Cross(Vector3.zero, 0.35f, 0.35f);
            Place("HatLampHalo", halo, "HeadlampHalo", lampAt + Vector3.down * 0.17f, default, g);
            return g.gameObject;
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
