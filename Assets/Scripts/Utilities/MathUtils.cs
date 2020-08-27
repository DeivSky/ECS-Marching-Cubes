using Unity.Mathematics;

public static class MathUtils
{
    public static int IndexFromCoord(int3 xyz, int n) => IndexFromCoord(xyz.x, xyz.y, xyz.z, n);  
    public static int IndexFromCoord(int x, int y, int z, int n) => x * n * n + y * n + z;
    public static int IndexFromCoord(int2 xy, int n) => IndexFromCoord(xy.x, xy.y, n);
    public static int IndexFromCoord(int x, int y, int n) => x * n + y;
    public static float3 Interpolate(float4 a, float4 b, float x) =>
        a.xyz + (x - a.w) / (b.w - a.w) * (b.xyz - a.xyz);
    public static float3 xyz(this float4 v) => new float3(v.x, v.y, v.z);
}
