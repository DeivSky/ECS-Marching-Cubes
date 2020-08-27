using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MarchingCubes
{
    public class MarchingCubesAuthoring : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        public NoiseReference noiseRef;
        public GameObject chunkPrefab;

        [Range(10f, 200f)]
        public float ViewDistance = 60f;
        [Range(2f, 100f)]
        public float BoundsSize = 20f;
        [Range(0.001f, 0.999f)]
        public float Surface = 0.5f;
        [Range(2, 100)]
        public int Resolution = 8;


        //////////////////////////////////
        /// Entity conversion workflow ///
        //////////////////////////////////
        public void DeclareReferencedPrefabs(List<GameObject> prefabs) => prefabs.Add(chunkPrefab);

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new MarchingCubesData
            {
                Terrain = new MarchingTerrain
                {
                    BoundsSize = BoundsSize,
                    Resolution = Resolution,
                    Surface = Surface,
                    ViewDistance = ViewDistance
                },
                Noise = noiseRef.Value,
                ChunkPrefab = conversionSystem.GetPrimaryEntity(chunkPrefab)
            });
        }

#if UNITY_EDITOR
        //////////////////////////
        /// Scene-view testing ///
        //////////////////////////
        [Header("Test")]
        public bool updateInEdit;
        public bool generate = false;
        public Material material;
        public Vector3Int chunksAxis = new Vector3Int(2, 1, 2);
        private BlobAssetReference<MarchingTables> tables;
        private float4[] cube = new float4[8];
        private Mesh[] meshes;
        private Vector3Int chunksPerAxis = default;

        private void Awake() => EditorApplication.update += DestroyChunkHolder;

		private Noise noise = new Noise
        {
            Seed = 6,
            Octaves = 6,
            Lacunarity = 2f,
            Persistence = 0.52f,
            Scale = 2.99f,
            Weight = 6.09f,
            FloorOffset = 5.19f,
            WeightMultiplier = 3.61f,
            HardFloorHeight = -2.84f,
            HardFloorWeight = 3.05f
        };
        private MarchingTerrain terrain = default;

        private void OnValidate()
        {
            EditorApplication.update -= Validate;
            EditorApplication.update += Validate;
        }

		private MarchingTerrain ParamsToTerrainStruct() => new MarchingTerrain
        {
            BoundsSize = BoundsSize,
            Resolution = Resolution,
            Surface = Surface,
            ViewDistance = ViewDistance
        };

        private void Validate()
		{
            if (chunksAxis.x < 0)
                chunksAxis.x = 0;
            if (chunksAxis.y < 0)
                chunksAxis.y = 0;
            if (chunksAxis.z < 0)
                chunksAxis.z = 0;

            if (generate)
            {
                generate = false;
                EditorApplication.update += DestroyChunkHolder;
                EditorApplication.update += Generate;
                return;
            }

			if (updateInEdit && !EditorApplication.isPlaying)
			{
                if(noise != noiseRef.Value || terrain != ParamsToTerrainStruct() || chunksPerAxis != chunksAxis)
				{
                    EditorApplication.update += DestroyChunkHolder;
                    EditorApplication.update += Generate;
                }
			}
        }

        private void Generate()
		{
            EditorApplication.update -= Generate;
            noise = noiseRef.Value;
            terrain = ParamsToTerrainStruct();
            chunksPerAxis = chunksAxis;

            var chunkHolder = new GameObject("ChunkHolder").transform;

            meshes = new Mesh[(chunksAxis.x * 2 + 1) * (chunksAxis.y * 2 + 1) * (chunksAxis.z * 2 + 1)];
            for (int i = 0; i < meshes.Length; i++)
            {
                var go = new GameObject($"Chunk {i}");
                go.layer = LayerMask.NameToLayer("Terrain");
                go.tag = "Terrain";
                go.transform.SetParent(chunkHolder);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = new Mesh();
                meshes[i] = filter.sharedMesh;
                meshes[i].indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            tables = MarchingTables.CreateBlobAssetReference(Unity.Collections.Allocator.Temp);

            int idx = 0;
            Vector3Int xyz = Vector3Int.zero;
            for (xyz.x = -chunksAxis.x; xyz.x <= chunksAxis.x; xyz.x++)
                for (xyz.y = -chunksAxis.y; xyz.y <= chunksAxis.y; xyz.y++)
                    for (xyz.z = -chunksAxis.z; xyz.z <= chunksAxis.z; xyz.z++)
                        March(int3(xyz.x, xyz.y, xyz.z), meshes[idx++]);

            tables.Dispose();
        }

        private void March(int3 coord, Mesh mesh)
        {
            float4[] points = new float4[Resolution * Resolution * Resolution];
            float spacing = BoundsSize / (Resolution - 1);
            float3 center = (float3)coord * BoundsSize;

            int3 xyz = 0;
            for (xyz.x = 0; xyz.x < Resolution; xyz.x++)
                for (xyz.y = 0; xyz.y < Resolution; xyz.y++)
                    for (xyz.z = 0; xyz.z < Resolution; xyz.z++)
                    {
                        float3 pos = center + (float3)xyz * spacing - BoundsSize / 2f;
                        float f = noise.Generate(pos);
                        int i = xyz.x * Resolution * Resolution + xyz.y * Resolution + xyz.z;
                        points[i] = new float4(pos, f);
                    }

            List<float3x3> trianglePoints = new List<float3x3>();
            for (xyz.x = 0; xyz.x < Resolution - 1; xyz.x++)
                for (xyz.y = 0; xyz.y < Resolution - 1; xyz.y++)
                    for (xyz.z = 0; xyz.z < Resolution - 1; xyz.z++)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            int3 corner = xyz + tables.Value.CubeCorners[i];
                            cube[i] = points[corner.x * Resolution * Resolution + corner.y * Resolution + corner.z];
                        }

                        int triangulationIndex = 0;
                        for (int i = 0; i < 8; i++)
                            if (cube[i].w > Surface)
                                triangulationIndex |= 1 << i;

                        triangulationIndex *= 16;
                        for (int i = 0; tables.Value.TriangulationTable[triangulationIndex + i] != -1; i += 3)
                        {
                            int2 edgeA = tables.Value.EdgeConnections[tables.Value.TriangulationTable[triangulationIndex + i]];
                            int2 edgeB = tables.Value.EdgeConnections[tables.Value.TriangulationTable[triangulationIndex + i + 1]];
                            int2 edgeC = tables.Value.EdgeConnections[tables.Value.TriangulationTable[triangulationIndex + i + 2]];

                            float3x3 triangle = new float3x3
                            {
                                c0 = interpolate(cube[edgeA.x], cube[edgeA.y]),
                                c1 = interpolate(cube[edgeB.x], cube[edgeB.y]),
                                c2 = interpolate(cube[edgeC.x], cube[edgeC.y])
                            };

                            trianglePoints.Add(triangle);
                        }
                    }

            mesh.Clear();
            if (trianglePoints.Count == 0)
                return;

            Vector3[] vertices = new Vector3[trianglePoints.Count * 3];
            int[] triangles = new int[trianglePoints.Count * 3];

            for (int i = 0; i < trianglePoints.Count; i++)
                for (int j = 0; j < 3; j++)
                {
                    int idx = i * 3 + j;
                    triangles[idx] = idx;
                    vertices[idx] = trianglePoints[i][j];
                }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }

        float3 interpolate(float4 a, float4 b)
        {
            return new float3(a.x, a.y, a.z) + (Surface - a.w) / (b.w - a.w) * (new float3(b.x, b.y, b.z) - new float3(a.x, a.y, a.z));
        }

        private void DestroyChunkHolder()
        {
            EditorApplication.update -= DestroyChunkHolder;
            DestroyImmediate(GameObject.Find("ChunkHolder"));
		}
    }
#endif
}
