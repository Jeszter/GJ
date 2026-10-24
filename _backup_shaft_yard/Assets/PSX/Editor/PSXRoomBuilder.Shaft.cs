using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // Room 2, the lift shaft, made from the pump room (PSXRoomBuilder.Room2.cs, kept as it was, with its own scene, as
    // the fallback). The front of the room is still the pump room's landing; past ShaftFront it opens up into a tall
    // shaft with a cage at the bottom. The same cistern, chute and counterweight bucket work the same way, but the bolt
    // is the cage's catch: fill the bucket and the catch lets go, the cage rides up to the vault door at the top landing,
    // and whoever lies in it comes back to life up there.
    public static partial class PSXRoomBuilder
    {
        const string ShaftPath = "Assets/Scenes/Shaft.unity";

        [MenuItem("GJ/Build Shaft (room 2)")]
        public static void BuildShaftRoom()
        {
            if (!BeginBuild()) return;
            BuildShaft();
            FinishBuild();
        }

        // The shaft: everything past ShaftFront is open up to ShaftTop. The cage runs from the floor to the landing in
        // front of the vault door, ShaftLanding up; its floor spans CageFront..CageBack and ±CageHalfWidth.
        const float ShaftFront = 4.9f, ShaftTop = 14f, ShaftLanding = 11f;
        const float CageHalfWidth = 0.88f, CageFront = 5.6f, CageBack = 7.28f, CageRoof = 2.3f;

        // Machinery positions, shared by the geometry and the puzzle wiring. The bucket hangs to the left of the cage's
        // way up (the chute swings almost straight ahead to reach it), so its chain runs along the wall, clear of the cage.
        static readonly Vector3 ShaftTankBase = new Vector3(-2.45f, 0f, 2.3f);
        const float ShaftTankRadius = 0.7f;
        static readonly Vector3 ShaftChutePivot = new Vector3(-1.25f, 1.62f, 3.9f);
        const float ShaftChuteLength = 1.3f, ShaftChuteSlope = 14f, ShaftDrainYaw = -67.4f, ShaftBucketYaw = -4.5f;
        static readonly Vector3 ShaftPulleyLow = new Vector3(-1.35f, 3.35f, 5.16f);
        static readonly Vector3 ShaftPulleyDoor = new Vector3(-1.35f, 3.35f, 7.22f);
        const float ShaftPulleyRadius = 0.08f;

        const string ShaftNoteDay5 =
            "DAY 5\n\n" +
            "The lift shaft. The cage sits on\n" +
            "its catch at the bottom.\n\n" +
            "The catch hangs on a chain over\n" +
            "to a bucket. Fill the bucket, the\n" +
            "catch lets go and the weight\n" +
            "takes the cage up.\n\n" +
            "It never fills in one breath.";

        const string ShaftNoteChain =
            "The chain slipped off the catch\n" +
            "again. Hook it back first.\n\n" +
            "The chute empties into the drain\n" +
            "unless you swing it over.\n\n" +
            "One tank. Don't waste it.\n\n" +
            "And be IN the cage when\n" +
            "you go.\n\n" +
            "                        - M.";

        static Vector3 ShaftChuteDir(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(ShaftChuteSlope, 0f, 0f) * Vector3.forward;
        static Vector3 ShaftChuteEnd(float yaw) => ShaftChutePivot + ShaftChuteDir(yaw) * ShaftChuteLength;

        static void BuildShaft()
        {
            RoomMin = new Vector3(-3.5f, 0f, 0f);
            RoomMax = new Vector3(3.5f, 3.8f, 7.5f);
            var scene = NewRoomScene("Shaft");

            BuildShaftShell(new[] { 1.8f, 4.2f });
            BuildDoor();
            // The vault door is the way on, up at the landing, and opens without a blast: the cage brings you to it.
            if (_door.flash != null) Object.DestroyImmediate(_door.flash.gameObject);
            _door.flash = null;
            _door.blastClip = null;
            // Its air display stays down here, hung under the edge of the landing ceiling where you can read it.
            var display = new GameObject("AirDisplay").transform;
            display.SetParent(_world, false);
            display.localPosition = new Vector3(0f, 0.45f, ShaftFront + 0.02f);
            var parts = new System.Collections.Generic.List<Transform>();
            foreach (Transform part in _door.transform)
                if (part.name.StartsWith("Display") || part.name.StartsWith("Bezel") || part.name.StartsWith("Bracket") ||
                    part.name == "Housing" || part.name == "Screw" || part.name == "Visor" || part.name == "HazardStripes")
                    parts.Add(part);
            foreach (var part in parts)
            {
                if (part.name == "DisplayConduit" || part.name == "HazardStripes") Object.DestroyImmediate(part.gameObject);
                else part.SetParent(display, false);
            }
            foreach (float hx in new[] { -0.42f, 0.42f })
                Block("Hanger", "DarkMetal", new Vector3(hx, 3.25f, -0.05f), new Vector3(0.025f, 0.2f, 0.025f), default, 4f, display);
            _door.transform.localPosition = new Vector3(0f, ShaftLanding, RoomMax.z);

            var puzzle = BuildShaftCistern();
            BuildShaftProps();
            BuildShaftLights();
            DressRoom2();
            Models2();
            AutoColliders(_world);
            Life2();
            ClearShaft();
            // After the automatic colliders: the cage gets just the ones it needs to stand on and be shut in.
            BuildShaftCage(puzzle);

            var rig = BuildPlayer(new Vector3(0.6f, 0f, 0.9f), 0f);
            puzzle.body = rig.controller.transform;
            BuildDirector(rig, puzzle, 75f, "SECTOR 4 - LIFT SHAFT\nyou have 75 seconds of air", 2, "Yard");

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.12f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.03f, 0.045f, 0.05f);
            // Thinner than in the rooms, so the light at the top of the shaft reads from the bottom.
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 22f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;

            EditorSceneManager.SaveScene(scene, ShaftPath);
            Debug.Log("PSX room built: " + ShaftPath);
        }

        // Floor; the landing ceiling over the front of the room; side walls that rise to the shaft's roof past ShaftFront
        // and the shaft's face above the landing; the back wall, full height, with the doorway up at the landing and the
        // corridor behind it; beams and pillars under the landing ceiling; girders, guide rails and level marks up the
        // shaft, and the beam the bucket's pulleys hang from.
        static void BuildShaftShell(float[] beamZ)
        {
            var shell = new GameObject("Shell").transform;
            shell.SetParent(_world, false);
            Vector3 mn = RoomMin, mx = RoomMax, size = mx - mn;
            const float cell = 0.5f, stone = 0.5f, floorUv = 0.5f;
            float landingDepth = ShaftFront - mn.z, shaftDepth = mx.z - ShaftFront, lid = ShaftLanding + DoorHeight;

            var floor = new MeshBuilder { FaceShade = false };
            floor.Quad(new Vector3(mn.x, 0, mn.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), floorUv, cell);
            Place("Floor", floor, "Floor", Vector3.zero, default, shell, true);

            var ceiling = new MeshBuilder { FaceShade = false };
            ceiling.Quad(new Vector3(mn.x, mx.y, mn.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, landingDepth), 1f, cell, true);
            ceiling.Quad(new Vector3(mn.x, ShaftTop, ShaftFront), new Vector3(size.x, 0, 0), new Vector3(0, 0, shaftDepth), 1f, cell, true);
            Place("Ceiling", ceiling, "Ceiling", Vector3.zero, default, shell, true);

            var walls = new MeshBuilder { FaceShade = false };
            walls.Quad(new Vector3(mx.x, 0, mn.z), new Vector3(0, 0, landingDepth), new Vector3(0, mx.y, 0), stone, cell, true);      // right, landing
            walls.Quad(new Vector3(mx.x, 0, ShaftFront), new Vector3(0, 0, shaftDepth), new Vector3(0, ShaftTop, 0), stone, cell, true); // right, shaft
            walls.Quad(new Vector3(mn.x, 0, ShaftFront), new Vector3(0, 0, -landingDepth), new Vector3(0, mx.y, 0), stone, cell, true);  // left, landing
            walls.Quad(new Vector3(mn.x, 0, mx.z), new Vector3(0, 0, -shaftDepth), new Vector3(0, ShaftTop, 0), stone, cell, true);      // left, shaft
            walls.Quad(new Vector3(mn.x, 0, mn.z), new Vector3(size.x, 0, 0), new Vector3(0, mx.y, 0), stone, cell, true);                // front
            walls.Quad(new Vector3(mn.x, mx.y, ShaftFront), new Vector3(size.x, 0, 0), new Vector3(0, ShaftTop - mx.y, 0), stone, cell, true); // shaft face
            // Back wall round the doorway up at the landing.
            walls.Quad(new Vector3(mx.x, 0, mx.z), new Vector3(-(mx.x - DoorHalfWidth), 0, 0), new Vector3(0, ShaftTop, 0), stone, cell, true);
            walls.Quad(new Vector3(-DoorHalfWidth, 0, mx.z), new Vector3(-(mx.x - DoorHalfWidth), 0, 0), new Vector3(0, ShaftTop, 0), stone, cell, true);
            walls.Quad(new Vector3(DoorHalfWidth, 0, mx.z), new Vector3(-2f * DoorHalfWidth, 0, 0), new Vector3(0, ShaftLanding, 0), stone, cell, true);
            walls.Quad(new Vector3(DoorHalfWidth, lid, mx.z), new Vector3(-2f * DoorHalfWidth, 0, 0), new Vector3(0, ShaftTop - lid, 0), stone, cell, true);
            Place("Walls", walls, "Stone", Vector3.zero, default, shell, true);

            // Behind the door, up at the landing: a short corridor ending in a cold bright light, the way on.
            const float corridorLen = 3.5f;
            float cz = mx.z, cw = DoorHalfWidth * 2f, cy = ShaftLanding;
            var corridor = new MeshBuilder { FaceShade = false };
            corridor.Quad(new Vector3(-DoorHalfWidth, cy, cz), new Vector3(cw, 0, 0), new Vector3(0, 0, corridorLen), floorUv, cell);
            corridor.Quad(new Vector3(-DoorHalfWidth, lid, cz), new Vector3(cw, 0, 0), new Vector3(0, 0, corridorLen), 1f, cell, true);
            corridor.Quad(new Vector3(DoorHalfWidth, cy, cz), new Vector3(0, 0, corridorLen), new Vector3(0, DoorHeight, 0), stone, cell, true);
            corridor.Quad(new Vector3(-DoorHalfWidth, cy, cz + corridorLen), new Vector3(0, 0, -corridorLen), new Vector3(0, DoorHeight, 0), stone, cell, true);
            Place("Corridor", corridor, "Stone", Vector3.zero, default, shell, true);
            var exitWall = new MeshBuilder { FaceShade = false };
            exitWall.Quad(new Vector3(DoorHalfWidth, cy, cz + corridorLen), new Vector3(-cw, 0, 0), new Vector3(0, DoorHeight, 0), 1f, cell, true);
            Place("ExitGlow", exitWall, "ExitGlow", Vector3.zero, default, shell, true);
            var exitLight = PointLight("ExitLight", new Vector3(0, cy + 1.6f, cz + corridorLen - 0.6f), new Color(0.8f, 0.9f, 1f), 4.5f, 2.5f);
            exitLight.transform.SetParent(shell, true);

            // Skirting, beams and pillars under the landing ceiling, and a heavy lintel along its edge over the shaft.
            Block("Skirting_L", "DarkMetal", new Vector3(mn.x + 0.04f, 0.08f, (mn.z + mx.z) * 0.5f), new Vector3(0.08f, 0.16f, size.z), default, 1f, shell);
            Block("Skirting_R", "DarkMetal", new Vector3(mx.x - 0.04f, 0.08f, (mn.z + mx.z) * 0.5f), new Vector3(0.08f, 0.16f, size.z), default, 1f, shell);
            for (int i = 0; i < beamZ.Length; i++)
            {
                float z = beamZ[i];
                Block("Beam_" + i, "Rust", new Vector3(0, mx.y - 0.12f, z), new Vector3(size.x, 0.24f, 0.22f), default, 1f, shell);
                foreach (float side in new[] { -1f, 1f })
                {
                    float wx = side < 0 ? mn.x : mx.x;
                    Block("Pillar", "Concrete", new Vector3(wx - side * 0.07f, mx.y * 0.5f, z), new Vector3(0.14f, mx.y, 0.3f), default, 1f, shell, true);
                    Block("PillarFoot", "Concrete", new Vector3(wx - side * 0.1f, 0.12f, z), new Vector3(0.2f, 0.24f, 0.38f), default, 1f, shell, true);
                }
            }
            Block("Lintel", "Rust", new Vector3(0, mx.y - 0.16f, ShaftFront - 0.12f), new Vector3(size.x, 0.32f, 0.24f), default, 1f, shell);

            // Up the shaft: girders round the walls (never across the cage's way), the cage's guide rails, a band of
            // hazard paint at each level it passes, and the beam the bucket's pulleys hang from.
            float sz = ShaftFront + shaftDepth * 0.5f, edge = CageHalfWidth + 0.17f;
            foreach (float gy in new[] { 5.6f, 8.3f })
            {
                Block("Girder_L", "Rust", new Vector3(mn.x + 0.1f, gy, sz), new Vector3(0.2f, 0.26f, shaftDepth), default, 1f, shell);
                Block("Girder_R", "Rust", new Vector3(mx.x - 0.1f, gy, sz), new Vector3(0.2f, 0.26f, shaftDepth), default, 1f, shell);
                Block("Girder_BL", "Rust", new Vector3((mn.x - edge) * 0.5f, gy, mx.z - 0.1f), new Vector3(-edge - mn.x, 0.26f, 0.2f), default, 1f, shell);
                Block("Girder_BR", "Rust", new Vector3((mx.x + edge) * 0.5f, gy, mx.z - 0.1f), new Vector3(mx.x - edge, 0.26f, 0.2f), default, 1f, shell);
            }
            foreach (float rx in new[] { -CageHalfWidth - 0.1f, CageHalfWidth + 0.1f })
                Block("GuideRail", "Steel", new Vector3(rx, ShaftLanding * 0.5f, mx.z - 0.06f), new Vector3(0.07f, ShaftLanding, 0.1f), default, 1f, shell);
            foreach (float ly in new[] { mx.y + 0.1f, 7.6f, ShaftLanding - 0.1f })
                foreach (float lx in new[] { -2.3f, 2.3f })
                    Plate("LevelMark", "Hazard", new Vector3(lx, ly, mx.z - 0.012f), 1.7f, 0.16f, Vector3.back, shell);
            Block("PulleyBeam", "Rust", new Vector3(ShaftPulleyLow.x, mx.y + 0.06f, sz), new Vector3(0.16f, 0.2f, shaftDepth), default, 1f, shell);
        }

        // Anything the shared dressing put in the cage's way up, or hung from a ceiling that isn't there past ShaftFront,
        // comes out again.
        static void ClearShaft()
        {
            var way = new Bounds(new Vector3(0f, ShaftTop * 0.5f, (CageFront + RoomMax.z) * 0.5f),
                                 new Vector3(2f * CageHalfWidth + 0.2f, ShaftTop, RoomMax.z - CageFront + 0.1f));
            var keep = new[] { "Shell", "Cistern", "VaultDoor", "AirDisplay" };
            foreach (Transform group in _world)
            {
                if (System.Array.IndexOf(keep, group.name) >= 0) continue;
                for (int i = group.childCount - 1; i >= 0; i--)
                {
                    var item = group.GetChild(i);
                    if (item.name.StartsWith("Dust")) continue;
                    var renderers = item.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) continue;
                    var b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    bool inTheWay = b.Intersects(way) && b.max.y > 0.09f;
                    bool hungFromNothing = b.center.z > ShaftFront && b.max.y > RoomMax.y - 0.3f && b.min.y > 1.5f;
                    if (inTheWay || hungFromNothing) Object.DestroyImmediate(item.gameObject);
                }
            }
        }

        // The cage at the bottom of the shaft: a grated floor, corner posts and a roof frame, barred sides, open at the
        // back (the vault door is behind it at the top), a gate hung up over the front that drops shut when it goes, a
        // keeper on its back left post for the catch, and a bulb of its own. Its chain runs from the roof to a sheave
        // under the shaft's roof.
        static void BuildShaftCage(Game.ShaftPuzzle puzzle)
        {
            var cage = new GameObject("Cage").transform;
            cage.SetParent(_world, false);
            float w = CageHalfWidth, z0 = CageFront, z1 = CageBack, zc = (z0 + z1) * 0.5f, depth = z1 - z0;
            Block("Floor", "Grate", new Vector3(0, 0.04f, zc), new Vector3(2f * w, 0.08f, depth), default, 2f, cage, true);
            Plate("FloorEdge", "Hazard", new Vector3(0, 0.084f, z0 + 0.08f), 2f * w, 0.14f, Vector3.up, cage);
            foreach (var px in new[] { -w, w })
                foreach (var pz in new[] { z0, z1 })
                    Block("Post", "DarkMetal", new Vector3(px, CageRoof * 0.5f, pz), new Vector3(0.07f, CageRoof, 0.07f), default, 2f, cage);
            Block("Roof_F", "DarkMetal", new Vector3(0, CageRoof, z0), new Vector3(2f * w + 0.07f, 0.07f, 0.07f), default, 2f, cage);
            Block("Roof_B", "DarkMetal", new Vector3(0, CageRoof, z1), new Vector3(2f * w + 0.07f, 0.07f, 0.07f), default, 2f, cage);
            Block("Roof_L", "DarkMetal", new Vector3(-w, CageRoof, zc), new Vector3(0.07f, 0.07f, depth), default, 2f, cage);
            Block("Roof_R", "DarkMetal", new Vector3(w, CageRoof, zc), new Vector3(0.07f, 0.07f, depth), default, 2f, cage);
            Block("RoofBar", "DarkMetal", new Vector3(0, CageRoof, zc), new Vector3(2f * w, 0.06f, 0.06f), default, 2f, cage);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * w;
                for (int i = 1; i < 6; i++)
                    Block("Bar", "Steel", new Vector3(x, CageRoof * 0.5f, z0 + depth * i / 6f), new Vector3(0.025f, CageRoof, 0.025f), default, 4f, cage);
                foreach (float y in new[] { 0.55f, 1.1f })
                    Block("Rail", "DarkMetal", new Vector3(x, y, zc), new Vector3(0.04f, 0.05f, depth), default, 2f, cage);
                var wall = new GameObject("Side").AddComponent<BoxCollider>();
                wall.transform.SetParent(cage, false);
                wall.center = new Vector3(x, CageRoof * 0.5f, zc);
                wall.size = new Vector3(0.06f, CageRoof, depth);
            }
            // The gate hangs with its foot at roof height until it drops.
            var gate = new GameObject("Gate").transform;
            gate.SetParent(cage, false);
            gate.localPosition = new Vector3(0f, CageRoof - 0.1f, z0 - 0.06f);
            for (int i = 0; i <= 6; i++)
                Block("GateBar", "Steel", new Vector3(-w + 2f * w * i / 6f, 1.08f, 0), new Vector3(0.025f, 2.06f, 0.025f), default, 4f, gate);
            foreach (float y in new[] { 0.06f, 1.08f, 2.1f })
                Block("GateRail", "DarkMetal", new Vector3(0, y, 0), new Vector3(2f * w, 0.05f, 0.04f), default, 2f, gate);
            var gateWall = gate.gameObject.AddComponent<BoxCollider>();
            gateWall.center = new Vector3(0f, 1.08f, 0f);
            gateWall.size = new Vector3(2f * w, 2.1f, 0.06f);
            Block("Keeper", "DarkMetal", new Vector3(-w - 0.1f, 1.3f, z1 - 0.04f), new Vector3(0.24f, 0.1f, 0.08f), default, 4f, cage);
            Block("CageBulb", "Bulb", new Vector3(0.55f, CageRoof - 0.08f, zc), new Vector3(0.06f, 0.08f, 0.06f), default, 1f, cage);
            var bulb = PointLight("CageLight", new Vector3(0.55f, CageRoof - 0.3f, zc), new Color(1f, 0.82f, 0.55f), 3.2f, 0.9f);
            bulb.transform.SetParent(cage, true);

            // The sheave under the shaft's roof and the chain down to the cage.
            float sheaveY = ShaftTop - 0.55f, cableTop = sheaveY - 0.28f;
            Block("SheaveBeam", "Rust", new Vector3(0, ShaftTop - 0.18f, zc), new Vector3(RoomMax.x - RoomMin.x, 0.3f, 0.3f), default, 1f, _world);
            Cyl("Sheave", "DarkMetal", new Vector3(-0.04f, sheaveY, zc), 0.28f, 0.08f, 12, new Vector3(0, 0, -90), 1f, _world);
            Block("SheaveFork", "DarkMetal", new Vector3(0, ShaftTop - 0.38f, zc), new Vector3(0.14f, 0.3f, 0.06f), default, 4f, _world);
            var cable = HangingChain("CageChain", new Vector3(0, cableTop, zc), cableTop - CageRoof, _world);

            var focus = new GameObject("LandingFocus").transform;
            focus.SetParent(_world, false);
            focus.localPosition = new Vector3(0f, ShaftLanding + 1.6f, RoomMax.z + 0.4f);
            var ride = cage.gameObject.AddComponent<AudioSource>();
            ride.clip = PSXSoundGenerator.Load("rumble");
            ride.loop = true;
            ride.playOnAwake = false;
            ride.spatialBlend = 0.6f;
            ride.pitch = 0.7f;
            ride.volume = 0.8f;

            puzzle.cage = cage;
            puzzle.riseHeight = ShaftLanding;
            puzzle.cageGate = gate;
            puzzle.cable = cable;
            puzzle.cableTop = cableTop;
            puzzle.cageRoof = CageRoof;
            puzzle.floorMin = new Vector2(-w + 0.12f, z0 + 0.2f);
            puzzle.floorMax = new Vector2(w - 0.12f, z1 - 0.05f);
            puzzle.landing = focus;
            puzzle.rideSource = ride;
            puzzle.gateClip = PSXSoundGenerator.Load("chain");
        }

        static Game.ShaftPuzzle BuildShaftCistern()
        {
            var works = new GameObject("Cistern").transform;
            works.SetParent(_world, false);

            // The tank on its stand, banded and riveted, fed from the ceiling.
            var tank = new GameObject("Tank").transform;
            tank.SetParent(works, false);
            tank.localPosition = ShaftTankBase;
            foreach (var lx in new[] { -0.45f, 0.45f })
                foreach (var lz in new[] { -0.45f, 0.45f })
                    Block("Leg", "DarkMetal", new Vector3(lx, 0.27f, lz), new Vector3(0.08f, 0.54f, 0.08f), default, 2f, tank);
            Cyl("Saddle", "DarkMetal", new Vector3(0, 0.5f, 0), ShaftTankRadius + 0.02f, 0.06f, 12, default, 1f, tank);
            Cyl("Body", "TankPaint", new Vector3(0, 0.55f, 0), ShaftTankRadius, 1.6f, 12, default, 0.5f, tank, true);
            var dome = new MeshBuilder();
            dome.Tube(new Vector3(0, 2.15f, 0), new Vector3(0, 2.4f, 0), ShaftTankRadius, 0.24f, 12, Vector3.forward, 1f);
            Place("Dome", dome, "TankPaint", Vector3.zero, default, tank);
            Cyl("Manhole", "DarkMetal", new Vector3(0.2f, 2.32f, 0.1f), 0.16f, 0.06f, 8, new Vector3(-12, 0, 8), 2f, tank);
            foreach (float y in new[] { 0.8f, 1.35f, 1.9f })
                Cyl("Band", "DarkMetal", new Vector3(0, y, 0), ShaftTankRadius + 0.015f, 0.05f, 12, default, 1f, tank);
            PipeRun("TankFeed", "Rust", new[] { ShaftTankBase + new Vector3(0, 2.4f, 0), ShaftTankBase + new Vector3(0, RoomMax.y, 0) }, 0.07f, works);
            Plate("TankStencil", "SignSector2", ShaftTankBase + new Vector3(ShaftTankRadius * 0.7f + 0.005f, 1.6f, -ShaftTankRadius * 0.7f - 0.005f), 0.5f, 0.12f, Vector3.right, works, 0f)
                .transform.localRotation = Quaternion.Euler(0, -45, 0);

            // Sight glass on the tank's flank: a lit water column in a steel cage with a scale plate behind it.
            float ang = 35f * Mathf.Deg2Rad;
            var glassPos = ShaftTankBase + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (ShaftTankRadius + 0.08f);
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
                stubs.Tube(ShaftTankBase + radial * (ShaftTankRadius - 0.02f) + Vector3.up * y, glassPos + Vector3.up * y, 0.018f, 0.018f, 6, Vector3.up, 4f);
            Place("GlassStubs", stubs, "Steel", Vector3.zero, default, works);

            // Outlet: out of the tank, up, across, and down to the spout over the chute pivot. The valve sits on the
            // climb, its wheel facing the room.
            var spout = ShaftChutePivot + new Vector3(0, 0.33f, 0);
            var outlet = PipeRun("Outlet", "Rust", new[]
            {
                ShaftTankBase + new Vector3(ShaftTankRadius - 0.05f, 0.85f, 0), new Vector3(-1.25f, 0.85f, ShaftTankBase.z),
                new Vector3(-1.25f, 2.15f, ShaftTankBase.z), new Vector3(-1.25f, 2.15f, spout.z), spout
            }, 0.065f, works);
            Block("OutletBracket", "DarkMetal", new Vector3(-1.25f, 2.15f, 3.1f) + Vector3.up * 0.82f, new Vector3(0.03f, 1.5f, 0.03f), default, 4f, outlet);
            Cyl("Valve_Body", "DarkMetal", new Vector3(-1.25f, 1.25f, ShaftTankBase.z), 0.11f, 0.2f, 8, default, 1f, works);
            Cyl("Valve_Stem", "Steel", new Vector3(-1.15f, 1.35f, ShaftTankBase.z), 0.02f, 0.1f, 6, new Vector3(0, 0, -90), 1f, works);
            var valveWheel = new GameObject("ValveWheel").transform;
            valveWheel.SetParent(works, false);
            valveWheel.localPosition = new Vector3(-1.04f, 1.35f, ShaftTankBase.z);
            valveWheel.localRotation = Quaternion.Euler(0, 0, -90);
            ValveWheel(valveWheel, 0.15f, "RedPaint");
            Block("Spout_Lip", "DarkMetal", spout + new Vector3(0, -0.02f, 0), new Vector3(0.16f, 0.03f, 0.16f), default, 4f, works);

            // The chute: a riveted trough on a post, swinging about its upper end.
            Cyl("ChutePost", "DarkMetal", new Vector3(ShaftChutePivot.x, 0f, ShaftChutePivot.z), 0.045f, ShaftChutePivot.y - 0.04f, 8, default, 2f, works, true);
            Cyl("ChutePostFoot", "DarkMetal", new Vector3(ShaftChutePivot.x, 0f, ShaftChutePivot.z), 0.14f, 0.04f, 8, default, 2f, works);
            var chute = new GameObject("Chute").transform;
            chute.SetParent(works, false);
            chute.localPosition = ShaftChutePivot;
            chute.localRotation = Quaternion.Euler(0, ShaftDrainYaw, 0);
            var trough = new GameObject("Trough").transform;
            trough.SetParent(chute, false);
            trough.localRotation = Quaternion.Euler(ShaftChuteSlope, 0, 0);
            Block("Bed", "Rust", new Vector3(0, 0, ShaftChuteLength * 0.5f), new Vector3(0.2f, 0.015f, ShaftChuteLength), default, 2f, trough);
            Block("Side_L", "Rust", new Vector3(-0.1f, 0.04f, ShaftChuteLength * 0.5f), new Vector3(0.015f, 0.09f, ShaftChuteLength), default, 2f, trough);
            Block("Side_R", "Rust", new Vector3(0.1f, 0.04f, ShaftChuteLength * 0.5f), new Vector3(0.015f, 0.09f, ShaftChuteLength), default, 2f, trough);
            Block("Back", "Rust", new Vector3(0, 0.04f, 0.005f), new Vector3(0.2f, 0.09f, 0.015f), default, 2f, trough);
            Cyl("PivotCollar", "DarkMetal", new Vector3(0, -0.06f, 0.05f), 0.05f, 0.06f, 8, default, 2f, trough);
            Block("Handle", "Steel", new Vector3(0.14f, 0.05f, ShaftChuteLength * 0.55f), new Vector3(0.02f, 0.02f, 0.3f), default, 4f, trough);
            var tip = new GameObject("ChuteTip").transform;
            tip.SetParent(trough, false);
            tip.localPosition = new Vector3(0, 0.05f, ShaftChuteLength * 0.55f);

            // Floor drain under the chute's resting end, and the puddle that spreads round it while water is wasted.
            Vector3 drainEnd = ShaftChuteEnd(ShaftDrainYaw);
            Block("Drain", "Grate", new Vector3(drainEnd.x, 0.004f, drainEnd.z), new Vector3(0.42f, 0.008f, 0.42f), default, 1f / 0.42f, works);
            var puddle = new GameObject("DrainPuddle").transform;
            puddle.SetParent(works, false);
            puddle.localPosition = new Vector3(drainEnd.x, 0.002f, drainEnd.z);
            var puddleMb = new MeshBuilder { FaceShade = false };
            puddleMb.Cylinder(Vector3.zero, 0.5f, 0.002f, 10, 1f);
            Place("Water", puddleMb, "Water", Vector3.zero, default, puddle);

            // Pulleys, and the chain over them between the bucket and the bolt.
            Vector3 chainDir = new Vector3(ShaftPulleyDoor.x - ShaftPulleyLow.x, 0f, ShaftPulleyDoor.z - ShaftPulleyLow.z).normalized;
            var rigging = new GameObject("Rigging").transform;
            rigging.SetParent(works, false);
            Pulley(ShaftPulleyLow, chainDir, rigging);
            Pulley(ShaftPulleyDoor, chainDir, rigging);
            Vector3 lowDrop = ShaftPulleyLow - chainDir * ShaftPulleyRadius;
            Vector3 doorDrop = ShaftPulleyDoor + chainDir * ShaftPulleyRadius;
            var span = new MeshBuilder();
            span.Limb(ShaftPulleyLow + Vector3.up * ShaftPulleyRadius, ShaftPulleyDoor + Vector3.up * ShaftPulleyRadius, 0.028f, 0.028f, 0.028f, 0.028f, Vector3.up, 6f);
            Place("ChainSpan", span, "Chain", Vector3.zero, default, rigging);

            // The counterweight bucket hanging from the low pulley, right under the chute's other end.
            Vector3 bucketEnd = ShaftChuteEnd(ShaftBucketYaw);
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
            var fromSpout = Stream("FromSpout", ShaftChutePivot + new Vector3(0, 0.02f, 0), spout.y - ShaftChutePivot.y - 0.04f, 0.05f, streams);
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

            var puzzle = works.gameObject.AddComponent<Game.ShaftPuzzle>();
            puzzle.valveWheel = valveWheel;
            puzzle.chute = chute;
            puzzle.drainYaw = ShaftDrainYaw;
            puzzle.bucketYaw = ShaftBucketYaw;
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

        static void BuildShaftProps()
        {
            var props = new GameObject("Props").transform;
            props.SetParent(_world, false);
            float wallX = RoomMax.x - 0.012f, doorZ = RoomMax.z - 0.012f;

            // Signs and notes.
            Plate("Sign_Pump", "SignPump", new Vector3(0.4f, 2.7f, 0.012f), 1.2f, 0.3f, Vector3.forward, props);
            Plate("Sign_Drain", "SignDrain", new Vector3(-wallX, 1.5f, 5.0f), 0.5f, 0.25f, Vector3.right, props);
            Plate("Sign_Bolt", "SignWeight", new Vector3(-2.05f, 2.45f, doorZ), 0.6f, 0.2f, Vector3.back, props);
            Plate("Sign_Exit", "SignExit", new Vector3(DoorHalfWidth - 0.012f, ShaftLanding + 2.0f, RoomMax.z + 2.2f), 0.5f, 0.17f, Vector3.left, props);
            var chainNote = Plate("Note_Chain", "NoteScrap", new Vector3(-2.15f, 1.5f, doorZ - 0.004f), 0.22f, 0.28f, Vector3.back, props, 5f);
            chainNote.AddComponent<Game.ReadableNote>().text = ShaftNoteChain;

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
            note.AddComponent<Game.ReadableNote>().text = ShaftNoteDay5;
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
            foreach (var (pos, r) in new[] { (new Vector3(0.9f, 0.002f, 3.3f), 0.45f), (new Vector3(3.0f, 0.002f, 6.1f), 0.35f), (new Vector3(-2.1f, 0.002f, 4.5f), 0.3f) })
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
            Helmet(new Vector3(1.0f, 0.1f, 4.4f), -60f, props);
        }

        static void BuildShaftLights()
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

            // The goal, up at the top: a big caged lamp over the vault door, bright enough to read through the shaft's
            // gloom from the bottom.
            var landingLamp = new GameObject("LandingLamp").transform;
            landingLamp.SetParent(_world, false);
            landingLamp.localPosition = new Vector3(0, ShaftLanding + DoorHeight + 0.55f, RoomMax.z - 0.1f);
            Block("Mount", "DarkMetal", new Vector3(0, 0, 0.04f), new Vector3(0.3f, 0.2f, 0.08f), default, 2f, landingLamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.02f, -0.06f), new Vector3(0.14f, 0.12f, 0.12f), default, 1f, landingLamp);
            foreach (float cx in new[] { -0.09f, 0.09f })
                Block("Cage", "DarkMetal", new Vector3(cx, -0.02f, -0.06f), new Vector3(0.012f, 0.18f, 0.16f), default, 4f, landingLamp);
            var landingHalo = new MeshBuilder();
            landingHalo.Cross(Vector3.zero, 1.6f, 1.6f);
            Place("LandingHalo", landingHalo, "Halo", new Vector3(0, -0.05f, -0.2f), default, landingLamp);
            PointLight("LandingLight", new Vector3(0, ShaftLanding + 2.2f, RoomMax.z - 1.0f), new Color(0.8f, 0.9f, 1f), 9f, 2.4f);
            // On the way up: a dim lamp on the left wall at the first girder, a red one on the right higher up.
            WallLamp(new Vector3(RoomMin.x + 0.2f, 4.9f, 6.3f), Vector3.right, _world);
            PointLight("ShaftLampLight", new Vector3(RoomMin.x + 0.6f, 4.8f, 6.3f), new Color(1f, 0.78f, 0.5f), 4f, 0.8f);
            Block("ShaftRedLamp", "RedLamp", new Vector3(RoomMax.x - 0.06f, 7.3f, 6.2f), new Vector3(0.1f, 0.14f, 0.14f), default, 1f, _world);
            PointLight("ShaftRedLight", new Vector3(RoomMax.x - 0.5f, 7.2f, 6.2f), new Color(1f, 0.12f, 0.08f), 3.5f, 0.8f);

            // Red warning lamp beside the cage, and cold light picking out the cistern and the chute.
            Block("RedLamp", "RedLamp", new Vector3(1.4f, 2.9f, RoomMax.z - 0.06f), new Vector3(0.14f, 0.14f, 0.1f), default, 1f, _world);
            var redHalo = new MeshBuilder();
            redHalo.Cross(Vector3.zero, 0.6f, 0.6f);
            Place("RedHalo", redHalo, "RedHalo", new Vector3(1.4f, 2.6f, RoomMax.z - 0.15f), default);
            PointLight("RedLight", new Vector3(1.4f, 2.75f, RoomMax.z - 0.3f), new Color(1f, 0.12f, 0.08f), 2.4f, 0.7f);
            PointLight("TankFill", new Vector3(-1.2f, 3.0f, 2.6f), new Color(0.45f, 0.6f, 0.85f), 5f, 1.1f);
            PointLight("WeightFill", new Vector3(-0.9f, 2.4f, 4.9f), new Color(0.5f, 0.62f, 0.9f), 4f, 0.7f);
            WallLamp(new Vector3(RoomMin.x + 0.2f, 2.3f, 4.2f), Vector3.right, _world);
            PointLight("WallLampLight", new Vector3(RoomMin.x + 0.5f, 2.2f, 4.2f), new Color(1f, 0.78f, 0.5f), 3f, 0.7f);
        }
    }
}
