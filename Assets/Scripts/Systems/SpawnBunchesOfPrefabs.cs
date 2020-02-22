using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;
using float4x4 = Unity.Mathematics.float4x4;

public struct SpawnerOfBunches : IComponentData
{
    public Entity prefab;
    public Vector3Int amounts;
    public Vector3 padding;
    public bool hasSpawned;
}

public class SpawnBunchesOfPrefabsSyncronously : ComponentSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref SpawnerOfBunches spawner, ref LocalToWorld localToWorld) => SpawnPrefabs(ref spawner, ref localToWorld) );
    }

    private void SpawnPrefabs(ref SpawnerOfBunches spawner, ref LocalToWorld localToWorld)
    {
        if (spawner.hasSpawned)
            return;
        spawner.hasSpawned = true;

        for (int z = 0; z < spawner.amounts.z; z++)
        {
            for (int y = 0; y < spawner.amounts.y; y++)
            {
                for (int x = 0; x < spawner.amounts.x; x++)
                {
                    Entity instance = World.EntityManager.Instantiate(spawner.prefab);

                    float4x4 parentTransform =
                        localToWorld.Value * float4x4.Translate(new float3(x, y, z) * spawner.padding);
                    RecursivelyTransform(instance, ref parentTransform);
                }
            }
        }
    }

    private void RecursivelyTransform(Entity parent, ref float4x4 parentTransform)
    {
        World.EntityManager.SetComponentData(parent, new LocalToWorld
        {
            Value = parentTransform
        });

        if (World.EntityManager.HasComponent<LinkedEntityGroup>(parent))
        {
            var children = World.EntityManager.GetBuffer<LinkedEntityGroup>(parent);

            foreach (var child in children)
            {
                LocalToWorld childLocalToWorld = World.EntityManager.GetComponentData<LocalToWorld>(child.Value);
                float4x4 localToWorld = parentTransform * childLocalToWorld.Value;
                RecursivelyTransform(child.Value, ref localToWorld);
            }
        }
    }
}

//public class SpawnBunchesOfPrefabs : JobComponentSystem
//{
//    // Not a long name at all
//    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

//    //[BurstCompile]
//    struct SpawnBunchesOfPrefabsJob : IJobForEachWithEntity<SpawnerOfBunches, LocalToWorld>
//    {
//        public EntityCommandBuffer.Concurrent entityCommandBuffer;

//        public void Execute(Entity entity, int index, ref SpawnerOfBunches spawner, [ReadOnly] ref LocalToWorld localToWorld)
//        {
//            if (spawner.hasSpawned)
//                return;
//            spawner.hasSpawned = true;

//            for (int z = 0; z < spawner.amounts.z; z++)
//            {
//                for (int y = 0; y < spawner.amounts.y; y++)
//                {
//                    for (int x = 0; x < spawner.amounts.x; x++)
//                    {
//                        Entity instance = entityCommandBuffer.Instantiate(index, spawner.prefab);
//                        entityCommandBuffer.SetComponent(index, instance, new Translation
//                        {
//                            Value = localToWorld.Position + new float3(x, y, z) * spawner.padding
//                        });
                        
//                    }
//                }
//            }
//        }
//    }

//    protected override void OnCreate()
//    {
//        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
//    }

//    protected override JobHandle OnUpdate(JobHandle inputDependencies)
//    {
//        EntityQuery query = GetEntityQuery(typeof(SpawnerOfBunches), ComponentType.ReadOnly<LocalToWorld>());

//        var spawners = query.ToComponentDataArray<SpawnerOfBunches>(Allocator.Temp);
//        foreach (var spawner in spawners)
//        {
//            var children = World.EntityManager.GetBuffer<LinkedEntityGroup>(spawner.prefab);
//            foreach (var child in children)
//            {
                
//            }
//        }
        
//        var job = new SpawnBunchesOfPrefabsJob
//        {
//            entityCommandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
//        };
        
//        // Now that the job is set up, schedule it to be run. 
//        JobHandle jobHandle = job.Schedule(this, inputDependencies);

//        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(jobHandle);

//        return jobHandle;
//    }
//}
