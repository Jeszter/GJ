using System.Collections;
using GJ.PSX;
using UnityEngine;

namespace GJ.Game
{
    // The miners' chapel past the corridor's last door: plenty of air, pews, candles, and whatever the men who came
    // down before left lying about (boxes of rounds, a revolver on the altar). The way up to the surface is a wooden
    // door chained shut with a padlock: a round through the lock and it swings. Or the revolver turned the other way,
    // and the run ends down here. It swings out on the night: a few seconds of graves and fog and a lamp out there,
    // then something in a robe steps into the doorway, comes for you, takes you by the wrist, knocks the revolver out
    // of your hand and drags you out into the graveyard.
    public class ChapelPuzzle : RoomPuzzle
    {
        [System.Serializable]
        public class Loot
        {
            public GameObject item;
            public Transform point;
            public string prompt;    // "[E] take the box of rounds"
            public int rounds;       // rounds in it
            public bool revolver;    // the gun itself, loaded with those rounds
            public string line;      // said when taken
        }

        public Loot[] loot;
        public Shootable padlock, chainTarget, doorTarget;
        public GameObject padlockPiece, chain;
        public AudioClip takeClip, snapClip, bellClip;
        public RobedFigure figure;           // waiting in the dark beyond the door
        public Transform doorway;            // where it steps in from
        public GameObject droppedGun;        // the revolver falling out of your hand
        public float lookSeconds = 2.8f;     // how long the open door shows you the night before it comes
        public float ambushZ = 17f;          // or step into the doorway before that, and it is on you at once
        public AudioClip grabClip, clatterClip;

        bool _open, _ambushed;
        float _openedAt;

        public override bool Working => false;

        public override string[] SelfShotEnding => new[]
        {
            "SECTOR 4  -  YOU STAYED",
            "the stairs to the surface went on without you.\nthank you for playing."
        };

        void Awake()
        {
            if (padlock != null) padlock.Shot += _ => Unlock();
            if (chainTarget != null) chainTarget.Shot += _ => Unlock();
            if (doorTarget != null) doorTarget.Shot += _ => Unlock();
            if (figure != null) figure.gameObject.SetActive(false);
            if (droppedGun != null) droppedGun.SetActive(false);
        }

        void Update()
        {
            if (_open && !_ambushed && figure != null && Director != null && Director.Alive
                && (Time.time - _openedAt > lookSeconds || Director.Controller.transform.position.z > ambushZ))
                StartCoroutine(Ambush());
        }

        // Filmed like a blow you never saw coming: it is out of the dark and on you in half a second, the world drops to
        // a crawl as it takes your wrist, a white flash and a crack as the revolver is struck away, the sound goes dull,
        // and you are hauled backwards off your feet into the night.
        IEnumerator Ambush()
        {
            _ambushed = true;
            var body = Director.Controller;
            var head = Director.Head;
            var hand = Director.hand;
            body.canMove = false;
            FirstPersonController.Frozen = true;
            // Empty-handed for it: the revolver gone from view, the lighter dropped into a pocket, the hand open.
            var gunNow = Director.Gun;
            if (gunNow != null) gunNow.Holster();
            var lighterHand = hand != null ? hand.GetComponent<HandAnimator>() : null;
            if (lighterHand != null) { lighterHand.Lowered = false; lighterHand.Empty(true); }
            if (Director.lighterLight != null) Director.lighterLight.enabled = false;

            Vector3 from = doorway.position;
            Vector3 dir = body.transform.position - from;
            dir.y = 0f;
            dir.Normalize();
            Vector3 to = body.transform.position - dir * 0.85f;
            // Out of the dark into the doorway first, and it stands there a moment, eyes on you, the view dragged to it.
            Vector3 outside = from - dir * 2.2f;
            figure.gameObject.SetActive(true);
            figure.Show(figure.idle);
            figure.transform.SetPositionAndRotation(outside, Quaternion.LookRotation(dir));
            if (figure.hissClip != null) AudioSource.PlayClipAtPoint(figure.hissClip, from + Vector3.up * 1.5f, 1f);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                figure.transform.position = Vector3.Lerp(outside, from, Mathf.SmoothStep(0f, 1f, t / 0.9f));
                LookAt(head, figure.eyes.position, 9f);
                yield return null;
            }
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                LookAt(head, figure.eyes.position, 9f);
                yield return null;
            }

            // The rush: fast however far away you stand, the head held on it.
            float rush = Mathf.Max(0.45f, Vector3.Distance(from, to) / 11f);
            for (float t = 0f; t < rush; t += Time.deltaTime)
            {
                float k = t / rush;
                figure.transform.position = Vector3.Lerp(from, to, k * k);
                LookAt(head, figure.eyes.position, 14f);
                yield return null;
            }
            figure.transform.position = to;
            figure.Show(figure.reach);

            // The grab, in slow motion: the hand yanked out toward it, the view rolling, the heart in the ears.
            Time.timeScale = 0.25f;
            Director.ScreenFlash(new Color(0.6f, 0f, 0f, 0.45f), 0.6f);
            if (grabClip != null) AudioSource.PlayClipAtPoint(grabClip, head.position, 1f);
            Vector3 handRest = hand.localPosition;
            Quaternion headFrom = head.rotation;
            for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.7f);
                hand.localPosition = handRest + new Vector3(-0.06f, 0.05f, 0.22f) * Mathf.SmoothStep(0f, 1f, k);
                var look = Quaternion.LookRotation(figure.eyes.position - head.position);
                head.rotation = Quaternion.Slerp(headFrom, look, Mathf.SmoothStep(0f, 1f, k)) * Quaternion.Euler(0f, 0f, -14f * k);
                yield return null;
            }

            // The revolver struck out of the hand: a white crack, the gun spinning away over the stone.
            var gun = Director.Gun;
            Director.ScreenFlash(new Color(1f, 0.97f, 0.9f, 0.85f), 0.5f);
            if (clatterClip != null) AudioSource.PlayClipAtPoint(clatterClip, head.position, 1f);
            if (gun != null && gun.Owned)
            {
                gun.TakeAway();
                GameSession.GunInGraveyard = true;
                if (droppedGun != null) StartCoroutine(Drop(droppedGun, head.position + head.right * 0.3f + head.forward * 0.3f));
                Say("the revolver is struck out of your hand");
            }
            else Say("a cold hand closes round your wrist");
            Director.Stun(true);
            Time.timeScale = 0.4f;
            Quaternion shakeBase = head.rotation;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / 0.6f;
                head.rotation = shakeBase * Quaternion.Euler(Random.Range(-6f, 6f) * k, Random.Range(-6f, 6f) * k, Random.Range(-10f, 10f) * k);
                yield return null;
            }
            Time.timeScale = 1f;

            // Hauled off your feet: the view tips back toward the ceiling as it drags you out through the door.
            hand.localPosition = handRest;
            Director.flow.EnterRoom("Graveyard");
            Vector3 bodyFrom = body.transform.position, figFrom = figure.transform.position;
            Quaternion tipFrom = head.rotation;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                float k = t / 0.7f;
                figure.transform.position = Vector3.Lerp(figFrom, from, k);
                body.transform.position = Vector3.Lerp(bodyFrom, from - dir * 0.9f, k * 0.7f);
                head.rotation = Quaternion.Slerp(tipFrom, Quaternion.LookRotation(Vector3.up * 0.8f - dir) * Quaternion.Euler(0f, 0f, 25f), k);
                yield return null;
            }
        }

        static void LookAt(Transform head, Vector3 point, float speed)
        {
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(point - head.position), 1f - Mathf.Exp(-speed * Time.deltaTime));
        }

        IEnumerator Drop(GameObject gun, Vector3 at)
        {
            gun.SetActive(true);
            gun.transform.position = at;
            Vector3 v = new Vector3(Random.Range(-0.6f, 0.6f), 0.8f, Random.Range(0.4f, 1f));
            Vector3 spin = new Vector3(400f, 250f, 120f);
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                v.y -= 9.8f * Time.deltaTime;
                gun.transform.position += v * Time.deltaTime;
                gun.transform.Rotate(spin * Time.deltaTime, Space.World);
                if (gun.transform.position.y <= 0.03f)
                {
                    gun.transform.position = new Vector3(gun.transform.position.x, 0.03f, gun.transform.position.z);
                    gun.transform.rotation = Quaternion.Euler(0f, gun.transform.eulerAngles.y, 90f);
                    if (clatterClip != null) AudioSource.PlayClipAtPoint(clatterClip, gun.transform.position, 1f);
                    yield break;
                }
                yield return null;
            }
        }

        public override void Arrived(string cameFrom)
        {
            Director.startMessage = "THE MINERS' CHAPEL\n<size=10>the way up is through the far door</size>";
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            var gun = Director != null ? Director.Gun : null;
            foreach (var l in loot)
            {
                if (l.item == null || !l.item.activeSelf || Aim(eye, forward, l.point.position, 2f) < 0.9f) continue;
                // A second revolver is no use: its rounds go into the one you carry.
                string prompt = l.revolver && gun != null && gun.Owned ? "[E] take the rounds out of the old revolver" : l.prompt;
                var taken = l;
                return new Option { prompt = prompt, act = () => Take(taken), gesture = Gesture.Grab };
            }
            if (!_open && padlock != null && Aim(eye, forward, padlock.transform.position, 6f) > 0.97f)
            {
                if (gun == null || !gun.Owned) return new Option { prompt = "chained shut. a padlock the size of a fist" };
                if (gun.rounds <= 0) return new Option { prompt = "chained shut. no rounds left" };
                if (!gun.Drawn) return new Option { prompt = "[Q] draw the revolver" };
                return new Option { prompt = "[LMB] shoot the lock off" };
            }
            return null;
        }

        void Take(Loot l)
        {
            l.item.SetActive(false);
            if (takeClip != null) AudioSource.PlayClipAtPoint(takeClip, l.point.position, 0.8f);
            var gun = Director.Gun;
            string line = l.line;
            if (gun != null)
            {
                if (l.revolver && !gun.Owned) { gun.Give(l.rounds + _spare); _spare = 0; }
                else if (gun.Owned) gun.AddRounds(l.rounds);
                else { _spare += l.rounds; line = "a box of rounds. nothing to load them into yet"; }
            }
            if (!string.IsNullOrEmpty(line)) Say(line);
        }

        int _spare;   // rounds picked up before the revolver was

        // The lock flies apart, the chain rattles down, and somewhere above a bell answers.
        void Unlock()
        {
            if (_open) return;
            _open = true;
            _openedAt = Time.time;
            if (padlockPiece != null) padlockPiece.SetActive(false);
            if (chain != null) chain.SetActive(false);
            if (snapClip != null) AudioSource.PlayClipAtPoint(snapClip, padlock.transform.position, 1f);
            if (bellClip != null) AudioSource.PlayClipAtPoint(bellClip, padlock.transform.position + Vector3.up * 3f, 0.8f);
            Say("the padlock flies apart. the door swings out into the dark");
            RaiseSolved();
        }
    }
}
