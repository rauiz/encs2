using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;

[UpdateBefore(typeof(S_ManageServerConnections))]
[DisableAutoCreation]
public class S_MovePlayer : JobComponentSystem
{
    public EntityCommandBufferSystem CleanupMessageBarrier;

    private EntityQuery m_MovePlayerMessages;
    private EntityQuery m_Players;
    private EntityQuery m_Grid;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_MovePlayerMessages = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<MC_MovePlayer>() }
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
        if (m_MovePlayerMessages.CalculateEntityCount() == 0) return inputDependencies;

        SINGLETON_GridDefinitions t_GridDef = GetSingleton<SINGLETON_GridDefinitions>();

        NativeArray<Entity> na_MovePlayerMessageEntities = m_MovePlayerMessages.ToEntityArray(Allocator.TempJob);
        NativeArray<MC_MovePlayer> na_MovePlayerMessages = m_MovePlayerMessages.ToComponentDataArray<MC_MovePlayer>(Allocator.TempJob);

        NativeArray<Entity> na_PlayerEntities = m_Players.ToEntityArray(Allocator.TempJob);
        NativeArray<C_PlayerIndex> na_PlayerIndexes = m_Players.ToComponentDataArray<C_PlayerIndex>(Allocator.TempJob);
        NativeArray<C_PlayerPos> na_PlayerPositions = m_Players.ToComponentDataArray<C_PlayerPos>(Allocator.TempJob);

        NativeArray<C_GridPos> na_Grid = m_Grid.ToComponentDataArray<C_GridPos>(Allocator.TempJob);


        EntityCommandBuffer ecb_Cleanup = CleanupMessageBarrier.CreateCommandBuffer();
        MC_MovePlayer mc_CurrentMessage;

        Entity t_CurrentPlayerEntity = default;
        C_PlayerPos c_CurrentPlayerPos = default;
        C_PlayerPos c_EnemyPlayerPos = default;

        C_GridPos c_CurrentGridPos;
        for (int i = 0; i < na_MovePlayerMessages.Length; i++)
        {
            mc_CurrentMessage = na_MovePlayerMessages[i];

            for (int j = 0; j < na_PlayerIndexes.Length; j++)
            {
                if (mc_CurrentMessage.PlayerId != na_PlayerIndexes[j].PlayerId) 
                    c_EnemyPlayerPos = na_PlayerPositions[j];
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
                break;
            }

            unsafe
            {
                if (c_CurrentGridPos.WallStates[(int)mc_CurrentMessage.WallDirection] == (byte)WallStates.Open)
                {
                    Vector2Int t_NewPos = default;
                    switch (mc_CurrentMessage.WallDirection)
                    {
                        case WallIndexes.Up:
                            t_NewPos = c_CurrentPlayerPos.Pos + new Vector2Int(0, -1);
                            break;
                        case WallIndexes.Down:
                            t_NewPos = c_CurrentPlayerPos.Pos + new Vector2Int(0, 1);
                            break;
                        case WallIndexes.Left:
                            t_NewPos = c_CurrentPlayerPos.Pos + new Vector2Int(-1, 0);
                            break;
                        case WallIndexes.Right:
                            t_NewPos = c_CurrentPlayerPos.Pos + new Vector2Int(1, 0);
                            break;
                        default:
                            break;
                    }

                    if ((t_NewPos.x >= 0 && t_NewPos.x < t_GridDef.RowCount) &&
                        (t_NewPos.y >= 0 && t_NewPos.y < t_GridDef.ColumnCount))
                    {
                        ecb_Cleanup.SetComponent(t_CurrentPlayerEntity, new C_PlayerPos
                        {
                            Pos = t_NewPos
                        });                        

                        // Notifies the player that he moved.
                        U_ServerUtils
                            .NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 6);

                        // Finds the new position in the grid.
                        for (int j = 0; j < na_Grid.Length; j++)
                        {
                            if (t_NewPos != na_Grid[j].Pos) continue;
                            c_CurrentGridPos = na_Grid[j];
                            break;
                        }

                        // Writes to the player the status of the room.
                        if(c_CurrentGridPos.WallStates[(int)WallIndexes.Up] == (byte) WallStates.Damaged)
                            U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 2);
                        if (c_CurrentGridPos.WallStates[(int)WallIndexes.Left] == (byte)WallStates.Damaged)
                            U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 3);
                        if (c_CurrentGridPos.WallStates[(int)WallIndexes.Right] == (byte)WallStates.Damaged)
                            U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 4);
                        if (c_CurrentGridPos.WallStates[(int)WallIndexes.Down] == (byte)WallStates.Damaged)
                            U_ServerUtils.NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 5);

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
                                i_EnemySoundMessage = 8;
                                break;
                            case WallIndexes.Down:
                                i_EnemySoundMessage = 11;
                                break;
                            case WallIndexes.Left:
                                i_EnemySoundMessage = 9;
                                break;
                            case WallIndexes.Right:
                                i_EnemySoundMessage = 10;
                                break;
                            case WallIndexes.Diagonal:
                                i_EnemySoundMessage = 7;
                                break;
                            case WallIndexes.Center:
                                i_EnemySoundMessage = 1;
                                break;
                            default:
                                break;
                        }

                        U_ServerUtils.NotifyEnemyText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, i_EnemySoundMessage);
                    }
                    else
                    {
                        // Notifies the player that he failed to move due to touching the edge of the Grid.
                        U_ServerUtils
                            .NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, 33);
                    }
                }
                else
                {
                    int i_MessageIndex = -1;
                    switch (mc_CurrentMessage.WallDirection)
                    {
                        case WallIndexes.Up:
                            i_MessageIndex = 2;
                            break;
                        case WallIndexes.Down:
                            i_MessageIndex = 5;
                            break;
                        case WallIndexes.Left:
                            i_MessageIndex = 3;
                            break;
                        case WallIndexes.Right:
                            i_MessageIndex = 4;
                            break;
                        default:
                            break;
                    }
                    // Notifies the player that he failed to move due to the wall being broken.
                    U_ServerUtils
                        .NotifyPlayerText(ref ecb_Cleanup, in na_PlayerEntities, in na_PlayerIndexes, mc_CurrentMessage.PlayerId, i_MessageIndex);                    
                } 
            }
            ecb_Cleanup.DestroyEntity(na_MovePlayerMessageEntities[i]);
        }

        na_MovePlayerMessageEntities.Dispose();
        na_MovePlayerMessages.Dispose();

        na_PlayerEntities.Dispose();
        na_PlayerIndexes.Dispose();
        na_PlayerPositions.Dispose();

        na_Grid.Dispose();
        return inputDependencies;
    }

}