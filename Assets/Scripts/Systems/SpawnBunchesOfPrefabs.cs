using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public struct SpawnerOfBunches : IComponentData
{
    public Entity prefab;
    public Vector3Int amounts;
    public Vector3 padding;
    public bool hasSpawned;
}

public class SpawnBunchesOfPrefabs : JobComponentSystem
{
    // Not a long name at all
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    //[BurstCompile]
    struct SpawnBunchesOfPrefabsJob : IJobForEachWithEntity<SpawnerOfBunches, LocalToWorld>
    {
        public EntityCommandBuffer.Concurrent entityCommandBuffer;

        public void Execute(Entity entity, int index, ref SpawnerOfBunches spawner, [ReadOnly] ref LocalToWorld localToWorld)
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
                        Entity instance = entityCommandBuffer.Instantiate(index, spawner.prefab);
                        entityCommandBuffer.SetComponent(index, instance, new Translation
                        {
                            Value = localToWorld.Position + new float3(x, y, z) * spawner.padding
                        });
                    }
                }
            }
        }
    }

    protected override void OnCreate()
    {
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {

        var job = new SpawnBunchesOfPrefabsJob
        {
            entityCommandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
        };
        
        // Now that the job is set up, schedule it to be run. 
        JobHandle jobHandle = job.Schedule(this, inputDependencies);

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(jobHandle);

        return jobHandle;
    }
}