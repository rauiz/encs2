using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public static class U_ServerUtils
{
    
    public static void NotifyPlayerText(ref EntityCommandBuffer p_Buffer, 
        in NativeArray<Entity> p_PlayerEntities,
        in NativeArray<C_PlayerIndex> na_PlayerIndexes,
        int p_PlayerId,
        int p_TextHandle)
    {
        for (int i = 0; i < p_PlayerEntities.Length; i++)
        {
            if (na_PlayerIndexes[i].PlayerId != p_PlayerId) continue;
            DynamicBuffer<NMBF_NotifyPlayerText> p_NotifyTextBuffer = p_Buffer.SetBuffer<NMBF_NotifyPlayerText>(p_PlayerEntities[p_PlayerId]);
            p_NotifyTextBuffer.Add(new NMBF_NotifyPlayerText
            {
                PlayerTextHandle = p_TextHandle
            });
        }        
    }

    public static void NotifyEnemyText(ref EntityCommandBuffer p_Buffer,
        in NativeArray<Entity> p_PlayerEntities,
        in NativeArray<C_PlayerIndex> na_PlayerIndexes,
        int p_PlayerId,
        int p_TextHandle)
    {
        for (int i = 0; i < p_PlayerEntities.Length; i++)
        {
            if (na_PlayerIndexes[i].PlayerId == p_PlayerId) continue;
            DynamicBuffer<NMBF_NotifyPlayerText> p_NotifyTextBuffer = p_Buffer.SetBuffer<NMBF_NotifyPlayerText>(p_PlayerEntities[p_PlayerId]);
            p_NotifyTextBuffer.Add(new NMBF_NotifyPlayerText
            {
                PlayerTextHandle = p_TextHandle
            });
        }
    }

    public static void NotifyEnemyVisor(ref EntityCommandBuffer p_Buffer,
        in NativeArray<Entity> p_PlayerEntities,
        in NativeArray<C_PlayerIndex> na_PlayerIndexes,
        in NativeArray<C_PlayerPos> na_PlayerPos,
        int p_PlayerId,
        int p_FeedbackType,
        out int p_AdjacentRoomMapping)
    {

        Vector2Int t_PlayerPos = default;
        Vector2Int t_EnemyPos = default;
        Entity t_EnemyEntity = default;
        for (int i = 0; i < p_PlayerEntities.Length; i++)
        {
            if (na_PlayerIndexes[i].PlayerId == p_PlayerId)
                t_PlayerPos = na_PlayerPos[i].Pos;
            else
            {
                t_EnemyEntity = p_PlayerEntities[i];
                t_EnemyPos = na_PlayerPos[i].Pos;
            }   
        }
        NMBF_NotifyPlayerVisor nmbf_Message = new NMBF_NotifyPlayerVisor
        {
            FeedbackHandle = p_FeedbackType
        };

        U_ServerUtils
            .MapEnemyToVisor(t_PlayerPos, t_EnemyPos, out nmbf_Message.TileHandle, out nmbf_Message.Intensity);

        p_AdjacentRoomMapping = nmbf_Message.TileHandle;

        DynamicBuffer<NMBF_NotifyPlayerVisor> p_NotifyVisorBuffer = p_Buffer.SetBuffer<NMBF_NotifyPlayerVisor>(t_EnemyEntity);
        p_NotifyVisorBuffer.Add(nmbf_Message);
    }

    public static void MapEnemyToVisor(
        Vector2Int p_PlayerPos,
        Vector2Int p_EnemyPos,        
        out int p_VisorTile,
        out int p_Intensity)
    {
        Vector2Int t_Distance = p_PlayerPos - p_EnemyPos;

        if (t_Distance.x < 0 && t_Distance.y < 0) p_VisorTile = 0;
        else if (t_Distance.x == 0 && t_Distance.y < 0) p_VisorTile = 1;
        else if (t_Distance.x > 0 && t_Distance.y < 0) p_VisorTile = 2;

        else if (t_Distance.x < 0 && t_Distance.y == 0) p_VisorTile = 3;
        else if (t_Distance.x == 0 && t_Distance.y == 0) p_VisorTile = 4;
        else if (t_Distance.x > 0 && t_Distance.y == 0) p_VisorTile = 5;

        else if (t_Distance.x < 0 && t_Distance.y > 0) p_VisorTile = 6;
        else if (t_Distance.x == 0 && t_Distance.y > 0) p_VisorTile = 7;
        else if (t_Distance.x > 0 && t_Distance.y > 0) p_VisorTile = 8;
        else p_VisorTile = -1;

        t_Distance = new Vector2Int { x = Mathf.Abs(t_Distance.x), y = Mathf.Abs(t_Distance.y) };
        p_Intensity = Mathf.FloorToInt(Mathf.Abs(t_Distance.magnitude));
    }


    public static WallIndexes MapTilePositionToIndex(int p_TilePosition)
    {
        switch (p_TilePosition)
        {
            case 0:
            case 2:
            case 6:
            case 8:
                return WallIndexes.Diagonal;
            case 1:
                return WallIndexes.Up;
            case 3:
                return WallIndexes.Left;
            case 5:
                return WallIndexes.Right;
            case 7:
                return WallIndexes.Down;
            case 4:
            default:
                return WallIndexes.Center;
        }
        
    }
}
