using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

public class GetNearestEnemySystem : JobComponentSystem
{
    [BurstCompile]
    struct GetNearestEnemySystemJob_BruteForce : IJobForEachWithEntity<LocalToWorld, EnemyTag>
    {
        public float3 defenseTowerPosition;

        public float closestDistanceSq;
        public NativeSlice<Entity> closestEntity;

        public void Execute(Entity entity, int index, [ReadOnly] ref LocalToWorld localToWorld, [ReadOnly] ref EnemyTag tag)
        {
            float dist2 = distancesq(defenseTowerPosition, localToWorld.Position);
            if(dist2 < closestDistanceSq)
            {
                closestDistanceSq = dist2;
                closestEntity[0] = entity;
            }
        }
    }


    // ===================================================
    // Brute force technique:
    // ===================================================
    private (JobHandle handle, NativeArray<Entity> singleResult) GetNearestEnemy_BruteForce_Concurrent(float3 position)
    {
        NativeArray<Entity> closestEntityArr = new NativeArray<Entity>(1, Allocator.TempJob);

        var job = new GetNearestEnemySystemJob_BruteForce
        {
            defenseTowerPosition = position,
            closestDistanceSq = float.MaxValue,
            closestEntity = closestEntityArr
        };
        var handle = job.ScheduleSingle(this);

        return (handle, closestEntityArr);
    }

    private Entity GetNearestEnemy_BruteForce_Syncronous(float3 position)
    {
        NativeArray<Entity> closestEntityArr = new NativeArray<Entity>(1, Allocator.TempJob);

        var job = new GetNearestEnemySystemJob_BruteForce
        {
            defenseTowerPosition = position,
            closestDistanceSq = float.MaxValue,
            closestEntity = closestEntityArr
        };

        job.Run(this);
        
        Entity closestEntity = closestEntityArr[0];
        closestEntityArr.Dispose();
        return closestEntity;
    }

    private (JobHandle combinedHandle, NativeArray<Entity> results) GetNearestEnemies_BruteForce_Concurrent(NativeSlice<float3> positions)
    {
        NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(positions.Length, Allocator.Temp);
        NativeArray<Entity> results = new NativeArray<Entity>(positions.Length, Allocator.TempJob);

        for (int i = 0; i < positions.Length; i++)
        {
            float3 position = positions[i];
            var job = new GetNearestEnemySystemJob_BruteForce
            {
                defenseTowerPosition = position,
                closestDistanceSq = float.MaxValue,
                closestEntity = results.Slice(i, 1)
            };
            JobHandle handle = job.ScheduleSingle(this);
            jobs[i] = handle;
        }

        JobHandle combinedHandle = JobHandle.CombineDependencies(jobs);
        jobs.Dispose();
        return (combinedHandle, results);
    }


    private NativeArray<Entity> GetNearestEnemies_BruteForce_Syncronous(NativeSlice<float3> positions)
    {
        var (combinedHandle, results) = GetNearestEnemies_BruteForce_Concurrent(positions);

        combinedHandle.Complete();

        return results;
    }


    // ===================================================
    // Physics variations:
    // ===================================================
    private void Test()
    {

    }


    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        {
            Entity closest = GetNearestEnemy_BruteForce_Syncronous(Camera.main.transform.position);

            if (closest != Entity.Null)
            {
                float3 closestPosition = EntityManager.GetComponentData<Translation>(closest).Value;
                Debug.DrawLine(Camera.main.transform.position, closestPosition);
            }
        }

        // This code is not working yet :(
        if(false)
        {
            NativeList<float3> positions = new NativeList<float3>(8, Allocator.Temp);
            Entities.WithAll<DefenseTowerTag>().ForEach((in Translation translation) =>
            {
                positions.Add(translation.Value);
            }).Run();

            NativeArray<Entity> closestEntities = GetNearestEnemies_BruteForce_Syncronous(positions.AsArray());
            for (int i = 0; i < closestEntities.Length; i++)
            {
                if (closestEntities[i] != Entity.Null)
                { 
                    float3 closestPosition = EntityManager.GetComponentData<Translation>(closestEntities[i]).Value;
                    Debug.DrawLine(positions[i], closestPosition);
                }
            }
            closestEntities.Dispose();
            positions.Dispose();
        }

        return inputDependencies;
    }
}