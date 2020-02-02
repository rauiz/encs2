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
public class S_RepairWall : JobComponentSystem
{
    public EntityCommandBufferSystem CleanupMessageBarrier;

    private EntityQuery m_RepairWallMessages;
    private EntityQuery m_Players;
    private EntityQuery m_Grid;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_RepairWallMessages = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<MC_RepairWall>() }
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
        if (m_RepairWallMessages.CalculateEntityCount() == 0) return inputDependencies;

        SINGLETON_GridDefinitions t_GridDef = GetSingleton<SINGLETON_GridDefinitions>();

        NativeArray<Entity> na_BreakWallMessageEntities = m_RepairWallMessages.ToEntityArray(Allocator.TempJob);
        NativeArray<MC_RepairWall> na_BreakWallMessages = m_RepairWallMessages.ToComponentDataArray<MC_RepairWall>(Allocator.TempJob);

        NativeArray<Entity> na_PlayerEntities = m_Players.ToEntityArray(Allocator.TempJob);
        NativeArray<C_PlayerIndex> na_PlayerIndexes = m_Players.ToComponentDataArray<C_PlayerIndex>(Allocator.TempJob);
        NativeArray<C_PlayerPos> na_PlayerPositions = m_Players.ToComponentDataArray<C_PlayerPos>(Allocator.TempJob);

        NativeArray<Entity> na_GridEntities = m_Grid.ToEntityArray(Allocator.TempJob);
        NativeArray<C_GridPos> na_Grid = m_Grid.ToComponentDataArray<C_GridPos>(Allocator.TempJob);


        EntityCommandBuffer ecb_Cleanup = CleanupMessageBarrier.CreateCommandBuffer();
        
        MC_RepairWall mc_CurrentMessage;
        
        Entity t_CurrentPlayerEntity = default;        
        C_PlayerPos c_CurrentPlayerPos = default;
        C_PlayerPos c_CurrentEnemyPos = default;
        
        Entity t_CurrentGridEntity = default;
        C_GridPos c_CurrentGridPos = default;
        for (int i = 0; i < na_BreakWallMessages.Length; i++)
        {
            mc_CurrentMessage = na_BreakWallMessages[i];

            for (int j = 0; j < na_PlayerIndexes.Length; j++)
            {
                if (mc_CurrentMessage.PlayerId != na_PlayerIndexes[j].PlayerId) c_CurrentEnemyPos = na_PlayerPositions[j];
                else
                {
                    t_CurrentPlayerEntity = na_PlayerEntities[j];
                    c_CurrentPlayerPos = na_PlayerPositions[j];
                }
            }

            for (int j = 0; j < na_Grid.Length; j++)
            {
                if (c_CurrentPlayerPos.Pos != na_Grid[j].Pos) continue;
                c_CurrentGridPos = na_Grid[j];
                t_CurrentGridEntity = na_GridEntities[j];
                break;
            }

            unsafe
            {
                if (c_CurrentGridPos.WallStates[(int)mc_CurrentMessage.WallDirection] != (byte)WallStates.Damaged)
                {
                    Debug.Log($"Already Repaired: {c_CurrentGridPos}");
                    U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 19);
                }
                else
                {
                    c_CurrentGridPos.WallStates[(int)mc_CurrentMessage.WallDirection] = (byte)WallStates.Open;
                    ecb_Cleanup.SetComponent(t_CurrentGridEntity, c_CurrentGridPos);

                    Vector2Int t_DuplicateWall = default;
                    switch (mc_CurrentMessage.WallDirection)
                    {
                        case WallIndexes.Up:
                            t_DuplicateWall = c_CurrentGridPos.Pos + new Vector2Int(0, -1);
                            break;
                        case WallIndexes.Down:
                            t_DuplicateWall = c_CurrentGridPos.Pos + new Vector2Int(0, 1);
                            break;
                        case WallIndexes.Left:
                            t_DuplicateWall = c_CurrentGridPos.Pos + new Vector2Int(-1, 0);
                            break;
                        case WallIndexes.Right:
                            t_DuplicateWall = c_CurrentGridPos.Pos + new Vector2Int(1, 0);
                            break;
                        default:
                            break;
                    }
                    Debug.Log($"Repaired Wall: {c_CurrentGridPos}");

                    for (int j = 0; j < na_Grid.Length; j++)
                    {
                        if (t_DuplicateWall != na_Grid[j].Pos) continue;

                        C_GridPos c_UpdatedDuplicateWall = na_Grid[j];

                        switch (mc_CurrentMessage.WallDirection)
                        {
                            case WallIndexes.Up:
                                c_UpdatedDuplicateWall.WallStates[(int)WallIndexes.Down] = (byte)WallStates.Open;
                                break;
                            case WallIndexes.Down:
                                c_UpdatedDuplicateWall.WallStates[(int)WallIndexes.Up] = (byte)WallStates.Open;
                                break;
                            case WallIndexes.Left:
                                c_UpdatedDuplicateWall.WallStates[(int)WallIndexes.Right] = (byte)WallStates.Open;
                                break;
                            case WallIndexes.Right:
                                c_UpdatedDuplicateWall.WallStates[(int)WallIndexes.Left] = (byte)WallStates.Open;
                                break;
                            default:
                                break;
                        }
                        ecb_Cleanup.SetComponent(na_GridEntities[j], c_UpdatedDuplicateWall);
                        Debug.Log($"Repaired Wall: {c_UpdatedDuplicateWall}");                        
                    }

                    U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 20);

                    // Notifies the enemy visor
                    int i_TileHandle;
                    U_ServerUtils.
                        NotifyEnemyVisor(
                            ref ecb_Cleanup,
                            in na_PlayerEntities,
                            in na_PlayerIndexes,
                            in na_PlayerPositions,
                            mc_CurrentMessage.PlayerId,
                            0, out i_TileHandle);

                    WallIndexes e_RelativeToEnemyDirection = U_ServerUtils.MapTilePositionToIndex(i_TileHandle);
                    int i_EnemySoundMessage = 0;
                    switch (e_RelativeToEnemyDirection)
                    {
                        case WallIndexes.Up:
                            i_EnemySoundMessage = 22;
                            break;
                        case WallIndexes.Down:
                            i_EnemySoundMessage = 25;
                            break;
                        case WallIndexes.Left:
                            i_EnemySoundMessage = 23;
                            break;
                        case WallIndexes.Right:
                            i_EnemySoundMessage = 24;
                            break;
                        case WallIndexes.Diagonal:
                            i_EnemySoundMessage = 21;
                            break;
                        case WallIndexes.Center:
                            i_EnemySoundMessage = 1;
                            break;
                        default:
                            break;
                    }

                    U_ServerUtils.NotifyEnemyText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, i_EnemySoundMessage);
                }
            }
            ecb_Cleanup.DestroyEntity(na_BreakWallMessageEntities[i]);
        }

        na_BreakWallMessageEntities.Dispose();
        na_BreakWallMessages.Dispose();

        na_PlayerEntities.Dispose();
        na_PlayerIndexes.Dispose();
        na_PlayerPositions.Dispose();

        na_GridEntities.Dispose();
        na_Grid.Dispose();
        return inputDependencies;
    }

}