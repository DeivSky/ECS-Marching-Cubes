using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using static MathUtils;
using static Unity.Mathematics.math;

namespace MarchingCubes
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class MarchingCubesSystem : SystemBase
    {
        private EntityCommandBufferSystem ecbSystem;
        private BlobAssetReference<MarchingTables> marchingTablesPtr;
        private EntityQuery query;

        protected override void OnCreate()
        {
            base.OnCreate();
            ecbSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            marchingTablesPtr = MarchingTables.CreateBlobAssetReference();
            var disabledDirtyChunks = new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<MarchingChunk>(),
                    ComponentType.ReadOnly<DirtyChunkTag>(),
                    ComponentType.ReadOnly<Disabled>()
                }
            };

            var enabledDirtyChunks = new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<MarchingChunk>(),
                    ComponentType.ReadOnly<DirtyChunkTag>(),
                },
                None = new ComponentType[] { ComponentType.ReadOnly<Disabled>() }
            };

            query = GetEntityQuery(disabledDirtyChunks, enabledDirtyChunks);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            marchingTablesPtr.Dispose();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var entity = GetSingletonEntity<MarchingCubesData>();
			var data = EntityManager.GetComponentData<MarchingCubesData>(entity);
            int maxChunks = (int)pow(ceil(data.Terrain.ViewDistance / data.Terrain.BoundsSize) * 2 + 1, 3);
            _ = MarchingUtilities.CreateChunks(data.ChunkPrefab, maxChunks, EntityManager);
        }

        protected override void OnUpdate()
        {
            var entity = GetSingletonEntity<MarchingCubesData>();
            var data = EntityManager.GetComponentData<MarchingCubesData>(entity);
            int pointsCount = (int)pow(data.Terrain.Resolution + 1, 3);
            
            int entityCount = query.CalculateEntityCount();
            if (entityCount == 0)
                return;

            var entities = query.ToEntityArray(Allocator.TempJob);
            var chunks = GetComponentDataFromEntity<MarchingChunk>(true);
            var pointsArray = new NativeArray<float4>[entityCount];
            NativeArray<float3> offsets = new NativeArray<float3>(data.Noise.Octaves, Allocator.TempJob);

            var offsetsHandle = Job.WithCode(() =>
            {
                const float offsetRange = 1000;
                var rng = new Unity.Mathematics.Random(data.Noise.Seed);
                for (int i = 0; i < data.Noise.Octaves; i++)
				    offsets[i] = float3((float)mul(rng.NextDouble(), 2f) - 1f, (float)mul(rng.NextDouble(), 2f) - 1f, (float)mul(rng.NextDouble(), 2f) - 1f) * offsetRange;
            }).Schedule(default);

            var handles = new NativeArray<JobHandle>(entityCount, Allocator.Temp);
            for (int i = 0; i < entityCount; i++)
            {
                pointsArray[i] = new NativeArray<float4>(pointsCount, Allocator.TempJob);

                var pointsJob = new PointsJob
                {
                    Points = pointsArray[i],
                    Terrain = data.Terrain,
                    Chunk = chunks[entities[i]]
                };

                var noiseJob = new NoiseJob
                {
                    Points = pointsArray[i],
                    Offsets = offsets.AsReadOnly(),
                    Noise = data.Noise
                };

                handles[i] = pointsJob.Schedule(offsetsHandle);
				handles[i] = noiseJob.Schedule(pointsCount, 32, handles[i]);
			}

            offsets.Dispose(JobHandle.CombineDependencies(handles));

            int maxTrisCount = mul((int)pow(data.Terrain.Resolution - 1, 3), 15);
            for (int i = 0; i < entityCount; i++)
            {
                var ecb = ecbSystem.CreateCommandBuffer();
                var vertices = new NativeList<Vertex>(maxTrisCount, Allocator.TempJob);
                var triangles = new NativeList<Triangle>(maxTrisCount, Allocator.TempJob);
                var triJob = new TriangulationJob
                {
                    Points = pointsArray[i].AsReadOnly(),
                    Vertices = vertices,
                    Triangles = triangles,
                    Terrain = data.Terrain,
                    Tables = marchingTablesPtr
                };

                var setMeshJob = new SetMeshBuffersJob
                {
                    Vertices = vertices.AsDeferredJobArray(),
                    Triangles = triangles.AsDeferredJobArray(),
                    Entity = entities[i],
                    Ecb = ecb
                };

                handles[i] = triJob.Schedule(handles[i]);
                pointsArray[i].Dispose(handles[i]);
                handles[i] = setMeshJob.Schedule(handles[i]);
                vertices.Dispose(handles[i]);
                triangles.Dispose(handles[i]);

                ecbSystem.AddJobHandleForProducer(handles[i]);
            }

            Dependency = JobHandle.CombineDependencies(handles);
            entities.Dispose(Dependency);
            handles.Dispose();
        }

        [BurstCompile]
        public struct PointsJob : IJob
        {
            public NativeArray<float4> Points;
            public MarchingTerrain Terrain;
            public MarchingChunk Chunk;

            public void Execute()
            {
                float spacing = Terrain.BoundsSize / (Terrain.Resolution - 1);
                float3 center = (float3)Chunk.Coordinate * Terrain.BoundsSize;
                int3 xyz = 0;

                for (xyz.x = 0; xyz.x < Terrain.Resolution; xyz.x++)
                    for (xyz.y = 0; xyz.y < Terrain.Resolution; xyz.y++)
                        for (xyz.z = 0; xyz.z < Terrain.Resolution; xyz.z++)
                        {
                            float3 pos = center + (float3)xyz * spacing - Terrain.BoundsSize / 2f;
                            int i = IndexFromCoord(xyz, Terrain.Resolution);
                            Points[i] = float4(pos, 0);
                        }
            }
        }

        [BurstCompile]
        public struct NoiseJob : IJobParallelFor
        {
            public NativeArray<float4> Points;
            public NativeArray<float3>.ReadOnly Offsets;
            public Noise Noise;

            public void Execute(int index)
            {
                float4 point = Points[index];

                float noiseValue = 0f;
                float frequency = Noise.Scale / 100f;
                float amplitude = 1f;
                float weight = 1f;

                for (int i = 0; i < Noise.Octaves; i++)
                {
                    float n = noise.snoise(point.xyz * frequency + Offsets[i] + Noise.Offset);
                    float v = 1 - abs(n);
                    v = mul(pow(v, 2), weight);
                    weight = max(min(v * Noise.WeightMultiplier, 1f), 0f);
                    noiseValue += mul(v, amplitude);
                    amplitude *= Noise.Persistence;
                    frequency *= Noise.Lacunarity;
                }

                noiseValue = -(point.y + Noise.FloorOffset) + mul(noiseValue, Noise.Weight) + (point.y % Noise.TerraceHeight) * Noise.TerraceWeight;
                if (point.y < Noise.HardFloorHeight)
                    noiseValue += Noise.HardFloorWeight;

                point.w = noiseValue;
                Points[index] = point;
            }
        }

        [BurstCompile]
        public struct TriangulationJob : IJob
        {
            [ReadOnly] public NativeArray<float4>.ReadOnly Points;
            public NativeList<Vertex> Vertices;
            public NativeList<Triangle> Triangles;
            public MarchingTerrain Terrain;
            public BlobAssetReference<MarchingTables> Tables;

            public void Execute()
            {
                var cube = new NativeArray<float4>(8, Allocator.Temp);
                var vertices = new NativeArray<Vertex>(3, Allocator.Temp);
                var triangles = new NativeArray<Triangle>(3, Allocator.Temp);
                int3 xyz = 0;
                int tris = 0;
                for (xyz.x = 0; xyz.x < Terrain.Resolution - 1; xyz.x++)
                    for (xyz.y = 0; xyz.y < Terrain.Resolution - 1; xyz.y++)
                        for (xyz.z = 0; xyz.z < Terrain.Resolution - 1; xyz.z++)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                int3 corner = xyz + Tables.Value.CubeCorners[i];
                                cube[i] = Points[IndexFromCoord(corner, Terrain.Resolution)];
                            }

                            int triangulationIndex = 0;
                            for (int i = 0; i < 8; i++)
                                if (cube[i].w > Terrain.Surface)
                                    triangulationIndex |= 1 << i;

                            triangulationIndex *= 16;
                            for (int i = 0; Tables.Value.TriangulationTable[triangulationIndex + i] != -1; i += 3)
                            {
                                int2 edgeA = Tables.Value.EdgeConnections[Tables.Value.TriangulationTable[triangulationIndex + i]];
                                int2 edgeB = Tables.Value.EdgeConnections[Tables.Value.TriangulationTable[triangulationIndex + i + 1]];
                                int2 edgeC = Tables.Value.EdgeConnections[Tables.Value.TriangulationTable[triangulationIndex + i + 2]];

                                vertices[0] = Interpolate(cube[edgeA.x], cube[edgeA.y], Terrain.Surface);
                                vertices[1] = Interpolate(cube[edgeB.x], cube[edgeB.y], Terrain.Surface);
                                vertices[2] = Interpolate(cube[edgeC.x], cube[edgeC.y], Terrain.Surface);

                                triangles[0] = tris++;
                                triangles[1] = tris++;
                                triangles[2] = tris++;

                                Vertices.AddRange(vertices);
                                Triangles.AddRange(triangles);
                            }
                        }

                cube.Dispose();
                vertices.Dispose();
                triangles.Dispose();
            }
        }

        [BurstCompile]
        public struct SetMeshBuffersJob : IJob
        {
            [ReadOnly] public NativeArray<Vertex>Vertices;
            [ReadOnly] public NativeArray<Triangle> Triangles;
            public Entity Entity;
            public EntityCommandBuffer Ecb;

            public void Execute()
            {
                var verticesBuffer = Ecb.SetBuffer<Vertex>(Entity);
                verticesBuffer.Clear();
				verticesBuffer.AddRange(Vertices);
                var trianglesBuffer = Ecb.SetBuffer<Triangle>(Entity);
                trianglesBuffer.Clear();
                trianglesBuffer.AddRange(Triangles);
                Ecb.AddComponent<DirtyMeshTag>(Entity);
                Ecb.RemoveComponent<DirtyChunkTag>(Entity);
            }
        }
    }
}
