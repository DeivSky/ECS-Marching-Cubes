using Unity.Collections;
using UnityEngine;

public static class RenderingUtils
{
	public static void SetTriangles(this Mesh mesh, NativeArray<int> triangles)
	{
		var array = new int[triangles.Length];
		triangles.CopyTo(array);
		mesh.SetTriangles(array, 0);
	}
}
