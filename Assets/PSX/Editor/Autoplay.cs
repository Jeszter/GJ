using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GJ.PSX.EditorTools
{
    // Plays a room's solution by itself in Play mode (no keyboard needed) and writes what happened to a log:
    // the puzzle's steps, then the player's air is cut, then it waits for the door to open and the player to come back.
    // Fire, water and candles run ten times faster so a test takes seconds, not minutes.
    public static class Autoplay
    {
        static string LogPath => System.IO.Path.Combine(Application.dataPath, "../Logs/autoplay.txt");
        static readonly StringBuilder Log = new StringBuilder();
        static double _start, _next;
        static int _step;
        static Game.RoomDirector _director;
        static Game.RoomPuzzle _puzzle;

        [MenuItem("GJ/Debug/Autoplay Room")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Autoplay: enter Play mode first"); return; }
            Log.Clear();
            // Keep the game running at full speed while the editor window is in the background (this run only).
            Application.runInBackground = true;
            _director = UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>();
            _puzzle = _director.puzzle;
            var flow = UnityEngine.Object.FindFirstObjectByType<Game.GameFlow>();
            // Skip the title straight into play.
            Call(flow, "BeginPlay", false);
            Write($"scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, puzzle {_puzzle.GetType().Name}");
            _start = EditorApplication.timeSinceStartup;
            _next = _start + 1.0;
            _step = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        // Hands the player the revolver and checks that a shot from the eye toward each target reaches it.
        [MenuItem("GJ/Debug/Probe Revolver")]
        public static void ProbeRevolver()
        {
            if (!EditorApplication.isPlaying) return;
            Log.Clear();
            Application.runInBackground = true;
            var p = UnityEngine.Object.FindFirstObjectByType<Game.HopperPuzzle>();
            Call(p, "TakeRevolver");
            var eye = Camera.main.transform.position;
            foreach (var (name, target) in new[] { ("bolt hopper", p.boltHopperBody.position), ("vent hopper", p.ventHopperBody.position), ("lamp", p.lampLight.transform.position + Vector3.up * 0.15f) })
            {
                var ok = Physics.Raycast(eye, (target - eye).normalized, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore);
                var s = ok ? hit.collider.GetComponentInParent<Game.Shootable>() : null;
                Write($"{name}: hit {(ok ? hit.collider.name + " under " + hit.collider.transform.parent?.name : "nothing")} -> shootable {(s != null ? s.name : "none")}");
            }
            File.WriteAllText(LogPath, Log.ToString());
        }

        [MenuItem("GJ/Debug/Probe View")]
        public static void ProbeView()
        {
            Log.Clear();
            var cam = Camera.main;
            Write($"main cam {cam?.name} enabled={cam?.enabled} target={cam?.targetTexture?.name} pos={cam?.transform.position} fwd={cam?.transform.forward}");
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Write($"camera {c.name} enabled={c.enabled} depth={c.depth} target={c.targetTexture?.name} mask={c.cullingMask}");
            var flow = UnityEngine.Object.FindFirstObjectByType<Game.GameFlow>();
            var black = (UnityEngine.UI.Image)Get(flow, "_black");
            Write($"flow state={Get(flow, "_state")} black={black.color} crt={Shader.GetGlobalFloat("_PsxCrt")} tvOff={Shader.GetGlobalFloat("_PsxTvOff")}");
            var d = UnityEngine.Object.FindFirstObjectByType<Game.RoomDirector>();
            Write($"director phase={Get(d, "_phase")} fade={((UnityEngine.UI.Image)Get(d, "_fade")).color} enabled={d.enabled} timeScale={Time.timeScale}");
            File.WriteAllText(LogPath, Log.ToString());
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish("stopped"); return; }
            double now = EditorApplication.timeSinceStartup;
            if (now - _start > 150) { Finish("TIMEOUT phase=" + Get(_director, "_phase") + " door open=" + _director.door.IsOpen); return; }
            // An unfocused editor barely ticks Play mode; keep frames coming.
            EditorApplication.QueuePlayerLoopUpdate();
            if (now < _next) return;
            _next = now + 0.5;

            switch (_puzzle)
            {
                case Game.FusePuzzle fuse: StepFuse(fuse); break;
                case Game.CisternPuzzle cistern: StepCistern(cistern); break;
                default: StepGeneric(); break;
            }
        }

        static void StepFuse(Game.FusePuzzle p)
        {
            switch (_step)
            {
                case 0:
                    Write("bucket at " + p.bucket.position);
                    p.ShoveBucket();
                    Write("shoved bucket to " + p.bucket.position);
                    _step++;
                    break;
                case 1:
                    p.fuse.speed *= 10f;
                    Write("bucket now at " + p.bucket.position + "; ignite: " + p.fuse.Ignite() + ", fuse length " + p.fuse.Length.ToString("F2"));
                    _step++;
                    break;
                case 2:
                    Set(_director, "_air", 1f);
                    _step++;
                    break;
                default:
                    Watch("fuse " + p.fuse.Current + " burnt " + p.fuse.Burnt.ToString("F2"));
                    break;
            }
        }

        static void StepCistern(Game.CisternPuzzle p)
        {
            switch (_step)
            {
                case 0:
                    p.tankSeconds /= 10f; p.fillSeconds /= 10f;
                    Set(p, "_tank", p.tankSeconds);
                    Call(p, "Hook");
                    p.StartCoroutine((System.Collections.IEnumerator)Call(p, "SwingChute"));
                    Write("hooked chain, swinging chute");
                    _step++;
                    break;
                case 1:
                    p.StartCoroutine((System.Collections.IEnumerator)Call(p, "TurnValve"));
                    Write("turning valve");
                    _step++;
                    break;
                case 2:
                    Set(_director, "_air", 1f);
                    _step++;
                    break;
                default:
                    Watch("working " + p.Working + " fill " + ((float)Get(p, "_fill")).ToString("F1") + "/" + p.fillSeconds.ToString("F1") +
                          " tank " + ((float)Get(p, "_tank")).ToString("F1") + " valve " + Get(p, "_valveOpen") + " onBucket " + Get(p, "_onBucket") +
                          " busyChute " + Get(p, "_busyChute") + " hooked " + Get(p, "_hooked"));
                    break;
            }
        }

        // A puzzle with its own scripted solution (room 3 and later) exposes Autosolve().
        static void StepGeneric()
        {
            if (_step == 0)
            {
                var m = _puzzle.GetType().GetMethod("Autosolve", BindingFlags.Public | BindingFlags.Instance);
                if (m == null) { Finish("no Autosolve on " + _puzzle.GetType().Name); return; }
                m.Invoke(_puzzle, null);
                Write("autosolve started");
                _step++;
            }
            else if (_step < 4) _step++;
            else if (_step == 4) { Set(_director, "_air", 1f); _step++; }
            else
            {
                var sand = _puzzle.GetType().GetField("_boltSand", BindingFlags.Instance | BindingFlags.NonPublic);
                Watch("working " + _puzzle.Working + (sand != null ? " sand " + ((float)sand.GetValue(_puzzle)).ToString("F2") + " enabled " + _puzzle.isActiveAndEnabled : ""));
            }
        }

        static void Watch(string state)
        {
            string phase = Get(_director, "_phase").ToString();
            Write($"t={EditorApplication.timeSinceStartup - _start:F1} frame={Time.frameCount} scale={Time.timeScale} paused={EditorApplication.isPaused} air={Get(_director, "_air")} phase={phase} door opening={_director.door.IsOpening} open={_director.door.IsOpen} {state}");
            if (_director.door.IsOpen && phase == "Alive") Finish("PASS: door open and the player is back");
            else if (phase == "Reviving" && !_director.door.IsOpen && !_director.door.IsOpening && !_puzzle.Working) Finish("FAIL: one more time (nothing left working)");
        }

        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            Write(result);
            File.WriteAllText(LogPath, Log.ToString());
        }

        static void Write(string line) { Log.AppendLine(line); File.WriteAllText(LogPath, Log.ToString()); }

        static object Call(object o, string method, params object[] args) =>
            o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, args);

        static object Get(object o, string field) =>
            o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);

        static void Set(object o, string field, object value) =>
            o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(o, value);
    }
}
