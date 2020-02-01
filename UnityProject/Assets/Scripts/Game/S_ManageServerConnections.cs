using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;

[AlwaysUpdateSystem]
public class S_ManageServerConnections : JobComponentSystem
{
    public UdpNetworkDriver Driver;
    public UdpNetworkDriver.Concurrent ConcurrentDriver;
    public EntityCommandBufferSystem CreateConnectionsBarrier;

    private EntityQuery m_ConnectionsToServerQuery;
    private EntityArchetype m_NetworkConnectionArchetype;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_ConnectionsToServerQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { ComponentType.ReadWrite<C_NetworkConnection>(), ComponentType.ReadOnly<TC_ServerConnectionHandler>() }
        });
        
        m_NetworkConnectionArchetype = EntityManager.CreateArchetype(
            typeof(C_NetworkConnection),
            typeof(TC_ServerConnectionHandler),
            typeof(NMBF_NotifyPlayerText),
            typeof(NMBF_NotifyPlayerSound),
            typeof(NMBF_NotifyPlayerVisor));
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {        
        // Updates the driver
        JobHandle j_DriverUpdate = Driver.ScheduleUpdate(inputDependencies);

        NativeArray<Entity> na_ExistingConnectionsEntities = m_ConnectionsToServerQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<C_NetworkConnection> na_ExistingConnections = m_ConnectionsToServerQuery.ToComponentDataArray<C_NetworkConnection>(Allocator.TempJob);
        
        // Ensure Connections are validated and accepts new connections
        JobHandle j_ConnectionJob = new J_ServerUpdateConnections
        {
            R_ExistingConnectionsEntities = na_ExistingConnectionsEntities,
            R_ExistingConnections = na_ExistingConnections,            
            R_ServerConnectionArchetype = m_NetworkConnectionArchetype,
            R_GridDefinitions = GetSingleton<SINGLETON_GridDefinitions>(),

            RW_Driver = Driver,
            RW_CommandBuffer = CreateConnectionsBarrier.CreateCommandBuffer()
        }.Schedule(j_DriverUpdate);        

        // Reads from the connected clients
        JobHandle j_ServerUpdateJob = new J_ServerReceiveMessages
        {
            RW_Driver = ConcurrentDriver,
            RW_CommandBuffer = CreateConnectionsBarrier.CreateCommandBuffer().ToConcurrent()
        }.Schedule(this, j_ConnectionJob);


        JobHandle j_ServerSendTextMessages = new J_ServerSendTextMessages
        {
            RW_Driver = Driver
        }.ScheduleSingle(this, j_ServerUpdateJob);

        JobHandle j_ServerSendSoundMessages = new J_ServerSendSoundMessages
        {
            RW_Driver = Driver
        }.ScheduleSingle(this, j_ServerSendTextMessages);        

        return j_ServerSendSoundMessages;
    }


    private struct J_ServerUpdateConnections : IJob
    {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<C_NetworkConnection> R_ExistingConnections;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> R_ExistingConnectionsEntities;

        [ReadOnly] public EntityArchetype R_ServerConnectionArchetype;
        [ReadOnly] public SINGLETON_GridDefinitions R_GridDefinitions;

        public UdpNetworkDriver RW_Driver;
        public EntityCommandBuffer RW_CommandBuffer;

        public void Execute()
        {
            // Clean up connections
            for (int i = 0; i < R_ExistingConnections.Length; i++)
            {
                if (!R_ExistingConnections[i].Connection.IsCreated)
                {
                    RW_CommandBuffer.DestroyEntity(R_ExistingConnectionsEntities[i]);
                }
            }
            // Accept new connections
            NetworkConnection c;
            while ((c = RW_Driver.Accept()) != default(NetworkConnection))
            {
                Entity t_ConnectionToClient = RW_CommandBuffer.CreateEntity(R_ServerConnectionArchetype);
                RW_CommandBuffer.SetComponent(t_ConnectionToClient, new C_NetworkConnection
                {
                    Connection = c
                });
                RW_CommandBuffer.AddComponent<C_PlayerIndex>(t_ConnectionToClient);
                RW_CommandBuffer.SetComponent(t_ConnectionToClient, new C_PlayerIndex
                {
                    PlayerId = c.InternalId
                });
                RW_CommandBuffer.AddComponent<C_PlayerPos>(t_ConnectionToClient);
                Vector2Int t_StartingPos = default;
                switch (c.InternalId)
                {
                    case 0:
                        t_StartingPos = new Vector2Int(0, 0);
                        break;
                    case 1:
                        t_StartingPos = new Vector2Int(R_GridDefinitions.RowCount - 1, R_GridDefinitions.ColumnCount - 1);
                        break;
                    default:
                        break;
                }
                RW_CommandBuffer.SetComponent(t_ConnectionToClient, new C_PlayerPos
                {
                    Pos = t_StartingPos
                });

                Debug.Log($"Accepted a connection and placed new player {c.InternalId} at {t_StartingPos}");
            }
        }
    }

    [RequireComponentTag(typeof(TC_ServerConnectionHandler))]
    struct J_ServerReceiveMessages : IJobForEachWithEntity<C_NetworkConnection>
    {
        public UdpNetworkDriver.Concurrent RW_Driver;
        public EntityCommandBuffer.Concurrent RW_CommandBuffer;

        public void Execute(Entity p_Entity, int p_Index, ref C_NetworkConnection p_ServerConnection)
        {
            DataStreamReader stream;
            if (!p_ServerConnection.Connection.IsCreated)
                Assert.IsTrue(true);

            NetworkEvent.Type cmd;
            while ((cmd = RW_Driver.PopEventForConnection(p_ServerConnection.Connection, out stream)) !=
            NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    DataStreamReader.Context t_ReaderCtx = default(DataStreamReader.Context);
                    MessageFromClientTypes e_MsgType = (MessageFromClientTypes) stream.ReadInt(ref t_ReaderCtx);
                    Debug.Log($"Got {e_MsgType} from the Client.");

                    switch (e_MsgType)
                    {
                        case MessageFromClientTypes.ConnectionEstablished:
                            Debug.Log($"Connection is still established on Client {p_ServerConnection.ConnectionId}.");
                            break;

                        case MessageFromClientTypes.MovePlayer:
                            HandleAttemptMoveMessage(in p_Index, in p_ServerConnection, ref stream, ref t_ReaderCtx);
                            break;

                        case MessageFromClientTypes.BreakWall:
                            HandleAttemptBreakWall(in p_Index, in p_ServerConnection, ref stream, ref t_ReaderCtx);
                            break;

                        case MessageFromClientTypes.RepairWall:
                            HandleAttemptRepairWall(in p_Index, in p_ServerConnection, ref stream, ref t_ReaderCtx);
                            break;

                        case MessageFromClientTypes.AttemptShot:
                            HandleAttemptShootDirection(in p_Index, in p_ServerConnection, ref stream, ref t_ReaderCtx);
                            break;

                        default:
                            break;
                    }                    
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from server");
                    p_ServerConnection.Connection = default(NetworkConnection);
                }
            }
        }

        private void HandleAttemptMoveMessage(in int p_Index, in C_NetworkConnection p_ServerConnection, ref DataStreamReader stream, ref DataStreamReader.Context t_ReaderCtx)
        {
            WallIndexes e_Direction = (WallIndexes)stream.ReadInt(ref t_ReaderCtx);

            Entity t_MovePlayerRequest = RW_CommandBuffer.CreateEntity(p_Index);
            RW_CommandBuffer.AddComponent<MC_MovePlayer>(p_Index, t_MovePlayerRequest);
            RW_CommandBuffer.SetComponent(p_Index, t_MovePlayerRequest, new MC_MovePlayer
            {
                PlayerId = p_ServerConnection.ConnectionId,
                WallDirection = e_Direction
            });

            Debug.Log($"Attempting to move player in direction {e_Direction}.");            
        }

        private void HandleAttemptBreakWall(in int p_Index, in C_NetworkConnection p_ServerConnection, ref DataStreamReader stream, ref DataStreamReader.Context t_ReaderCtx)
        {
            WallIndexes e_Direction = (WallIndexes)stream.ReadInt(ref t_ReaderCtx);

            Entity t_MovePlayerRequest = RW_CommandBuffer.CreateEntity(p_Index);
            RW_CommandBuffer.AddComponent<MC_BreakWall>(p_Index, t_MovePlayerRequest);
            RW_CommandBuffer.SetComponent(p_Index, t_MovePlayerRequest, new MC_BreakWall
            {
                PlayerId = p_ServerConnection.ConnectionId,
                WallDirection = e_Direction
            });

            Debug.Log($"Attempting to damage the wall in direction {e_Direction}.");
        }

        private void HandleAttemptRepairWall(in int p_Index, in C_NetworkConnection p_ServerConnection, ref DataStreamReader stream, ref DataStreamReader.Context t_ReaderCtx)
        {
            WallIndexes e_Direction = (WallIndexes)stream.ReadInt(ref t_ReaderCtx);

            Entity t_MovePlayerRequest = RW_CommandBuffer.CreateEntity(p_Index);
            RW_CommandBuffer.AddComponent<MC_RepairWall>(p_Index, t_MovePlayerRequest);
            RW_CommandBuffer.SetComponent(p_Index, t_MovePlayerRequest, new MC_RepairWall
            {
                PlayerId = p_ServerConnection.ConnectionId,
                WallDirection = e_Direction
            });

            Debug.Log($"Attempting to repair the wall in direction {e_Direction}.");
        }

        private void HandleAttemptShootDirection(in int p_Index, in C_NetworkConnection p_ServerConnection, ref DataStreamReader stream, ref DataStreamReader.Context t_ReaderCtx)
        {
            WallIndexes e_Direction = (WallIndexes)stream.ReadInt(ref t_ReaderCtx);

            Entity t_MovePlayerRequest = RW_CommandBuffer.CreateEntity(p_Index);
            RW_CommandBuffer.AddComponent<MC_ShootInDirection>(p_Index, t_MovePlayerRequest);
            RW_CommandBuffer.SetComponent(p_Index, t_MovePlayerRequest, new MC_ShootInDirection
            {
                PlayerId = p_ServerConnection.ConnectionId,
                WallDirection = e_Direction
            });

            Debug.Log($"Attempting to shoot in direction {e_Direction}.");
        }
    }

    struct J_ServerSendTextMessages : IJobForEachWithEntity_EBC<NMBF_NotifyPlayerText, C_NetworkConnection>
    {
        public UdpNetworkDriver RW_Driver;

        public void Execute(Entity entity, int index, DynamicBuffer<NMBF_NotifyPlayerText> p_NotifyPlayerText, [ReadOnly] ref C_NetworkConnection p_Connection)
        {
            NMBF_NotifyPlayerText t_TextNotification;
            
            for (int i = 0; i < p_NotifyPlayerText.Length; i++)
            {
                t_TextNotification = p_NotifyPlayerText[i];

                using (var writer = new DataStreamWriter(8, Allocator.Temp))
                {   
                    writer.Write((int)MessageFromServerTypes.NotifyPlayerText);
                    writer.Write(t_TextNotification.PlayerTextHandle);
                    p_Connection.Connection.Send(RW_Driver, writer);
                }
            }

            p_NotifyPlayerText.Clear();
        }
    }

    struct J_ServerSendSoundMessages : IJobForEachWithEntity_EBC<NMBF_NotifyPlayerSound, C_NetworkConnection>
    {
        public UdpNetworkDriver RW_Driver;

        public void Execute(Entity entity, int index, DynamicBuffer<NMBF_NotifyPlayerSound> p_NotifyPlayerText, [ReadOnly] ref C_NetworkConnection p_Connection)
        {
            NMBF_NotifyPlayerSound t_SoundNotification;

            for (int i = 0; i < p_NotifyPlayerText.Length; i++)
            {
                t_SoundNotification = p_NotifyPlayerText[i];

                using (var writer = new DataStreamWriter(8, Allocator.Temp))
                {
                    writer.Write((int)MessageFromServerTypes.NotifyPlayerSound);
                    writer.Write(t_SoundNotification.SoundHandle);
                    p_Connection.Connection.Send(RW_Driver, writer);
                }
            }

            p_NotifyPlayerText.Clear();
        }
    }

}