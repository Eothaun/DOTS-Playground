using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class SpawnerOfBunchesAuthoring : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefab;
    public Vector3Int amounts = new Vector3Int(10, 10, 10);
    public Vector3 padding = new Vector3(5, 5, 5);

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(prefab);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new SpawnerOfBunches
        {
            amounts = amounts,
            hasSpawned = false,
            padding = padding,
            prefab = conversionSystem.GetPrimaryEntity(prefab)
        });
    }
}
