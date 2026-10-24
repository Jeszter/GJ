using UnityEngine;
using UnityEngine.InputSystem;

namespace GJ.Game
{
    // The graveyard behind the chapel, at night: the figure in the robe let go of you here, and now it wants you in the
    // ground. Your breaths are what you have left to take: a blow knocks one out of you, the last one and it is one
    // more time. Fists (the lighter balled in one), or the revolver if you find where it fell in the grass. Put the
    // figure down and the cemetery gate swings open on the dawn.
    public class GraveyardPuzzle : RoomPuzzle
    {
        public RobedFigure figure;
        public GameObject lostGun;           // where the revolver landed, if it was knocked out of your hand
        public Transform lostGunPoint;
        public float punchRange = 1.7f;
        public AudioClip punchClip, swishClip, takeClip;
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
            if (lostGun != null) lostGun.SetActive(GameSession.GunInGraveyard);
            figure.Struck = _ => Director.KnockBreath("the figure in the robe put you in the ground.");
            figure.Died = Won;
            // A round from the revolver takes it harder than a fist.
            var target = figure.GetComponent<Shootable>();
            if (target != null) target.Shot += _ => figure.Hit(3, Director.Head.position);
        }

        public override void Arrived(string cameFrom)
        {
            Director.startMessage = GameSession.GunInGraveyard
                ? "it let go of you in the graveyard\n<size=10>[LMB] fight. your revolver fell somewhere in the grass</size>"
                : "it let go of you in the graveyard\n<size=10>[LMB] fight</size>";
            StartCoroutine(GetUp());
        }

        // Thrown down on the grass: the view starts at the ground, black clearing, and climbs back up to standing
        // before the figure comes on.
        System.Collections.IEnumerator GetUp()
        {
            var body = Director.Controller;
            var head = Director.Head;
            body.canMove = false;
            Director.ScreenFlash(new Color(0f, 0f, 0f, 1f), 1.4f);
            Vector3 rest = head.localPosition, low = rest + Vector3.down * 1.3f;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                head.localPosition = low;
                yield return null;
            }
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.3f);
                head.localPosition = Vector3.Lerp(low, rest, k) + Vector3.right * Mathf.Sin(t * 7f) * 0.03f * (1f - k);
                yield return null;
            }
            head.localPosition = rest;
            body.canMove = true;
            figure.Engage(body.transform);
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
            Director.Gun?.Give(GameSession.Rounds);
            Say("your revolver. " + GameSession.Rounds + " rounds left in it");
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

            // Fists, with the revolver away: a jab with the lighter balled in the hand.
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
                    figure.Hit(1, Director.Controller.transform.position);
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
