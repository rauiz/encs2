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

    public Entity ClientEntity { get; private set; }

    protected override void OnCreate()
    {
        base.OnCreate();
        m_ConnectionsToClientQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { ComponentType.ReadWrite<C_NetworkConnection>(), ComponentType.ReadOnly<TC_ClientConnectionHandler>() }
        });

        m_ClientNetworkConnectionArchetype = EntityManager.CreateArchetype(
            typeof(C_NetworkConnection),
            typeof(TC_ClientConnectionHandler),
            typeof(NMBF_RequestServer)
            );
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        if (ClientEntity == Entity.Null)
        {
            NativeArray<Entity> na_ExistingConnectionEntities = m_ConnectionsToClientQuery.ToEntityArray(Allocator.TempJob);
            if (na_ExistingConnectionEntities.Length > 0)
                ClientEntity = na_ExistingConnectionEntities[0];
            na_ExistingConnectionEntities.Dispose();
        }

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
            NativeArray<Entity> na_ExistingConnectionEntities = m_ConnectionsToClientQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<C_NetworkConnection> na_ExistingConnections = m_ConnectionsToClientQuery.ToComponentDataArray<C_NetworkConnection>(Allocator.TempJob);
            // Reads from the connected clients
            return new J_ClientUpdate
            {
                R_ConnectionEntities = na_ExistingConnectionEntities,
                RW_Driver = Driver,
                RW_Connections = na_ExistingConnections,                
                
                RW_NetworkMessages = GetBufferFromEntity<NMBF_RequestServer>(false),
                RW_CommandBuffer = CreateConnectionsBarrier.CreateCommandBuffer()

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

            Entity t_OnClientConnectedMessage = RW_CommandBuffer.CreateEntity();
            RW_CommandBuffer.AddComponent(t_OnClientConnectedMessage, new MC_OnClientConnected { });
        }
    }

    [RequireComponentTag(typeof(TC_ClientConnectionHandler))]
    private struct J_ClientUpdate : IJob
    {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> R_ConnectionEntities;

        public BufferFromEntity<NMBF_RequestServer> RW_NetworkMessages;        
        [DeallocateOnJobCompletion] public NativeArray<C_NetworkConnection> RW_Connections;
        public UdpNetworkDriver RW_Driver;
        public EntityCommandBuffer RW_CommandBuffer;

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
                        writer.Write((int)MessageFromClientTypes.PingConnection);
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

                            Entity t_TextMessage = RW_CommandBuffer.CreateEntity();
                            RW_CommandBuffer.AddComponent<MC_OnTextNotified>(t_TextMessage);
                            RW_CommandBuffer.SetComponent(t_TextMessage, new MC_OnTextNotified
                            {
                                TextHandle = i_TextHandle
                            });
                            break;

                        case MessageFromServerTypes.NotifyPlayerVisor:
                            // writer.Write(t_TextNotification.PlayerText.LengthInBytes);
                            // writer.WriteBytes(ptr_StringBytes, t_TextNotification.PlayerText.LengthInBytes);
                            int i_FeedbackHandle = t_Stream.ReadInt(ref t_ReaderCtx);
                            int i_TileHandle = t_Stream.ReadInt(ref t_ReaderCtx);
                            int i_Intensity = t_Stream.ReadInt(ref t_ReaderCtx);
                            Debug.Log($"Writing Visor Handle {i_FeedbackHandle} at Tile #{i_TileHandle} with Intensity #{i_Intensity}");

                            Entity t_VisorMessage = RW_CommandBuffer.CreateEntity();
                            RW_CommandBuffer.AddComponent<MC_OnVisorNotified>(t_VisorMessage);
                            RW_CommandBuffer.SetComponent(t_VisorMessage, new MC_OnVisorNotified
                            {
                                FeedbackHandle = i_FeedbackHandle,
                                TileHandle = i_TileHandle,
                                Intensity = i_Intensity
                            });
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

            DynamicBuffer<NMBF_RequestServer> db_Requests = RW_NetworkMessages[R_ConnectionEntities[0]];
            NMBF_RequestServer nmbf_CurrentRequest;
            for (int i = 0; i < db_Requests.Length; i++)
            {
                nmbf_CurrentRequest = db_Requests[i];

                using (var writer = new DataStreamWriter(8, Allocator.Temp))
                {
                    writer.Write((int)nmbf_CurrentRequest.Type);
                    switch (nmbf_CurrentRequest.Type)
                    {
                        case MessageFromClientTypes.PingConnection:                                                        
                            break;
                        case MessageFromClientTypes.MovePlayer:
                        case MessageFromClientTypes.BreakWall:
                        case MessageFromClientTypes.RepairWall:
                        case MessageFromClientTypes.ShootPlayer:
                            writer.Write((int)nmbf_CurrentRequest.Index);                            
                            break;
                        default:
                            break;
                    }
                    
                    RW_Connections[0].Connection.Send(RW_Driver, writer);
                }
            }
            db_Requests.Clear();
        }
    }
}