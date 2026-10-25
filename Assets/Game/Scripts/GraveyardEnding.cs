using System.Collections;
using GJ.PSX;
using UnityEngine;

namespace GJ.Game
{
    // The end of the run. Out through the cemetery gate the legs go: a few stumbling steps and you are down in the wet
    // grass, the view on its side. Nobody comes. The dawn comes up, birds go over, the mist drifts; you blink and the
    // world is still going on. Then an engine, headlights and an orange beacon on the road: the mine rescue truck. Two of
    // the crew climb out with lamps on their hats, cross the grass, one kneels over you. You are lifted, the view
    // rights itself and sways toward the open truck, and it all goes white.
    public class GraveyardEnding : MonoBehaviour
    {
        public Transform truck;
        public Vector3 truckFrom, truckStop;   // along the road, world space
        public Transform beacon;               // the amber light on the roof, turned round and round
        public Light beaconLight;
        public AudioSource engine;
        public RescueWorker[] crew;            // first one kneels over you, second stands with the lamp on you
        public Vector3[] crewStart;            // where they get out, world space
        public Transform[] birds;
        public Light moonlight;
        public Color dawnLight = new Color(1f, 0.72f, 0.55f);
        public AudioClip thudClip, doorClip, stepClip, gaspClip, heartClip, shotClip;

        public Color dawnAmbient = new Color(1f, 0.92f, 0.88f), dawnFog = new Color(0.55f, 0.52f, 0.55f);

        const float LyingRoll = 28f;   // the head on its side in the grass, the world tipped

        public bool Running { get; private set; }

        RoomDirector _director;
        System.Action<string> _say;

        void Awake()
        {
            if (truck != null) truck.gameObject.SetActive(false);
            foreach (var c in crew) if (c != null) c.gameObject.SetActive(false);
            foreach (var b in birds) if (b != null) b.gameObject.SetActive(false);
        }

        public void Run(RoomDirector director, System.Action<string> say)
        {
            if (Running) return;
            Running = true;
            _director = director;
            _say = say;
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            var d = _director;
            var body = d.Controller;
            var head = d.Head;
            body.canMove = false;
            FirstPersonController.Frozen = true;
            var cc = body.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            if (d.Gun != null && d.Gun.Owned) d.Gun.Holster();
            d.hand.gameObject.SetActive(false);
            d.enabled = false;
            var veil = d.Veil;
            HideHud(veil);
            d.HideHud();
            Play(heartClip, head.position, 0.7f);

            // A. Stumbling out through the gate: slower every step, the view swaying and sinking.
            Vector3 rest = head.localPosition;
            Quaternion look = head.rotation;
            Vector3 ahead = Vector3.forward;
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                float k = t / 2.4f;
                body.transform.position += ahead * Mathf.Lerp(1.3f, 0.25f, k) * Time.deltaTime;
                head.localPosition = rest + new Vector3(Mathf.Sin(t * 4.5f) * 0.07f * k, -0.12f * k - Mathf.Abs(Mathf.Sin(t * 6f)) * 0.03f, 0f);
                look = Quaternion.Slerp(look, Quaternion.LookRotation(ahead + Vector3.down * 0.3f * k) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.3f) * 9f * k), 1f - Mathf.Exp(-3f * Time.deltaTime));
                head.rotation = look;
                veil.color = new Color(0f, 0f, 0f, 0.25f * k * (0.6f + 0.4f * Mathf.Sin(t * 7f)));
                if (stepClip != null && Mathf.Repeat(t, 0.6f) < Time.deltaTime) Play(stepClip, body.transform.position, 0.5f);
                yield return null;
            }

            // B. Down: onto the grass, on your side, looking out along the ground at the road.
            Vector3 from = head.localPosition;
            Vector3 down = new Vector3(rest.x, 0.18f - body.transform.position.y, rest.z);
            Quaternion lying = Quaternion.LookRotation(ahead + Vector3.up * 0.08f) * Quaternion.Euler(0f, 0f, LyingRoll);
            Quaternion fall = head.rotation;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = t / 0.5f;
                k *= k;
                head.localPosition = Vector3.Lerp(from, down, k);
                head.rotation = Quaternion.Slerp(fall, lying, k);
                yield return null;
            }
            head.localPosition = down;
            head.rotation = lying;
            Play(thudClip, head.position, 1f);
            d.Stun(true);
            yield return Veil(veil, 0.9f, 0f, 0.15f);
            yield return Veil(veil, 0.9f, 0.9f, 0.6f);
            yield return Veil(veil, 0.9f, 0.15f, 1.6f);

            // C. Nobody. The light comes up, birds go over, slow blinks; in a while an engine on the road.
            if (moonlight != null) StartCoroutine(Dawn());
            LightsOwned = true;
            for (int i = 0; i < birds.Length; i++) if (birds[i] != null) StartCoroutine(Fly(birds[i], i));
            d.Stun(false);
            float lie = 0f;
            bool truckOn = false;
            while (lie < 7f)
            {
                lie += Time.deltaTime;
                head.localPosition = down + Vector3.up * Mathf.Sin(lie * 1.3f) * 0.006f;
                float blink = Blink(lie, 2.2f) + Blink(lie, 5.0f);
                veil.color = new Color(0f, 0f, 0f, Mathf.Max(0.15f * (1f - lie / 7f), blink));
                if (!truckOn && lie > 3.5f)
                {
                    truckOn = true;
                    StartCoroutine(Drive());
                }
                yield return null;
            }
            veil.color = Color.clear;

            // D. The truck pulls up; the doors; two of the crew get out and come over the grass with their lamps.
            while (_driving) { Breathe(head, down, ref lie); yield return null; }
            Play(doorClip, truck.position, 1f);
            yield return Wait(head, down, lie, 0.6f);
            Play(doorClip, truck.position, 0.8f);
            Vector3 eye = head.position;
            Vector3 lay = body.transform.position;
            Vector3[] goal = { lay + new Vector3(0.35f, 0f, 1.05f), lay + new Vector3(-1.0f, 0f, 1.5f) };
            for (int i = 0; i < crew.Length; i++)
            {
                crew[i].transform.position = crewStart[i];
                crew[i].gameObject.SetActive(true);
            }
            _say?.Invoke("...there. by the gate.");
            float walk = 0f;
            bool[] there = new bool[crew.Length];
            while (System.Array.IndexOf(there, false) >= 0)
            {
                walk += Time.deltaTime;
                for (int i = 0; i < crew.Length; i++)
                {
                    var c = crew[i];
                    Vector3 to = goal[i] - c.transform.position;
                    to.y = 0f;
                    if (to.magnitude < 0.08f)
                    {
                        if (!there[i]) { there[i] = true; c.Show(i == 0 ? c.kneel : c.stand); }
                    }
                    else
                    {
                        float speed = i == 0 ? 1.7f : 1.4f;
                        c.transform.position += to.normalized * Mathf.Min(speed * Time.deltaTime, to.magnitude);
                        c.transform.rotation = Quaternion.LookRotation(to);
                        c.Step(walk * 1.6f + i * 0.5f);
                        if (stepClip != null && Mathf.Repeat(walk * 1.6f + i * 0.5f, 0.5f) < Time.deltaTime * 1.6f) Play(stepClip, c.transform.position, 0.35f);
                    }
                    if (there[i])
                    {
                        Vector3 face = eye - c.transform.position;
                        face.y = 0f;
                        if (face.sqrMagnitude > 0.01f) c.transform.rotation = Quaternion.Slerp(c.transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-6f * Time.deltaTime));
                    }
                    c.ShineAt(eye);
                }
                // The eyes drag over to them.
                Vector3 seen = crew[0].transform.position + Vector3.up * 1.2f;
                head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(seen - head.position) * Quaternion.Euler(0f, 0f, LyingRoll), 1f - Mathf.Exp(-1.5f * Time.deltaTime));
                Breathe(head, down, ref lie);
                yield return null;
            }
            _say?.Invoke("hey. hey! stay with me.\n<size=10>this one's breathing</size>");
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                Vector3 seen = crew[0].transform.position + Vector3.up * 0.8f;
                head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(seen - head.position) * Quaternion.Euler(0f, 0f, LyingRoll - 6f), 1f - Mathf.Exp(-2f * Time.deltaTime));
                foreach (var c in crew) c.ShineAt(eye);
                Breathe(head, down, ref lie);
                yield return null;
            }

            // E. Lifted: a blink, then the view rights itself and rises, and sways toward the truck. Then, cut: black,
            // and a single shot in the dark.
            yield return Veil(veil, 0f, 1f, 0.35f);
            Play(gaspClip, head.position, 0.8f);
            crew[0].Show(crew[0].stand);
            Vector3 carried = new Vector3(rest.x, 1.2f - body.transform.position.y, rest.z);
            Vector3 bodyFrom = body.transform.position;
            Vector3 toTruck = truck.position - truck.forward * 2.8f;
            toTruck.y = bodyFrom.y;
            Quaternion lifted = head.rotation;
            for (float t = 0f; t < 3.2f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 1.4f);
                head.localPosition = Vector3.Lerp(down, carried, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * Mathf.Abs(Mathf.Sin(t * 5f)) * 0.03f * k;
                Vector3 over = toTruck + Vector3.up * 1.6f - head.position;
                head.rotation = Quaternion.Slerp(lifted, Quaternion.LookRotation(over) * Quaternion.Euler(-8f, 0f, Mathf.Sin(t * 2.5f) * 4f), Mathf.SmoothStep(0f, 1f, k));
                if (t > 1.2f)
                {
                    float m = Mathf.SmoothStep(0f, 1f, (t - 1.2f) / 3f);
                    body.transform.position = Vector3.Lerp(bodyFrom, toTruck, m * 0.75f);
                    var c = crew[0];
                    c.transform.position = body.transform.position + Vector3.right * 0.55f;
                    c.transform.rotation = Quaternion.LookRotation(Flat(toTruck - bodyFrom));
                    c.Step(t * 1.4f);
                }
                foreach (var w in crew) w.ShineAt(head.position + head.forward * 2f);
                float open = t < 0.35f ? 1f - t / 0.35f : 0f;
                veil.color = new Color(0f, 0f, 0f, open);
                yield return null;
            }
            if (engine != null) engine.Stop();
            AudioListener.pause = false;
            // The first shot: right there, a white flash through the eyelids and the view knocked sideways.
            Shot(head.position + head.right * 0.6f);
            Quaternion hit = head.rotation;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = t / 0.5f;
                veil.color = Color.Lerp(new Color(1f, 0.95f, 0.85f, 1f), Color.black, k * 2f);
                head.rotation = hit * Quaternion.Euler(-12f * (1f - k), 0f, 25f * k);
                yield return null;
            }
            // Black. A long breath of nothing, then the second.
            veil.color = Color.black;
            d.Stun(true);
            yield return new WaitForSeconds(1.6f);
            d.Stun(false);
            Shot(head.position + head.forward * 2f);
            yield return new WaitForSeconds(2.4f);
            d.flow.OnRoomCleared();
        }

        bool _driving;

        // From the moment the dawn takes over, the yard's own dawn (GraveyardPuzzle) leaves the light alone.
        public bool LightsOwned { get; private set; }

        IEnumerator Drive()
        {
            _driving = true;
            truck.gameObject.SetActive(true);
            truck.position = truckFrom;
            if (engine != null) { engine.volume = 0f; engine.Play(); }
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                float k = t / 6f;
                truck.position = Vector3.Lerp(truckFrom, truckStop, 1f - (1f - k) * (1f - k));
                if (engine != null) { engine.volume = Mathf.Min(1f, t / 1.5f) * 0.7f; engine.pitch = Mathf.Lerp(1.05f, 0.75f, k); }
                yield return null;
            }
            truck.position = truckStop;
            if (engine != null) engine.volume = 0.3f;
            _driving = false;
        }

        void Update()
        {
            // Nothing of the game's HUD over the ending, even if a pause brought the director back for a moment.
            if (Running && _director != null) _director.HideHud();
            // The beacon on the roof goes round and round, its light sweeping the grass.
            if (beacon != null && truck != null && truck.gameObject.activeInHierarchy)
            {
                beacon.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
                if (beaconLight != null) beaconLight.intensity = 1.2f + 1.6f * Mathf.Max(0f, Mathf.Sin(Time.time * Mathf.PI * 2f));
            }
        }

        IEnumerator Dawn()
        {
            Color from = moonlight.color, ambient = RenderSettings.ambientLight, fog = RenderSettings.fogColor;
            float fromI = moonlight.intensity, fogEnd = RenderSettings.fogEndDistance;
            var sky = _director.Head.GetComponent<Camera>();
            Color skyFrom = sky != null ? sky.backgroundColor : Color.black;
            for (float t = 0f; t < 10f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 10f);
                moonlight.color = Color.Lerp(from, dawnLight, k);
                moonlight.intensity = Mathf.Lerp(fromI, 1.1f, k);
                RenderSettings.ambientLight = Color.Lerp(ambient, dawnAmbient, k);
                RenderSettings.fogColor = Color.Lerp(fog, dawnFog, k);
                RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd, 40f, k);
                DynamicGI.UpdateEnvironment();
                if (sky != null) sky.backgroundColor = Color.Lerp(skyFrom, dawnFog, k);
                yield return null;
            }
        }

        // A bird crossing the sky high over the yard, wings beating, then gliding.
        IEnumerator Fly(Transform bird, int i)
        {
            yield return new WaitForSeconds(0.6f + i * 0.7f);
            bird.gameObject.SetActive(true);
            float y = 7f + (i % 3) * 1.4f, z = 58f + i * 2.5f;
            Vector3 a = new Vector3(-30f + i * 2f, y, z), b = new Vector3(32f, y + 2f, z - 4f);
            bird.rotation = Quaternion.LookRotation(b - a);
            for (float t = 0f; t < 9f; t += Time.deltaTime)
            {
                bird.position = Vector3.Lerp(a, b, t / 9f);
                float flap = Mathf.Repeat(t, 2.4f) < 1.4f ? Mathf.Sin(t * (13f + i)) : 0.35f;
                bird.localScale = new Vector3(1f, flap, 1f);
                yield return null;
            }
            bird.gameObject.SetActive(false);
        }

        // A slow blink: the lids come down, rest, lift again.
        static float Blink(float t, float at)
        {
            float u = t - at;
            if (u < 0f || u > 0.9f) return 0f;
            return u < 0.3f ? u / 0.3f : u < 0.5f ? 1f : 1f - (u - 0.5f) / 0.4f;
        }

        static void Breathe(Transform head, Vector3 down, ref float t)
        {
            t += Time.deltaTime;
            head.localPosition = down + Vector3.up * Mathf.Sin(t * 1.3f) * 0.006f;
        }

        IEnumerator Wait(Transform head, Vector3 down, float t, float seconds)
        {
            for (float s = 0f; s < seconds; s += Time.deltaTime)
            {
                Breathe(head, down, ref t);
                yield return null;
            }
        }

        static IEnumerator Veil(UnityEngine.UI.Image veil, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                veil.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            veil.color = new Color(0f, 0f, 0f, to);
        }

        // The lungs, prompts and controls go; the veil and the line at the bottom stay.
        static void HideHud(UnityEngine.UI.Image veil)
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas) continue;
                if (c.name == "RevolverHUD") { c.enabled = false; continue; }
                if (c.name != "HUD") continue;
                foreach (Transform child in c.transform)
                    if (child != veil.transform && child.name != "Message") child.gameObject.SetActive(false);
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }

        // A gunshot loud enough to hurt: the clip twice over itself, a hair apart, for the crack and the slap back.
        void Shot(Vector3 at)
        {
            if (shotClip == null) return;
            var src = new GameObject("Shot").AddComponent<AudioSource>();
            src.transform.position = at;
            src.spatialBlend = 0f;
            src.PlayOneShot(shotClip, 1f);
            src.PlayOneShot(shotClip, 0.7f);
            Destroy(src.gameObject, shotClip.length + 0.2f);
        }

        static void Play(AudioClip clip, Vector3 at, float volume)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
        }
    }
}
