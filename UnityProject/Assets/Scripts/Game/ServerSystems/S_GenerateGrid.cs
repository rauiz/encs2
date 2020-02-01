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
public class S_GenerateGrid : JobComponentSystem
{
    public int GridRows { get; set; }
    public int GridColumns { get; set; }


    public S_GenerateGrid(int p_RowCount, int p_ColumnCount) 
    {
        GridRows = p_RowCount;
        GridColumns = p_ColumnCount;
    }

    protected override void OnCreate()
    {
        base.OnCreate();

        Entity t_GridDefinitions = EntityManager.CreateEntity();
        EntityManager.AddComponent<SINGLETON_GridDefinitions>(t_GridDefinitions);
        EntityManager.AddComponent<TSINGLETON_UninitializedGrid>(t_GridDefinitions);        
        EntityManager.SetComponentData(t_GridDefinitions, new SINGLETON_GridDefinitions
        {
            RowCount = GridRows,
            ColumnCount = GridColumns
        });        
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        Entity t_GridDefinitionsEntity = GetSingletonEntity<SINGLETON_GridDefinitions>();
        SINGLETON_GridDefinitions singleton_GridDefinitions = GetSingleton<SINGLETON_GridDefinitions>();

        if(!EntityManager.HasComponent<TSINGLETON_UninitializedGrid>(t_GridDefinitionsEntity))
            return inputDependencies;

        Entity t_GridPosEntity;
        for (int i = 0; i < singleton_GridDefinitions.RowCount; i++)
        {
            for (int j = 0; j < singleton_GridDefinitions.ColumnCount; j++)
            {
                C_GridPos c_Pos = new C_GridPos
                {
                    Pos = new Vector2Int(i, j)
                };

                unsafe
                {
                    for (int k = 0; k < C_GridPos.Wall_Count; k++)
                    {
                        c_Pos.WallStates[k] = (byte)WallStates.Damaged;
                    }
                }

                t_GridPosEntity = EntityManager.CreateEntity();
                EntityManager.AddComponent<C_GridPos>(t_GridPosEntity);
                EntityManager.SetComponentData(t_GridPosEntity, c_Pos);

                Debug.Log($"Created {c_Pos.ToString()}.");
            }
        }

        EntityManager.RemoveComponent<TSINGLETON_UninitializedGrid>(t_GridDefinitionsEntity);
        return inputDependencies;
    }

}