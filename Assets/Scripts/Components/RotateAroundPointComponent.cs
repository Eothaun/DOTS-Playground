using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct RotateAroundPoint : IComponentData
{
    public float3 middlePoint;
    public float3 startPoint;
}
