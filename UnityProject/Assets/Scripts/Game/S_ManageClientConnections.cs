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
public class S_ManageClientConnections : JobComponentSystem
{
    public UdpNetworkDriver Driver;
    
    public EntityCommandBufferSystem CreateConnectionsBarrier;

    private EntityQuery m_ConnectionsToClientQuery;
    private EntityArchetype m_ClientNetworkConnectionArchetype;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_ConnectionsToClientQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { ComponentType.ReadWrite<C_NetworkConnection>(), ComponentType.ReadOnly<TC_ClientConnectionHandler>() }
        });

        m_ClientNetworkConnectionArchetype = EntityManager.CreateArchetype(ComponentType.ReadOnly<C_NetworkConnection>(), ComponentType.ReadOnly<TC_ClientConnectionHandler>());
    }


    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        if (!Driver.IsCreated) return inputDependencies;

        // Updates the driver
        JobHandle j_DriverUpdate = Driver.ScheduleUpdate(inputDependencies);       
        
        // Ensure Connections are validated and accepts new connections
        JobHandle j_ParseConnectionJob = new J_ConnectClientsToServer
        {
            R_ClientConnectionArchetype = m_ClientNetworkConnectionArchetype,

            RW_Driver = Driver,
            RW_CommandBuffer = CreateConnectionsBarrier.CreateCommandBuffer()
        }.ScheduleSingle(this, j_DriverUpdate);

        //CreateConnectionsBarrier.AddJobHandleForProducer(j_ParseConnectionJob);
                
        if (m_ConnectionsToClientQuery.CalculateEntityCount() != 0)
        {
            NativeArray<C_NetworkConnection> na_ExistingConnections = m_ConnectionsToClientQuery.ToComponentDataArray<C_NetworkConnection>(Allocator.TempJob);
            // Reads from the connected clients
            return new J_ClientUpdate
            {
                RW_Driver = Driver,
                RW_Connections = na_ExistingConnections
            }.Schedule(j_ParseConnectionJob);
        }
                
        return j_ParseConnectionJob;
    }

    private struct J_ConnectClientsToServer : IJobForEachWithEntity<C_ConnectionRequest>
    {
        [ReadOnly] public EntityArchetype R_ClientConnectionArchetype;

        public UdpNetworkDriver RW_Driver;
        public EntityCommandBuffer RW_CommandBuffer; 

        public void Execute(Entity entity, int index, ref C_ConnectionRequest p_ConnectionRequest)
        {
            NetworkConnection t_Connection = RW_Driver.Connect(p_ConnectionRequest.EndPoint);

            Entity t_ClientConnectionEntity = RW_CommandBuffer.CreateEntity(R_ClientConnectionArchetype);
            RW_CommandBuffer.SetComponent(t_ClientConnectionEntity, new C_NetworkConnection
            {
                Connection = t_Connection,
                ConnectionId = t_Connection.InternalId
            });            

            RW_CommandBuffer.DestroyEntity(entity);
        }
    }

    [RequireComponentTag(typeof(TC_ClientConnectionHandler))]
    private struct J_ClientUpdate : IJob
    {        
        [DeallocateOnJobCompletion] public NativeArray<C_NetworkConnection> RW_Connections;
        public UdpNetworkDriver RW_Driver;

        public void Execute() 
        {
            if (RW_Connections.Length < 1 || !RW_Connections[0].Connection.IsCreated)
            {                
                Debug.Log("Something went wrong during connect");
                return;
            }
            DataStreamReader t_Stream;
            NetworkEvent.Type cmd;

            while ((cmd = RW_Connections[0].Connection.PopEvent(RW_Driver, out t_Stream)) !=
                   NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    Debug.Log("We are now connected to the server");
                    
                    using (var writer = new DataStreamWriter(4, Allocator.Temp))
                    {
                        writer.Write((int)MessageFromClientTypes.ConnectionEstablished);
                        RW_Connections[0].Connection.Send(RW_Driver, writer);
                    }

                    using (var writer = new DataStreamWriter(8, Allocator.Temp))
                    {
                        writer.Write((int)MessageFromClientTypes.AttemptMovePlayer);
                        writer.Write((int)WallIndexes.Down);
                        RW_Connections[0].Connection.Send(RW_Driver, writer);
                    }

                    using (var writer = new DataStreamWriter(8, Allocator.Temp))
                    {
                        writer.Write((int)MessageFromClientTypes.BreakWall);
                        writer.Write((int)WallIndexes.Down);
                        RW_Connections[0].Connection.Send(RW_Driver, writer);
                    }

                    using (var writer = new DataStreamWriter(8, Allocator.Temp))
                    {
                        writer.Write((int)MessageFromClientTypes.RepairWall);
                        writer.Write((int)WallIndexes.Down);
                        RW_Connections[0].Connection.Send(RW_Driver, writer);
                    }

                    using (var writer = new DataStreamWriter(8, Allocator.Temp))
                    {
                        writer.Write((int)MessageFromClientTypes.AttemptShot);
                        writer.Write((int)WallIndexes.Down);
                        RW_Connections[0].Connection.Send(RW_Driver, writer);
                    }
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    DataStreamReader.Context t_ReaderCtx = default(DataStreamReader.Context);
                    MessageFromServerTypes e_MsgType = (MessageFromServerTypes)t_Stream.ReadInt(ref t_ReaderCtx);
                    Debug.Log($"Got {e_MsgType} from the Server.");

                    switch (e_MsgType)
                    {
                        case MessageFromServerTypes.NotifyPlayerText:                            
                            // writer.Write(t_TextNotification.PlayerText.LengthInBytes);
                            // writer.WriteBytes(ptr_StringBytes, t_TextNotification.PlayerText.LengthInBytes);
                            int i_TextHandle = t_Stream.ReadInt(ref t_ReaderCtx);
                            Debug.Log($"Writing TextHandle {i_TextHandle}");
                            break;

                        case MessageFromServerTypes.NotifyPlayerSound:
                            // writer.Write(t_TextNotification.PlayerText.LengthInBytes);
                            // writer.WriteBytes(ptr_StringBytes, t_TextNotification.PlayerText.LengthInBytes);
                            int i_SoundHandle = t_Stream.ReadInt(ref t_ReaderCtx);
                            Debug.Log($"Writing SoundHandle {i_SoundHandle}");
                            break;
                        default:
                            break;
                    }
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client got disconnected from server");
                    RW_Connections[0] = new C_NetworkConnection
                    {
                        Connection = default(NetworkConnection)
                    };
                }
            }
        }
    }
}