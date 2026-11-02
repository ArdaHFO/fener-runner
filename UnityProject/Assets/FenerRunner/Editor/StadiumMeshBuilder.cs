using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FenerRunner.Editor
{
    // Stadium geometry is combined once; the fixed runtime pool reuses each module.
    internal sealed class StadiumMeshBuilder
    {
        private sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public readonly List<Vector3> normals = new List<Vector3>();
        }
        private readonly Dictionary<Material, Geometry> groups = new Dictionary<Material, Geometry>();
        private static readonly Vector3[] Cube =
        {
            new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f),
            new Vector3(.5f,-.5f,-.5f), new Vector3(-.5f,-.5f,-.5f), new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,.5f,-.5f),
            new Vector3(.5f,-.5f,.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(.5f,.5f,.5f),
            new Vector3(-.5f,-.5f,-.5f), new Vector3(-.5f,-.5f,.5f), new Vector3(-.5f,.5f,.5f), new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,-.5f,.5f), new Vector3(-.5f,-.5f,.5f)
        };
        private static readonly Vector3[] Normals =
            { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };

        public void Box(Material material, Vector3 position, Vector3 size, float yaw = 0f, float roll = 0f)
        {
            if (!groups.TryGetValue(material, out Geometry geometry))
            { geometry = new Geometry(); groups.Add(material, geometry); }
            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, roll), size);
            Quaternion rotation = Quaternion.Euler(0f, yaw, roll);
            for (int face = 0; face < 6; face++)
            {
                int start = geometry.vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    geometry.vertices.Add(matrix.MultiplyPoint3x4(Cube[face * 4 + corner]));
                    geometry.normals.Add(rotation * Normals[face]);
                }
                geometry.triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }
        private void Save(Transform root)
        {
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = "Stadium_" + pair.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(pair.Value.vertices);
                mesh.SetNormals(pair.Value.normals);
                mesh.SetTriangles(pair.Value.triangles, 0);
                mesh.RecalculateBounds();
                Mesh saved = RunnerSceneBuilder.Upsert(mesh, RunnerSceneBuilder.Generated + "/Meshes/" + mesh.name + ".asset");
                var go = new GameObject(saved.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = saved;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }
        public static GameObject Build(RunnerSettings settings, Dictionary<string, Material> materials, Font font)
        {
            var root = new GameObject("StadiumSegment");
            try
            {
            var b = new StadiumMeshBuilder();
            float length = settings.chunkLength;
            float width = settings.laneSpacing * 3f + 1.4f;
            var navy = materials["Navy"]; var yellow = materials["Yellow"]; var turf = materials["Turf"];
            var white = materials["Ivory"]; var pitch = materials["PitchStripe"]; var steel = materials["Steel"];
            b.Box(materials["Track"], new Vector3(0f, -.13f, length / 2f), new Vector3(width, .26f, length));
            b.Box(turf, new Vector3(0f, -.21f, length / 2f), new Vector3(112f, .2f, length));
            for (int n = 0; n < 4; n++)
                for (int side = -1; side <= 1; side += 2)
                    b.Box(pitch, new Vector3(side * 10.8f, -.104f, n * 10f + 2.5f), new Vector3(12f, .012f, 5f));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(yellow, new Vector3(side * (width / 2f + .1f), .006f, length / 2f), new Vector3(.13f, .012f, length));
                b.Box(white, new Vector3(side * 15.4f, -.09f, length / 2f), new Vector3(.1f, .018f, length));
                for (int tier = 0; tier < 4; tier++)
                {
                    float x = side * (21f + tier * 4f), y = 1.4f + tier * 2.55f;
                    b.Box(navy, new Vector3(x, y - .8f, length / 2f), new Vector3(4.4f, 1.6f, length));
                    for (int block = 0; block < 8; block++)
                    {
                        Material color = (block + tier) % 3 == 0 ? yellow : navy;
                        b.Box(color, new Vector3(x, y + .22f, block * 5f + 2.5f), new Vector3(3.7f, .7f, 4.5f));
                        // Inexpensive seat-row highlights read as a crowd from the follow camera.
                        for (int row = 0; row < 3; row++)
                            b.Box((block + row) % 2 == 0 ? white : yellow,
                                new Vector3(x + side * (row - 1) * .95f, y + .61f, block * 5f + 2.5f),
                                new Vector3(.1f, .05f, 4.1f));
                    }
                }
                b.Box(navy, new Vector3(side * 27f, 13.8f, length / 2f), new Vector3(23f, .48f, length));
                b.Box(yellow, new Vector3(side * 15.6f, 13.5f, length / 2f), new Vector3(.25f, .38f, length));
                b.Box(steel, new Vector3(side * 37f, 7f, 20f), new Vector3(.4f, 14f, .5f));
                b.Box(steel, new Vector3(side * 25f, 13.2f, 20f), new Vector3(23f, .18f, .22f));
                // Floodlight masts and flags stay well outside the playable corridor.
                b.Box(steel, new Vector3(side * 17f, 7.6f, 6f), new Vector3(.23f, 15.2f, .23f));
                b.Box(navy, new Vector3(side * 17f, 15f, 6f), new Vector3(4.3f, 1.9f, .35f));
                for (int col = 0; col < 6; col++)
                    for (int row = 0; row < 2; row++)
                        b.Box(materials["Lamp"], new Vector3(side * 17f - 1.7f + col * .68f, 14.6f + row * .72f, 5.79f),
                            new Vector3(.5f, .48f, .055f));
                b.Box(steel, new Vector3(side * 11f, 3.6f, 29f), new Vector3(.07f, 7.2f, .07f));
                b.Box(yellow, new Vector3(side * 11f + .9f, 6.6f, 29f), new Vector3(1.8f, .65f, .045f), 10f);
                b.Box(navy, new Vector3(side * 11f + .9f, 5.95f, 29f), new Vector3(1.8f, .65f, .045f), 10f);
                b.Box(navy, new Vector3(side * 18.9f, 1.8f, 20f), new Vector3(.22f, 2f, 34f));
                TextMesh sign = WorldText(root.transform, font, "FENERBAHÇE", new Vector3(side * 18.72f, 2.1f, 20f), side * 90f, .32f, Color.white);
                sign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                WorldText(root.transform, font, "KADIKÖY  /  1907", new Vector3(side * 18.7f, 1.2f, 20f), side * 90f, .13f, new Color(1f, .83f, .2f));
            }
            for (int lane = -1; lane <= 1; lane += 2)
                for (int dash = 0; dash < 10; dash++)
                    b.Box(white, new Vector3(lane * settings.laneSpacing * .5f, .008f, dash * 4f + 1f), new Vector3(.055f, .016f, 1.8f));
            // Football touchline details belong to the field beside the runner.
            b.Box(white, new Vector3(10f, -.085f, 20f), new Vector3(10f, .02f, .1f));
            for(int side=-1;side<=1;side+=2)
            {
                float edge=width*.5f;
                for(int row=0;row<4;row++) b.Box(navy,new Vector3(side*(edge+3.4f+row*1.2f),.15f+row*.72f,20),new Vector3(1.3f,.8f,length));
                b.Box(navy,new Vector3(side*(edge+2.4f),.45f,20),new Vector3(.18f,.9f,length));
                b.Box(yellow,new Vector3(side*(edge+2.38f),.95f,20),new Vector3(.2f,.08f,length));
                // Keep glyph planes in front of the yellow rail's inner face
                // (edge+2.28), including after floating-origin depth changes.
                WorldText(root.transform,font,"YAŞA FENERBAHÇE",new Vector3(side*(edge+2.25f),.55f,14),side*90,.18f,new Color(1,.84f,.1f));
                WorldText(root.transform,font,"1907  •  SARI LACİVERT",new Vector3(side*(edge+2.25f),.55f,32),side*90,.15f,Color.white);
            }
            b.Save(root.transform);
            root.AddComponent<WorldSignFont>();
            SupporterBuilder.Build(root.transform,settings);
            var ground = root.AddComponent<BoxCollider>();
            ground.center = new Vector3(0f, -.15f, length * .5f);
            ground.size = new Vector3(width, .3f, length);
            string path = RunnerSceneBuilder.Generated + "/Prefabs/StadiumSegment.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            return prefab;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }
        private static TextMesh WorldText(Transform parent, Font font, string text, Vector3 position, float yaw, float scale, Color color)
        {
            var go = new GameObject(text, typeof(TextMesh));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var label = go.GetComponent<TextMesh>();
            label.font = font; label.fontSize = 72; label.characterSize = scale;
            label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = CharacterSurfaceBuilder.WorldSignMaterial(font);
            return label;
        }
    }
}
