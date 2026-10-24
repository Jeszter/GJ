using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GJ.PSX.EditorTools
{
    // Synthesises the room's sound effects into plain WAV files (22 kHz mono, a little lo-fi like the picture),
    // so they can be listened to, tweaked here, or simply replaced by recorded ones with the same names.
    // Everything is kept soft: no clip peaks above ~0.7.
    public static class PSXSoundGenerator
    {
        public const string Folder = "Assets/Game/Audio";
        const int Rate = 22050;

        [MenuItem("GJ/Generate Sounds")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Save("fuse_hiss", FuseHiss());
            Save("ignite", Ignite());
            Save("fizzle", Fizzle());
            Save("heartbeat", Heartbeat());
            Save("explosion", Explosion());
            Save("door_wheel", DoorWheel());
            Save("door_open", DoorOpen());
            Save("bunker_hum", BunkerHum());
            Save("zippo_lid", ZippoLid());
            Save("zippo_strike", ZippoStrike());
            Save("ui_move", UiTick(1800f, 0.04f, 0.25f));
            Save("ui_pick", UiTick(900f, 0.12f, 0.35f));
            Save("type_tick", TypeTick());
            Save("paper", Paper());
            Save("zippo_close", ZippoClose());
            Save("tv_static", TvStatic());
            Save("water_flow", WaterFlow());
            Save("valve", Valve());
            Save("chain", Chain());
            Save("clunk", Clunk());
            Save("music_title", MusicTitle());
            Save("music_room", MusicRoom());
            Save("footstep", Footstep());
            Save("drip", Drip());
            Save("rumble", Rumble());
            Save("gasp", Gasp());
            Save("clock_tick", ClockTick());
            Save("spark", Spark());
            Save("squeak", Squeak());
            Save("radio_loop", RadioLoop());
            Save("steam", Steam());
            Save("buzz", Buzz());
            Save("gunshot", Gunshot());
            Save("dry_click", DryClick());
            Save("locker", Locker());
            Save("rifle_bolt", RifleBolt());
            Save("glass_break", GlassBreak());
            Save("ratchet", Ratchet());
            Save("sand_pour", SandPour());
            AssetDatabase.Refresh();

            // Unity normalises imported clips by default, which would undo the soft levels set above.
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path);
                var so = new SerializedObject(importer);
                var normalize = so.FindProperty("m_Normalize");
                if (normalize == null || !normalize.boolValue) continue;
                normalize.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }
        }

        public static AudioClip Load(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Folder}/{name}.wav");

        // ---------------------------------------------------------------- building blocks

        static System.Random _rng = new System.Random(7);
        static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);
        static float Coef(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        // Two-pole resonator: rings at freq, q sets how long.
        struct Resonator
        {
            float _a1, _a2, _g, _y1, _y2;
            public Resonator(float freq, float r)
            {
                _a1 = 2f * r * Mathf.Cos(2f * Mathf.PI * freq / Rate);
                _a2 = -r * r;
                _g = 1f - r;
                _y1 = _y2 = 0f;
            }
            public float Tick(float x)
            {
                float y = _a1 * _y1 + _a2 * _y2 + _g * x;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }

        // Crossfades the tail over the head so the clip loops without a click.
        static float[] MakeLoop(float[] src, float fadeSeconds)
        {
            int fade = Mathf.RoundToInt(fadeSeconds * Rate);
            var dst = new float[src.Length - fade];
            Array.Copy(src, dst, dst.Length);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                dst[i] = src[i] * k + src[dst.Length + i] * (1f - k);
            }
            return dst;
        }

        static void Save(string name, float[] samples)
        {
            string path = $"{Folder}/{name}.wav";
            using var w = new BinaryWriter(File.Create(path));
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }

        // ---------------------------------------------------------------- the sounds

        // Burning powder: bright airy hiss that wavers, peppered with little pops. Loops.
        static float[] FuseHiss()
        {
            _rng = new System.Random(11);
            var s = Buffer(2.15f);
            float lp = 0f, hp = 0f, flutter = 0.85f, pop = 0f;
            float aLp = Coef(6500f), aHp = Coef(1400f), aFl = Coef(6f);
            for (int i = 0; i < s.Length; i++)
            {
                float n = Noise();
                lp += aLp * (n - lp);
                hp += aHp * (lp - hp);
                flutter += aFl * ((0.7f + 0.3f * Mathf.Abs(Noise())) - flutter);
                if (_rng.NextDouble() < 28.0 / Rate) pop = 0.35f + 0.25f * (float)_rng.NextDouble();
                pop *= 0.992f;
                s[i] = (lp - hp) * 0.32f * flutter + Noise() * pop * 0.5f;
            }
            return MakeLoop(s, 0.15f);
        }

        // Flint click then a soft whoomph as the powder catches.
        static float[] Ignite()
        {
            _rng = new System.Random(12);
            var s = Buffer(0.7f);
            float lp = 0f;
            var ring = new Resonator(3200f, 0.985f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float click = t < 0.006f ? Noise() * (1f - t / 0.006f) : 0f;
                float whoosh = t > 0.05f ? Mathf.Sin(Mathf.Clamp01((t - 0.05f) / 0.55f) * Mathf.PI) : 0f;
                lp += Coef(400f + 2600f * whoosh) * (Noise() - lp);
                s[i] = ring.Tick(click) * 6f * 0.25f + click * 0.25f + lp * whoosh * 0.45f;
            }
            return s;
        }

        // Steam hiss when water kills the flame.
        static float[] Fizzle()
        {
            _rng = new System.Random(13);
            var s = Buffer(1.1f);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t / 0.32f);
                float n = Noise();
                lp += Coef(7000f) * (n - lp);
                hp += Coef(2200f) * (lp - hp);
                s[i] = (lp - hp) * env * 0.5f;
            }
            return s;
        }

        // One "lub-dub": two low thumps with falling pitch.
        static float[] Heartbeat()
        {
            var s = Buffer(0.75f);
            void Thump(float start, float gain)
            {
                double phase = 0;
                for (int i = Mathf.RoundToInt(start * Rate); i < s.Length; i++)
                {
                    float t = (float)i / Rate - start;
                    float f = Mathf.Lerp(72f, 42f, Mathf.Clamp01(t / 0.12f));
                    phase += 2.0 * Math.PI * f / Rate;
                    float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-t / 0.07f);
                    s[i] += (float)Math.Sin(phase) * env * gain;
                }
            }
            Thump(0f, 0.62f);
            Thump(0.2f, 0.45f);
            return s;
        }

        // Blast: a sharp crack, a dark roar that closes down, a long rumble and a few falling bits.
        static float[] Explosion()
        {
            _rng = new System.Random(14);
            var s = Buffer(3.2f);
            float lp = 0f, lp2 = 0f;
            double rumblePhase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise();
                float cutoff = Mathf.Lerp(2800f, 160f, Mathf.Clamp01(t / 0.9f));
                lp += Coef(cutoff) * (n - lp);
                lp2 += Coef(cutoff * 0.7f) * (lp - lp2);
                float roar = lp2 * Mathf.Exp(-t / 0.55f) * 1.6f;
                float crack = t < 0.03f ? n * (1f - t / 0.03f) * 0.5f : 0f;
                rumblePhase += 2.0 * Math.PI * (36f + 4f * Mathf.Sin(t * 3f)) / Rate;
                float rumble = (float)Math.Sin(rumblePhase) * Mathf.Exp(-t / 1.1f) * Mathf.Min(1f, t / 0.05f) * 0.35f;
                s[i] = Mathf.Clamp(roar + crack + rumble, -0.7f, 0.7f);
            }
            // Debris ticking down afterwards.
            for (int k = 0; k < 9; k++)
            {
                int at = Mathf.RoundToInt((0.35f + (float)_rng.NextDouble() * 1.6f) * Rate);
                var r = new Resonator(1200f + (float)_rng.NextDouble() * 1800f, 0.99f);
                for (int i = 0; i < 900 && at + i < s.Length; i++) s[at + i] += r.Tick(i < 20 ? Noise() : 0f) * 1.2f * 0.12f;
            }
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.7f, 0.7f);
            return s;
        }

        // Locking wheel turning: a run of metallic ratchet clicks.
        static float[] DoorWheel()
        {
            _rng = new System.Random(15);
            var s = Buffer(1.3f);
            var r1 = new Resonator(820f, 0.996f);
            var r2 = new Resonator(1310f, 0.995f);
            var r3 = new Resonator(2150f, 0.993f);
            float next = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float x = 0f;
                if (t >= next && t < 1.15f)
                {
                    x = 1f;
                    next = t + 0.055f + (float)_rng.NextDouble() * 0.03f;
                }
                float click = x + (t % 0.07f < 0.002f ? Noise() * 0.3f : 0f);
                // The resonators pass only (1 - r) of the impulse, hence the large make-up gain.
                s[i] = (r1.Tick(click) + r2.Tick(click) * 0.7f + r3.Tick(click) * 0.5f) * 16f;
            }
            return s;
        }

        // Heavy leaf swinging: a low metal groan with grinding friction, ending in a thud against the stop.
        static float[] DoorOpen()
        {
            _rng = new System.Random(16);
            var s = Buffer(2.8f);
            double phase = 0;
            float lp = 0f, fr = 0f, fr2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float swing = Mathf.Sin(Mathf.Clamp01(t / 2.4f) * Mathf.PI);
                float f = 58f + 9f * Mathf.Sin(t * 2.1f) + 4f * Mathf.Sin(t * 7.3f);
                phase += 2.0 * Math.PI * f / Rate;
                float saw = (float)(phase / Math.PI % 2.0) - 1f;
                lp += Coef(520f) * (saw - lp);
                float n = Noise();
                fr += Coef(380f) * (n - fr);
                fr2 += Coef(160f) * (fr - fr2);
                float groan = lp * swing * 0.32f;
                float grind = (fr - fr2) * swing * 0.6f;
                float thudT = t - 2.35f;
                float thud = thudT > 0f ? Mathf.Sin(thudT * 2f * Mathf.PI * 48f) * Mathf.Exp(-thudT / 0.18f) * 0.55f + n * Mathf.Exp(-thudT / 0.03f) * 0.15f : 0f;
                s[i] = groan + grind + thud;
            }
            return s;
        }

        // The Zippo's famous clink: a bright metallic ping as the lid snaps open.
        static float[] ZippoLid()
        {
            _rng = new System.Random(18);
            var s = Buffer(0.45f);
            var r1 = new Resonator(2900f, 0.9985f);
            var r2 = new Resonator(4400f, 0.998f);
            var r3 = new Resonator(6100f, 0.997f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float hit = t < 0.002f ? Noise() : 0f;
                float bounce = t > 0.045f && t < 0.047f ? Noise() * 0.4f : 0f;
                s[i] = (r1.Tick(hit + bounce) + r2.Tick(hit + bounce) * 0.6f + r3.Tick(hit) * 0.4f) * 70f * 0.5f + hit * 0.15f;
            }
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.6f, 0.6f);
            return s;
        }

        // Thumb rolling the flint wheel: a short gritty scratch.
        static float[] ZippoStrike()
        {
            _rng = new System.Random(19);
            var s = Buffer(0.3f);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t / 0.06f);
                float grit = _rng.NextDouble() < 0.08 ? Noise() * 2f : Noise() * 0.4f;
                lp += Coef(7500f) * (grit - lp);
                hp += Coef(1800f) * (lp - hp);
                s[i] = (lp - hp) * env * 0.6f;
            }
            return s;
        }

        // Soft menu blip: a short decaying sine.
        static float[] UiTick(float freq, float seconds, float gain)
        {
            var s = Buffer(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t / (seconds * 0.3f)) * Mathf.Min(1f, t / 0.002f) * gain;
            }
            return s;
        }

        // Typewriter key for the story text: a dull wooden tick.
        static float[] TypeTick()
        {
            _rng = new System.Random(20);
            var s = Buffer(0.06f);
            var r = new Resonator(1400f, 0.99f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float x = t < 0.003f ? Noise() : 0f;
                s[i] = r.Tick(x) * 25f * 0.3f + x * 0.1f;
            }
            return s;
        }

        // Paper rustle when picking up or putting down a note.
        static float[] Paper()
        {
            _rng = new System.Random(21);
            var s = Buffer(0.5f);
            float lp = 0f, hp = 0f, crinkle = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
                if (_rng.NextDouble() < 120.0 / Rate) crinkle = 1f;
                crinkle *= 0.995f;
                lp += Coef(6000f) * (Noise() - lp);
                hp += Coef(900f) * (lp - hp);
                s[i] = (lp - hp) * env * (0.15f + crinkle * 0.35f);
            }
            return s;
        }

        // Shutting the Zippo: a duller, shorter clack than the opening ping, the lid landing on the case.
        static float[] ZippoClose()
        {
            _rng = new System.Random(22);
            var s = Buffer(0.25f);
            var r1 = new Resonator(1700f, 0.996f);
            var r2 = new Resonator(3100f, 0.994f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float hit = t < 0.003f ? Noise() : 0f;
                lp += Coef(2500f) * (hit - lp);
                s[i] = (r1.Tick(hit) + r2.Tick(hit) * 0.5f) * 40f * 0.5f + lp * 0.6f;
            }
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.55f, 0.55f);
            return s;
        }

        // TV snow: bright hiss with a faint mains buzz under it, as the tube warms up or the channel changes.
        static float[] TvStatic()
        {
            _rng = new System.Random(23);
            var s = Buffer(0.6f);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Clamp01((0.6f - t) / 0.25f);
                float n = Noise();
                lp += Coef(9000f) * (n - lp);
                hp += Coef(900f) * (lp - hp);
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 50f * t)) * 0.04f;
                s[i] = ((lp - hp) * 0.35f + buzz) * env;
            }
            return s;
        }

        // Water pouring from a spout into a trough: a band of noise that gurgles. Loops.
        static float[] WaterFlow()
        {
            _rng = new System.Random(24);
            var s = Buffer(3.2f);
            float lp = 0f, hp = 0f, gurgle = 0f;
            var bubble = new Resonator(600f, 0.995f);
            for (int i = 0; i < s.Length; i++)
            {
                float n = Noise();
                lp += Coef(3200f) * (n - lp);
                hp += Coef(500f) * (lp - hp);
                gurgle += Coef(3f) * ((0.6f + 0.4f * Mathf.Abs(Noise())) - gurgle);
                float pop = _rng.NextDouble() < 18.0 / Rate ? 1f : 0f;
                s[i] = (lp - hp) * 0.4f * gurgle + bubble.Tick(pop) * 6f * 0.12f;
            }
            return MakeLoop(s, 0.25f);
        }

        // An old valve wheel turning: a rusty squeal that wavers in pitch over a grinding scrape.
        static float[] Valve()
        {
            _rng = new System.Random(25);
            var s = Buffer(0.95f);
            double phase = 0;
            float lp = 0f, fr = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI);
                float f = 620f + 140f * Mathf.Sin(t * 9f) + 60f * Mathf.Sin(t * 23f);
                phase += 2.0 * Math.PI * f / Rate;
                float squeal = (float)Math.Sin(phase) * (0.5f + 0.5f * Mathf.Sin(t * 31f));
                lp += Coef(1400f) * (squeal - lp);
                fr += Coef(300f) * (Noise() - fr);
                s[i] = (lp * 0.22f + fr * 0.5f) * env;
            }
            return s;
        }

        // A chain dropping onto its hook: a cluster of small metal clinks.
        static float[] Chain()
        {
            _rng = new System.Random(26);
            var s = Buffer(0.7f);
            for (int k = 0; k < 11; k++)
            {
                int at = Mathf.RoundToInt((float)_rng.NextDouble() * 0.45f * Rate);
                var r = new Resonator(1800f + (float)_rng.NextDouble() * 2600f, 0.993f);
                float gain = 0.25f + (float)_rng.NextDouble() * 0.25f;
                for (int i = 0; i < 2500 && at + i < s.Length; i++) s[at + i] += r.Tick(i < 12 ? Noise() : 0f) * 18f * gain * 0.12f;
            }
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.6f, 0.6f);
            return s;
        }

        // A heavy metal part knocking into place: a low thud with a short ring.
        static float[] Clunk()
        {
            _rng = new System.Random(27);
            var s = Buffer(0.6f);
            var r = new Resonator(310f, 0.997f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float hit = t < 0.006f ? Noise() : 0f;
                lp += Coef(700f) * (hit * 2f - lp);
                s[i] = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t / 0.08f) * 0.4f + r.Tick(hit) * 60f * 0.06f + lp * 0.3f;
            }
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.65f, 0.65f);
            return s;
        }

        // ---------------------------------------------------------------- music

        // Renders "seconds" of music plus a tail, folds the tail back over the start so it loops seamlessly (the
        // last chord's release and the echoes ring on into the first bar), then scales it to a soft peak.
        static float[] Looped(float seconds, float tail, float peak, Action<float[]> render)
        {
            var s = Buffer(seconds + tail);
            render(s);
            int len = Mathf.RoundToInt(seconds * Rate);
            var dst = new float[len];
            for (int i = 0; i < s.Length; i++) dst[i % len] += s[i];
            float max = 0f;
            foreach (float v in dst) max = Mathf.Max(max, Mathf.Abs(v));
            if (max > 0f) for (int i = 0; i < len; i++) dst[i] *= peak / max;
            return dst;
        }

        // Two feedback echoes, a cheap stand-in for a big concrete room.
        static void Echo(float[] s, float wet)
        {
            int a = Mathf.RoundToInt(0.37f * Rate), b = Mathf.RoundToInt(0.53f * Rate);
            for (int i = 0; i < s.Length; i++)
            {
                if (i >= a) s[i] += s[i - a] * 0.38f * wet;
                if (i >= b) s[i] += s[i - b] * 0.27f * wet;
            }
        }

        static float Note(int semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

        // Title and story theme: a low drone, slow pad chords (Am - F - C - E) and a music-box melody that never quite
        // resolves, all washed in echo. 24 s, loops.
        static float[] MusicTitle()
        {
            _rng = new System.Random(30);
            return Looped(24f, 4f, 0.55f, s =>
            {
                float[][] chords =
                {
                    new[] { Note(-24), Note(-21), Note(-17) },   // A2 C3 E3
                    new[] { Note(-28), Note(-24), Note(-21) },   // F2 A2 C3
                    new[] { Note(-21), Note(-17), Note(-14) },   // C3 E3 G3
                    new[] { Note(-29), Note(-25), Note(-22) }    // E2 G#2 B2
                };
                // Pad: each chord swells in over 1.5 s and lets go over 2 s, overlapping the next.
                for (int c = 0; c < chords.Length; c++)
                {
                    int start = Mathf.RoundToInt(c * 6f * Rate), end = Mathf.Min(s.Length, Mathf.RoundToInt((c * 6f + 8f) * Rate));
                    foreach (float f in chords[c])
                    {
                        double p1 = _rng.NextDouble() * 6.28, p2 = _rng.NextDouble() * 6.28;
                        for (int i = start; i < end; i++)
                        {
                            float t = (float)(i - start) / Rate;
                            float env = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((8f - t) / 2f);
                            p1 += 2.0 * Math.PI * f / Rate;
                            p2 += 2.0 * Math.PI * f * 1.004 / Rate;
                            float v = (float)(Math.Sin(p1) + Math.Sin(p2) + 0.3 * Math.Sin(p1 * 2.0) + 0.12 * Math.Sin(p2 * 3.0));
                            s[i] += v * env * 0.06f;
                        }
                    }
                }
                // Drone on A1 with a slow swell.
                for (int i = 0; i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    s[i] += (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.12f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.04f)
                            * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / 12f));
                }
                // Music box: A minor pentatonic up high, a phrase per chord.
                int[] melody = { 0, 3, 7, 5, -2, 0, 3, -5, 7, 10, 7, 3, 2, 0, -1, -5 };
                float[] when = { 0.5f, 1.6f, 2.7f, 4.2f, 6.5f, 7.6f, 8.7f, 10.4f, 12.5f, 13.6f, 14.7f, 16.2f, 18.5f, 19.6f, 20.7f, 22.2f };
                for (int k = 0; k < melody.Length; k++)
                {
                    float f = Note(melody[k] + 12);
                    int at = Mathf.RoundToInt(when[k] * Rate);
                    for (int i = 0; i < Mathf.RoundToInt(2.5f * Rate) && at + i < s.Length; i++)
                    {
                        float t = (float)i / Rate;
                        float bell = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / 0.9f)
                                   + Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t / 0.3f) * 0.35f
                                   + Mathf.Sin(2f * Mathf.PI * f * 5.4f * t) * Mathf.Exp(-t / 0.1f) * 0.15f;
                        s[at + i] += bell * Mathf.Min(1f, t / 0.003f) * 0.09f;
                    }
                }
                Echo(s, 1f);
            });
        }

        // In-room bed: a low beating drone, wind in the ventilation, the odd distant metal groan. 20 s, loops; it
        // sits quietly under the room tone.
        static float[] MusicRoom()
        {
            _rng = new System.Random(31);
            return Looped(20f, 3f, 0.45f, s =>
            {
                float air = 0f, air2 = 0f;
                for (int i = 0; i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    float drone = Mathf.Sin(2f * Mathf.PI * 41.2f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 41.45f * t) * 0.25f
                                + Mathf.Sin(2f * Mathf.PI * 61.7f * t) * 0.1f + Mathf.Sin(2f * Mathf.PI * 98f * t) * 0.03f * (0.5f + 0.5f * Mathf.Sin(t * 0.7f));
                    air += Coef(500f) * (Noise() - air);
                    air2 += Coef(90f) * (air - air2);
                    float wind = (air - air2) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 10f)) * 0.6f;
                    s[i] += drone + wind;
                }
                // Distant groans of the structure.
                foreach (float at in new[] { 6.5f, 15.2f })
                {
                    double phase = 0;
                    int start = Mathf.RoundToInt(at * Rate);
                    var ring = new Resonator(310f, 0.998f);
                    for (int i = 0; i < Mathf.RoundToInt(2.5f * Rate) && start + i < s.Length; i++)
                    {
                        float t = (float)i / Rate;
                        float f = Mathf.Lerp(130f, 92f, t / 2.5f);
                        phase += 2.0 * Math.PI * f / Rate;
                        float env = Mathf.Sin(Mathf.Clamp01(t / 2.5f) * Mathf.PI);
                        float saw = (float)(phase / Math.PI % 2.0) - 1f;
                        s[start + i] += (saw * 0.12f + ring.Tick(Noise() * 0.3f) * 2f) * env * 0.5f;
                    }
                }
                Echo(s, 0.7f);
            });
        }

        // ---------------------------------------------------------------- more room sounds

        // A boot on gritty concrete: a dull thud and a scuff. Played with a random pitch per step.
        static float[] Footstep()
        {
            _rng = new System.Random(32);
            var s = Buffer(0.2f);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float thud = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 60f, t / 0.05f) * t) * Mathf.Exp(-t / 0.025f) * 0.5f;
                lp += Coef(2600f) * (Noise() - lp);
                hp += Coef(380f) * (lp - hp);
                float grit = (lp - hp) * Mathf.Exp(-Mathf.Max(0f, t - 0.008f) / 0.045f) * Mathf.Min(1f, t / 0.004f) * 0.55f;
                s[i] = thud + grit;
            }
            return s;
        }

        // A single drop landing in a puddle: a rising "plip" and a tiny echo.
        static float[] Drip()
        {
            var s = Buffer(0.5f);
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 900f + 1700f * Mathf.Clamp01(t / 0.04f);
                phase += 2.0 * Math.PI * f / Rate;
                s[i] = (float)Math.Sin(phase) * Mathf.Exp(-t / 0.035f) * Mathf.Min(1f, t / 0.002f) * 0.45f;
            }
            int d = Mathf.RoundToInt(0.12f * Rate);
            for (int i = s.Length - 1; i >= d; i--) s[i] += s[i - d] * 0.3f;
            return s;
        }

        // The mountain settling: a long low rumble, a creak and a little falling grit.
        static float[] Rumble()
        {
            _rng = new System.Random(33);
            var s = Buffer(3.6f);
            float lp = 0f, lp2 = 0f;
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 3.4f) * Mathf.PI);
                lp += Coef(140f) * (Noise() - lp);
                lp2 += Coef(90f) * (lp - lp2);
                phase += 2.0 * Math.PI * Mathf.Lerp(190f, 150f, t / 3.6f) / Rate;
                float creak = (float)Math.Sin(phase) * Mathf.Clamp01(Mathf.Sin(t * 5f)) * Mathf.Exp(-Mathf.Abs(t - 1.6f)) * 0.08f;
                s[i] = lp2 * env * 3.5f + creak;
            }
            for (int k = 0; k < 14; k++)
            {
                int at = Mathf.RoundToInt((1.2f + (float)_rng.NextDouble() * 2f) * Rate);
                var r = new Resonator(2000f + (float)_rng.NextDouble() * 3000f, 0.985f);
                for (int i = 0; i < 600 && at + i < s.Length; i++) s[at + i] += r.Tick(i < 6 ? Noise() : 0f) * 0.5f;
            }
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.55f, 0.55f);
            return s;
        }

        // Coming back: one sharp, desperate breath in.
        static float[] Gasp()
        {
            _rng = new System.Random(34);
            var s = Buffer(1.0f);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((0.75f - t) / 0.35f);
                float cutoff = Mathf.Lerp(900f, 3800f, Mathf.Clamp01(t / 0.5f));
                lp += Coef(cutoff) * (Noise() - lp);
                hp += Coef(500f) * (lp - hp);
                float rasp = 1f + 0.4f * Mathf.Sin(2f * Mathf.PI * 95f * t);
                s[i] = (lp - hp) * env * rasp * 0.55f;
            }
            return s;
        }

        // ---------------------------------------------------------------- the room's small life

        // Clock escapement: a dry little tick.
        static float[] ClockTick()
        {
            _rng = new System.Random(40);
            var s = Buffer(0.08f);
            var r = new Resonator(3300f, 0.985f);
            for (int i = 0; i < s.Length; i++)
            {
                float x = i < 25 ? Noise() : 0f;
                s[i] = r.Tick(x) * 12f * 0.25f + x * 0.05f;
            }
            return s;
        }

        // Electric arc off a bare wire: crackling bursts with a buzz under them.
        static float[] Spark()
        {
            _rng = new System.Random(41);
            var s = Buffer(0.7f);
            float hp = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t / 0.25f);
                float crack = _rng.NextDouble() < 0.02 ? Noise() * 3f : Noise() * 0.3f;
                lp += Coef(8000f) * (crack - lp);
                hp += Coef(1500f) * (lp - hp);
                float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 100f * t)) * 0.08f;
                s[i] = Mathf.Clamp(((lp - hp) * 0.5f + buzz) * env, -0.6f, 0.6f);
            }
            return s;
        }

        // A rat's squeak: two quick chirps sliding up and down.
        static float[] Squeak()
        {
            var s = Buffer(0.35f);
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float chirp = t < 0.12f ? t / 0.12f : t > 0.17f && t < 0.3f ? (t - 0.17f) / 0.13f : -1f;
                if (chirp < 0f) continue;
                float f = 2800f + 1600f * Mathf.Sin(chirp * Mathf.PI) + 300f * Mathf.Sin(t * 180f);
                phase += 2.0 * Math.PI * f / Rate;
                s[i] = (float)Math.Sin(phase) * Mathf.Sin(chirp * Mathf.PI) * 0.22f;
            }
            return s;
        }

        // A transistor radio left on: hiss and crackle, and under it a faint, far-off tune that comes and goes. Loops.
        static float[] RadioLoop()
        {
            _rng = new System.Random(42);
            return Looped(16f, 2f, 0.4f, s =>
            {
                int[] tune = { 0, 3, 5, 7, 5, 3, 0, -2, 0, 3, 7, 10, 7, 5, 3, 0 };
                for (int k = 0; k < tune.Length; k++)
                {
                    float f = Note(tune[k]);
                    int at = Mathf.RoundToInt(k * 1f * Rate);
                    for (int i = 0; i < Rate && at + i < s.Length; i++)
                    {
                        float t = (float)i / Rate;
                        float tone = Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 3f * t);
                        s[at + i] += tone * Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t / 0.6f) * 0.12f;
                    }
                }
                float lp = 0f, hp = 0f, fade = 0f;
                for (int i = 0; i < s.Length; i++)
                {
                    float t = (float)i / Rate;
                    fade += Coef(0.5f) * ((0.4f + 0.6f * Mathf.Abs(Noise())) - fade);
                    // The tune through a tiny speaker: no lows, no highs, drifting in and out of the station.
                    lp += Coef(2200f) * (s[i] * fade - lp);
                    hp += Coef(350f) * (lp - hp);
                    float hiss = Noise() * 0.06f + (_rng.NextDouble() < 0.0015 ? Noise() * 0.5f : 0f);
                    s[i] = (lp - hp) * 1.6f + hiss * (1.3f - fade);
                }
            });
        }

        // Steam escaping a bad joint: steady hiss with a slow waver. Loops.
        static float[] Steam()
        {
            _rng = new System.Random(43);
            var s = Buffer(2.4f);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                lp += Coef(7500f) * (Noise() - lp);
                hp += Coef(2500f) * (lp - hp);
                s[i] = (lp - hp) * 0.3f * (0.85f + 0.15f * Mathf.Sin(t * 5.3f));
            }
            return MakeLoop(s, 0.2f);
        }

        // A bulb's filament stuttering: mains buzz with clicks.
        static float[] Buzz()
        {
            _rng = new System.Random(44);
            var s = Buffer(0.6f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float on = Mathf.Sin(t * 60f) > -0.2f ? 1f : 0.1f;
                float saw = (t * 100f) % 1f * 2f - 1f;
                s[i] = (saw * 0.12f + (_rng.NextDouble() < 0.004 ? Noise() * 0.4f : 0f)) * on * Mathf.Clamp01((0.6f - t) / 0.1f);
            }
            return s;
        }

        // ---------------------------------------------------------------- the armory

        // A rifle shot in a concrete room: a hard crack, a chesty boom, and the room ringing after it.
        static float[] Gunshot()
        {
            _rng = new System.Random(50);
            var s = Buffer(2.2f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise();
                float crack = t < 0.012f ? n * (1f - t / 0.012f) : 0f;
                lp += Coef(Mathf.Lerp(4000f, 300f, Mathf.Clamp01(t / 0.25f))) * (n - lp);
                lp2 += Coef(150f) * (lp - lp2);
                float body = lp * Mathf.Exp(-t / 0.09f) * 1.4f;
                float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 45f, Mathf.Clamp01(t / 0.2f)) * t) * Mathf.Exp(-t / 0.12f) * 0.6f;
                float tail = lp2 * Mathf.Exp(-t / 0.6f) * 2.5f;
                s[i] = crack + body + boom + tail;
            }
            Echo(s, 0.8f);
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.75f, 0.75f);
            return s;
        }

        // A firing pin falling on nothing.
        static float[] DryClick()
        {
            _rng = new System.Random(51);
            var s = Buffer(0.15f);
            var r = new Resonator(2600f, 0.99f);
            for (int i = 0; i < s.Length; i++) s[i] = r.Tick(i < 30 ? Noise() : 0f) * 14f * 0.3f;
            return s;
        }

        // A steel locker door: a latch knock, then a rusty hinge groan.
        static float[] Locker()
        {
            _rng = new System.Random(52);
            var s = Buffer(1.0f);
            var r = new Resonator(700f, 0.995f);
            double phase = 0;
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float knock = t < 0.004f ? Noise() : 0f;
                float env = t > 0.1f ? Mathf.Sin(Mathf.Clamp01((t - 0.1f) / 0.85f) * Mathf.PI) : 0f;
                phase += 2.0 * Math.PI * (420f + 160f * Mathf.Sin(t * 13f)) / Rate;
                lp += Coef(1500f) * ((float)Math.Sin(phase) - lp);
                s[i] = r.Tick(knock) * 30f * 0.15f + lp * env * 0.18f + Noise() * env * 0.03f;
            }
            return s;
        }

        // Working a rifle bolt: up, back, forward, down.
        static float[] RifleBolt()
        {
            _rng = new System.Random(53);
            var s = Buffer(0.7f);
            foreach (float at in new[] { 0.02f, 0.16f, 0.36f, 0.5f })
            {
                int start = Mathf.RoundToInt(at * Rate);
                var r = new Resonator(1500f + (float)_rng.NextDouble() * 1500f, 0.99f);
                float lp = 0f;
                for (int i = 0; i < 2500 && start + i < s.Length; i++)
                {
                    float x = i < 40 ? Noise() : 0f;
                    lp += Coef(3000f) * (Noise() * (i < 1500 ? 0.15f : 0f) - lp);
                    s[start + i] += r.Tick(x) * 16f * 0.25f + lp * 0.4f;
                }
            }
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.6f, 0.6f);
            return s;
        }

        // A bulb bursting: a pop and a shower of tinkling glass.
        static float[] GlassBreak()
        {
            _rng = new System.Random(54);
            var s = Buffer(1.3f);
            for (int i = 0; i < 400; i++) s[i] += Noise() * (1f - i / 400f) * 0.6f;
            for (int k = 0; k < 30; k++)
            {
                int at = Mathf.RoundToInt((0.02f + (float)_rng.NextDouble() * 0.9f) * Rate);
                var r = new Resonator(3000f + (float)_rng.NextDouble() * 5000f, 0.993f);
                float g = 0.1f + (float)_rng.NextDouble() * 0.2f;
                for (int i = 0; i < 1800 && at + i < s.Length; i++) s[at + i] += r.Tick(i < 8 ? Noise() : 0f) * 20f * g * 0.2f;
            }
            for (int i = 0; i < s.Length; i++) s[i] = Mathf.Clamp(s[i], -0.6f, 0.6f);
            return s;
        }

        // A mount turning on its ratchet: a few heavy clicks.
        static float[] Ratchet()
        {
            _rng = new System.Random(55);
            var s = Buffer(0.6f);
            var r1 = new Resonator(900f, 0.994f);
            var r2 = new Resonator(1700f, 0.992f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float x = Mathf.Repeat(t, 0.11f) < 0.002f && t < 0.5f ? Noise() : 0f;
                s[i] = (r1.Tick(x) + r2.Tick(x) * 0.6f) * 22f * 0.2f;
            }
            return s;
        }

        // Dry sand pouring from a hole onto a heap: a soft hiss of grains with a slow waver. Loops.
        static float[] SandPour()
        {
            _rng = new System.Random(56);
            var s = Buffer(2.6f);
            float lp = 0f, hp = 0f, grain = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                grain += Coef(40f) * ((_rng.NextDouble() < 0.3 ? 1f : 0.4f) - grain);
                lp += Coef(5000f) * (Noise() * grain - lp);
                hp += Coef(800f) * (lp - hp);
                s[i] = (lp - hp) * 0.35f * (0.85f + 0.15f * Mathf.Sin(t * 3.1f));
            }
            return MakeLoop(s, 0.2f);
        }

        // Bunker room tone: mains hum with a few harmonics and a breath of ventilation air. Loops (every
        // frequency is a whole number of cycles over 4 s).
        static float[] BunkerHum()
        {
            _rng = new System.Random(17);
            var s = Buffer(4.2f);
            float air = 0f, air2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float hum = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.16f
                          + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.07f
                          + Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.03f
                          + Mathf.Sin(2f * Mathf.PI * 37.5f * t) * 0.05f;
                hum *= 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                float n = Noise();
                air += Coef(900f) * (n - air);
                air2 += Coef(120f) * (air - air2);
                s[i] = hum + (air - air2) * 0.12f;
            }
            return MakeLoop(s, 0.2f);
        }
    }
}
