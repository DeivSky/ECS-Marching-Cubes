using Unity.Entities;
using Unity.Mathematics;

public struct Vertex : IBufferElementData
{
	public float3 Value;

	public static implicit operator float3(Vertex v) => v.Value;
	public static implicit operator Vertex(float3 v) => new Vertex { Value = v };
}

public struct Triangle : IBufferElementData
{
	public int Value;

	public static implicit operator int(Triangle v) => v.Value;
	public static implicit operator Triangle(int v) => new Triangle { Value = v };
}

public struct DirtyMeshTag : IComponentData { }
