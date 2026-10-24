using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // The player's right arm, from a rigged PSX first-person arms model (Assets/ThirdParty/Arms, the right arm only, in
    // its rest pose): posed bone by bone round whatever the hand holds (the Zippo, the revolver's grip), baked into a
    // plain mesh in the holder's own space, and put on PSX/Lit with the same warm glow as the skin it replaced.
    public static partial class PSXRoomBuilder
    {
        const string ArmsModel = "Assets/ThirdParty/Arms/arms_right.fbx";
        // Bare skin; Assets/ThirdParty/Arms/arms_gloves_01.png is the same arm in fingerless leather gloves.
        const string ArmsTexture = "Assets/ThirdParty/Arms/arms_01.png";

        // How the arm holds something. The hand's frame: +Z out along the straight fingers, +Y out of the back of the
        // hand (so the thumb side is -X).
        struct ArmPose
        {
            public Vector3 along, back;     // that frame before the wrist bends, in the holder's space: the forearm's line
            public Vector3 wrist;           // the hand bent on the forearm, degrees about the frame's X, Y, Z
            public Vector3 knuckle;         // where the middle finger's knuckle lands, in the holder's space
            public float scale;
            public Vector3[] curl;          // index, middle, ring, pinky: how far each of the three joints flexes
            public Vector3 thumb;           // the thumb's base turned (its own axes), then its two joints flexed
            public float thumbBend, thumbTip;
        }

        // Round the Zippo's case (see BuildPlayer): palm behind it, fingers along its back and round its left edge
        // onto the front with the nails showing, thumb over the top to the wheel, forearm away down to the right.
        static readonly ArmPose LighterGrip = new ArmPose
        {
            along = new Vector3(-0.75f, 0.66f, 0f), back = Vector3.forward, wrist = new Vector3(0f, 40f, 0f),
            knuckle = new Vector3(0f, 0.02f, -0.008f), scale = 0.75f,
            curl = new[] { new Vector3(10f, 85f, 70f), new Vector3(10f, 85f, 70f), new Vector3(15f, 85f, 70f), new Vector3(25f, 85f, 70f) },
            thumb = new Vector3(-25f, 0f, 0f), thumbBend = 20f, thumbTip = 30f
        };

        // Round the revolver's grip (see RevolverModel, in the gun's space): palm on the right panel, three fingers
        // round the front strap, the index finger up by the trigger, the thumb up the back strap toward the hammer,
        // forearm back along the gun's line and out to the lower right.
        static readonly ArmPose RevolverGrip = new ArmPose
        {
            along = new Vector3(-0.22f, 0.15f, 1f), back = Vector3.right, wrist = new Vector3(0f, -40f, 0f),
            knuckle = new Vector3(0.025f, -0.004f, -0.034f), scale = 0.625f,
            curl = new[] { new Vector3(20f, 40f, 20f), new Vector3(60f, 70f, 40f), new Vector3(60f, 70f, 40f), new Vector3(60f, 70f, 40f) },
            thumb = new Vector3(0f, 0f, -10f)
        };

        static void MakeArmsMaterial(Shader lit)
        {
            if (AssetImporter.GetAtPath(ArmsTexture) is TextureImporter importer && (importer.filterMode != FilterMode.Point || importer.mipmapEnabled))
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            // The hand sits closer to the flame than any light could sensibly reach, so its warmth is emission.
            Lit("Arms", lit, AssetDatabase.LoadAssetAtPath<Texture2D>(ArmsTexture), new Color(0.15f, 0.08f, 0.04f));
        }

        // The arm posed and baked as "Hand" under the holder (no offset of its own, so HandAnimator can squeeze it).
        static GameObject Arm(ArmPose pose, Transform holder)
        {
            var inst = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ArmsModel));
            inst.transform.position = Vector3.zero;
            var bones = new Dictionary<string, Transform>();
            foreach (var t in inst.GetComponentsInChildren<Transform>()) bones[t.name] = t;

            // The hand's frame in the rest pose, from where its knuckles are.
            Vector3 wrist = bones["hand.R"].position;
            Vector3 fingers = (bones["f_middle.01.R"].position - wrist).normalized;
            Vector3 side = bones["f_index.01.R"].position - bones["f_pinky.01.R"].position;
            side = (side - Vector3.Dot(side, fingers) * fingers).normalized;
            var rest = Quaternion.LookRotation(fingers, Vector3.Cross(side, fingers));

            var hand = bones["hand.R"];
            hand.rotation = rest * Quaternion.Euler(pose.wrist) * Quaternion.Inverse(rest) * hand.rotation;
            string[] names = { "f_index", "f_middle", "f_ring", "f_pinky" };
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 3; j++)
                    bones[$"{names[i]}.0{j + 1}.R"].localRotation *= Quaternion.Euler(pose.curl[i][j], 0f, 0f);
            bones["thumb.01.R"].localRotation *= Quaternion.Euler(pose.thumb);
            bones["thumb.02.R"].localRotation *= Quaternion.Euler(pose.thumbBend, 0f, 0f);
            bones["thumb.03.R"].localRotation *= Quaternion.Euler(pose.thumbTip, 0f, 0f);

            var skin = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            var posed = new Mesh();
            skin.BakeMesh(posed);
            // Skin space -> the hand's rest frame -> the holder: turned to the pose, scaled, knuckle on its mark.
            var toRest = Matrix4x4.TRS(wrist, rest, Vector3.one).inverse;
            Vector3 knuckle = toRest.MultiplyPoint3x4(bones["f_middle.01.R"].position);
            var turn = Quaternion.LookRotation(pose.along, pose.back);
            var toHolder = Matrix4x4.TRS(pose.knuckle - turn * (knuckle * pose.scale), turn, Vector3.one * pose.scale)
                           * toRest * Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            Object.DestroyImmediate(inst);

            var verts = posed.vertices;
            var normals = posed.normals;
            var colors = new Color[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = toHolder.MultiplyPoint3x4(verts[i]);
                normals[i] = toHolder.MultiplyVector(normals[i]).normalized;
                // The same baked top light as the other models.
                float k = Mathf.Clamp(0.74f + 0.26f * normals[i].y + 0.1f * Mathf.Abs(normals[i].x), 0.45f, 1f);
                colors[i] = new Color(k, k, k, 1f);
            }
            var mesh = new Mesh { name = "Arm" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, posed.uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(posed.triangles, 0);
            mesh.RecalculateBounds();
            Object.DestroyImmediate(posed);
            AssetDatabase.CreateAsset(mesh, $"{_meshFolder}/{_meshId++:000}_Arm.asset");

            var go = new GameObject("Hand");
            go.transform.SetParent(holder, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mats["Arms"];
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
