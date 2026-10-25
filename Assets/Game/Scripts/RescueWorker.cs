using UnityEngine;

namespace GJ.Game
{
    // One of the mine rescue crew who find you at the cemetery gate: orange coveralls, white hard hat with a lamp on it.
    // A handful of poses baked by the builder (standing, two walking steps, kneeling), swapped as it moves.
    public class RescueWorker : MonoBehaviour
    {
        public GameObject stand, stepA, stepB, kneel;
        public Light lamp;                 // the hard hat's lamp, moved to where the head is in each pose
        public Transform lampKneel;        // where it sits when kneeling (standing it is where the builder put it)

        Vector3 _lampStand;

        void Awake()
        {
            if (lamp != null) _lampStand = lamp.transform.localPosition;
            Show(stand);
        }

        public void Show(GameObject pose)
        {
            foreach (var p in new[] { stand, stepA, stepB, kneel }) if (p != null) p.SetActive(p == pose);
            if (lamp != null) lamp.transform.localPosition = pose == kneel && lampKneel != null ? lampKneel.localPosition : _lampStand;
        }

        // Walking: the two steps in turn, a little bounce.
        public void Step(float phase)
        {
            Show(Mathf.Repeat(phase, 1f) < 0.5f ? stepA : stepB);
        }

        // The lamp's beam onto a point (the face of whoever lies there).
        public void ShineAt(Vector3 point)
        {
            if (lamp != null) lamp.transform.rotation = Quaternion.LookRotation(point - lamp.transform.position);
        }
    }
}
