using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // The player's right arm, from a rigged PSX first-person arms model (Assets/ThirdParty/Arms, the right arm only, in
    // its rest pose): posed bone by bone round whatever the hand holds (the Zippo, the revolver's grip) and left as a
    // live skinned rig under the holder, so the fingers and thumb can still move (HandAnimator, Revolver). On PSX/Lit
    // with the same warm glow as the skin it replaced.
    public static partial class PSXRoomBuilder
    {
        const string ArmsModel = "Assets/ThirdParty/Arms/arms_right.fbx";
        // Bare skin; Assets/ThirdParty/Arms/arms_gloves_01.png is the same arm in fingerless leather gloves.
        const string ArmsTexture = "Assets/ThirdParty/Arms/arms_01.png";

        // How the arm holds something. The hand's frame: +Z out along the straight fingers, +Y out of the back of the
        // hand (so the thumb side is -X).
        struct ArmPose
        {
            public Vector3 along, back;     // the hand's frame in the holder's space
            public Vector3 swing;           // the forearm swung away from the hand's line (degrees about the frame's
                                            // X, Y, Z), the wrist bending to keep the hand where it is
            public Vector3 wrist;           // the hand bent on the forearm, degrees about the frame's X, Y, Z
            public Vector3 knuckle;         // where the middle finger's knuckle lands, in the holder's space
            public float scale;
            public Vector3[] curl;          // index, middle, ring, pinky: how far each of the three joints flexes
            public Vector3 thumb;           // the thumb's base turned (its own axes), then its two joints flexed
            public float thumbBend, thumbTip;
        }

        // What the posed arm exposes for animation.
        struct ArmRig
        {
            public Transform root;
            public Transform[] fingers;     // index, middle, ring, pinky, three joints each, knuckle first
            public Transform[] thumb;       // base, middle, tip
        }

        // A loose fist round the Zippo (see BuildPlayer), seen from the thumb side: the fingers round the body below the
        // lid, onto the face, the thumb up by the wheel, the forearm away down to the lower right. Found by searching for a
        // grip that touches the case without going into it.
        static readonly ArmPose LighterGrip = new ArmPose
        {
            along = new Vector3(-1f, 0.25f, -0.2f), back = new Vector3(-0.1f, 0.15f, 1f), swing = new Vector3(-15f, -35f, 0f),
            knuckle = new Vector3(0.005f, 0.016f, 0.015f), scale = 0.8f,
            curl = new[] { new Vector3(13f, 72f, 36f), new Vector3(17f, 72f, 36f), new Vector3(21f, 72f, 36f), new Vector3(25f, 72f, 36f) },
            thumb = new Vector3(-20f, 0f, 0f), thumbBend = 25f, thumbTip = 30f
        };

        // Round the revolver's grip (see RevolverModel, in the gun's space): palm on the right panel, three fingers
        // round the front strap, the index finger up by the trigger, the thumb up the back strap toward the hammer,
        // forearm back along the gun's line and out to the lower right.
        static readonly ArmPose RevolverGrip = new ArmPose
        {
            along = new Vector3(-0.22f, 0.15f, 1f), back = Vector3.right, wrist = new Vector3(0f, -40f, 0f),
            knuckle = new Vector3(0.029f, 0.018f, -0.034f), scale = 0.625f,
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

        // The arm posed as "Hand" under the holder.
        static ArmRig Arm(ArmPose pose, Transform holder)
        {
            var inst = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ArmsModel));
            inst.name = "Hand";
            inst.transform.position = Vector3.zero;
            var bones = new Dictionary<string, Transform>();
            foreach (var t in inst.GetComponentsInChildren<Transform>()) bones[t.name] = t;

            // The hand's frame in the rest pose, from where its knuckles are.
            Vector3 wrist = bones["hand.R"].position;
            Vector3 fingers = (bones["f_middle.01.R"].position - wrist).normalized;
            Vector3 side = bones["f_index.01.R"].position - bones["f_pinky.01.R"].position;
            side = (side - Vector3.Dot(side, fingers) * fingers).normalized;
            var rest = Quaternion.LookRotation(fingers, Vector3.Cross(side, fingers));

            var swing = Quaternion.Euler(pose.swing);
            var hand = bones["hand.R"];
            hand.rotation = rest * Quaternion.Inverse(swing) * Quaternion.Euler(pose.wrist) * Quaternion.Inverse(rest) * hand.rotation;
            string[] names = { "f_index", "f_middle", "f_ring", "f_pinky" };
            var fingerBones = new Transform[12];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 3; j++)
                {
                    var b = bones[$"{names[i]}.0{j + 1}.R"];
                    b.localRotation *= Quaternion.Euler(pose.curl[i][j], 0f, 0f);
                    fingerBones[i * 3 + j] = b;
                }
            bones["thumb.01.R"].localRotation *= Quaternion.Euler(pose.thumb);
            bones["thumb.02.R"].localRotation *= Quaternion.Euler(pose.thumbBend, 0f, 0f);
            bones["thumb.03.R"].localRotation *= Quaternion.Euler(pose.thumbTip, 0f, 0f);

            // The whole rig into the holder: turned to the pose, scaled, the middle knuckle on its mark. With the rig at
            // the origin that is TRS(mark, turn, scale) * TRS(wrist, rest)^-1, which folds into one transform.
            var toRest = Matrix4x4.TRS(wrist, rest, Vector3.one).inverse;
            Vector3 knuckle = toRest.MultiplyPoint3x4(bones["f_middle.01.R"].position);
            var turn = Quaternion.LookRotation(pose.along, pose.back) * swing;
            var spin = turn * Quaternion.Inverse(rest);
            inst.transform.SetParent(holder, false);
            inst.transform.localRotation = spin;
            inst.transform.localScale = Vector3.one * pose.scale;
            inst.transform.localPosition = pose.knuckle - turn * (knuckle * pose.scale) - spin * (wrist * pose.scale);

            // Its own copy of the mesh with vertex colours (PSX/Lit multiplies by them), on the rooms' material.
            var skin = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = Object.Instantiate(skin.sharedMesh);
            mesh.name = "Arm";
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0.9f, 0.9f, 0.9f, 1f);
            mesh.colors = colors;
            AssetDatabase.CreateAsset(mesh, $"{_meshFolder}/{_meshId++:000}_Arm.asset");
            skin.sharedMesh = mesh;
            skin.sharedMaterial = Mats["Arms"];
            skin.shadowCastingMode = ShadowCastingMode.Off;
            // Right in front of the camera and scaled down: never let it be culled on a stale bounding box.
            skin.updateWhenOffscreen = true;

            return new ArmRig
            {
                root = inst.transform,
                fingers = fingerBones,
                thumb = new[] { bones["thumb.01.R"], bones["thumb.02.R"], bones["thumb.03.R"] }
            };
        }
    }
}
