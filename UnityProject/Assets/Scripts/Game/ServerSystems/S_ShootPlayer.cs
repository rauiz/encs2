using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;

[UpdateBefore(typeof(S_ManageServerConnections))]
[UpdateAfter(typeof(S_DamageWall))]
[DisableAutoCreation]
public class S_ShootPlayer : JobComponentSystem
{
    public EntityCommandBufferSystem CleanupMessageBarrier;

    private EntityQuery m_ShootPlayerMessages;
    private EntityQuery m_Players;
    private EntityQuery m_Grid;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_ShootPlayerMessages = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<MC_ShootInDirection>() }
        });

        m_Players = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<C_PlayerIndex>(), ComponentType.ReadOnly<C_PlayerPos>() }
        });

        m_Grid = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<C_GridPos>() }
        });
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        if (m_ShootPlayerMessages.CalculateEntityCount() == 0) return inputDependencies;

        SINGLETON_GridDefinitions singletong_GridDef = GetSingleton<SINGLETON_GridDefinitions>();

        NativeArray<Entity> na_BreakWallMessageEntities = m_ShootPlayerMessages.ToEntityArray(Allocator.TempJob);
        NativeArray<MC_ShootInDirection> na_ShootPlayerMessages = m_ShootPlayerMessages.ToComponentDataArray<MC_ShootInDirection>(Allocator.TempJob);

        NativeArray<Entity> na_PlayerEntities = m_Players.ToEntityArray(Allocator.TempJob);
        NativeArray<C_PlayerIndex> na_PlayerIndexes = m_Players.ToComponentDataArray<C_PlayerIndex>(Allocator.TempJob);
        NativeArray<C_PlayerPos> na_PlayerPositions = m_Players.ToComponentDataArray<C_PlayerPos>(Allocator.TempJob);

        NativeArray<Entity> na_GridEntities = m_Grid.ToEntityArray(Allocator.TempJob);
        NativeArray<C_GridPos> na_Grid = m_Grid.ToComponentDataArray<C_GridPos>(Allocator.TempJob);


        EntityCommandBuffer ecb_Cleanup = CleanupMessageBarrier.CreateCommandBuffer();
        MC_ShootInDirection mc_CurrentMessage;
        C_PlayerPos c_CurrentPlayerPos = default;
        C_PlayerPos c_EnemyPlayerPos = new C_PlayerPos { Pos = new Vector2Int(-1, -1) };

        C_GridPos c_CurrentGridPos;


        for (int i = 0; i < na_ShootPlayerMessages.Length; i++)
        {
            mc_CurrentMessage = na_ShootPlayerMessages[i];

            for (int j = 0; j < na_PlayerIndexes.Length; j++)
            {
                if (mc_CurrentMessage.PlayerId != na_PlayerIndexes[j].PlayerId) c_EnemyPlayerPos = na_PlayerPositions[j];
                else c_CurrentPlayerPos = na_PlayerPositions[j];
            }

            if(c_CurrentPlayerPos.Pos == c_EnemyPlayerPos.Pos)
            {
                U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 28);
                Debug.Log("Shot Enemy");
                return inputDependencies;
            }

            Vector2Int t_RequiredEnemyPos = default;
            switch (mc_CurrentMessage.WallDirection)
            {
                case WallIndexes.Up:
                    t_RequiredEnemyPos = new Vector2Int(0, -1);
                    break;
                case WallIndexes.Down:
                    t_RequiredEnemyPos = new Vector2Int(0, 1);
                    break;
                case WallIndexes.Left:
                    t_RequiredEnemyPos = new Vector2Int(-1, 0);
                    break;
                case WallIndexes.Right:
                    t_RequiredEnemyPos = new Vector2Int(1, 0);
                    break;
                default:
                    break;
            }
            t_RequiredEnemyPos.x = Mathf.Clamp(t_RequiredEnemyPos.x, 0, singletong_GridDef.RowCount);
            t_RequiredEnemyPos.y = Mathf.Clamp(t_RequiredEnemyPos.x, 0, singletong_GridDef.ColumnCount);

            int i_CheckingWallDirection = 0;
            int i_HitTextMessage = 0;
            switch (mc_CurrentMessage.WallDirection)
            {
                case WallIndexes.Up:
                    i_CheckingWallDirection = (int)WallIndexes.Down;
                    i_HitTextMessage = 29;
                    break;
                case WallIndexes.Down:
                    i_CheckingWallDirection = (int)WallIndexes.Up;
                    i_HitTextMessage = 32;
                    break;
                case WallIndexes.Left:
                    i_CheckingWallDirection = (int)WallIndexes.Right;
                    i_HitTextMessage = 30;
                    break;
                case WallIndexes.Right:
                    i_CheckingWallDirection = (int)WallIndexes.Left;
                    i_HitTextMessage = 31;
                    break;
                default:
                    break;
            }

            for (int j = 0; j < na_GridEntities.Length; j++)
            {   
                c_CurrentGridPos = na_Grid[j];

                unsafe {

                    if (c_CurrentGridPos.Pos != t_RequiredEnemyPos) continue;
                    
                    if(c_CurrentGridPos.WallStates[i_CheckingWallDirection] != (byte)WallStates.Open)
                    {
                        U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 26);
                    }                        
                    else
                    {
                        if(c_CurrentGridPos.Pos == t_RequiredEnemyPos)
                        {
                            U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, i_HitTextMessage);                            
                        }                            
                    }                        
                }
            }
            ecb_Cleanup.DestroyEntity(na_BreakWallMessageEntities[i]);
        }

        na_BreakWallMessageEntities.Dispose();
        na_ShootPlayerMessages.Dispose();

        na_PlayerIndexes.Dispose();
        na_PlayerPositions.Dispose();

        na_GridEntities.Dispose();
        na_Grid.Dispose();
        return inputDependencies;
    }

}