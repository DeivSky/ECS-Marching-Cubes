using System;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Serializable]
public struct Noise : IComponentData, IEquatable<Noise>
{
	public uint Seed;
	public byte Octaves;
	public float Lacunarity;
	public float Persistence;
	public float Scale;
	public float Weight;
	public float WeightMultiplier;
	public float3 Offset;
	public float FloorOffset;
	public float HardFloorHeight;
	public float HardFloorWeight;
	public float TerraceHeight;
	public float TerraceWeight;

	public float Generate(float3 p)
	{
		var rng = new Unity.Mathematics.Random(Seed);
		const float offsetRange = 1000f;
		float noiseValue = 0f;
		float frequency = Scale / 100f;
		float amplitude = 1f;
		float weight = 1f;

		for (int i = 0; i < Octaves; i++)
		{
			float3 offset = float3((float)mul(rng.NextDouble(), 2f) - 1f, (float)mul(rng.NextDouble(), 2f) - 1f, (float)mul(rng.NextDouble(), 2f) - 1f) * offsetRange;
			float n = noise.snoise(p.xyz * frequency + offset + Offset);
			float v = 1 - abs(n);
			v = mul(pow(v, 2), weight);
			weight = max(min(v * WeightMultiplier, 1f), 0f);
			noiseValue += mul(v, amplitude);
			amplitude *= Persistence;
			frequency *= Lacunarity;
		}

		noiseValue = -(p.y + FloorOffset) + mul(noiseValue, Weight) + (p.y % TerraceHeight) * TerraceWeight;
		if (p.y < HardFloorHeight)
			noiseValue += HardFloorWeight;

		return noiseValue;
	}

	public static bool operator ==(Noise left, Noise right) => left.Equals(right);
	public static bool operator !=(Noise left, Noise right) => !(left == right);
	public override bool Equals(object obj) => obj is Noise noise && Equals(noise);
	public bool Equals(Noise other) => 
		Seed == other.Seed
		&& Octaves == other.Octaves
		&& Lacunarity == other.Lacunarity
		&& Persistence == other.Persistence
		&& Scale == other.Scale
		&& Weight == other.Weight
		&& WeightMultiplier == other.WeightMultiplier
		&& Offset.Equals(other.Offset)
		&& FloorOffset == other.FloorOffset
		&& HardFloorHeight == other.HardFloorHeight
		&& HardFloorWeight == other.HardFloorWeight
		&& TerraceHeight == other.TerraceHeight
		&& TerraceWeight == other.TerraceWeight;
	public override int GetHashCode() => base.GetHashCode();
}
