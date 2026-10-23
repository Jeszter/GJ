using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GJ.PSX.EditorTools
{
    // Ready-made CC0 models (Kenney's Survival Kit and Furniture Kit, in Assets/ThirdParty/Kenney) baked into the
    // rooms' PS1 look: each model is re-centred on its base, scaled to a real-world height, given the same baked
    // top-light vertex shading as the builder's own meshes, and put on PSX/Lit. The furniture's flat colours are swapped
    // for the rooms' own pixel textures (box-projected UVs); the survival kit keeps its palette texture, point-filtered.
    public static partial class PSXRoomBuilder
    {
        const string KenneyRoot = "Assets/ThirdParty/Kenney";

        // Furniture Kit material name -> one of the rooms' materials.
        static readonly Dictionary<string, string> KenneyMaterials = new Dictionary<string, string>
        {
            ["wood"] = "Wood", ["woodDark"] = "Crate", ["carpet"] = "Blanket", ["carpetDarker"] = "Blanket",
            ["carpetWhite"] = "Mattress", ["metal"] = "Steel", ["metalLight"] = "Chrome", ["metalMedium"] = "Steel",
            ["metalDark"] = "DarkMetal", ["_defaultMat"] = "Steel", ["lamp"] = "Bulb", ["plant"] = "PaintGreen",
            ["glass"] = "Glass", ["fur"] = "Burlap", ["colormap"] = "KenneyPalette"
        };

        static Texture2D _palette;

        // The survival kit paints everything from one palette of bright flat colours. Swap each colour for the rooms'
        // own textured material of the same family, so the models get the same grime as everything else.
        static string PaletteMaterial(Vector2 uv)
        {
            if (_palette == null) return "KenneyPalette";
            Color c = _palette.GetPixelBilinear(uv.x, uv.y);
            Color.RGBToHSV(c, out float h, out float sat, out float val);
            if (sat < 0.35f) return val > 0.8f ? "Paper" : val > 0.45f ? "Steel" : "DarkMetal";
            if (h < 0.02f || h > 0.95f) return sat > 0.6f ? "RedPaint" : "Crate";
            if (h < 0.14f) return val > 0.55f ? "Wood" : "Crate";
            if (h < 0.18f) return "Helmet";
            if (h < 0.45f) return "PaintGreen";
            if (h < 0.75f) return "PaintBlue";
            return "RedPaint";
        }

        static void MakeModelMaterials(Shader lit)
        {
            string texPath = KenneyRoot + "/Survival/Textures/colormap.png";
            if (AssetImporter.GetAtPath(texPath) is TextureImporter importer &&
                (importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !importer.isReadable))
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            _palette = tex;
            if (tex == null) return;
            var m = new Material(lit);
            m.SetTexture("_BaseMap", tex);
            // Grimier than the bright originals.
            m.SetColor("_BaseColor", new Color(0.72f, 0.68f, 0.62f));
            m.SetColor("_EmissionColor", Color.black);
            SaveMaterial("KenneyPalette", m);
        }

        // Places a Kenney model (file relative to KenneyRoot, without .fbx) standing on pos, turned by yaw, leaning by
        // tilt (degrees about its own X), scaled so it is "height" metres tall.
        static GameObject Model(string file, Vector3 pos, float yaw, float height, Transform parent, float tilt = 0f,
                                Dictionary<string, string> overrides = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyRoot}/{file}.fbx");
            if (prefab == null) { Debug.LogWarning("Missing model " + file); return null; }
            var inst = (GameObject)Object.Instantiate(prefab);
            inst.transform.position = Vector3.zero;

            // Gather every triangle by target material, in the model root's space.
            var groups = new Dictionary<string, List<(Vector3 p, Vector3 n, Vector2 uv)>>();
            var bounds = new Bounds();
            bool first = true;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mesh == null || mr == null) continue;
                var toRoot = mf.transform.localToWorldMatrix;
                var verts = mesh.vertices;
                var normals = mesh.normals;
                var uvs = mesh.uv;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var src = sub < mr.sharedMaterials.Length && mr.sharedMaterials[sub] != null ? mr.sharedMaterials[sub].name : "_defaultMat";
                    if ((overrides == null || !overrides.TryGetValue(src, out var target)) && !KenneyMaterials.TryGetValue(src, out target)) target = "Steel";
                    if (!groups.TryGetValue(target, out var list)) groups[target] = list = new List<(Vector3, Vector3, Vector2)>();
                    var tris = mesh.GetTriangles(sub);
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        var dest = list;
                        if (target == "KenneyPalette" && uvs.Length > 0)
                        {
                            // One colour per triangle, read at its centre.
                            Vector2 mid = (uvs[tris[t]] + uvs[tris[t + 1]] + uvs[tris[t + 2]]) / 3f;
                            string m = PaletteMaterial(mid);
                            if (!groups.TryGetValue(m, out dest)) groups[m] = dest = new List<(Vector3, Vector3, Vector2)>();
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            int i = tris[t + k];
                            Vector3 p = toRoot.MultiplyPoint3x4(verts[i]);
                            Vector3 n = normals.Length > i ? toRoot.MultiplyVector(normals[i]).normalized : Vector3.up;
                            dest.Add((p, n, uvs.Length > i ? uvs[i] : Vector2.zero));
                            if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                            else bounds.Encapsulate(p);
                        }
                    }
                }
            }
            Object.DestroyImmediate(inst);
            if (first) return null;

            float scale = height / Mathf.Max(0.001f, bounds.size.y);
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var v = new List<Vector3>();
            var nrm = new List<Vector3>();
            var uv = new List<Vector2>();
            var col = new List<Color>();
            var subs = new List<List<int>>();
            var mats = new List<Material>();
            foreach (var g in groups)
            {
                if (g.Value.Count == 0) continue;
                var tris = new List<int>();
                bool palette = g.Key == "KenneyPalette";
                foreach (var (p0, n, uv0) in g.Value)
                {
                    Vector3 p = (p0 - origin) * scale;
                    tris.Add(v.Count);
                    v.Add(p);
                    nrm.Add(n);
                    // The rooms' textures are laid on in metres, projected along the face's main axis.
                    Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                    Vector2 box = a.x >= a.y && a.x >= a.z ? new Vector2(p.z, p.y) : a.y >= a.z ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
                    uv.Add(palette ? uv0 : box * 2f);
                    float s = Mathf.Clamp(0.74f + 0.26f * n.y + 0.1f * Mathf.Abs(n.x), 0.45f, 1f);
                    col.Add(new Color(s, s, s, 1f));
                }
                subs.Add(tris);
                mats.Add(Mats.TryGetValue(g.Key, out var m) ? m : Mats["Steel"]);
            }

            var baked = new Mesh { name = file.Replace('/', '_') };
            if (v.Count > 65000) baked.indexFormat = IndexFormat.UInt32;
            baked.SetVertices(v);
            baked.SetNormals(nrm);
            baked.SetUVs(0, uv);
            baked.SetColors(col);
            baked.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) baked.SetTriangles(subs[i], i);
            baked.RecalculateBounds();
            AssetDatabase.CreateAsset(baked, $"{_meshFolder}/{_meshId++:000}_K_{baked.name}.asset");

            var go = new GameObject(System.IO.Path.GetFileName(file));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = baked;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats.ToArray();
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // The vintage TV: dark glass for a screen, a brown cabinet.
        static readonly Dictionary<string, string> TvLook = new Dictionary<string, string>
        {
            ["metal"] = "Display", ["metalMedium"] = "DarkMetal", ["wood"] = "Bakelite"
        };

        static void Models1()
        {
            var k = new GameObject("Kenney").transform;
            k.SetParent(_world, false);
            // An old TV on a crate in the back corner, and the dead man's footlocker.
            Model("Survival/box-large", new Vector3(-1.75f, 0f, 0.32f), 90f, 0.42f, k);
            Model("Furniture/televisionVintage", new Vector3(-1.75f, 0.42f, 0.3f), 180f, 0.36f, k, 0f, TvLook);
            Model("Survival/chest", new Vector3(-2.6f, 0f, 2.5f), 90f, 0.42f, k);
            // Tools leaning on the left wall, a barrel and a bin by the door.
            Model("Survival/tool-pickaxe", new Vector3(-2.88f, 0f, 1.12f), 90f, 0.85f, k, -14f);
            Model("Survival/tool-shovel", new Vector3(-2.9f, 0f, 1.32f), 90f, 1.0f, k, -10f);
            Model("Survival/barrel", new Vector3(1.62f, 0f, 7.15f), 20f, 0.8f, k);
            Model("Furniture/trashcan", new Vector3(1.42f, 0f, 7.7f), 0f, 0.5f, k);
            Model("Survival/box-open", new Vector3(1.45f, 0f, 6.35f), -25f, 0.36f, k);
        }

        static void Models2()
        {
            var k = new GameObject("Kenney").transform;
            k.SetParent(_world, false);
            // At the foot of the cot, a footlocker; a bin and a stool by the table.
            Model("Survival/chest", new Vector3(2.9f, 0f, 3.85f), -90f, 0.45f, k);
            Model("Furniture/trashcan", new Vector3(2.05f, 0f, 3.95f), 0f, 0.5f, k);
            // Tools against the back wall, barrels and boxes by the door.
            Model("Survival/tool-pickaxe", new Vector3(-0.85f, 0f, 0.1f), 0f, 0.85f, k, -14f);
            Model("Survival/tool-shovel", new Vector3(-0.6f, 0f, 0.1f), 0f, 1.0f, k, -12f);
            Model("Survival/barrel", new Vector3(1.4f, 0f, 6.95f), 0f, 0.8f, k);
            Model("Survival/barrel-open", new Vector3(1.75f, 0f, 6.35f), 40f, 0.8f, k);
            Model("Survival/box-large", new Vector3(-2.0f, 0f, 6.1f), 5f, 0.4f, k);
            Model("Survival/box", new Vector3(-2.05f, 0.4f, 6.1f), 30f, 0.3f, k);
            Model("Furniture/radio", new Vector3(-3.18f, 1.565f, 5.8f), 90f, 0.18f, k);
        }
    }
}
