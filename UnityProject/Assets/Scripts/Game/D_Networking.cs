using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

public class UG_Network : ComponentSystemGroup { }

public class B_EndOfFrame : EntityCommandBufferSystem { }


/////////// CLIENT TO SERVER MESSAGE REQUESTS

public enum MessageFromServerTypes : int 
{
    NotifyPlayerText = 1,
    NotifyPlayerVisor = 2
}

[InternalBufferCapacity(Buffer_Size)]
public struct NMBF_NotifyPlayerText : IBufferElementData
{
    public const int Buffer_Size = 10;

    public int PlayerId;
    public int PlayerTextHandle;
}

[InternalBufferCapacity(Buffer_Size)]
public struct NMBF_NotifyPlayerVisor : IBufferElementData
{
    public const int Buffer_Size = 10;

    public int Intensity;
    public int TileHandle;
    public int FeedbackHandle;
}


/////////// CLIENT TO SERVER MESSAGE REQUESTS

public enum MessageFromClientTypes : int { 
    PingConnection = 1, 
    MovePlayer = 2,  
    BreakWall = 3, 
    RepairWall = 4,

    ShootPlayer = 5
}

[InternalBufferCapacity(Buffer_Size)]
public struct NMBF_RequestServer : IBufferElementData
{
    public const int Buffer_Size = 10;

    public MessageFromClientTypes Type;    
    public WallIndexes Index;
}

public struct C_ConnectionRequest : IComponentData
{
    public NetworkEndPoint EndPoint;
}

public struct C_NetworkConnection : IComponentData
{
    public NetworkConnection Connection;
    public int ConnectionId;
}

public struct C_PlayerIndex : IComponentData
{
    public int PlayerId;
}

public struct TC_ServerConnectionHandler : IComponentData { }

public struct TC_ClientConnectionHandler : IComponentData { }
