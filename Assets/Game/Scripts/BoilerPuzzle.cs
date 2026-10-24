using System.Collections;
using UnityEngine;

namespace GJ.Game
{
    // Room 4, the boiler room. The door's bolt is drawn back by a steam piston fed from a pot on a stand. Fill the
    // pot, shut the relief valve and leave your lighter burning under it: it takes longer than your air to boil, but
    // the room doesn't need you for that. The pressure climbs, the piston pulls, the door swings.
    // The other way: the bolt's magnetic coil has its own line, cut, its live end hanging over two tanks joined by a
    // pipe. Fill the tanks, plug the line in and drop the live end in the water: the current runs through to the coil
    // and the bolt snaps back. It runs up the wire through you as well.
    public class BoilerPuzzle : RoomPuzzle
    {
        public Transform feedValve;            // water to the pot; spins about its local Y
        public Transform potLevel;             // sight-glass column beside the pot, scaled along Y
        public Transform burnerPoint;          // the grate under the pot
        public GameObject leftLighter;         // the Zippo standing there, burning
        public Transform reliefValve;          // on the steam line; spins about its local Y
        public ParticleSystem potSteam, reliefSteam;
        public AudioSource potHiss, reliefHiss;
        public Transform gaugeNeedle;          // turns about its local Z with the pressure
        public Transform pistonRod;
        public Vector3 rodTravel = new Vector3(-0.25f, 0f, 0f);
        public Transform bolt;
        public Vector3 boltTravel = new Vector3(-0.25f, 0f, 0f);
        public float fillSeconds = 4f;
        public float boilSeconds = 45f;        // a full pot over the lighter takes this long to boil
        public float pressureSeconds = 30f;    // then this long to build enough pressure to draw the bolt

        public Transform tankTap;              // water to the two tanks
        public GameObject tankStream;
        public Transform tankWaterA, tankWaterB;   // surfaces, rising as they fill
        public float tankLow = 0.05f, tankHigh = 0.5f;
        public Transform socketPoint;
        public GameObject plugLoose, plugIn;
        public Transform liveEnd;              // the cut line's bare end, hanging over tank B
        public GameObject liveEndDropped;      // the same end lying in the tank
        public Transform waterTip;             // where it went in: the water keeps spitting there
        public GameObject liveSparks;          // spits while the line is plugged in and hanging dry
        public GameObject coilGlow;            // the coil lit while current runs through it

        public AudioClip valveClip, clunkClip, plugClip, zapClip, takeClip, boilClip;

        bool _feedOpen, _reliefOpen = true, _lighterHere, _tapOpen, _plugged, _dropped, _solved, _busy;
        float _water, _heat, _pressure, _tanks;
        bool _saidBoil, _saidVent, _saidDry;
        Vector3 _rodRest, _boltRest;

        bool Boiling => _heat >= 1f;
        // A flame under water, and nowhere for the steam to go but the piston.
        bool WillBoil => _lighterHere && (_water > 0.5f || _feedOpen);
        public override bool Working => !_solved && WillBoil && !_reliefOpen;

        public override Vector3? Focus => Working ? (Boiling ? gaugeNeedle.position : burnerPoint.position + Vector3.up * 0.4f) : (Vector3?)null;

        public override string FailReason =>
            _solved ? null :
            !_lighterHere && _water > 0.5f ? "the pot was full, but there was no flame under it" :
            !_lighterHere ? "nothing in here got hot enough to move the bolt" :
            _water <= 0.5f && !_feedOpen ? "the pot was dry. the flame only blackened it" :
            _reliefOpen ? "the steam blew out of the open relief valve" : null;

        void Awake()
        {
            _rodRest = pistonRod.localPosition;
            _boltRest = bolt.localPosition;
            leftLighter.SetActive(false);
            plugIn.SetActive(false);
            liveEndDropped.SetActive(false);
            if (coilGlow != null) coilGlow.SetActive(false);
            if (liveSparks != null) liveSparks.SetActive(false);
            Steam(potSteam, potHiss, false, 0f);
            Steam(reliefSteam, reliefHiss, false, 0f);
            UpdateVisuals();
        }

        public override Option? Look(Vector3 eye, Vector3 forward, bool lit)
        {
            if (_busy) return null;
            Option? best = null;
            float bestAim = 0.86f;
            void Consider(Vector3 point, float range, Option option)
            {
                float aim = Aim(eye, forward, point, range);
                if (aim <= bestAim) return;
                bestAim = aim;
                best = option;
            }

            Consider(feedValve.position, 1.6f, new Option
            {
                prompt = _feedOpen ? "[E] shut the water to the pot" : "[E] open the water to the pot",
                act = () => StartCoroutine(Turn(feedValve, () => _feedOpen = !_feedOpen))
            });
            Consider(reliefValve.position, 1.6f, new Option
            {
                prompt = _reliefOpen ? "[E] close the relief valve" : "[E] open the relief valve",
                act = () => StartCoroutine(Turn(reliefValve, () => _reliefOpen = !_reliefOpen))
            });
            if (!Director.LighterLeft)
                Consider(burnerPoint.position, 1.7f, lit
                    ? new Option { prompt = "[E] leave the lighter burning under the pot", reach = true, act = LeaveLighter }
                    : new Option { prompt = "[F] open the lighter" });
            else if (_lighterHere)
                Consider(burnerPoint.position, 1.7f, new Option { prompt = "[E] take the lighter back", act = TakeLighter, gesture = Gesture.Grab });

            Consider(tankTap.position, 1.6f, new Option
            {
                prompt = _tapOpen ? "[E] shut the tap" : "[E] open the tap over the tanks",
                act = () => StartCoroutine(Turn(tankTap, () => _tapOpen = !_tapOpen))
            });
            if (!_solved)
            {
                Consider(socketPoint.position, 1.6f, new Option
                {
                    prompt = _plugged ? "[E] pull the plug" : "[E] plug the cut line in",
                    act = TogglePlug,
                    gesture = Gesture.Grab
                });
                if (!_dropped)
                    Consider(liveEnd.position, 2.0f,
                        !_plugged ? new Option { prompt = "the end of the cut line. dead: it isn't plugged in" } :
                        _tanks < 0.6f ? new Option { prompt = "the live end sparks over an empty tank" } :
                        new Option { prompt = "[E] drop the live end into the water", reach = true, act = DropLiveEnd });
            }
            return best;
        }

        IEnumerator Turn(Transform wheel, System.Action flip)
        {
            _busy = true;
            Play(valveClip, 0.8f, wheel.position);
            Quaternion start = wheel.localRotation;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                wheel.localRotation = start * Quaternion.Euler(0f, 300f * Mathf.SmoothStep(0f, 1f, t / 0.7f), 0f);
                yield return null;
            }
            wheel.localRotation = start;
            flip();
            _busy = false;
        }

        void LeaveLighter()
        {
            _lighterHere = true;
            leftLighter.SetActive(true);
            Director.LeaveLighter(true);
            Play(clunkClip, 0.4f, burnerPoint.position);
            Say(_water > 0.5f || _feedOpen ? "the flame licks the bottom of the pot\nit's dark without it"
                                           : "the flame licks the bottom of an empty pot");
        }

        void TakeLighter()
        {
            _lighterHere = false;
            leftLighter.SetActive(false);
            Director.LeaveLighter(false);
            Play(takeClip, 0.5f, burnerPoint.position);
        }

        void TogglePlug()
        {
            _plugged = !_plugged;
            plugLoose.SetActive(!_plugged);
            plugIn.SetActive(_plugged);
            if (liveSparks != null) liveSparks.SetActive(_plugged);
            Play(plugClip, 0.8f, socketPoint.position);
            if (_plugged) Say("the cut end hanging over the tanks starts to spit");
        }

        // Into the water with it: through the tanks and the pipe to the coil, and back up the wire through the hand
        // still holding it.
        void DropLiveEnd()
        {
            _dropped = true;
            _solved = true;
            liveEnd.gameObject.SetActive(false);
            liveEndDropped.SetActive(true);
            // The line is still live: it keeps fizzing where it went into the water.
            if (liveSparks != null && waterTip != null)
            {
                liveSparks.SetActive(false);
                liveSparks.transform.position = waterTip.position;
                liveSparks.SetActive(true);
            }
            if (coilGlow != null) coilGlow.SetActive(true);
            Play(zapClip, 1f, liveEnd.position);
            Play(clunkClip, 1f, bolt.position);
            bolt.localPosition = _boltRest + boltTravel;
            RaiseSolved();
            Director.Kill("the current ran through the water to the lock.\nand up the wire, through you.");
        }

        void Update()
        {
            if (_feedOpen) _water = Mathf.Min(1f, _water + Time.deltaTime / fillSeconds);
            if (_tapOpen) _tanks = Mathf.Min(1f, _tanks + Time.deltaTime / fillSeconds);

            if (_lighterHere && _water > 0.5f) _heat = Mathf.Min(1f, _heat + Time.deltaTime / boilSeconds);
            else if (!_lighterHere) _heat = Mathf.Max(0f, _heat - Time.deltaTime / (boilSeconds * 2f));

            // Boiling: with the relief shut the pressure builds on the piston; open, it all screams out of the valve.
            if (Boiling && !_reliefOpen && !_solved) _pressure = Mathf.Min(1f, _pressure + Time.deltaTime / pressureSeconds);
            else if (_pressure > 0f && !_solved) _pressure = Mathf.Max(0f, _pressure - Time.deltaTime / 3f);

            if (Boiling && !_saidBoil) { _saidBoil = true; Say("the pot is boiling"); }
            if (Boiling && _reliefOpen && !_saidVent) { _saidVent = true; Say("the steam is blowing out of the relief valve"); }
            if (_lighterHere && _water <= 0.5f && !_feedOpen && !_saidDry) { _saidDry = true; Say("the pot is dry"); }

            if (!_solved && _pressure >= 1f)
            {
                _solved = true;
                Play(clunkClip, 1f, bolt.position);
                RaiseSolved();
            }
            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            var level = potLevel.localScale;
            level.y = Mathf.Max(0.02f, _water * (1f - _heat * 0.15f));
            potLevel.localScale = level;
            if (tankStream != null) tankStream.SetActive(_tapOpen && _tanks < 1f);
            tankWaterA.gameObject.SetActive(_tanks > 0.02f);
            tankWaterB.gameObject.SetActive(_tanks > 0.02f);
            float h = Mathf.Lerp(tankLow, tankHigh, _tanks);
            tankWaterA.localPosition = new Vector3(tankWaterA.localPosition.x, h, tankWaterA.localPosition.z);
            tankWaterB.localPosition = new Vector3(tankWaterB.localPosition.x, h, tankWaterB.localPosition.z);

            gaugeNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(120f, -120f, _pressure) + (Boiling ? Mathf.Sin(Time.time * 23f) * 2f : 0f));
            // The piston only starts to move with most of the pressure behind it; the bolt goes with it.
            float push = Mathf.Clamp01((_pressure - 0.6f) / 0.4f);
            pistonRod.localPosition = _rodRest + rodTravel * push;
            if (!_dropped) bolt.localPosition = _boltRest + boltTravel * push;

            Steam(potSteam, potHiss, _heat > 0.6f && _lighterHere, Boiling ? 0.35f : 0.12f);
            Steam(reliefSteam, reliefHiss, Boiling && _reliefOpen, 0.6f);
        }

        static void Steam(ParticleSystem ps, AudioSource hiss, bool on, float volume)
        {
            if (ps != null)
            {
                if (on && !ps.isEmitting) ps.Play();
                if (!on && ps.isEmitting) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (hiss != null)
            {
                hiss.volume = Mathf.MoveTowards(hiss.volume, on ? volume : 0f, Time.deltaTime);
                if (hiss.volume > 0f && !hiss.isPlaying) hiss.Play();
                if (hiss.volume <= 0f && hiss.isPlaying) hiss.Stop();
            }
        }

        static void Play(AudioClip clip, float volume, Vector3 at)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
        }
    }
}
