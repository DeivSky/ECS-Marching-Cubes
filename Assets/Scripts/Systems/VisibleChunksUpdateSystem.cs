using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace MarchingCubes
{
	[UpdateInGroup(typeof(PresentationSystemGroup))]
	public class VisibleChunksUpdateSystem : SystemBase
	{
		private EntityCommandBufferSystem ecbSystem;
		private Transform transform;
		private EntityQuery chunksQuery;
		private EntityQuery disabledChunksQuery;
		private const float epsilon = 1E-5f;

		protected override void OnCreate()
		{
			base.OnCreate();
			ecbSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();

			var enabledChunksQueryDesc = new EntityQueryDesc
			{
				All = new ComponentType[] { ComponentType.ReadOnly<MarchingChunk>() },
				None = new ComponentType[]
				{
					ComponentType.ReadOnly<Disabled>(),
					ComponentType.ReadOnly<DirtyChunkTag>()
				}
			};
			var inProcessChunksQueryDesc = new EntityQueryDesc
			{
				All = new ComponentType[]
				{
					ComponentType.ReadOnly<MarchingChunk>(),
					ComponentType.ReadOnly<Disabled>(),
					ComponentType.ReadOnly<DirtyChunkTag>()
				}
			};

			chunksQuery = GetEntityQuery(enabledChunksQueryDesc, inProcessChunksQueryDesc);
			disabledChunksQuery = GetEntityQuery(ComponentType.ReadOnly<MarchingChunk>(), 
				ComponentType.ReadOnly<Disabled>(), ComponentType.Exclude<DirtyChunkTag>());
		}

		protected override void OnStartRunning()
		{
			base.OnStartRunning();
			transform = Camera.main.transform;
		}

		protected override void OnUpdate()
		{
			float3 position = transform.position;

			var data = GetSingleton<MarchingCubesData>();
			var entities = chunksQuery.ToEntityArrayAsync(Allocator.TempJob, out var handle1);
			var chunks = chunksQuery.ToComponentDataArrayAsync<MarchingChunk>(Allocator.TempJob, out var handle2);
			var disabledEntities = disabledChunksQuery.ToEntityArrayAsync(Allocator.TempJob, out var handle3);
			var disabledChunks = disabledChunksQuery.ToComponentDataArrayAsync<MarchingChunk>(Allocator.TempJob, out var handle4);
			Dependency = DotsUtils.CombineDependencies(handle1, handle2, handle3, handle4, Dependency);

			NativeList<int3> requestedCoordinates = new NativeList<int3>(Allocator.TempJob);
			EntityCommandBuffer ecb = ecbSystem.CreateCommandBuffer();

			var requestChunkCoordinates = new RequestChunkCoordinatesJob
			{
				RequestedCoordinates = requestedCoordinates,
				Terrain = data.Terrain,
				Position = position
			};

			var setChunksCoordinate = new SetChunksCoordinatesJob
			{
				RequestedCoordinates = requestedCoordinates,
				VisibleEntities = entities.AsReadOnly(),
				VisibleChunks = chunks,
				DisabledEntities = disabledEntities.AsReadOnly(),
				Terrain = data.Terrain,
				Position = position,
				Ecb = ecb
			};

			Dependency = requestChunkCoordinates.Schedule(Dependency);
			Dependency = setChunksCoordinate.Schedule(Dependency);
			Dependency = DotsUtils.CombineDependencies(
				entities.Dispose(Dependency),
				chunks.Dispose(Dependency),
				disabledEntities.Dispose(Dependency),
				disabledChunks.Dispose(Dependency),
				requestedCoordinates.Dispose(Dependency));

			ecbSystem.AddJobHandleForProducer(Dependency);
		}

		[BurstCompile]
		public struct RequestChunkCoordinatesJob : IJob
		{
			public NativeList<int3> RequestedCoordinates;
			public MarchingTerrain Terrain;
			public float3 Position;

			public void Execute()
			{
				int maxChunks = (int)ceil(Terrain.ViewDistance / Terrain.BoundsSize);
				int3 coordinate = (int3)round(Position / Terrain.BoundsSize);

				int3 xyz = 0;
				for (xyz.x = -maxChunks; xyz.x <= maxChunks; xyz.x++)
					for (xyz.y = -maxChunks; xyz.y <= maxChunks; xyz.y++)
						for (xyz.z = -maxChunks; xyz.z <= maxChunks; xyz.z++)
						{
							int3 coord = xyz + coordinate;

							float3 center = (float3)coord * Terrain.BoundsSize;
							float3 offset = Position - center;
							float3 v = abs(offset) - float3(Terrain.BoundsSize / 2f);
							float sqrDistance = lengthsq(float3(max(v.x, 0), max(v.y, 0), max(v.z, 0)));

							if (sqrDistance <= Terrain.SqrViewDistance)
								RequestedCoordinates.Add(coord);
						}
			}
		}

		[BurstCompile]
		public struct SetChunksCoordinatesJob : IJob
		{
			public NativeList<int3> RequestedCoordinates;
			[ReadOnly] public NativeArray<Entity>.ReadOnly VisibleEntities;
			[ReadOnly] public NativeArray<MarchingChunk> VisibleChunks;
			[ReadOnly] public NativeArray<Entity>.ReadOnly DisabledEntities;
			public MarchingTerrain Terrain;
			public float3 Position;
			public EntityCommandBuffer Ecb;

			public void Execute()
			{
				for (int i = 0; i < VisibleChunks.Length; i++)
				{
					int idx = RequestedCoordinates.IndexOf((int3)VisibleChunks[i]);
					if (idx > -1)
						RequestedCoordinates.RemoveAt(idx);
					else
						Ecb.AddComponent<Disabled>(VisibleEntities[i]);
				}

				for (int i = 0, j = 0; i < RequestedCoordinates.Length && j < DisabledEntities.Length; i++, j++)
				{
					Ecb.SetComponent(DisabledEntities[j], (MarchingChunk)RequestedCoordinates[i]);
					Ecb.AddComponent<DirtyChunkTag>(DisabledEntities[j]);
				}
					
			}
		}
	}
}
