using UnityEngine;

public class Reference<T> : ScriptableObject where T : struct
{
    public T Value;
}
