using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;

public class RotateAroundPointSystem : JobComponentSystem
{
    [BurstCompile]
    struct RotateAroundPointSystemJob : IJobForEach<Translation, RotateAroundPoint>
    {
        public float totalTime;
        
        public void Execute(ref Translation translation, [ReadOnly] ref RotateAroundPoint aroundPoint)
        {
            float3 dir = aroundPoint.startPoint - aroundPoint.middlePoint;
            dir = mul(quaternion.AxisAngle(new float3(0, 1, 0), totalTime), dir);
            translation.Value = aroundPoint.middlePoint + dir;
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new RotateAroundPointSystemJob
        {
            totalTime = (float) Time.ElapsedTime,
        };

        // Now that the job is set up, schedule it to be run. 
        return job.Schedule(this, inputDependencies);
    }
}