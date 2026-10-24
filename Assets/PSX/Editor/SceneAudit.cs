using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GJ.PSX.EditorTools
{
    // Checks the built rooms for things that look broken: missing meshes, props hanging in the air, props tipped over
    // that shouldn't be, and surfaces with no texture. Writes Logs/audit.txt.
    public static class SceneAudit
    {
        static readonly string[] Scenes = { "Assets/Scenes/Hub.unity", "Assets/Scenes/Room01.unity", "Assets/Scenes/Room02.unity", "Assets/Scenes/Room03.unity", "Assets/Scenes/Room04.unity", "Assets/Scenes/Chapel.unity", "Assets/Scenes/Graveyard.unity" };
        static readonly string[] Groups = { "Props", "Details", "Dressing", "Kenney", "Pipes", "Cistern", "Armory", "Boiler", "Corridor", "Nave", "Yard" };

        // Props meant to lean or lie on their side, and things meant to hang from the ceiling or sit on a wall.
        static readonly string[] Tilted = { "Chair", "chair", "tool-", "Helmet", "Bottle", "bottle", "Sack", "Tin", "Lid", "Rubble", "Mop", "Crowbar", "Chute", "Trough" };
        static readonly string[] Hanging = { "Lamp", "Halo", "Cable", "Pulley", "Chain", "Hopper", "Weight", "Counterweight", "Pipe", "Clamp", "Sign", "Note", "Calendar",
                                             "Poster", "Notice", "Photo", "Clock", "Coat", "Breaker", "FirstAid", "Vent", "Rack", "Shelf", "Display", "Bolt", "Guide",
                                             "Keeper", "Spout", "Valve", "Wheel", "Stream", "Tube", "Fluorescent", "Target", "Gauge", "Conduit", "Hasp", "Hook", "Rod",
                                             "Shutter", "Crank", "Leak", "Sparks", "Rat", "Housing", "Lever", "Cord", "Bulb", "Window", "Flap",
                                             "Piston", "Coil", "Winding", "Needle", "Tap", "Socket", "Plug", "Lead", "Line", "Bracket", "LiveEnd",
                                             "Relief", "Feed", "Wire", "Glow", "Lighter", "Stack", "Dial", "Cage" };

        [MenuItem("GJ/Debug/Audit Rooms")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path);
                Physics.SyncTransforms();
                sb.AppendLine("##### " + path);
                int missing = 0;
                foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (mf.sharedMesh == null) { missing++; if (missing <= 10) sb.AppendLine("MISSING MESH " + Path(mf.transform)); }
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (r.sharedMaterial == null) sb.AppendLine("MISSING MATERIAL " + Path(r.transform));
                    else if (r.sharedMaterial.HasProperty("_BaseMap") && r.sharedMaterial.GetTexture("_BaseMap") == null) sb.AppendLine("NO TEXTURE " + Path(r.transform) + " " + r.sharedMaterial.name);
                sb.AppendLine($"missing meshes: {missing}");

                // The scene's root by name ("Hub" is also what the door wheels' centres are called).
                string rootName = System.IO.Path.GetFileNameWithoutExtension(path);
                var world = System.Array.Find(scene.GetRootGameObjects(), g => g.name == rootName)?.transform;
                if (world == null) continue;
                // Indoors the shell's floor; out in the graveyard the ground.
                var floorT = world.Find("Shell/Floor") ?? world.Find("Yard/Ground");
                if (floorT == null) continue;
                var floor = floorT.GetComponent<Renderer>().bounds;
                foreach (var g in Groups)
                {
                    var group = world.Find(g);
                    if (group == null) continue;
                    foreach (Transform t in group) Check(t, floor, sb);
                }
            }
            File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/audit.txt"), sb.ToString());
            Debug.Log("Audit written to Logs/audit.txt");
        }

        static void Check(Transform t, Bounds room, StringBuilder sb)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            string name = t.name;

            // Tipped over when it shouldn't be.
            float tilt = Vector3.Angle(t.up, Vector3.up);
            if (tilt > 12f && !Matches(name, Tilted) && !Matches(name, Hanging)) sb.AppendLine($"TILT  {Path(t)} {tilt:F0} deg @ {t.position:F2}");

            if (Matches(name, Hanging) || b.max.y > 3.0f) return;
            // On a wall? Then it may hang there.
            float wallGap = Mathf.Min(Mathf.Min(b.min.x - room.min.x, room.max.x - b.max.x), Mathf.Min(b.min.z - room.min.z, room.max.z - b.max.z));
            if (wallGap < 0.06f && b.min.y > 0.3f) return;
            if (b.min.y < 0.03f) return;     // on the floor

            // Something under it? Cast down from its centre and four inset corners, ignoring its own colliders.
            var own = new HashSet<Collider>(t.GetComponentsInChildren<Collider>());
            float best = float.MaxValue;
            foreach (var off in new[] { Vector2.zero, new Vector2(-0.35f, -0.35f), new Vector2(0.35f, -0.35f), new Vector2(-0.35f, 0.35f), new Vector2(0.35f, 0.35f) })
            {
                var o = new Vector3(b.center.x + b.extents.x * off.x, b.min.y + 0.02f, b.center.z + b.extents.z * off.y);
                foreach (var hit in Physics.RaycastAll(o, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (own.Contains(hit.collider)) continue;
                    best = Mathf.Min(best, hit.distance - 0.02f);
                }
            }
            if (best > 0.05f) sb.AppendLine($"FLOAT {Path(t)} gap={(best == float.MaxValue ? -1 : best):F2} bottom={b.min.y:F2} @ {t.position:F2}");
        }

        static bool Matches(string name, string[] keys)
        {
            foreach (var k in keys) if (name.Contains(k)) return true;
            return false;
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
