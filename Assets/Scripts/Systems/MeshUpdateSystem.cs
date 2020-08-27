using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;

namespace MarchingCubes
{
	public class MeshUpdateSystem : SystemBase
	{
		EntityCommandBufferSystem ecbSystem;

		protected override void OnCreate()
		{
			base.OnCreate();
			ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
		}

		protected override void OnUpdate()
		{
			var ecb = ecbSystem.CreateCommandBuffer();
			Entities
				.WithoutBurst()
				.WithAll<DirtyMeshTag, Disabled>()
				.WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
				.ForEach((RenderMesh renderMesh, DynamicBuffer<Vertex> vertices, DynamicBuffer<Triangle> triangles, Entity entity) =>
				{
					renderMesh.mesh.Clear();
					if (vertices.Length > 64000)
						renderMesh.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
					renderMesh.mesh.SetVertices(vertices.Reinterpret<Vector3>().AsNativeArray());
					renderMesh.mesh.SetTriangles(triangles.Reinterpret<int>().AsNativeArray());
					renderMesh.mesh.RecalculateNormals();
					vertices.Clear();
					triangles.Clear();
					ecb.RemoveComponent<DirtyMeshTag>(entity);
					ecb.RemoveComponent<Disabled>(entity);
				}).Run();
		}
	} 
}
