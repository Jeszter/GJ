using UnityEngine;
using UnityEngine.InputSystem;

namespace GJ.Game
{
    // The graveyard behind the chapel, at night: the figure in the robe knocked your revolver out of your hand and
    // dragged you here, and now it wants you in the ground. Your breaths are what you have left to take: a blow knocks
    // one out of you, the last one and it is one more time. Bare fists only rock it back a step; the revolver lying out
    // in the grass is the only thing that will kill it. Get to it, shoot, and the cemetery gate opens on the dawn.
    public class GraveyardPuzzle : RoomPuzzle
    {
        public RobedFigure figure;
        public GameObject lostGun;           // where the revolver landed when it was knocked out of your hand
        public Transform lostGunPoint;
        public float punchRange = 1.7f;
        public int roundsAtLeast = 3;        // picked up with at least this many: the fight can't be lost to an empty cylinder
        public AudioClip punchClip, swishClip, takeClip, drawClip;
        public Color dawnFog = new Color(0.32f, 0.3f, 0.36f), dawnAmbient = new Color(0.42f, 0.38f, 0.42f);

        bool _won;
        float _nextPunch, _dawn;
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
            // However you got here, the revolver is out in the grass, and the hand is bare: nothing in it but a fist.
            if (Director.Gun != null && Director.Gun.Owned) Director.Gun.TakeAway();
            GameSession.GunInGraveyard = true;
            if (lostGun != null) lostGun.SetActive(true);
            Director.BareHands();
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
            Say("get to the revolver\n<size=10>fists only hold it off. [LMB] punch</size>");

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
            Say("your revolver. " + GameSession.Rounds + " rounds\n<size=10>[LMB] shoot it</size>");
        }

        void Update()
        {
            if (_won)
            {
                // Grey light coming up over the wall.
                _dawn = Mathf.MoveTowards(_dawn, 1f, Time.deltaTime / 5f);
                RenderSettings.fogColor = Color.Lerp(_fog, dawnFog, _dawn);
                RenderSettings.ambientLight = Color.Lerp(_ambient, dawnAmbient, _dawn);
                return;
            }
            if (Director == null || !Director.Alive || FirstPersonPaused()) return;

            // Bare fists, with the revolver not in hand: they rock it back a step, no more.
            var gun = Director.Gun;
            var mouse = Mouse.current;
            bool armed = gun != null && gun.Drawn;
            if (!armed && mouse != null && mouse.leftButton.wasPressedThisFrame && Time.time >= _nextPunch && Cursor.lockState == CursorLockMode.Locked)
            {
                _nextPunch = Time.time + 0.45f;
                Director.Jab();
                Vector3 to = figure.transform.position + Vector3.up * 1.2f - Director.Head.position;
                bool hit = !figure.Dead && to.magnitude < punchRange + 0.4f && Vector3.Angle(Director.Head.forward, to) < 35f;
                if (hit)
                {
                    figure.Hit(0, Director.Controller.transform.position);
                    if (punchClip != null) AudioSource.PlayClipAtPoint(punchClip, figure.transform.position + Vector3.up * 1.3f, 1f);
                }
                else if (swishClip != null) AudioSource.PlayClipAtPoint(swishClip, Director.Head.position, 0.4f);
            }
        }

        static bool FirstPersonPaused() => GJ.PSX.FirstPersonController.Frozen;

        void Won()
        {
            _won = true;
            Say("it doesn't get up again.\nthe gate stands open");
            RaiseSolved();
        }
    }
}
