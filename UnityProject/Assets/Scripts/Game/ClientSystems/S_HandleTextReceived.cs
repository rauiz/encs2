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
public class S_HandleTextReceived : JobComponentSystem
{
    public CommandWindow CommandWindow;
    public GameAssetDatabase GameAssetDatabase;

    public EntityCommandBufferSystem CleanupMessageBarrier;

    private EntityQuery m_OnClientConnectedMessage;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_OnClientConnectedMessage = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<MC_OnTextNotified>() }
        });
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        NativeArray<MC_OnTextNotified> na_TextMessages = m_OnClientConnectedMessage.ToComponentDataArray<MC_OnTextNotified>(Allocator.TempJob);

        MC_OnTextNotified mc_CurrentTextNotification;
        for (int i = 0; i < na_TextMessages.Length; i++)
        {
            mc_CurrentTextNotification = na_TextMessages[i];
            CommandWindow.RegisterLog(GameAssetDatabase.m_gameTexts[mc_CurrentTextNotification.TextHandle]);
        }

        if(na_TextMessages.Length > 0)
            CleanupMessageBarrier.CreateCommandBuffer().DestroyEntity(m_OnClientConnectedMessage);

        na_TextMessages.Dispose();

        return inputDependencies;
    }

}