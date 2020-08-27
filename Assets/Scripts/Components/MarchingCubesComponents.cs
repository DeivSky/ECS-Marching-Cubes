using System;
using Unity.Entities;
using Unity.Mathematics;

namespace MarchingCubes
{
	[Serializable]
	public struct MarchingCubesData : IComponentData
	{
		public MarchingTerrain Terrain;
		public Noise Noise;
		public Entity ChunkPrefab;
	}

	[Serializable]
	public struct MarchingTerrain : IComponentData, IEquatable<MarchingTerrain>
	{
		public float ViewDistance
		{
			get => viewDistance;
			set
			{
				viewDistance = value;
				sqrViewDistance = value * value;
			}
		}
		public float SqrViewDistance => sqrViewDistance;
		public float BoundsSize;
		public float Surface;
		public int Resolution;

		private float viewDistance;
		private float sqrViewDistance;

		public override bool Equals(object obj) => obj is MarchingTerrain terrain && Equals(terrain);
		public bool Equals(MarchingTerrain other) => 
			ViewDistance == other.ViewDistance
			&& BoundsSize == other.BoundsSize
			&& Surface == other.Surface
			&& Resolution == other.Resolution
			&& viewDistance == other.viewDistance
			&& sqrViewDistance == other.sqrViewDistance;
		public override int GetHashCode() => base.GetHashCode();

		public static bool operator ==(MarchingTerrain left, MarchingTerrain right) => left.Equals(right);
		public static bool operator !=(MarchingTerrain left, MarchingTerrain right) => !(left == right);
	}

	public struct MarchingChunk : IComponentData, IEquatable<MarchingChunk>
	{
		public int3 Coordinate;

		public static implicit operator int3(MarchingChunk chunk) => chunk.Coordinate;
		public static implicit operator MarchingChunk(int3 coordinate) => new MarchingChunk { Coordinate = coordinate };

		public bool Equals(MarchingChunk other) => Coordinate.Equals(other.Coordinate);
		public override int GetHashCode() => Coordinate.GetHashCode();
		public override string ToString() => $"Chunk ({Coordinate.x}, {Coordinate.y}, {Coordinate.z})";
	}

	public struct DirtyChunkTag : IComponentData { }

	public struct VisibleChunksBuffer : IBufferElementData
	{
		public MarchingChunk Chunk;
	}

	public struct VisibleEntitiesBuffer : IBufferElementData
	{
		public Entity Entity;

		public static implicit operator Entity(VisibleEntitiesBuffer entityBuffer) => entityBuffer.Entity;
		public static implicit operator VisibleEntitiesBuffer(Entity entity) => new VisibleEntitiesBuffer { Entity = entity };
	}

	public struct DisabledChunksBuffer : IBufferElementData
	{
		public MarchingChunk Chunk;
	}

	public struct DisabledEntitiesBuffer : IBufferElementData
	{
		public Entity Entity;

		public static implicit operator Entity(DisabledEntitiesBuffer entityBuffer) => entityBuffer.Entity;
		public static implicit operator DisabledEntitiesBuffer(Entity entity) => new DisabledEntitiesBuffer { Entity = entity };
	}
}
