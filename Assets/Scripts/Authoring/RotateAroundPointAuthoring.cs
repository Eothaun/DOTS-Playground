using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class RotateAroundPointAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public Vector3 pivot;
    
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new RotateAroundPoint
        {
            middlePoint = pivot,
            startPoint = transform.position
        });
    }
}
