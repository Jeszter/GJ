using System.Collections.Generic;
using UnityEngine;

namespace GJ.Game
{
    // You, from an earlier try at this room, still lying where you fell. Laid out along local +Z, head first, on its
    // back; its chest and hips weigh down whatever they lie on (a pressure plate).
    public class Corpse : MonoBehaviour
    {
        public static readonly List<Corpse> All = new List<Corpse>();

        // Chest and hips, in local space: what presses down.
        public Vector3[] weightPoints = { new Vector3(0f, 0.1f, -0.35f), new Vector3(0f, 0.1f, -0.75f) };

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool LiesOn(PressurePlate plate)
        {
            foreach (var p in weightPoints)
                if (plate.Covers(transform.TransformPoint(p))) return true;
            return false;
        }

        // Down where the body dropped: the torso on the spot it stood on, head the way it was facing. Turned round the
        // torso until it fits between the walls and whatever stands on the floor.
        public void LayDown(Vector3 at, float yaw, float roll)
        {
            Vector3 torso = new Vector3(at.x, 0f, at.z);
            float best = yaw;
            for (int i = 0; i < 8; i++)
            {
                float y = yaw + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 45f;
                Vector3 dir = Quaternion.Euler(0f, y, 0f) * Vector3.forward;
                Vector3 head = torso + dir * 0.5f + Vector3.up * 0.28f;
                Vector3 feet = torso - dir * 1.05f + Vector3.up * 0.28f;
                if (!Physics.CheckCapsule(head, feet, 0.16f, ~0, QueryTriggerInteraction.Ignore)) { best = y; break; }
            }
            Vector3 along = Quaternion.Euler(0f, best, 0f) * Vector3.forward;
            transform.SetPositionAndRotation(torso + along * 0.55f, Quaternion.Euler(0f, best, roll));
        }
    }
}
