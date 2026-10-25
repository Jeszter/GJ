using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GJ.PSX.EditorTools
{
    // The main menu, a scene of its own and first in the build: room 1 once more (a copy of its built world, sharing
    // its meshes, with nothing left in it to solve) as the dark room the title drifts through, the story's little sets
    // built below it, and the TV with no room to play (GameFlow without a director).
    public static partial class PSXRoomBuilder
    {
        const string MenuPath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("GJ/Build Main Menu")]
        public static void BuildMainMenu()
        {
            if (!BeginBuild()) return;
            BuildMenu();
            FinishBuild();
        }

        static void BuildMenu()
        {
            if (!File.Exists(ScenePath)) { Debug.LogWarning("Build room 1 first: the main menu is set in it."); return; }
            var room = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var source = GameObject.Find("Room01");

            _meshFolder = $"{Root}/Meshes/MainMenu";
            AssetDatabase.DeleteAsset(_meshFolder);
            Directory.CreateDirectory(_meshFolder);
            AssetDatabase.Refresh();
            _meshId = 0;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var copy = Object.Instantiate(source);
            copy.name = "Room01";
            SceneManager.MoveGameObjectToScene(copy, scene);
            // Nothing to solve or read in here.
            foreach (var p in copy.GetComponentsInChildren<Game.RoomPuzzle>(true)) Object.DestroyImmediate(p);
            foreach (var n in copy.GetComponentsInChildren<Game.ReadableNote>(true)) Object.DestroyImmediate(n);
            EditorSceneManager.CloseScene(room, true);
            _world = copy.transform;

            // A bare eye for the title to drift with: no body, no hands.
            var head = new GameObject("MenuCamera").transform;
            head.position = new Vector3(0f, 1.55f, 0.8f);
            MakeEye(head.gameObject);
            var flow = BuildFlow(head);
            flow.room = 0;
            flow.shotPositions = new[] { new Vector3(-0.4f, 0.35f, 1.2f), new Vector3(2.3f, 2.6f, 0.6f), new Vector3(-1.6f, 1.5f, 5.6f) };
            flow.shotAngles = new[] { new Vector3(-6f, 8f, 0f), new Vector3(24f, -32f, 0f), new Vector3(8f, 40f, 0f) };
            flow.stage = BuildStoryStage();

            SetupRenderSettings();
            // Behind the title the room is seen through the tube's scanlines and vignette and a dark veil: lit as it
            // is for play it reads as black. Turn its lamps and the ambient up for the menu's copy only.
            foreach (var l in copy.GetComponentsInChildren<Light>(true)) l.intensity *= 1.7f;
            RenderSettings.ambientLight *= 2.2f;
            RenderSettings.fogEndDistance *= 1.5f;
            EditorSceneManager.SaveScene(scene, MenuPath);
            Debug.Log("PSX main menu built: " + MenuPath);
        }
    }
}
