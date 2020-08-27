using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct ArraysToHashMap<T1, T2> : IJob where T1 : struct, IEquatable<T1> where T2 : struct
{
	[ReadOnly] public NativeArray<T1>.ReadOnly Keys;
	[ReadOnly] public NativeArray<T2>.ReadOnly Values;
	[WriteOnly] public NativeHashMap<T1, T2> HashMap;

	public void Execute()
	{
		Assert.AreEqual(Keys.Length, Values.Length);

		for (int i = 0; i < Keys.Length; i++)
			HashMap.TryAdd(Keys[i], Values[i]);
	}
}
