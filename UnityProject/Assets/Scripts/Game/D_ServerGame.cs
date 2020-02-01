using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

public enum WallStates : byte { Open = 0, Closed = 1, Damaged = 2 }

public enum WallIndexes : int { Up = 0, Down = 1, Left = 2, Right = 3 }

public struct TSINGLETON_UninitializedGrid : IComponentData { }

public struct SINGLETON_GridDefinitions : IComponentData
{
    public int ColumnCount;
    public int RowCount;
}

public unsafe struct C_GridPos : IComponentData
{
    public const int Wall_Count = 4;

    public Vector2Int Pos;
    public fixed byte WallStates[Wall_Count];

    public override string ToString()
    {
        StringBuilder t_Builder = new StringBuilder();
        t_Builder.AppendLine($"Pos - {Pos}");

        unsafe
        {
            for (int i = 0; i < Wall_Count; i++)
            {
                t_Builder.Append($"Wall Index [{((WallIndexes)i).ToString()}] ");                
                t_Builder.Append($"State [{(WallStates)WallStates[i]}] ");
                t_Builder.Append("\n");
            }
        }

        return t_Builder.ToString();
    }
}

public struct C_PlayerPos : IComponentData
{
    public Vector2Int Pos;
}

public struct C_RepairingWall : IComponentData
{
    public WallIndexes WallDirection;
    public int CurrentCount;
}

public struct C_DestroyingWall : IComponentData
{
    public WallIndexes WallDirection;
    public int CurrentCount;
}

public struct MC_MovePlayer : IComponentData
{
    public int PlayerId;
    public WallIndexes WallDirection;
}

public struct MC_RepairWall : IComponentData 
{
    public int PlayerId;
    public WallIndexes WallDirection;
}

public struct MC_BreakWall : IComponentData
{
    public int PlayerId;
    public WallIndexes WallDirection;
}

public struct MC_ShootInDirection : IComponentData
{
    public int PlayerId;
    public WallIndexes WallDirection;
}
