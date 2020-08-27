using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace MarchingCubes
{
	public static class MarchingUtilities
	{
		public static Entity CreateChunk(Entity prefab, EntityManager em)
		{
			var e = em.Instantiate(prefab);
			InitializeChunkEntity(e, em);

			return e;
		}

		public static NativeArray<Entity> CreateChunks(Entity prefab, int count, EntityManager em, Allocator allocator = Allocator.Temp)
		{
			var es = em.Instantiate(prefab, count, allocator);
			for (int i = 0; i < count; i++)
				InitializeChunkEntity(es[i], em);

			return es;
		}

		public static void InitializeChunkEntity(Entity e, EntityManager em)
		{
			em.AddComponents(e, GetInitialChunkComponents());
			em.SetComponentData(e, (MarchingChunk)math.int3(int.MaxValue));
			em.AddBuffer<Vertex>(e);
			em.AddBuffer<Triangle>(e);
			em.SetName(e, "Chunk");
			InitializeEntityMesh(e, em);
		}

		public static void InitializeEntityMesh(Entity e, EntityManager em)
		{
			var render = em.GetSharedComponentData<RenderMesh>(e);
			render.mesh = new Mesh();
			em.SetSharedComponentData(e, render);
		}

		public static ComponentTypes GetInitialChunkComponents() =>
			new ComponentTypes(new ComponentType[] {
				ComponentType.ReadWrite<MarchingChunk>(),
				ComponentType.ReadOnly<Disabled>() });
	}
}
