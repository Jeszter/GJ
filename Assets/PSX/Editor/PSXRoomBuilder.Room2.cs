using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // Room 2, the pump room: an old cistern feeds a valve and a swinging chute; the door's bolt hangs on a chain that
    // runs over two pulleys to a counterweight bucket. Hook the chain, swing the chute over the bucket, open the valve,
    // and the bucket fills (long after the air is gone), sinks, and lifts the bolt.
    public static partial class PSXRoomBuilder
    {
        // Machinery positions, shared by the geometry and the puzzle wiring.
        static readonly Vector3 TankBase = new Vector3(-2.45f, 0f, 2.3f);
        const float TankRadius = 0.7f;
        static readonly Vector3 ChutePivot = new Vector3(-1.25f, 1.62f, 3.9f);
        const float ChuteLength = 1.3f, ChuteSlope = 14f, DrainYaw = -67.4f, BucketYaw = 51f;
        static readonly Vector3 PulleyLow = new Vector3(-0.27f, 3.35f, 4.69f);
        static readonly Vector3 PulleyDoor = new Vector3(-1.35f, 3.35f, 7.22f);
        const float PulleyRadius = 0.08f;

        const string NoteDay5 =
            "DAY 5\n\n" +
            "The pump room. The old cistern\n" +
            "still holds water.\n\n" +
            "The door bolt hangs on a chain\n" +
            "over to a bucket. Fill the bucket,\n" +
            "the bolt comes up.\n\n" +
            "It never fills in one breath.";

        const string NoteChain =
            "The chain slipped off the bolt\n" +
            "again. Hook it back first.\n\n" +
            "The chute empties into the drain\n" +
            "unless you swing it over.\n\n" +
            "There is only one tank.\n" +
            "Don't waste it.\n\n" +
            "                        - M.";

        static Vector3 ChuteDir(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(ChuteSlope, 0f, 0f) * Vector3.forward;
        static Vector3 ChuteEnd(float yaw) => ChutePivot + ChuteDir(yaw) * ChuteLength;

        static void BuildRoom2()
        {
            RoomMin = new Vector3(-3.5f, 0f, 0f);
            RoomMax = new Vector3(3.5f, 3.8f, 7.5f);
            var scene = NewRoomScene("Room02");

            BuildShell(new[] { 1.8f, 4.2f, 6.4f });
            BuildDoor();
            // No blast here: the bolt just lets go.
            if (_door.flash != null) Object.DestroyImmediate(_door.flash.gameObject);
            _door.flash = null;
            _door.blastClip = null;

            var puzzle = BuildCistern();
            BuildRoom2Props();
            BuildRoom2Lights();
            DressRoom2();
            Models2();
            AutoColliders(_world);
            Life2();

            var rig = BuildPlayer(new Vector3(0.6f, 0f, 0.9f), 0f);
            BuildDirector(rig, puzzle, 75f, "SECTOR 4 - PUMP ROOM\nyou have 75 seconds of air", 2, "Room03",
                new[] { new Vector3(0.5f, 1.6f, 1f) }, new[] { new Vector3(5f, -20f, 0f) });

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.12f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.03f, 0.045f, 0.05f);
            RenderSettings.fogStartDistance = 3f;
            RenderSettings.fogEndDistance = 14f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;

            EditorSceneManager.SaveScene(scene, Scene2Path);
            Debug.Log("PSX room built: " + Scene2Path);
        }

        // A length of chain hanging down from a point: a unit-long strip under a transform that the puzzle stretches.
        static Transform HangingChain(string name, Vector3 top, float length, Transform parent, float tilt = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = top;
            t.localRotation = Quaternion.Euler(0f, 0f, tilt);
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0, -0.5f, 0), new Vector3(0.028f, 1f, 0.028f), 6f);
            mb.Box(new Vector3(0, -0.5f, 0), new Vector3(0.012f, 1f, 0.034f), 6f);
            Place("Links", mb, "Chain", Vector3.zero, default, t);
            t.localScale = new Vector3(1f, length, 1f);
            return t;
        }

        static void Pulley(Vector3 axle, Vector3 chainDir, Transform parent)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, chainDir).normalized;
            var mb = new MeshBuilder();
            mb.Tube(axle - axis * 0.025f, axle + axis * 0.025f, PulleyRadius, PulleyRadius, 10, Vector3.up, 4f);
            mb.Tube(axle - axis * 0.035f, axle + axis * 0.035f, PulleyRadius * 0.3f, PulleyRadius * 0.3f, 6, Vector3.up, 4f);
            Place("Pulley", mb, "Steel", Vector3.zero, default, parent);
            // Fork and hanger rod up into the ceiling.
            foreach (float s in new[] { -1f, 1f })
                Block("Fork", "DarkMetal", axle + axis * 0.045f * s + Vector3.up * 0.06f, new Vector3(0.012f, 0.14f, 0.04f),
                      new Vector3(0f, Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg, 0f), 4f, parent);
            Block("Hanger", "DarkMetal", new Vector3(axle.x, (axle.y + RoomMax.y) * 0.5f + 0.06f, axle.z),
                  new Vector3(0.03f, RoomMax.y - axle.y - 0.12f, 0.03f), default, 4f, parent);
        }

        // Thin vertical stream of water whose bottom sits at "bottom".
        static GameObject Stream(string name, Vector3 bottom, float height, float width, Transform parent)
        {
            var mb = new MeshBuilder { FaceShade = false };
            mb.Cross(Vector3.zero, width, height);
            return Place(name, mb, "WaterStream", bottom, default, parent);
        }

        static Game.CisternPuzzle BuildCistern()
        {
            var works = new GameObject("Cistern").transform;
            works.SetParent(_world, false);

            // The tank on its stand, banded and riveted, fed from the ceiling.
            var tank = new GameObject("Tank").transform;
            tank.SetParent(works, false);
            tank.localPosition = TankBase;
            foreach (var lx in new[] { -0.45f, 0.45f })
                foreach (var lz in new[] { -0.45f, 0.45f })
                    Block("Leg", "DarkMetal", new Vector3(lx, 0.27f, lz), new Vector3(0.08f, 0.54f, 0.08f), default, 2f, tank);
            Cyl("Saddle", "DarkMetal", new Vector3(0, 0.5f, 0), TankRadius + 0.02f, 0.06f, 12, default, 1f, tank);
            Cyl("Body", "TankPaint", new Vector3(0, 0.55f, 0), TankRadius, 1.6f, 12, default, 0.5f, tank, true);
            var dome = new MeshBuilder();
            dome.Tube(new Vector3(0, 2.15f, 0), new Vector3(0, 2.4f, 0), TankRadius, 0.24f, 12, Vector3.forward, 1f);
            Place("Dome", dome, "TankPaint", Vector3.zero, default, tank);
            Cyl("Manhole", "DarkMetal", new Vector3(0.2f, 2.32f, 0.1f), 0.16f, 0.06f, 8, new Vector3(-12, 0, 8), 2f, tank);
            foreach (float y in new[] { 0.8f, 1.35f, 1.9f })
                Cyl("Band", "DarkMetal", new Vector3(0, y, 0), TankRadius + 0.015f, 0.05f, 12, default, 1f, tank);
            PipeRun("TankFeed", "Rust", new[] { TankBase + new Vector3(0, 2.4f, 0), TankBase + new Vector3(0, RoomMax.y, 0) }, 0.07f, works);
            Plate("TankStencil", "SignSector2", TankBase + new Vector3(TankRadius * 0.7f + 0.005f, 1.6f, -TankRadius * 0.7f - 0.005f), 0.5f, 0.12f, Vector3.right, works, 0f)
                .transform.localRotation = Quaternion.Euler(0, -45, 0);

            // Sight glass on the tank's flank: a lit water column in a steel cage with a scale plate behind it.
            float ang = 35f * Mathf.Deg2Rad;
            var glassPos = TankBase + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (TankRadius + 0.08f);
            Block("GaugeScale", "Paper", glassPos + new Vector3(-0.03f, 1.38f, -0.02f), new Vector3(0.06f, 1.38f, 0.06f), new Vector3(0, -35, 0), 4f, works);
            var level = new GameObject("TankLevel").transform;
            level.SetParent(works, false);
            level.localPosition = glassPos + new Vector3(0, 0.7f, 0);
            Block("Column", "SightWater", new Vector3(0, 0.65f, 0), new Vector3(0.04f, 1.3f, 0.04f), default, 4f, level);
            foreach (var off in new[] { new Vector3(0.03f, 0, 0.03f), new Vector3(-0.03f, 0, -0.03f) })
                Block("Guard", "Steel", glassPos + off + new Vector3(0, 1.38f, 0), new Vector3(0.012f, 1.38f, 0.012f), default, 4f, works);
            Block("GlassCap_Low", "DarkMetal", glassPos + new Vector3(0, 0.68f, 0), new Vector3(0.09f, 0.04f, 0.09f), default, 4f, works);
            Block("GlassCap_High", "DarkMetal", glassPos + new Vector3(0, 2.07f, 0), new Vector3(0.09f, 0.04f, 0.09f), default, 4f, works);
            // Two short pipe stubs from the tank wall into the glass caps: the glass is plumbed to the tank.
            var radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            var stubs = new MeshBuilder();
            foreach (float y in new[] { 0.68f, 2.07f })
                stubs.Tube(TankBase + radial * (TankRadius - 0.02f) + Vector3.up * y, glassPos + Vector3.up * y, 0.018f, 0.018f, 6, Vector3.up, 4f);
            Place("GlassStubs", stubs, "Steel", Vector3.zero, default, works);

            // Outlet: out of the tank, up, across, and down to the spout over the chute pivot. The valve sits on the
            // climb, its wheel facing the room.
            var spout = ChutePivot + new Vector3(0, 0.33f, 0);
            var outlet = PipeRun("Outlet", "Rust", new[]
            {
                TankBase + new Vector3(TankRadius - 0.05f, 0.85f, 0), new Vector3(-1.25f, 0.85f, TankBase.z),
                new Vector3(-1.25f, 2.15f, TankBase.z), new Vector3(-1.25f, 2.15f, spout.z), spout
            }, 0.065f, works);
            Block("OutletBracket", "DarkMetal", new Vector3(-1.25f, 2.15f, 3.1f) + Vector3.up * 0.82f, new Vector3(0.03f, 1.5f, 0.03f), default, 4f, outlet);
            Cyl("Valve_Body", "DarkMetal", new Vector3(-1.25f, 1.25f, TankBase.z), 0.11f, 0.2f, 8, default, 1f, works);
            Cyl("Valve_Stem", "Steel", new Vector3(-1.15f, 1.35f, TankBase.z), 0.02f, 0.1f, 6, new Vector3(0, 0, -90), 1f, works);
            var valveWheel = new GameObject("ValveWheel").transform;
            valveWheel.SetParent(works, false);
            valveWheel.localPosition = new Vector3(-1.04f, 1.35f, TankBase.z);
            valveWheel.localRotation = Quaternion.Euler(0, 0, -90);
            ValveWheel(valveWheel, 0.15f, "RedPaint");
            Block("Spout_Lip", "DarkMetal", spout + new Vector3(0, -0.02f, 0), new Vector3(0.16f, 0.03f, 0.16f), default, 4f, works);

            // The chute: a riveted trough on a post, swinging about its upper end.
            Cyl("ChutePost", "DarkMetal", new Vector3(ChutePivot.x, 0f, ChutePivot.z), 0.045f, ChutePivot.y - 0.04f, 8, default, 2f, works, true);
            Cyl("ChutePostFoot", "DarkMetal", new Vector3(ChutePivot.x, 0f, ChutePivot.z), 0.14f, 0.04f, 8, default, 2f, works);
            var chute = new GameObject("Chute").transform;
            chute.SetParent(works, false);
            chute.localPosition = ChutePivot;
            chute.localRotation = Quaternion.Euler(0, DrainYaw, 0);
            var trough = new GameObject("Trough").transform;
            trough.SetParent(chute, false);
            trough.localRotation = Quaternion.Euler(ChuteSlope, 0, 0);
            Block("Bed", "Rust", new Vector3(0, 0, ChuteLength * 0.5f), new Vector3(0.2f, 0.015f, ChuteLength), default, 2f, trough);
            Block("Side_L", "Rust", new Vector3(-0.1f, 0.04f, ChuteLength * 0.5f), new Vector3(0.015f, 0.09f, ChuteLength), default, 2f, trough);
            Block("Side_R", "Rust", new Vector3(0.1f, 0.04f, ChuteLength * 0.5f), new Vector3(0.015f, 0.09f, ChuteLength), default, 2f, trough);
            Block("Back", "Rust", new Vector3(0, 0.04f, 0.005f), new Vector3(0.2f, 0.09f, 0.015f), default, 2f, trough);
            Cyl("PivotCollar", "DarkMetal", new Vector3(0, -0.06f, 0.05f), 0.05f, 0.06f, 8, default, 2f, trough);
            Block("Handle", "Steel", new Vector3(0.14f, 0.05f, ChuteLength * 0.55f), new Vector3(0.02f, 0.02f, 0.3f), default, 4f, trough);
            var tip = new GameObject("ChuteTip").transform;
            tip.SetParent(trough, false);
            tip.localPosition = new Vector3(0, 0.05f, ChuteLength * 0.55f);

            // Floor drain under the chute's resting end, and the puddle that spreads round it while water is wasted.
            Vector3 drainEnd = ChuteEnd(DrainYaw);
            Block("Drain", "Grate", new Vector3(drainEnd.x, 0.004f, drainEnd.z), new Vector3(0.42f, 0.008f, 0.42f), default, 1f / 0.42f, works);
            var puddle = new GameObject("DrainPuddle").transform;
            puddle.SetParent(works, false);
            puddle.localPosition = new Vector3(drainEnd.x, 0.002f, drainEnd.z);
            var puddleMb = new MeshBuilder { FaceShade = false };
            puddleMb.Cylinder(Vector3.zero, 0.5f, 0.002f, 10, 1f);
            Place("Water", puddleMb, "Water", Vector3.zero, default, puddle);

            // Pulleys, and the chain over them between the bucket and the bolt.
            Vector3 chainDir = new Vector3(PulleyDoor.x - PulleyLow.x, 0f, PulleyDoor.z - PulleyLow.z).normalized;
            var rigging = new GameObject("Rigging").transform;
            rigging.SetParent(works, false);
            Pulley(PulleyLow, chainDir, rigging);
            Pulley(PulleyDoor, chainDir, rigging);
            Vector3 lowDrop = PulleyLow - chainDir * PulleyRadius;
            Vector3 doorDrop = PulleyDoor + chainDir * PulleyRadius;
            var span = new MeshBuilder();
            span.Limb(PulleyLow + Vector3.up * PulleyRadius, PulleyDoor + Vector3.up * PulleyRadius, 0.028f, 0.028f, 0.028f, 0.028f, Vector3.up, 6f);
            Place("ChainSpan", span, "Chain", Vector3.zero, default, rigging);

            // The counterweight bucket hanging from the low pulley, right under the chute's other end.
            Vector3 bucketEnd = ChuteEnd(BucketYaw);
            var weight = new GameObject("Counterweight").transform;
            weight.SetParent(works, false);
            weight.localPosition = new Vector3(lowDrop.x, 1.2f, lowDrop.z);
            var drum = new MeshBuilder();
            OpenDrum(drum, new Vector3(0, -0.62f, 0), 0.24f, 0.42f, 12, 0.015f, 1f);
            Place("Body", drum, "Rust", Vector3.zero, default, weight);
            foreach (float y in new[] { -0.25f, -0.5f })
            {
                var band = new MeshBuilder();
                OpenDrum(band, new Vector3(0, y, 0), 0.252f, 0.03f, 12, 0.012f, 2f);
                Place("Band", band, "DarkMetal", Vector3.zero, default, weight);
            }
            var bail = new MeshBuilder();
            bail.Tube(new Vector3(-0.24f, -0.25f, 0), new Vector3(-0.08f, -0.02f, 0), 0.01f, 0.01f, 5, Vector3.forward, 4f);
            bail.Tube(new Vector3(-0.08f, -0.02f, 0), new Vector3(0.08f, -0.02f, 0), 0.01f, 0.01f, 5, Vector3.forward, 4f);
            bail.Tube(new Vector3(0.08f, -0.02f, 0), new Vector3(0.24f, -0.25f, 0), 0.01f, 0.01f, 5, Vector3.forward, 4f);
            Place("Bail", bail, "Steel", Vector3.zero, default, weight);
            Plate("WeightSign", "SignWeight", new Vector3(0, -0.42f, -0.245f), 0.3f, 0.1f, Vector3.back, weight);
            var bucketWater = new GameObject("BucketWater").transform;
            bucketWater.SetParent(weight, false);
            var bucketWaterMb = new MeshBuilder { FaceShade = false };
            bucketWaterMb.Cylinder(Vector3.zero, 0.22f, 0.004f, 10, 1f);
            Place("Water", bucketWaterMb, "SightWater", Vector3.zero, default, bucketWater);
            var bucketChain = HangingChain("BucketChain", lowDrop, lowDrop.y - weight.localPosition.y, rigging);

            // The bolt: a bar sliding up in two guides on the wall beside the frame, its hasp through the door's edge.
            var bolt = new GameObject("Bolt").transform;
            bolt.SetParent(works, false);
            bolt.localPosition = new Vector3(doorDrop.x, 0f, doorDrop.z);
            Block("Bar", "Steel", new Vector3(0, 1.6f, 0), new Vector3(0.05f, 0.8f, 0.05f), default, 4f, bolt);
            Block("Eye", "Steel", new Vector3(0, 2.02f, 0), new Vector3(0.012f, 0.05f, 0.05f), default, 4f, bolt);
            Block("Hasp", "Steel", new Vector3(0.12f, 1.3f, 0.04f), new Vector3(0.26f, 0.05f, 0.05f), default, 4f, bolt);
            foreach (float gy in new[] { 1.35f, 1.88f })
            {
                Block("Guide", "DarkMetal", new Vector3(doorDrop.x, gy, doorDrop.z), new Vector3(0.1f, 0.06f, 0.1f), default, 4f, works);
                Block("GuideArm", "DarkMetal", new Vector3(doorDrop.x, gy, (doorDrop.z + RoomMax.z) * 0.5f), new Vector3(0.04f, 0.04f, RoomMax.z - doorDrop.z), default, 4f, works);
            }
            Block("Keeper", "DarkMetal", new Vector3(-1.13f, 1.3f, doorDrop.z + 0.04f), new Vector3(0.06f, 0.12f, 0.1f), default, 4f, works);
            var hooked = HangingChain("HookedChain", doorDrop, doorDrop.y - 2.04f, rigging);
            // Off its hook: the chain end hangs loose beside the bolt, the hook dangling at hand height.
            var loose = HangingChain("LooseChain", doorDrop, doorDrop.y - 1.25f, rigging, -4f);
            var hookPoint = new GameObject("HookPoint").transform;
            hookPoint.SetParent(works, false);
            hookPoint.localPosition = doorDrop + Quaternion.Euler(0, 0, -4f) * Vector3.down * (doorDrop.y - 1.25f);
            var hook = new MeshBuilder();
            hook.Tube(new Vector3(0, 0, 0), new Vector3(0, -0.06f, 0), 0.008f, 0.008f, 5, Vector3.forward, 4f);
            hook.Tube(new Vector3(0, -0.06f, 0), new Vector3(0.035f, -0.075f, 0), 0.008f, 0.008f, 5, Vector3.forward, 4f);
            hook.Tube(new Vector3(0.035f, -0.075f, 0), new Vector3(0.045f, -0.04f, 0), 0.008f, 0.008f, 5, Vector3.forward, 4f);
            Place("Hook", hook, "Steel", new Vector3(0, -1f, 0), default, loose.GetChild(0)).transform.localScale = new Vector3(1f, 1f / loose.localScale.y, 1f);

            // Water: from the spout into the chute, and from the chute's end into the bucket or down the drain.
            var streams = new GameObject("Streams").transform;
            streams.SetParent(works, false);
            var fromSpout = Stream("FromSpout", ChutePivot + new Vector3(0, 0.02f, 0), spout.y - ChutePivot.y - 0.04f, 0.05f, streams);
            var toBucket = Stream("ToBucket", new Vector3(bucketEnd.x, 0.75f, bucketEnd.z), bucketEnd.y - 0.75f, 0.05f, streams);
            var toDrain = Stream("ToDrain", new Vector3(drainEnd.x, 0f, drainEnd.z), drainEnd.y, 0.05f, streams);

            var flow = streams.gameObject.AddComponent<AudioSource>();
            flow.clip = PSXSoundGenerator.Load("water_flow");
            flow.loop = true;
            flow.playOnAwake = false;
            flow.spatialBlend = 1f;
            flow.rolloffMode = AudioRolloffMode.Linear;
            flow.minDistance = 0.8f;
            flow.maxDistance = 10f;
            streams.localPosition = Vector3.zero;

            var puzzle = works.gameObject.AddComponent<Game.CisternPuzzle>();
            puzzle.valveWheel = valveWheel;
            puzzle.chute = chute;
            puzzle.drainYaw = DrainYaw;
            puzzle.bucketYaw = BucketYaw;
            puzzle.chuteTip = tip;
            puzzle.counterweight = weight;
            puzzle.bucketWater = bucketWater;
            puzzle.waterLow = -0.58f;
            puzzle.waterHigh = -0.26f;
            puzzle.chainToBucket = bucketChain;
            puzzle.pulleyLow = bucketChain;
            puzzle.tankLevel = level;
            puzzle.bolt = bolt;
            puzzle.looseChain = loose.gameObject;
            puzzle.hookedChain = hooked;
            puzzle.hookPoint = hookPoint;
            puzzle.streamFromSpout = fromSpout;
            puzzle.streamToBucket = toBucket;
            puzzle.streamToDrain = toDrain;
            puzzle.drainPuddle = puddle;
            puzzle.flowSource = flow;
            puzzle.valveClip = PSXSoundGenerator.Load("valve");
            puzzle.chainClip = PSXSoundGenerator.Load("chain");
            puzzle.clunkClip = PSXSoundGenerator.Load("clunk");

            // Start the scene the way the puzzle starts: dry, chain off its hook.
            bucketWater.localPosition = new Vector3(0f, puzzle.waterLow, 0f);
            foreach (var go in new[] { bucketWater.gameObject, puddle.gameObject, hooked.gameObject, fromSpout, toBucket, toDrain }) go.SetActive(false);
            return puzzle;
        }

        static void BuildRoom2Props()
        {
            var props = new GameObject("Props").transform;
            props.SetParent(_world, false);
            float wallX = RoomMax.x - 0.012f, doorZ = RoomMax.z - 0.012f;

            // Signs and notes.
            Plate("Sign_Pump", "SignPump", new Vector3(0.4f, 2.7f, 0.012f), 1.2f, 0.3f, Vector3.forward, props);
            Plate("Sign_Drain", "SignDrain", new Vector3(-wallX, 1.5f, 5.0f), 0.5f, 0.25f, Vector3.right, props);
            Plate("Sign_Bolt", "SignWeight", new Vector3(-2.05f, 2.45f, doorZ), 0.6f, 0.2f, Vector3.back, props);
            Plate("Sign_Exit", "SignExit", new Vector3(DoorHalfWidth - 0.012f, 2.0f, RoomMax.z + 2.2f), 0.5f, 0.17f, Vector3.left, props);
            var chainNote = Plate("Note_Chain", "NoteScrap", new Vector3(-2.15f, 1.5f, doorZ - 0.004f), 0.22f, 0.28f, Vector3.back, props, 5f);
            chainNote.AddComponent<Game.ReadableNote>().text = NoteChain;

            // A cot along the right wall: someone has been sleeping down here.
            var cot = new GameObject("Cot").transform;
            cot.SetParent(props, false);
            cot.localPosition = new Vector3(2.9f, 0f, 2.6f);
            Model("Furniture/bedSingle", Vector3.zero, 0f, 0f, cot, 0f, null, new Vector3(0.72f, 0.62f, 1.95f));

            // Table with a lantern, tins, a bottle and the diary page; a calendar on the wall above.
            var table = new GameObject("Table").transform;
            table.SetParent(props, false);
            table.localPosition = new Vector3(2.75f, 0f, 4.95f);
            Model("Furniture/table", Vector3.zero, 90f, 0f, table, 0f, null, new Vector3(1.0f, 0.785f, 0.8f));
            for (int i = 0; i < 3; i++)
                Cyl("Tin", "Steel", new Vector3(-0.2f + i * 0.11f, 0.785f, 0.32f - i * 0.04f), 0.04f, 0.08f + i * 0.01f, 7, default, 4f, table);
            Cyl("Bottle", "Glass", new Vector3(0.22f, 0.785f, 0.35f), 0.035f, 0.2f, 6, default, 4f, table);
            Cyl("BottleNeck", "Glass", new Vector3(0.22f, 0.985f, 0.35f), 0.014f, 0.07f, 6, default, 4f, table);
            var note = Plate("Note_Day5", "NoteDiary", new Vector3(-0.1f, 0.797f, -0.15f), 0.2f, 0.26f, Vector3.up, table, -14f);
            note.AddComponent<Game.ReadableNote>().text = NoteDay5;
            var lantern = new GameObject("Lantern").transform;
            lantern.SetParent(table, false);
            lantern.localPosition = new Vector3(0.18f, 0.785f, -0.3f);
            Cyl("Base", "DarkMetal", Vector3.zero, 0.07f, 0.04f, 8, default, 4f, lantern);
            Cyl("Glass", "LanternGlass", new Vector3(0, 0.04f, 0), 0.05f, 0.13f, 8, default, 4f, lantern);
            Cyl("Cap", "DarkMetal", new Vector3(0, 0.17f, 0), 0.065f, 0.03f, 8, default, 4f, lantern);
            Block("Handle", "DarkMetal", new Vector3(0, 0.23f, 0), new Vector3(0.1f, 0.01f, 0.01f), default, 4f, lantern);
            var lanternLight = PointLight("LanternLight", Vector3.zero, new Color(1f, 0.7f, 0.38f), 3.6f, 1.2f);
            lanternLight.transform.SetParent(lantern, false);
            lanternLight.transform.localPosition = new Vector3(0, 0.12f, 0);
            var lanternFlicker = lantern.gameObject.AddComponent<PSX.FlameFlicker>();
            lanternFlicker.flameLight = lanternLight;
            lanternFlicker.baseIntensity = 1.2f;
            lanternFlicker.flicker = 0.25f;
            Plate("Calendar", "Calendar", new Vector3(wallX, 1.55f, 4.75f), 0.3f, 0.3f, Vector3.left, props, -3f);

            var chair = new GameObject("Chair").transform;
            chair.SetParent(props, false);
            chair.localPosition = new Vector3(2.15f, 0f, 5.1f);
            Model("Furniture/chair", Vector3.zero, 75f, 0f, chair, 0f, null, new Vector3(0.42f, 0.92f, 0.42f));

            // The old pump in the back corner: motor, flywheel, pipes into the floor and up the wall.
            var pump = new GameObject("Pump").transform;
            pump.SetParent(props, false);
            pump.localPosition = new Vector3(2.55f, 0f, 0.55f);
            Block("Plinth", "Concrete", new Vector3(0, 0.08f, 0), new Vector3(1.1f, 0.16f, 0.7f), default, 1f, pump);
            Block("Housing", "TankPaint", new Vector3(0.1f, 0.42f, 0), new Vector3(0.6f, 0.52f, 0.5f), default, 1f, pump);
            Cyl("Motor", "DarkMetal", new Vector3(-0.2f, 0.45f, 0), 0.18f, 0.35f, 10, new Vector3(0, 0, 90), 1f, pump);
            Cyl("Flywheel", "Steel", new Vector3(-0.56f, 0.45f, 0), 0.26f, 0.05f, 12, new Vector3(0, 0, 90), 1f, pump);
            Cyl("Hub", "DarkMetal", new Vector3(-0.6f, 0.45f, 0), 0.06f, 0.06f, 8, new Vector3(0, 0, 90), 2f, pump);
            Cyl("Gauge_Face", "Gauge", new Vector3(0.1f, 0.62f, -0.255f), 0.06f, 0.01f, 10, new Vector3(-90, 0, 0), 1f / 0.12f, pump);
            PipeRun("PumpRiser", "Rust", new[] { new Vector3(2.75f, 0.68f, 0.55f), new Vector3(2.75f, 1.2f, 0.55f), new Vector3(3.3f, 1.2f, 0.55f), new Vector3(3.3f, RoomMax.y, 0.55f) }, 0.06f, props);
            PipeRun("PumpIntake", "Rust", new[] { new Vector3(2.4f, 0.4f, 0.27f), new Vector3(2.4f, 0.4f, 0.05f), new Vector3(2.4f, -0.05f, 0.05f) }, 0.055f, props, false);

            // Crates by the door and shelving on the left wall.
            Model("Survival/box", new Vector3(2.75f, 0f, 6.95f), -6f, 0f, props, 0f, null, Vector3.one * 0.7f);
            Model("Survival/box-open", new Vector3(2.05f, 0f, 7.05f), 22f, 0f, props, 0f, null, new Vector3(0.5f, 0.55f, 0.5f));
            var shelf = new GameObject("Shelving").transform;
            shelf.SetParent(props, false);
            shelf.localPosition = new Vector3(-3.2f, 0f, 5.55f);
            foreach (var px in new[] { -0.15f, 0.15f })
                foreach (var pz in new[] { -0.45f, 0.45f })
                    Block("Post", "DarkMetal", new Vector3(px, 0.9f, pz), new Vector3(0.04f, 1.8f, 0.04f), default, 1f, shelf);
            for (int i = 0; i < 3; i++)
                Block("Shelf_" + i, "DarkMetal", new Vector3(0, 0.35f + i * 0.6f, 0), new Vector3(0.34f, 0.03f, 0.94f), default, 1f, shelf, true);
            for (int i = 0; i < 4; i++)
                Cyl("Jar_" + i, "Glass", new Vector3(0.02f, 0.965f, -0.33f + i * 0.21f), 0.045f, 0.1f + Hash(i, 3, 92) * 0.08f, 6, default, 4f, shelf);
            Block("Toolbox", "RedPaint", new Vector3(0, 0.43f, 0.15f), new Vector3(0.18f, 0.14f, 0.36f), new Vector3(0, 5, 0), 2f, shelf);
            Cyl("Rope", "Wood", new Vector3(0, 1.565f, -0.2f), 0.11f, 0.08f, 8, default, 4f, shelf);

            // Pipes along the walls and under the ceiling.
            var main = PipeRun("LeftMain", "Rust", new[] { new Vector3(-3.2f, 3.35f, -0.05f), new Vector3(-3.2f, 3.35f, 7.55f) }, 0.07f, props);
            foreach (float z in new[] { 0.9f, 3.0f, 5.3f, 7.1f }) PipeClamp(new Vector3(-3.2f, 3.35f, z), Vector3.forward, Vector3.left, 0.07f, 0.3f, main);
            PipeRun("CeilingA", "DarkMetal", new[] { new Vector3(1.4f, 3.42f, -0.05f), new Vector3(1.4f, 3.42f, 7.55f) }, 0.05f, props, true, true, "Steel");
            var wet = PipeRun("RightRun", "Rust", new[] { new Vector3(3.55f, 2.6f, 3.4f), new Vector3(3.2f, 2.6f, 3.4f), new Vector3(3.2f, 2.6f, 6.0f), new Vector3(3.2f, 0.25f, 6.0f), new Vector3(3.2f, -0.05f, 6.0f) }, 0.06f, props);
            PipeClamp(new Vector3(3.2f, 2.6f, 4.6f), Vector3.forward, Vector3.right, 0.06f, 0.3f, wet);
            PipeClamp(new Vector3(3.2f, 1.3f, 6.0f), Vector3.up, Vector3.right, 0.06f, 0.3f, wet);

            // Puddles from the leaks, and a little rubble.
            foreach (var (pos, r) in new[] { (new Vector3(0.9f, 0.002f, 3.3f), 0.45f), (new Vector3(3.0f, 0.002f, 6.1f), 0.35f), (new Vector3(-0.6f, 0.002f, 6.6f), 0.3f) })
            {
                var mb = new MeshBuilder { FaceShade = false };
                mb.Cylinder(Vector3.zero, r, 0.002f, 9, 1f);
                Place("Puddle", mb, "Water", pos, new Vector3(0, r * 100f, 0), props);
            }
            for (int i = 0; i < 9; i++)
            {
                float s = 0.07f + Hash(i, 2, 93) * 0.14f;
                var p = new Vector3(-0.2f + (Hash(i, 1, 94) - 0.5f) * 0.6f, s * 0.4f, 0.4f + (Hash(i, 3, 95) - 0.5f) * 0.4f);
                Block("Rubble", i % 3 == 0 ? "Concrete" : "Stone", p, new Vector3(s, s * 0.7f, s * 0.9f),
                      new Vector3(Hash(i, 4, 96) * 40f, Hash(i, 5, 97) * 360f, Hash(i, 6, 98) * 30f), 2f, props);
            }
            Helmet(new Vector3(-0.1f, 0.1f, 6.5f), -60f, props);
        }

        static void BuildRoom2Lights()
        {
            // A flickering fluorescent tube over the middle of the room: the only steady-ish light.
            var tube = new GameObject("Fluorescent").transform;
            tube.SetParent(_world, false);
            tube.localPosition = new Vector3(0.6f, RoomMax.y - 0.12f, 3.0f);
            Block("Housing", "DarkMetal", new Vector3(0, 0.05f, 0), new Vector3(0.18f, 0.05f, 1.3f), default, 2f, tube);
            Cyl("Tube", "Fluorescent", new Vector3(0, -0.01f, -0.6f), 0.025f, 1.2f, 6, new Vector3(90, 0, 0), 1f, tube);
            foreach (float z in new[] { -0.4f, 0.4f })
                Block("Chain", "DarkMetal", new Vector3(0, 0.1f, z), new Vector3(0.01f, 0.1f, 0.01f), default, 4f, tube);
            var tubeLight = PointLight("TubeLight", new Vector3(0.6f, RoomMax.y - 0.4f, 3.0f), new Color(0.7f, 0.95f, 0.85f), 7.5f, 1.5f);
            var buzz = tube.gameObject.AddComponent<PSX.FlameFlicker>();
            buzz.flameLight = tubeLight;
            buzz.baseIntensity = 1.5f;
            buzz.flicker = 0.55f;
            buzz.speed = 22f;

            // Caged lamp over the door: the door is the goal, so it gets light.
            var doorLamp = new GameObject("DoorLamp").transform;
            doorLamp.SetParent(_world, false);
            doorLamp.localPosition = new Vector3(0, RoomMax.y, RoomMax.z - 0.7f);
            Block("Cord", "DarkMetal", new Vector3(0, -0.15f, 0), new Vector3(0.02f, 0.3f, 0.02f), default, 1f, doorLamp);
            Cyl("Shade", "DarkMetal", new Vector3(0, -0.4f, 0), 0.18f, 0.08f, 8, default, 1f, doorLamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.45f, 0), new Vector3(0.07f, 0.08f, 0.07f), default, 1f, doorLamp);
            var doorHalo = new MeshBuilder();
            doorHalo.Cross(Vector3.zero, 0.5f, 0.5f);
            Place("BulbHalo", doorHalo, "Halo", new Vector3(0, -0.7f, 0), default, doorLamp);
            PointLight("DoorLight", new Vector3(0, RoomMax.y - 0.7f, RoomMax.z - 0.9f), new Color(1f, 0.85f, 0.62f), 4.5f, 1.8f);

            // Red warning lamp over the door, and cold light picking out the cistern and the chute.
            Block("RedLamp", "RedLamp", new Vector3(1.4f, 2.9f, RoomMax.z - 0.06f), new Vector3(0.14f, 0.14f, 0.1f), default, 1f, _world);
            var redHalo = new MeshBuilder();
            redHalo.Cross(Vector3.zero, 0.6f, 0.6f);
            Place("RedHalo", redHalo, "RedHalo", new Vector3(1.4f, 2.6f, RoomMax.z - 0.15f), default);
            PointLight("RedLight", new Vector3(1.4f, 2.75f, RoomMax.z - 0.3f), new Color(1f, 0.12f, 0.08f), 2.4f, 0.7f);
            PointLight("TankFill", new Vector3(-1.2f, 3.0f, 2.6f), new Color(0.45f, 0.6f, 0.85f), 5f, 1.1f);
            PointLight("WeightFill", new Vector3(0.4f, 2.4f, 5.4f), new Color(0.5f, 0.62f, 0.9f), 4f, 0.7f);
            WallLamp(new Vector3(RoomMin.x + 0.2f, 2.3f, 4.2f), Vector3.right, _world);
            PointLight("WallLampLight", new Vector3(RoomMin.x + 0.5f, 2.2f, 4.2f), new Color(1f, 0.78f, 0.5f), 3f, 0.7f);
        }
    }
}
