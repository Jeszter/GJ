using UnityEngine;
using UnityEngine.InputSystem;

namespace GJ.Game
{
    // The graveyard behind the chapel, at night: the figure in the robe knocked your revolver out of your hand and
    // dragged you here, and now it wants you in the ground. Your breaths are what you have left to take: a blow knocks
    // one out of you, the last one and it is one more time. The Zippo stays in your hand; a boot throws the figure back a
    // step, but the revolver lying out in the grass is the only thing that will hurt it. Find it, shoot, and the
    // cemetery gate opens on the dawn.
    public class GraveyardPuzzle : RoomPuzzle
    {
        public RobedFigure figure;
        public GameObject lostGun;           // where the revolver landed when it was knocked out of your hand
        public Transform lostGunPoint;
        public float punchRange = 1.7f;
        public int roundsAtLeast = 3;        // picked up with at least this many: the fight can't be lost to an empty cylinder
        public AudioClip punchClip, swishClip, takeClip, drawClip, kickClip;
        public Transform kickLeg;            // a boot and shin under the view, swung up into it to kick
        public GraveyardEnding ending;       // out through the gate: the end of the run
        public Color dawnFog = new Color(0.36f, 0.34f, 0.4f), dawnAmbient = new Color(0.85f, 0.8f, 0.85f);

        bool _won;
        float _nextKick, _dawn;
        Color _fog, _ambient;

        // The air down here is the only air there is: it doesn't run out, it gets knocked out of you.
        public override float AirDrain => 0f;
        public override bool Working => false;
        public override bool AllowsSelfShot => false;
        public override string FailReason => _won ? null : "the figure in the robe put you in the ground";
        public override Vector3? Focus => figure != null && !figure.Dead ? figure.eyes.position : (Vector3?)null;

        void Start()
        {
            _fog = RenderSettings.fogColor;
            _ambient = RenderSettings.ambientLight;
            figure.health = 2;
            figure.Struck = _ => Director.KnockBreath("the figure in the robe put you in the ground.");
            figure.Died = Won;
            // Only a round hurts it.
            var target = figure.GetComponent<Shootable>();
            if (target != null) target.Shot += _ => figure.Hit(1, Director.Head.position);
        }

        public override void Arrived(string cameFrom)
        {
            Director.startMessage = "";
            // The way out of here is the ending, not the director's exit.
            Director.exitZ = float.MaxValue;
            if (kickLeg != null) kickLeg.gameObject.SetActive(false);
            // However you got here, the revolver is out in the grass; the Zippo is still in your hand.
            if (Director.Gun != null && Director.Gun.Owned) Director.Gun.TakeAway();
            GameSession.GunInGraveyard = true;
            if (lostGun != null) lostGun.SetActive(true);
            StartCoroutine(Intro());
        }

        // Thrown down on the grass: the view starts at the ground and climbs back up to standing. Your revolver lies
        // out there in the grass; the figure turns from it to you and draws a blade as long as your arm.
        System.Collections.IEnumerator Intro()
        {
            var body = Director.Controller;
            var head = Director.Head;
            body.canMove = false;
            GJ.PSX.FirstPersonController.Frozen = true;
            Director.ScreenFlash(new Color(0f, 0f, 0f, 1f), 1.4f);
            Vector3 rest = head.localPosition, low = rest + Vector3.down * 1.3f;
            Vector3 gunAt = lostGunPoint.position;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                head.localPosition = low;
                Watch(head, figure.eyes.position, 20f);
                yield return null;
            }
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.3f);
                head.localPosition = Vector3.Lerp(low, rest, k) + Vector3.right * Mathf.Sin(t * 7f) * 0.03f * (1f - k);
                Watch(head, figure.eyes.position, 5f);
                yield return null;
            }
            head.localPosition = rest;

            // Your revolver, out in the grass where it fell.
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                Watch(head, gunAt, 5f);
                yield return null;
            }
            Say("your revolver");

            // Back to the figure: it turns to you and draws the blade.
            Quaternion face = Quaternion.LookRotation(Flat(body.transform.position - figure.transform.position));
            Quaternion from = figure.transform.rotation;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                figure.transform.rotation = Quaternion.Slerp(from, face, t / 0.7f);
                Watch(head, figure.eyes.position, 6f);
                yield return null;
            }
            figure.transform.rotation = face;
            figure.Armed = true;
            figure.Show(figure.strike);
            if (drawClip != null) AudioSource.PlayClipAtPoint(drawClip, figure.transform.position + Vector3.up * 1.4f, 1f);
            Director.ScreenFlash(new Color(0.5f, 0f, 0f, 0.35f), 0.5f);
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                Watch(head, figure.eyes.position, 8f);
                yield return null;
            }
            figure.Show(figure.idle);
            Say("find your revolver\n<size=10>only a bullet will stop it.   [V] kick it back</size>");

            // Hand the view back looking where the head ended up.
            var e = head.rotation.eulerAngles;
            body.SetLook(e.y, e.x > 180f ? e.x - 360f : e.x);
            head.localRotation = Quaternion.Euler(e.x > 180f ? e.x - 360f : e.x, 0f, 0f);
            GJ.PSX.FirstPersonController.Frozen = false;
            body.canMove = true;
            figure.Engage(body.transform);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }

        // The view held on something during the intro (and the mouse kept off it: the flow frees it when play starts).
        static void Watch(Transform head, Vector3 point, float speed)
        {
            GJ.PSX.FirstPersonController.Frozen = true;
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(point - head.position), 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            if (lostGun != null && lostGun.activeSelf && Aim(eye, forward, lostGunPoint.position, 2.2f) > 0.9f)
                return new Option { prompt = "[E] snatch up the revolver", act = TakeGun, gesture = Gesture.Grab };
            return null;
        }

        void TakeGun()
        {
            lostGun.SetActive(false);
            GameSession.GunInGraveyard = false;
            if (takeClip != null) AudioSource.PlayClipAtPoint(takeClip, lostGunPoint.position, 0.8f);
            Director.Gun?.Give(Mathf.Max(GameSession.Rounds, roundsAtLeast));
            Say(Loc.F("your revolver. {0} rounds", GameSession.Rounds) + "\n<size=10>" + Loc.T("[LMB] shoot it") + "</size>");
        }

        void Update()
        {
            if (_won)
            {
                // Grey light coming up over the wall.
                _dawn = Mathf.MoveTowards(_dawn, 1f, Time.deltaTime / 5f);
                if (ending == null || !ending.LightsOwned)
                {
                    RenderSettings.fogColor = Color.Lerp(_fog, dawnFog, _dawn);
                    RenderSettings.ambientLight = Color.Lerp(_ambient, dawnAmbient, _dawn);
                    if (_dawn < 1f) DynamicGI.UpdateEnvironment();
                }
                // Out through the gate and the legs give.
                if (ending != null && !ending.Running && Director != null && Director.Alive
                    && Director.Controller.transform.position.z > Director.door.transform.position.z + 1.2f)
                    ending.Run(Director, Say);
                return;
            }
            if (Director == null || !Director.Alive || FirstPersonPaused()) return;

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            // No punching it: a kick only throws the figure back a long step and breaks off its swing.
            var kb = Keyboard.current;
            bool kick = kb != null && kb.vKey.wasPressedThisFrame;
            if (kick && kickLeg != null && Time.time >= _nextKick && locked)
            {
                _nextKick = Time.time + 1.1f;
                StartCoroutine(Kick());
                StartCoroutine(Land(0.16f, punchRange + 0.3f, 45f, true));
            }
        }

        // The blow lands a beat after it is thrown, when the fist or the boot is all the way out: a thud, a moment
        // where everything stops, a jolt through the view.
        System.Collections.IEnumerator Land(float after, float range, float angle, bool kick)
        {
            yield return new WaitForSeconds(after);
            if (Director == null || !Director.Alive) yield break;
            Vector3 to = figure.transform.position + Vector3.up * 1.2f - Director.Head.position;
            bool hit = !figure.Dead && to.magnitude < range + 0.4f && Vector3.Angle(Director.Head.forward, to) < angle;
            if (!hit)
            {
                if (swishClip != null) AudioSource.PlayClipAtPoint(swishClip, Director.Head.position, 0.4f);
                yield break;
            }
            if (kick) figure.Kick(Director.Controller.transform.position);
            else figure.Hit(0, Director.Controller.transform.position);
            var clip = kick && kickClip != null ? kickClip : punchClip;
            if (clip != null) AudioSource.PlayClipAtPoint(clip, figure.transform.position + Vector3.up * 1.3f, 1f);
            Director.ScreenFlash(new Color(1f, 0.95f, 0.85f, kick ? 0.22f : 0.14f), 0.12f);
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(kick ? 0.09f : 0.06f);
            Time.timeScale = 1f;
            var head = Director.Head;
            Quaternion rest = head.localRotation;
            for (float t = 0f; t < 0.12f; t += Time.deltaTime)
            {
                head.localRotation = rest * Quaternion.Euler(Random.Range(-1.5f, 1.5f) * (1f - t / 0.12f), Random.Range(-1.5f, 1.5f) * (1f - t / 0.12f), 0f);
                yield return null;
            }
            head.localRotation = rest;
        }

        // The boot comes up from below into the view, drives out, and drops back.
        System.Collections.IEnumerator Kick()
        {
            var leg = kickLeg;
            var fist = Director.handAnimator;
            if (fist != null) fist.Lowered = true;
            leg.gameObject.SetActive(true);
            Vector3 low = new Vector3(0.12f, -1.1f, 0.1f), high = new Vector3(0.1f, -0.3f, 0.7f);
            Quaternion lowR = Quaternion.Euler(10f, -6f, 0f), highR = Quaternion.Euler(-65f, -8f, 0f);   // the sole at it
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                float k = t / 0.16f;
                k = 1f - (1f - k) * (1f - k);
                leg.localPosition = Vector3.Lerp(low, high, k);
                leg.localRotation = Quaternion.Slerp(lowR, highR, k);
                yield return null;
            }
            leg.localPosition = high;
            leg.localRotation = highR;
            yield return new WaitForSeconds(0.1f);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.3f);
                leg.localPosition = Vector3.Lerp(high, low, k);
                leg.localRotation = Quaternion.Slerp(highR, lowR, k);
                yield return null;
            }
            leg.gameObject.SetActive(false);
            if (fist != null && (Director.Gun == null || !Director.Gun.Drawn)) fist.Lowered = false;
        }

        static bool FirstPersonPaused() => GJ.PSX.FirstPersonController.Frozen;

        void Won()
        {
            _won = true;
            Say("it doesn't get up again.\nunder the hood: your own face.\nthe gate stands open");
            RaiseSolved();
        }
    }
}
