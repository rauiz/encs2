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
public class S_DamageWall : JobComponentSystem
{
    public EntityCommandBufferSystem CleanupMessageBarrier;

    private EntityQuery m_DamageWallMessages;
    private EntityQuery m_Players;
    private EntityQuery m_Grid;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_DamageWallMessages = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<MC_BreakWall>() }
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
        if (m_DamageWallMessages.CalculateEntityCount() == 0) return inputDependencies;

        SINGLETON_GridDefinitions t_GridDef = GetSingleton<SINGLETON_GridDefinitions>();

        NativeArray<Entity> na_BreakWallMessageEntities = m_DamageWallMessages.ToEntityArray(Allocator.TempJob);
        NativeArray<MC_BreakWall> na_BreakWallMessages = m_DamageWallMessages.ToComponentDataArray<MC_BreakWall>(Allocator.TempJob);
                
        NativeArray<C_PlayerIndex> na_PlayerIndexes = m_Players.ToComponentDataArray<C_PlayerIndex>(Allocator.TempJob);
        NativeArray<C_PlayerPos> na_PlayerPositions = m_Players.ToComponentDataArray<C_PlayerPos>(Allocator.TempJob);

        NativeArray<Entity> na_GridEntities = m_Grid.ToEntityArray(Allocator.TempJob);
        NativeArray<C_GridPos> na_Grid = m_Grid.ToComponentDataArray<C_GridPos>(Allocator.TempJob);


        EntityCommandBuffer ecb_Cleanup = CleanupMessageBarrier.CreateCommandBuffer();
        MC_BreakWall mc_CurrentMessage;
        C_PlayerPos c_CurrentPlayerPos;
        C_GridPos c_CurrentGridPos;
        for (int i = 0; i < na_BreakWallMessages.Length; i++)
        {
            mc_CurrentMessage = na_BreakWallMessages[i];

            for (int j = 0; j < na_PlayerIndexes.Length; j++)
            {
                if (mc_CurrentMessage.PlayerId != na_PlayerIndexes[j].PlayerId) continue;

                c_CurrentPlayerPos = na_PlayerPositions[j];
                for (int k = 0; k < na_Grid.Length; k++)
                {
                    if (c_CurrentPlayerPos.Pos != na_Grid[k].Pos) continue;
                    c_CurrentGridPos = na_Grid[k];

                    unsafe {

                        if (c_CurrentGridPos.WallStates[(int)mc_CurrentMessage.WallDirection] == (byte)WallStates.Damaged)
                            Debug.Log($"Already Damaged: {c_CurrentGridPos}");
                        else
                        {
                            c_CurrentGridPos.WallStates[(int)mc_CurrentMessage.WallDirection] = (byte)WallStates.Damaged;
                            ecb_Cleanup.SetComponent(na_GridEntities[k], c_CurrentGridPos);

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
                            Debug.Log($"Broken Wall: {c_CurrentGridPos}");

                            for (int l = 0; l < na_Grid.Length; l++)
                            {
                                if (t_DuplicateWall != na_Grid[l].Pos) continue;

                                C_GridPos c_UpdatedDuplicateWall = na_Grid[l];

                                switch (mc_CurrentMessage.WallDirection)
                                {
                                    case WallIndexes.Up:
                                        c_UpdatedDuplicateWall.WallStates[(int)WallIndexes.Down] = (byte)WallStates.Damaged;
                                        break;
                                    case WallIndexes.Down:
                                        c_UpdatedDuplicateWall.WallStates[(int)WallIndexes.Up] = (byte)WallStates.Damaged;
                                        break;
                                    case WallIndexes.Left:
                                        c_UpdatedDuplicateWall.WallStates[(int)WallIndexes.Right] = (byte)WallStates.Damaged;
                                        break;
                                    case WallIndexes.Right:
                                        c_UpdatedDuplicateWall.WallStates[(int)WallIndexes.Left] = (byte)WallStates.Damaged;
                                        break;
                                    default:
                                        break;
                                }
                                ecb_Cleanup.SetComponent(na_GridEntities[l], c_UpdatedDuplicateWall);
                                Debug.Log($"Broken Wall: {c_UpdatedDuplicateWall}");
                            }                            
                        }                        
                    }
                }
            }
            ecb_Cleanup.DestroyEntity(na_BreakWallMessageEntities[i]);
        }

        na_BreakWallMessageEntities.Dispose();
        na_BreakWallMessages.Dispose();

        
        na_PlayerIndexes.Dispose();
        na_PlayerPositions.Dispose();

        na_GridEntities.Dispose();
        na_Grid.Dispose();
        return inputDependencies;
    }

}