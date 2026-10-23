using UnityEngine;

namespace GJ.PSX.EditorTools
{
    // Things that move on their own, so the rooms feel alive while you stand still: the hanging lamp sways, a tired
    // bulb stutters, dust hangs in the light, water drips from a leak, a bare cable sparks, steam hisses from a bad
    // joint, a rat runs along the wall, a radio plays to nobody. Built after AutoColliders: none of it should block.
    public static partial class PSXRoomBuilder
    {
        static void MakeLifeMaterials()
        {
            var add = Shader.Find("PSX/Additive");
            Additive("Ripple", add, MakeTexture("ripple", 32, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.15f);
                return new Color(1f, 1f, 1f, Mathf.Round(a * 4f) / 4f);
            }, true), new Color(0.5f, 0.6f, 0.65f) * 0.5f);
            Lit("RatFur", Shader.Find("PSX/Lit"), MakeTexture("rat_fur", 16, (x, y) =>
                Shade(new Color(0.3f, 0.26f, 0.22f), (Hash(x, y, 140) - 0.5f) * 0.12f + (y % 3 == 0 ? -0.03f : 0f))));
            Lit("RatSkin", Shader.Find("PSX/Lit"), Plain("rat_skin", new Color(0.6f, 0.42f, 0.4f), 0.06f));
        }

        static Transform Find(string path)
        {
            var t = _world.Find(path);
            if (t == null) Debug.LogWarning("Room life: no " + path);
            return t;
        }

        static void SwingLamp(string lamp, string light, float angle, float period)
        {
            var l = Find(lamp);
            if (l == null) return;
            var swing = l.gameObject.AddComponent<Game.Swing>();
            swing.angle = angle;
            swing.period = period;
            var lt = Find(light);
            if (lt != null) lt.SetParent(l, true);
        }

        static void FlickeringBulb(string lamp, string light)
        {
            var l = Find(lamp);
            var lt = Find(light);
            if (l == null || lt == null) return;
            var f = l.gameObject.AddComponent<Game.BulbFlicker>();
            f.bulbLight = lt.GetComponent<Light>();
            f.glowing = new[] { l.Find("Bulb")?.gameObject, l.Find("BulbHalo")?.gameObject };
            f.buzzClip = PSXSoundGenerator.Load("buzz");
        }

        // Dust hanging in the air, catching the light: slow, drifting specks.
        static void Dust(Vector3 center, Vector3 size, int count, Transform parent)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.002f;
            var emission = ps.emission;
            emission.rateOverTime = count / 11f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.3f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats["Mote"];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Steam leaking from a joint, puffing out along dir and rising, with its hiss.
        static void SteamLeak(Vector3 pos, Vector3 dir, Transform parent)
        {
            var go = new GameObject("SteamLeak");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.LookRotation(dir);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.03f;
            var emission = ps.emission;
            emission.rateOverTime = 28f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.01f;
            var growth = ps.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats["Steam"];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var hiss = go.AddComponent<AudioSource>();
            hiss.clip = PSXSoundGenerator.Load("steam");
            hiss.loop = true;
            hiss.playOnAwake = true;
            hiss.volume = 0.35f;
            hiss.spatialBlend = 1f;
            hiss.rolloffMode = AudioRolloffMode.Linear;
            hiss.minDistance = 0.5f;
            hiss.maxDistance = 6f;
        }

        static void Leak(Vector3 top, Transform parent)
        {
            var root = new GameObject("Leak").transform;
            root.SetParent(parent, false);
            root.localPosition = top;
            Block("Stain", "Ash", new Vector3(0, 0.005f, 0), new Vector3(0.12f, 0.008f, 0.12f), default, 4f, root);
            var drop = Block("Drop", "SightWater", new Vector3(0, -0.02f, 0), new Vector3(0.014f, 0.02f, 0.014f), default, 8f, root);
            var ripple = new MeshBuilder { FaceShade = false };
            ripple.FloorCard(Vector3.zero, 1f);
            var rippleGo = Place("Ripple", ripple, "Ripple", new Vector3(0, 0.006f - top.y, 0), default, root);
            var d = root.gameObject.AddComponent<Game.Dripper>();
            d.drop = drop.transform;
            d.ripple = rippleGo.transform;
            d.floorY = 0.005f;
            d.dripClip = PSXSoundGenerator.Load("drip");
        }

        // The bare end of a dangling cable (built by DanglingCable with the same top and length) spitting sparks.
        static void SparkingEnd(Vector3 top, float length, Transform parent)
        {
            var end = top + new Vector3(Mathf.Sin(2.3f) * 0.08f, -length, Mathf.Sin(1.4f) * 0.05f);
            var root = new GameObject("Sparks").transform;
            root.SetParent(parent, false);
            root.localPosition = end;
            var flashMb = new MeshBuilder();
            flashMb.Cross(new Vector3(0, -0.06f, 0), 0.14f, 0.14f);
            var flash = Place("Flash", flashMb, "SparkFlash", Vector3.zero, default, root);
            var light = new GameObject("SparkLight").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = new Color(0.7f, 0.8f, 1f);
            light.range = 3.5f;
            light.shadows = LightShadows.None;
            var bits = new Transform[6];
            for (int i = 0; i < bits.Length; i++)
            {
                var b = new MeshBuilder();
                b.Cross(Vector3.zero, 0.015f, 0.015f);
                bits[i] = Place("Bit", b, "Flame", Vector3.zero, default, root).transform;
            }
            var s = root.gameObject.AddComponent<Game.Sparker>();
            s.flash = flash;
            s.sparkLight = light;
            s.bits = bits;
            s.sparkClip = PSXSoundGenerator.Load("spark");
        }

        // A rat: fur body, pointed head, pink ears and a long tail, and the holes it comes and goes through.
        static void Rat(Vector3[] path, Transform parent)
        {
            var rat = new GameObject("Rat").transform;
            rat.SetParent(parent, false);
            rat.position = path[0];
            var body = new MeshBuilder();
            body.ChamferBox(new Vector3(0, 0.035f, 0), new Vector3(0.07f, 0.06f, 0.15f), 0.022f, 8f);
            body.Tube(new Vector3(0, 0.04f, 0.07f), new Vector3(0, 0.03f, 0.13f), 0.028f, 0.008f, 6, Vector3.up, 8f);
            Place("Body", body, "RatFur", Vector3.zero, default, rat);
            var skin = new MeshBuilder();
            skin.Box(new Vector3(-0.016f, 0.065f, 0.085f), new Vector3(0.014f, 0.016f, 0.004f), 8f);
            skin.Box(new Vector3(0.016f, 0.065f, 0.085f), new Vector3(0.014f, 0.016f, 0.004f), 8f);
            skin.Box(new Vector3(0, 0.03f, 0.135f), new Vector3(0.008f, 0.008f, 0.006f), 8f);
            Place("Ears", skin, "RatSkin", Vector3.zero, default, rat);
            var eyes = new MeshBuilder();
            eyes.Box(new Vector3(-0.014f, 0.048f, 0.105f), new Vector3(0.005f, 0.005f, 0.005f), 8f);
            eyes.Box(new Vector3(0.014f, 0.048f, 0.105f), new Vector3(0.005f, 0.005f, 0.005f), 8f);
            Place("Eyes", eyes, "Rubber", Vector3.zero, default, rat);
            var tail = new GameObject("Tail").transform;
            tail.SetParent(rat, false);
            tail.localPosition = new Vector3(0, 0.02f, -0.07f);
            var tailMb = new MeshBuilder();
            tailMb.Tube(Vector3.zero, new Vector3(0.01f, -0.012f, -0.08f), 0.007f, 0.005f, 5, Vector3.up, 8f);
            tailMb.Tube(new Vector3(0.01f, -0.012f, -0.08f), new Vector3(-0.01f, -0.016f, -0.17f), 0.005f, 0.002f, 5, Vector3.up, 8f);
            Place("Tail", tailMb, "RatSkin", Vector3.zero, default, tail);
            var audio = rat.gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 0.5f;
            audio.maxDistance = 7f;
            audio.playOnAwake = false;
            var run = rat.gameObject.AddComponent<Game.Scurry>();
            run.path = path;
            run.tail = tail;
            run.squeakClip = PSXSoundGenerator.Load("squeak");

            // Holes in the skirting at both ends, facing back along the path.
            foreach (var (hole, next) in new[] { (path[0], path[1]), (path[path.Length - 1], path[path.Length - 2]) })
            {
                var g = new GameObject("RatHole").transform;
                g.SetParent(parent, false);
                g.position = hole;
                g.rotation = Quaternion.LookRotation(new Vector3(next.x - hole.x, 0f, next.z - hole.z));
                Block("Dark", "Rubber", new Vector3(0, 0.045f, -0.02f), new Vector3(0.13f, 0.09f, 0.06f), default, 8f, g);
                Block("Crumbs", "Concrete", new Vector3(0.05f, 0.01f, 0.03f), new Vector3(0.04f, 0.02f, 0.03f), new Vector3(0, 30, 0), 8f, g);
            }
        }

        static void Life1()
        {
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            SwingLamp("CeilingLamp", "CeilingLight", 2.5f, 4.8f);
            FlickeringBulb("DoorLamp", "DoorLight");
            Dust(new Vector3(0f, 1.8f, 3.8f), new Vector3(5f, 2.6f, 6.5f), 70, life);
            Leak(new Vector3(-2.35f, RoomMax.y - 0.01f, 5.9f), life);
            SparkingEnd(new Vector3(0.9f, RoomMax.y, 6.0f), 0.6f, life);
            SteamLeak(new Vector3(2.72f, 1.62f, 7.82f), new Vector3(-1f, 0.2f, -0.3f), life);
            Rat(new[] { new Vector3(-0.45f, 0f, 0.03f), new Vector3(-0.45f, 0f, 0.28f), new Vector3(0.9f, 0f, 0.3f), new Vector3(1.6f, 0f, 0.22f), new Vector3(1.62f, 0f, 0.03f) }, life);
        }

        static void Life2()
        {
            var life = new GameObject("Life").transform;
            life.SetParent(_world, false);
            SwingLamp("Fluorescent", "TubeLight", 1.2f, 6f);
            FlickeringBulb("DoorLamp", "DoorLight");
            Dust(new Vector3(0f, 1.9f, 3.7f), new Vector3(6f, 3f, 7f), 80, life);
            Leak(new Vector3(0.9f, RoomMax.y - 0.01f, 3.3f), life);
            Leak(new Vector3(-0.6f, RoomMax.y - 0.01f, 6.6f), life);
            SparkingEnd(new Vector3(1.9f, RoomMax.y, 5.9f), 0.7f, life);
            SteamLeak(new Vector3(3.2f, 2.6f, 5.85f), new Vector3(-1f, -0.1f, 0f), life);
            Rat(new[] { new Vector3(3.48f, 0f, 3.9f), new Vector3(3.15f, 0f, 3.95f), new Vector3(3.15f, 0f, 5.0f), new Vector3(3.3f, 0f, 5.85f), new Vector3(3.48f, 0f, 5.9f) }, life);
            // The radio on the table is still on, faintly.
            var radio = Find("Dressing/Radio");
            if (radio != null)
            {
                var a = radio.gameObject.AddComponent<AudioSource>();
                a.clip = PSXSoundGenerator.Load("radio_loop");
                a.loop = true;
                a.playOnAwake = true;
                a.volume = 0.45f;
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 0.4f;
                a.maxDistance = 6f;
            }
        }
    }
}
