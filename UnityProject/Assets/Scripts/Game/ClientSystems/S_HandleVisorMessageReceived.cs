using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

[UpdateAfter(typeof(S_ManageClientConnections))]
[DisableAutoCreation]
public class S_HandleVisorMessageReceived : JobComponentSystem
{   
    public FeedbackWindow FeedbackWindow;

    public EntityCommandBufferSystem CleanupMessageBarrier;

    private EntityQuery m_OnVisorMessageReceived;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_OnVisorMessageReceived = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<MC_OnVisorNotified>() }
        });
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        NativeArray<MC_OnVisorNotified> na_TextMessages = m_OnVisorMessageReceived.ToComponentDataArray<MC_OnVisorNotified>(Allocator.TempJob);

        MC_OnVisorNotified mc_CurrentVisorNotification;
        for (int i = 0; i < na_TextMessages.Length; i++)
        {
            mc_CurrentVisorNotification = na_TextMessages[i];
            if(mc_CurrentVisorNotification.Intensity > 1)
                FeedbackWindow.ShowFeedback(mc_CurrentVisorNotification.TileHandle, mc_CurrentVisorNotification.FeedbackHandle);
        }

        if(na_TextMessages.Length > 0)
            CleanupMessageBarrier.CreateCommandBuffer().DestroyEntity(m_OnVisorMessageReceived);

        na_TextMessages.Dispose();

        return inputDependencies;
    }

}