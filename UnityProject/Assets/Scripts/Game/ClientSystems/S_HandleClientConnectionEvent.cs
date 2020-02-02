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
public class S_HandleClientConnectionEvent : JobComponentSystem
{
    public EntityCommandBufferSystem CleanupMessageBarrier;

    private EntityQuery m_OnClientConnectedMessage;
    private EntityQuery m_Players;
    private EntityQuery m_Grid;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_OnClientConnectedMessage = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<MC_OnClientConnected>() }
        });
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        if(m_OnClientConnectedMessage.CalculateEntityCount() > 0)
        {
            SceneManager.LoadSceneAsync("UI", LoadSceneMode.Additive);
            EntityManager.DestroyEntity(m_OnClientConnectedMessage);
        }

        return inputDependencies;
    }

}