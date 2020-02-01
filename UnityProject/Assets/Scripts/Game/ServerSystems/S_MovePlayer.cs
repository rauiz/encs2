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
        C_PlayerPos c_CurrentPlayerPos;
        C_GridPos c_CurrentGridPos;
        for (int i = 0; i < na_MovePlayerMessages.Length; i++)
        {
            mc_CurrentMessage = na_MovePlayerMessages[i];

            for (int j = 0; j < na_PlayerIndexes.Length; j++)
            {
                if (mc_CurrentMessage.PlayerId != na_PlayerIndexes[j].PlayerId) continue;

                c_CurrentPlayerPos = na_PlayerPositions[j];
                for (int k = 0; k < na_Grid.Length; k++)
                {
                    if (c_CurrentPlayerPos.Pos != na_Grid[k].Pos) continue;
                    c_CurrentGridPos = na_Grid[k];

                    unsafe {

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

                            if((t_NewPos.x >= 0 && t_NewPos.x < t_GridDef.RowCount) &&
                                (t_NewPos.y >= 0 && t_NewPos.y < t_GridDef.ColumnCount))
                            {
                                ecb_Cleanup.SetComponent(na_PlayerEntities[j], new C_PlayerPos
                                {
                                    Pos = t_NewPos
                                });
                                DynamicBuffer<NMBF_NotifyPlayerText> db_NotifyText = ecb_Cleanup.SetBuffer<NMBF_NotifyPlayerText>(na_PlayerEntities[j]);
                                db_NotifyText.Add(new NMBF_NotifyPlayerText
                                {
                                    PlayerTextHandle = Random.Range(0, 10)
                                });
                            }
                            else
                            {
                                Debug.Log($"Wall leads to nowhere. Stayed in {c_CurrentPlayerPos.Pos}.");
                            }                            
                        }
                        else
                        {
                            Debug.Log($"Wall is closed. Cannot Move in the {mc_CurrentMessage.WallDirection} Direction");
                        }

                        
                    }
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