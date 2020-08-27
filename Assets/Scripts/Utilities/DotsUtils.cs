using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

public static class DotsUtils
{
	public static JobHandle CombineDependencies(params JobHandle[] handles)
	{
		using (var array = new NativeArray<JobHandle>(handles, Allocator.Temp))
			return JobHandle.CombineDependencies(array);
	}
}

