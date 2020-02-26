using Unity.Entities;
using UnityEngine;

public struct SpawnerOfBunches : IComponentData
{
    public Entity prefab;
    public Vector3Int amounts;
    public Vector3 padding;
    public bool hasSpawned;
}
