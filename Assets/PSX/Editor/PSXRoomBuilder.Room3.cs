using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // Room 3, the armory: a revolver in a locker, two sand hoppers hung under the ceiling (one holds the door's bolt,
    // one works a vent shutter), a lamp worth not shooting. Put a round through the right hopper and let the sand run
    // out after you are gone.
    public static partial class PSXRoomBuilder
    {
        const string Scene3Path = "Assets/Scenes/Room03.unity";
        static GameObject _crowbar;
        static Game.HeldGate _crowbarCage;
        // Open floor between the lamp and the right-hand boxes, well over a reach away from the cage.
        static readonly Vector3 CagePlateAt = new Vector3(1.5f, 0f, 4.0f);

        const string NoteCage =
            "The crowbar's in the cage.\n" +
            "The grille only lifts while\n" +
            "something holds the plate down.\n\n" +
            "Nothing in here is heavy enough.\n\n" +
            "Nothing but me.\n\n" +
            "                        - M.";

        const string NoteDay7 =
            "DAY 7\n\n" +
            "The armory. The bolt on the door\n" +
            "is held by a hopper of sand up\n" +
            "by the ceiling. Empty it and the\n" +
            "iron on the other side pulls the\n" +
            "bolt back.\n\n" +
            "Sand runs slower than I breathe.";

        const string NoteHopper =
            "Two hoppers. Only one is on the\n" +
            "bolt's chain - follow the chain.\n" +
            "The other just works the vent.\n\n" +
            "The revolver in the locker has\n" +
            "two rounds. Don't waste them\n" +
            "on the light.\n\n" +
            "                        - M.";

        static void BuildRoom3()
        {
            RoomMin = new Vector3(-3f, 0f, 0f);
            RoomMax = new Vector3(3f, 3.4f, 7.5f);
            var scene = NewRoomScene("Room03");

            BuildShell(new[] { 2.0f, 4.8f });
            BuildDoor();
            if (_door.flash != null) Object.DestroyImmediate(_door.flash.gameObject);
            _door.flash = null;
            _door.blastClip = null;

            var puzzle = BuildArmory();
            BuildRoom3Props();
            // The crowbar: a way to the hopper's gate without spending a round, caged behind a grille that only a weight
            // on the plate across the room holds up.
            puzzle.crowbar = _crowbar;
            var crowbarPoint = new GameObject("CrowbarPoint").transform;
            crowbarPoint.SetParent(_world, false);
            crowbarPoint.localPosition = new Vector3(-1.75f, 1.2f, 0.2f);
            puzzle.crowbarPoint = crowbarPoint;
            _crowbarCage.plate = PressurePlateAt("CagePlate", CagePlateAt, 0f, _world);
            puzzle.crowbarCage = _crowbarCage;
            BuildRoom3Lights();
            AutoColliders(_world);
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            Dust(new Vector3(0f, 1.8f, 3.7f), new Vector3(5.5f, 2.8f, 7f), 70, life);
            FlickeringBulb("DoorLamp", "DoorLight");

            var rig = BuildPlayer(new Vector3(0.4f, 0f, 0.9f), 0f);
            var revolver = BuildRevolver(rig);
            puzzle.revolver = revolver;
            BuildDirector(rig, puzzle, 3, "SECTOR 4 - ARMORY\nyou have three breaths", 3, "Hub");
            UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>().revolver = revolver;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.17f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.04f, 0.04f, 0.05f);
            RenderSettings.fogStartDistance = 3f;
            RenderSettings.fogEndDistance = 14f;
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            EditorSceneManager.SaveScene(scene, Scene3Path);
            Debug.Log("PSX room built: " + Scene3Path);
        }

        // ---------------------------------------------------------------- the revolver

        // A revolver modelled at true size along +Z (barrel forward), grip down; the cylinder and hammer are separate
        // so they can move. Used both in the hand and lying in the locker.
        static (Transform cylinder, Transform hammer, Transform muzzle) RevolverModel(Transform parent)
        {
            var steel = new MeshBuilder();
            steel.Tube(new Vector3(0, 0.046f, 0.022f), new Vector3(0, 0.046f, 0.17f), 0.0085f, 0.0085f, 8, Vector3.up, 8f);
            steel.Box(new Vector3(0, 0.057f, 0.095f), new Vector3(0.008f, 0.006f, 0.15f), 8f);           // rib
            steel.Box(new Vector3(0, 0.064f, 0.162f), new Vector3(0.004f, 0.01f, 0.008f), 8f);           // front sight
            steel.Box(new Vector3(0, 0.038f, 0.06f), new Vector3(0.012f, 0.012f, 0.08f), 8f);            // ejector housing
            steel.Box(new Vector3(0, 0.038f, -0.005f), new Vector3(0.022f, 0.05f, 0.075f), 8f);          // frame
            steel.Box(new Vector3(0, 0.062f, -0.03f), new Vector3(0.018f, 0.012f, 0.03f), 8f);           // top strap
            // Trigger guard: a loop under the frame.
            Vector3 prev = new Vector3(0, 0.012f, 0.012f);
            for (int i = 1; i <= 6; i++)
            {
                float a = i / 6f * Mathf.PI;
                var p = new Vector3(0, 0.012f - Mathf.Sin(a) * 0.018f, 0.012f - (1f - Mathf.Cos(a)) * 0.017f);
                steel.Tube(prev, p, 0.0025f, 0.0025f, 4, Vector3.right, 8f);
                prev = p;
            }
            steel.Box(new Vector3(0, 0.0f, -0.005f), new Vector3(0.004f, 0.02f, 0.004f), 8f);            // trigger
            Place("Frame", steel, "Gunmetal", Vector3.zero, default, parent);
            var grip = new MeshBuilder();
            grip.Limb(new Vector3(0, 0.022f, -0.035f), new Vector3(0, -0.055f, -0.062f), 0.024f, 0.034f, 0.026f, 0.04f, Vector3.forward, 8f);
            Place("Grip", grip, "GripWood", Vector3.zero, default, parent);
            var cyl = new GameObject("Cylinder").transform;
            cyl.SetParent(parent, false);
            cyl.localPosition = new Vector3(0, 0.044f, 0.004f);
            var drum = new MeshBuilder();
            drum.Tube(new Vector3(0, 0, -0.019f), new Vector3(0, 0, 0.019f), 0.019f, 0.019f, 6, Vector3.up, 8f);
            Place("Drum", drum, "Gunmetal", Vector3.zero, default, cyl);
            var rims = new MeshBuilder();
            for (int i = 0; i < 6; i++)
            {
                float a = (i + 0.5f) / 6f * Mathf.PI * 2f;
                rims.Tube(new Vector3(Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.012f, -0.0205f), new Vector3(Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.012f, -0.0185f), 0.004f, 0.004f, 5, Vector3.up, 8f);
            }
            Place("Rims", rims, "Helmet", Vector3.zero, default, cyl);
            var hammer = new GameObject("Hammer").transform;
            hammer.SetParent(parent, false);
            hammer.localPosition = new Vector3(0, 0.06f, -0.042f);
            Block("Spur", "Gunmetal", new Vector3(0, 0.006f, -0.004f), new Vector3(0.006f, 0.016f, 0.01f), new Vector3(-20, 0, 0), 8f, hammer);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(parent, false);
            muzzle.localPosition = new Vector3(0, 0.046f, 0.175f);
            return (cyl, hammer, muzzle);
        }

        // The right arm holding the revolver low in view, with the flash and light at the muzzle, and the Revolver
        // component wired to the lighter hand.
        static Game.Revolver BuildRevolver(PlayerRig rig)
        {
            var root = new GameObject("Revolver").transform;
            root.SetParent(rig.head, false);
            var view = new GameObject("ViewModel").transform;
            view.SetParent(root, false);
            view.localPosition = new Vector3(0.085f, -0.088f, 0.25f);
            view.localRotation = Quaternion.identity;
            var gun = new GameObject("Gun").transform;
            gun.SetParent(view, false);
            gun.localRotation = Quaternion.Euler(2f, -5f, 0f);
            gun.localScale = Vector3.one * 1.2f;
            var (cyl, hammer, muzzle) = RevolverModel(gun);

            // The right arm round the grip (see RevolverGrip); its index finger pulls the trigger.
            var arm = Arm(RevolverGrip, gun);

            var flashMb = new MeshBuilder();
            flashMb.Cross(new Vector3(0, -0.03f, 0), 0.07f, 0.07f);
            var flash = Place("Flash", flashMb, "Flame", Vector3.zero, new Vector3(90, 0, 0), muzzle);
            var light = new GameObject("MuzzleLight").AddComponent<Light>();
            light.transform.SetParent(muzzle, false);
            light.transform.localPosition = new Vector3(0, 0, 0.1f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.45f);
            light.range = 6f;
            light.shadows = LightShadows.None;

            var audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var r = root.gameObject.AddComponent<Game.Revolver>();
            r.viewModel = view;
            r.cylinder = cyl;
            r.hammer = hammer;
            r.muzzle = muzzle;
            r.muzzleFlash = flash;
            r.muzzleLight = light;
            r.lighterHand = rig.animator;
            r.trigger = new[] { arm.fingers[0], arm.fingers[1], arm.fingers[2] };
            r.eye = rig.head.GetComponent<Camera>();
            r.audioSource = audio;
            r.shotClip = PSXSoundGenerator.Load("gunshot");
            r.clickClip = PSXSoundGenerator.Load("dry_click");
            r.drawClip = PSXSoundGenerator.Load("ratchet");
            return r;
        }

        // ---------------------------------------------------------------- the hoppers

        // A riveted steel hopper with a funnel bottom and a stencil, hanging from a bail. Returns its body.
        static Transform Hopper(string name, Vector3 at, Transform parent, string stencil)
        {
            var body = new GameObject(name).transform;
            body.SetParent(parent, false);
            body.localPosition = at;
            Block("Box", "Rust", new Vector3(0, 0, 0), new Vector3(0.56f, 0.5f, 0.56f), default, 1f, body);
            var funnel = new MeshBuilder();
            funnel.Tube(new Vector3(0, -0.25f, 0), new Vector3(0, -0.45f, 0), 0.3f, 0.06f, 4, Vector3.forward, 2f);
            Place("Funnel", funnel, "Rust", Vector3.zero, new Vector3(0, 45, 0), body);
            foreach (float y in new[] { -0.2f, 0.2f }) Block("Band", "DarkMetal", new Vector3(0, y, 0), new Vector3(0.58f, 0.04f, 0.58f), default, 2f, body);
            Plate("Stencil", stencil, new Vector3(0, 0, -0.285f), 0.4f, 0.12f, Vector3.back, body);
            var bail = new MeshBuilder();
            bail.Tube(new Vector3(-0.26f, 0.25f, 0), new Vector3(0, 0.45f, 0), 0.01f, 0.01f, 5, Vector3.forward, 4f);
            bail.Tube(new Vector3(0, 0.45f, 0), new Vector3(0.26f, 0.25f, 0), 0.01f, 0.01f, 5, Vector3.forward, 4f);
            Place("Bail", bail, "Steel", Vector3.zero, default, body);
            return body;
        }

        static Transform SandStream(string name, Vector3 top, float length, Transform parent)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            go.localPosition = top;
            Block("Sand", "Sand", new Vector3(0, -length * 0.5f, 0), new Vector3(0.025f, length, 0.025f), default, 8f, go);
            return go;
        }

        static Transform SandPile(string name, Vector3 at, Transform parent)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            go.localPosition = at;
            var cone = new MeshBuilder();
            cone.Tube(new Vector3(0, 0, 0), new Vector3(0, 0.25f, 0), 0.4f, 0.02f, 9, Vector3.forward, 2f);
            Place("Pile", cone, "Sand", Vector3.zero, default, go);
            return go;
        }

        static Game.HopperPuzzle BuildArmory()
        {
            var works = new GameObject("Armory").transform;
            works.SetParent(_world, false);
            float ceil = RoomMax.y;

            // The bolt hopper by the door's left, on a chain over two pulleys to an iron weight on the wall.
            var boltHopper = new GameObject("BoltHopper").transform;
            boltHopper.SetParent(works, false);
            var boltBody = Hopper("Hopper", new Vector3(-1.5f, 2.35f, 6.3f), boltHopper, "SignSand");
            Pulley(new Vector3(-1.5f, ceil - 0.15f, 6.3f), Vector3.forward, works);
            Pulley(new Vector3(-1.5f, ceil - 0.15f, 7.2f), Vector3.forward, works);
            var span = new MeshBuilder();
            span.Limb(new Vector3(-1.5f, ceil - 0.07f, 6.3f), new Vector3(-1.5f, ceil - 0.07f, 7.2f), 0.025f, 0.025f, 0.025f, 0.025f, Vector3.up, 6f);
            Place("ChainSpan", span, "Chain", Vector3.zero, default, works);
            var hopperChain = HangingChain("HopperChain", new Vector3(-1.5f, ceil - 0.23f, 6.3f - PulleyRadius), 0.5f, boltBody);
            hopperChain.localPosition = new Vector3(0, 0.45f, 0);
            hopperChain.localScale = new Vector3(1f, ceil - 0.23f - 2.8f, 1f);
            hopperChain.localRotation = Quaternion.Euler(180f, 0f, 0f);
            // The chain is lashed to the bail with a few turns of hemp rope: something a flame can get at.
            var rope = new GameObject("Rope").transform;
            rope.SetParent(boltBody, false);
            rope.localPosition = new Vector3(0, 0.5f, 0);
            var ropeMb = new MeshBuilder();
            ropeMb.Tube(new Vector3(0, -0.06f, 0), new Vector3(0, 0.07f, 0), 0.028f, 0.024f, 7, Vector3.forward, 8f);
            Place("Lashing", ropeMb, "Rope", Vector3.zero, default, rope);
            var emberMb = new MeshBuilder();
            emberMb.Cross(Vector3.zero, 0.09f, 0.09f);
            var ember = Place("RopeEmber", emberMb, "FuseGlow", new Vector3(0, 0.46f, 0), default, boltBody);
            var weight = new GameObject("IronWeight").transform;
            weight.SetParent(works, false);
            weight.localPosition = new Vector3(-1.5f, 2.0f, 7.33f);
            Block("Iron", "DarkMetal", Vector3.zero, new Vector3(0.22f, 0.3f, 0.14f), default, 2f, weight);
            Block("Ring", "Steel", new Vector3(0, 0.17f, 0), new Vector3(0.05f, 0.05f, 0.02f), default, 4f, weight);
            var weightChain = HangingChain("WeightChain", new Vector3(0, ceil - 0.23f - 2.0f, -0.13f), ceil - 0.23f - 2.17f, weight);
            weightChain.localPosition = new Vector3(0, ceil - 0.23f - 2.0f, -0.13f + PulleyRadius);
            // From the weight a rod down to a bell crank, and the bolt through the frame into the door's edge.
            Block("Guide", "DarkMetal", new Vector3(-1.5f, 1.4f, 7.42f), new Vector3(0.06f, 1.6f, 0.06f), default, 2f, works);
            Block("Crank", "Steel", new Vector3(-1.32f, 1.25f, 7.4f), new Vector3(0.36f, 0.04f, 0.04f), new Vector3(0, 0, 20), 4f, works);
            var bolt = new GameObject("Bolt").transform;
            bolt.SetParent(works, false);
            bolt.localPosition = new Vector3(-0.95f, 1.2f, 7.3f);
            Block("Bar", "Steel", Vector3.zero, new Vector3(0.45f, 0.06f, 0.06f), default, 4f, bolt);
            Block("Knob", "Chrome", new Vector3(-0.2f, 0.05f, 0), new Vector3(0.03f, 0.06f, 0.03f), default, 4f, bolt);
            Block("Keeper", "DarkMetal", new Vector3(-1.2f, 1.2f, 7.33f), new Vector3(0.08f, 0.14f, 0.12f), default, 4f, works);

            // The vent hopper on the right, its chain to a shutter over a vent in the right wall.
            var ventHopper = new GameObject("VentHopper").transform;
            ventHopper.SetParent(works, false);
            var ventBody = Hopper("Hopper", new Vector3(1.5f, 2.35f, 6.3f), ventHopper, "SignSand");
            Pulley(new Vector3(1.5f, ceil - 0.15f, 6.3f), Vector3.right, works);
            var ventSpan = new MeshBuilder();
            ventSpan.Limb(new Vector3(1.5f, ceil - 0.07f, 6.3f), new Vector3(2.9f, ceil - 0.07f, 6.3f), 0.025f, 0.025f, 0.025f, 0.025f, Vector3.up, 6f);
            Place("ChainSpan", ventSpan, "Chain", Vector3.zero, default, works);
            var ventChain = HangingChain("HopperChain", Vector3.zero, 0.5f, ventBody);
            ventChain.localPosition = new Vector3(0, 0.45f, 0);
            ventChain.localScale = new Vector3(1f, ceil - 0.23f - 2.8f, 1f);
            ventChain.localRotation = Quaternion.Euler(180f, 0f, 0f);
            Block("VentFrame", "DarkMetal", new Vector3(RoomMax.x - 0.03f, 2.75f, 6.3f), new Vector3(0.06f, 0.5f, 0.6f), default, 2f, works);
            Block("VentHole", "Grate", new Vector3(RoomMax.x - 0.065f, 2.75f, 6.3f), new Vector3(0.01f, 0.42f, 0.52f), default, 1f / 0.5f, works);
            var shutter = new GameObject("VentShutter").transform;
            shutter.SetParent(works, false);
            shutter.localPosition = new Vector3(RoomMax.x - 0.08f, 3.0f, 6.3f);
            shutter.localRotation = Quaternion.Euler(0, 90, 0);
            Block("Flap", "Steel", new Vector3(0, -0.24f, 0.01f), new Vector3(0.56f, 0.48f, 0.02f), default, 2f, shutter);

            // Sand streams and the heaps they will make.
            var boltStream = SandStream("BoltStream", boltBody.localPosition + new Vector3(0, -0.47f, 0), 1.85f, works);
            var ventStream = SandStream("VentStream", ventBody.localPosition + new Vector3(0, -0.47f, 0), 1.85f, works);
            var boltPile = SandPile("BoltPile", new Vector3(-1.5f, 0f, 6.3f), works);
            var ventPile = SandPile("VentPile", new Vector3(1.5f, 0f, 6.3f), works);
            AudioSource PourSound(Transform at)
            {
                var a = at.gameObject.AddComponent<AudioSource>();
                a.clip = PSXSoundGenerator.Load("sand_pour");
                a.loop = true;
                a.playOnAwake = false;
                a.volume = 0.6f;
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 0.5f;
                a.maxDistance = 9f;
                return a;
            }

            // The lamp you shouldn't shoot.
            var lamp = new GameObject("CeilingLamp").transform;
            lamp.SetParent(works, false);
            lamp.localPosition = new Vector3(0.3f, ceil, 3.8f);
            Block("Cord", "DarkMetal", new Vector3(0, -0.3f, 0), new Vector3(0.02f, 0.6f, 0.02f), default, 1f, lamp);
            Cyl("Shade", "DarkMetal", new Vector3(0, -0.7f, 0), 0.24f, 0.1f, 8, default, 1f, lamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.76f, 0), new Vector3(0.09f, 0.11f, 0.09f), default, 1f, lamp);
            var lampHalo = new MeshBuilder();
            lampHalo.Cross(Vector3.zero, 0.7f, 0.7f);
            var lampGlow = Place("BulbHalo", lampHalo, "Halo", new Vector3(0, -1.1f, 0), default, lamp);
            var lampLight = PointLight("LampLight", new Vector3(0.3f, ceil - 0.9f, 3.8f), new Color(1f, 0.85f, 0.65f), 9.5f, 3.6f);
            lampLight.transform.SetParent(lamp, true);

            // The locker with the revolver.
            var locker = Group("Locker", new Vector3(RoomMin.x + 0.27f, 0f, 2.7f), 90f, works);
            const float lw = 0.5f, lh = 1.8f, ld = 0.45f;
            Block("Back", "PaintGreen", new Vector3(0, lh * 0.5f, -ld * 0.5f + 0.01f), new Vector3(lw, lh, 0.02f), default, 2f, locker);
            Block("Side_L", "PaintGreen", new Vector3(-lw * 0.5f + 0.01f, lh * 0.5f, 0), new Vector3(0.02f, lh, ld), default, 2f, locker);
            Block("Side_R", "PaintGreen", new Vector3(lw * 0.5f - 0.01f, lh * 0.5f, 0), new Vector3(0.02f, lh, ld), default, 2f, locker);
            Block("Top", "PaintGreen", new Vector3(0, lh - 0.01f, 0), new Vector3(lw, 0.02f, ld), default, 2f, locker);
            Block("Bottom", "PaintGreen", new Vector3(0, 0.05f, 0), new Vector3(lw, 0.1f, ld), default, 2f, locker);
            Block("Shelf", "PaintGreen", new Vector3(0, 1.25f, 0), new Vector3(lw - 0.04f, 0.02f, ld - 0.02f), default, 2f, locker);
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(locker, false);
            hinge.localPosition = new Vector3(-lw * 0.5f, 0, ld * 0.5f);
            Block("Door", "PaintGreen", new Vector3(lw * 0.5f, lh * 0.5f, 0.01f), new Vector3(lw, lh, 0.02f), default, 2f, hinge);
            for (int i = 0; i < 4; i++)
                Block("Slot", "Rubber", new Vector3(lw * 0.5f, lh - 0.2f - i * 0.04f, 0.021f), new Vector3(0.3f, 0.015f, 0.003f), default, 8f, hinge);
            Block("Handle", "Chrome", new Vector3(lw - 0.05f, lh * 0.5f, 0.03f), new Vector3(0.02f, 0.1f, 0.02f), default, 8f, hinge);
            Plate("Label", "SignArmory", new Vector3(lw * 0.5f, lh - 0.08f, 0.022f), 0.3f, 0.07f, Vector3.forward, hinge);
            var shelfGun = Group("Revolver", new Vector3(0.02f, 1.262f, 0.02f), 0f, locker);
            shelfGun.localRotation = Quaternion.Euler(0, 70, 90);
            RevolverModel(shelfGun);
            Model("../Kkryy/Box/Box", new Vector3(-0.12f, 1.262f, -0.05f), 10f, 0.08f, locker, 0f, null, null, null, true);
            var lockerPoint = new GameObject("LockerPoint").transform;
            lockerPoint.SetParent(locker, false);
            lockerPoint.localPosition = new Vector3(0, 1.2f, 0.2f);

            var puzzle = works.gameObject.AddComponent<Game.HopperPuzzle>();
            puzzle.lockerDoor = hinge;
            puzzle.lockerPoint = lockerPoint;
            puzzle.revolverInLocker = shelfGun.gameObject;
            puzzle.boltHopper = boltHopper.gameObject.AddComponent<Game.Shootable>();
            puzzle.boltHopperBody = boltBody;
            puzzle.boltWeight = weight;
            puzzle.bolt = bolt;
            puzzle.ventHopper = ventHopper.gameObject.AddComponent<Game.Shootable>();
            puzzle.ventHopperBody = ventBody;
            puzzle.ventShutter = shutter;
            puzzle.lamp = lamp.gameObject.AddComponent<Game.Shootable>();
            puzzle.lampLight = lampLight;
            puzzle.lampGlow = lampGlow;
            puzzle.boltStream = boltStream.gameObject;
            puzzle.ventStream = ventStream.gameObject;
            puzzle.boltPile = boltPile;
            puzzle.ventPile = ventPile;
            puzzle.boltPour = PourSound(boltStream);
            puzzle.ventPour = PourSound(ventStream);
            puzzle.lockerClip = PSXSoundGenerator.Load("locker");
            puzzle.takeClip = PSXSoundGenerator.Load("rifle_bolt");
            puzzle.hitClip = PSXSoundGenerator.Load("clunk");
            puzzle.glassClip = PSXSoundGenerator.Load("glass_break");
            puzzle.clunkClip = PSXSoundGenerator.Load("door_wheel");
            puzzle.rope = rope;
            puzzle.ropeEmber = ember;
            puzzle.hopperChain = hopperChain;
            puzzle.pryClip = PSXSoundGenerator.Load("squeak");
            puzzle.igniteClip = PSXSoundGenerator.Load("ignite");
            puzzle.snapClip = PSXSoundGenerator.Load("clunk");
            return puzzle;
        }

        // A steel box on the wall with a barred grille in front that slides up in two guides, hauled by a wire.
        static Game.HeldGate CrowbarCage(Vector3 at, Transform parent)
        {
            var cage = Group("CrowbarCage", at, 0f, parent);
            Block("Back", "DarkMetal", new Vector3(0, 0.25f, 0.01f), new Vector3(1.0f, 0.5f, 0.02f), default, 2f, cage);
            Block("Floor", "DarkMetal", new Vector3(0, 0.01f, 0.12f), new Vector3(1.0f, 0.02f, 0.24f), default, 2f, cage);
            Block("Roof", "DarkMetal", new Vector3(0, 0.49f, 0.12f), new Vector3(1.0f, 0.02f, 0.24f), default, 2f, cage);
            foreach (float s in new[] { -1f, 1f })
            {
                Block("Side", "DarkMetal", new Vector3(0.49f * s, 0.25f, 0.12f), new Vector3(0.02f, 0.5f, 0.24f), default, 2f, cage);
                Block("Guide", "Steel", new Vector3(0.515f * s, 0.5f, 0.245f), new Vector3(0.02f, 1.0f, 0.03f), default, 4f, cage);
            }
            var grille = new GameObject("Grille").transform;
            grille.SetParent(cage, false);
            grille.localPosition = new Vector3(0, 0f, 0.245f);
            var bars = new MeshBuilder();
            for (int i = 0; i <= 14; i++) bars.Box(new Vector3(-0.49f + i * 0.07f, 0.25f, 0), new Vector3(0.012f, 0.48f, 0.012f), 8f);
            foreach (float y in new[] { 0.02f, 0.25f, 0.48f }) bars.Box(new Vector3(0, y, 0), new Vector3(1.0f, 0.025f, 0.016f), 8f);
            Place("Bars", bars, "Steel", Vector3.zero, default, grille);
            var gate = cage.gameObject.AddComponent<Game.HeldGate>();
            gate.leaf = grille;
            gate.openOffset = new Vector3(0f, 0.47f, 0f);
            gate.openClip = PSXSoundGenerator.Load("chain");
            gate.slamClip = PSXSoundGenerator.Load("clunk");
            return gate;
        }

        static void BuildRoom3Props()
        {
            var props = new GameObject("Props").transform;
            props.SetParent(_world, false);
            float wallX = RoomMax.x - 0.012f, doorZ = RoomMax.z - 0.012f;

            Plate("Sign_Armory", "SignArmory", new Vector3(-0.6f, 2.6f, 0.012f), 1.2f, 0.3f, Vector3.forward, props);
            Plate("Sign_Exit", "SignExit", new Vector3(DoorHalfWidth - 0.012f, 2.0f, RoomMax.z + 2.2f), 0.5f, 0.17f, Vector3.left, props);
            Plate("Target_A", "Target", new Vector3(1.1f, 1.6f, 0.012f), 0.5f, 0.6f, Vector3.forward, props, 2f);
            Plate("Target_B", "Target", new Vector3(1.75f, 1.55f, 0.012f), 0.5f, 0.6f, Vector3.forward, props, -3f);
            var hopperNote = Plate("Note_Hopper", "NoteScrap", new Vector3(-2.3f, 1.5f, doorZ - 0.004f), 0.22f, 0.28f, Vector3.back, props, 4f);
            hopperNote.AddComponent<Game.ReadableNote>().text = NoteHopper;

            // A table with the diary page, a gas mask, a flashlight, bottles and a floppy disk; a chair.
            var table = Group("Table", new Vector3(-2.2f, 0f, 5.2f), 90f, props);
            Model("Furniture/table", Vector3.zero, 0f, 0f, table, 0f, null, new Vector3(1.1f, 0.78f, 0.7f));
            var note = Plate("Note_Day7", "NoteDiary", new Vector3(0.15f, 0.792f, 0.05f), 0.2f, 0.26f, Vector3.up, table, 8f);
            note.AddComponent<Game.ReadableNote>().text = NoteDay7;
            Model("../Kkryy/GasMask/GasMask", new Vector3(-0.3f, 0.78f, 0.05f), 30f, 0.16f, table, 0f, null, null, null, true);
            Model("../Kkryy/Flashlight/Flashlight", new Vector3(-0.05f, 0.78f, -0.2f), 80f, 0.06f, table, 0f, null, null, null, true);
            Model("../Kkryy/Bottles/Bottles", new Vector3(0.4f, 0.78f, -0.15f), 0f, 0.22f, table, 0f, null, null, null, true);
            Model("../Kkryy/Floppy/Floppy", new Vector3(0.35f, 0.78f, 0.2f), 25f, 0.01f, table, 0f, null, null, null, true);
            Model("Furniture/chair", new Vector3(-1.55f, 0f, 5.0f), -100f, 0f, props, 0f, null, new Vector3(0.42f, 0.92f, 0.42f));

            // An empty rifle rack on the left wall, the crowbar that won't reach, a barrel, bins and bags.
            var rack = Group("GunRack", new Vector3(RoomMin.x + 0.05f, 0f, 3.9f), 90f, props);
            Block("Board", "Wood", new Vector3(0, 1.35f, 0), new Vector3(1.1f, 0.9f, 0.04f), default, 1f, rack);
            Block("Rail_Low", "Wood", new Vector3(0, 0.95f, 0.06f), new Vector3(1.1f, 0.06f, 0.1f), default, 1f, rack);
            Block("Rail_High", "Wood", new Vector3(0, 1.6f, 0.06f), new Vector3(1.1f, 0.06f, 0.1f), default, 1f, rack);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.4f + i * 0.27f;
                Block("Notch", "Rubber", new Vector3(x, 0.985f, 0.1f), new Vector3(0.05f, 0.02f, 0.03f), default, 4f, rack);
                Block("Peg", "DarkMetal", new Vector3(x, 1.63f, 0.11f), new Vector3(0.03f, 0.06f, 0.06f), default, 4f, rack);
            }
            Plate("RackSign", "SignAmmo", new Vector3(0, 1.85f, 0.025f), 0.5f, 0.12f, Vector3.forward, rack);
            _crowbarCage = CrowbarCage(new Vector3(-1.75f, 1.0f, 0f), props);
            _crowbar = Model("../Kkryy/Crowbar/Crowbar", new Vector3(-1.75f, 1.03f, 0.12f), 90f, 0.05f, props, 0f, null, null, null, true);
            _crowbar.name = "Crowbar";
            var cageNote = Plate("Note_Cage", "NoteScrap", new Vector3(-0.98f, 1.3f, 0.012f), 0.22f, 0.28f, Vector3.forward, props, -5f);
            cageNote.AddComponent<Game.ReadableNote>().text = NoteCage;
            // Across the room the plate that lifts the grille (see CagePlateAt), its wire over the floor, up the wall,
            // under the beams along the ceiling and down to the grille.
            Plate("Sign_Hold", "SignHold", new Vector3(wallX, 1.25f, 3.7f), 0.5f, 0.25f, Vector3.left, props);
            PullWire("CageWire", new[]
            {
                CagePlateAt + new Vector3(0.44f, 0.015f, 0.2f), new Vector3(2.95f, 0.015f, CagePlateAt.z + 0.2f),
                new Vector3(2.95f, 3.1f, CagePlateAt.z + 0.2f), new Vector3(2.8f, 3.1f, CagePlateAt.z + 0.2f),
                new Vector3(2.8f, 3.1f, 0.1f), new Vector3(-1.75f, 3.1f, 0.1f), new Vector3(-1.75f, 3.1f, 0.245f),
                new Vector3(-1.75f, 1.5f, 0.245f)
            }, props);
            Model("../Kkryy/Barrel/Barrel", new Vector3(2.45f, 0f, 6.85f), 20f, 0.85f, props, 0f, null, null, null, true);
            Model("../Kkryy/Barrel/Barrel", new Vector3(2.5f, 0f, 1.2f), 70f, 0.85f, props, 0f, null, null, null, true);
            Model("../Kkryy/TrashCan/TrashCan", new Vector3(1.45f, 0f, 7.0f), 0f, 0.6f, props, 0f, null, null, null, true);
            Model("../Kkryy/GarbageBag/GarbageBag", new Vector3(1.95f, 0f, 7.05f), 40f, 0.45f, props, 0f, null, null, null, true);
            Model("../Kkryy/GarbageBag/GarbageBag", new Vector3(-2.5f, 0f, 0.5f), -20f, 0.4f, props, 0f, null, null, null, true);
            Model("../Kkryy/Box/Box", new Vector3(2.45f, 0f, 3.0f), 10f, 0.5f, props, 0f, null, null, null, true);
            Model("../Kkryy/Box/Box", new Vector3(2.4f, 0.5f, 3.05f), -15f, 0.4f, props, 0f, null, null, null, true);
            Model("../Kkryy/Cardboard/Cardboard", new Vector3(2.45f, 0f, 3.8f), 5f, 0.35f, props, 0f, null, null, null, true);
            Model("Survival/box-large", new Vector3(2.4f, 0f, 5.3f), 0f, 0f, props, 0f, null, new Vector3(0.5f, 0.45f, 0.9f));
            Model("Survival/box", new Vector3(2.4f, 0.45f, 5.3f), 20f, 0f, props, 0f, null, Vector3.one * 0.4f);
            for (int i = 0; i < 3; i++) Sack(new Vector3(-0.9f + i * 0.5f, 0f, 0.35f), 5f * i, props);
            Sack(new Vector3(-0.65f, 0.21f, 0.37f), -4f, props, 3f);
            Helmet(new Vector3(0.9f, 0.1f, 1.4f), 120f, props);
            Clock(new Vector3(2.2f, 2.5f, RoomMax.z), 180f, props);
            PipeRun("RightMain", "Rust", new[] { new Vector3(2.75f, 3.0f, -0.05f), new Vector3(2.75f, 3.0f, 5.7f), new Vector3(2.75f, 3.45f, 5.7f) }, 0.07f, props);
        }

        static void BuildRoom3Lights()
        {
            var doorLamp = new GameObject("DoorLamp").transform;
            doorLamp.SetParent(_world, false);
            doorLamp.localPosition = new Vector3(0, RoomMax.y, RoomMax.z - 0.9f);
            Block("Cord", "DarkMetal", new Vector3(0, -0.15f, 0), new Vector3(0.02f, 0.3f, 0.02f), default, 1f, doorLamp);
            Cyl("Shade", "DarkMetal", new Vector3(0, -0.4f, 0), 0.18f, 0.08f, 8, default, 1f, doorLamp);
            Block("Bulb", "Bulb", new Vector3(0, -0.45f, 0), new Vector3(0.07f, 0.08f, 0.07f), default, 1f, doorLamp);
            var halo = new MeshBuilder();
            halo.Cross(Vector3.zero, 0.5f, 0.5f);
            Place("BulbHalo", halo, "Halo", new Vector3(0, -0.7f, 0), default, doorLamp);
            PointLight("DoorLight", new Vector3(0, RoomMax.y - 0.7f, RoomMax.z - 1.1f), new Color(1f, 0.85f, 0.62f), 4.5f, 1.8f);
            Block("RedLamp", "RedLamp", new Vector3(1.4f, 2.9f, RoomMax.z - 0.06f), new Vector3(0.14f, 0.14f, 0.1f), default, 1f, _world);
            PointLight("RedLight", new Vector3(1.4f, 2.75f, RoomMax.z - 0.3f), new Color(1f, 0.12f, 0.08f), 2.4f, 0.7f);
            PointLight("Fill", new Vector3(-1.5f, 2.6f, 2.2f), new Color(0.45f, 0.55f, 0.85f), 6f, 1.0f);
            // Work lights clamped to the beam by the door, so both hoppers can be seen and aimed at in the dark.
            PointLight("HopperLight_L", new Vector3(-1.5f, 1.6f, 5.4f), new Color(1f, 0.8f, 0.55f), 3.2f, 1.4f);
            PointLight("HopperLight_R", new Vector3(1.5f, 1.6f, 5.4f), new Color(1f, 0.8f, 0.55f), 3.2f, 1.4f);
        }
    }
}
