using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

public class BV_Client : MonoBehaviour
{
    private World m_ClientWorld;

    private UdpNetworkDriver m_ClientDriver;
    private NetworkEndPoint m_ServerEndpoint;

    private UG_Network ug_Network;
    private B_EndOfFrame b_EndOfFrame;

    private EntityQuery m_ClientConnectionsQuery;

    void Awake()
    {
        // Client Initialization
        m_ClientDriver = new UdpNetworkDriver(new INetworkParameter[0]);

        m_ClientWorld = new World($"Client World #{GetInstanceID()}");
        m_ClientConnectionsQuery = m_ClientWorld.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadWrite<C_NetworkConnection>(), ComponentType.ReadOnly<TC_ClientConnectionHandler>() }
        });

        b_EndOfFrame = m_ClientWorld.CreateSystem<B_EndOfFrame>();

        S_ManageClientConnections s_ManageClientConnections = m_ClientWorld.CreateSystem<S_ManageClientConnections>();
        s_ManageClientConnections.Driver = m_ClientDriver;
        s_ManageClientConnections.CreateConnectionsBarrier = b_EndOfFrame;

        ug_Network = m_ClientWorld.CreateSystem<UG_Network>();
        ug_Network.AddSystemToUpdateList(s_ManageClientConnections);

        ug_Network.SortSystemUpdateList();
    }

    public void UpdateConnectionEndPoint(NetworkEndPoint p_EndPoint, ushort p_Port)
    {
        m_ServerEndpoint = p_EndPoint;
        m_ServerEndpoint.Port = p_Port;
    }

    public void UpdateConnectionEndPoint(string p_Ip, ushort p_Port)
    {
        m_ServerEndpoint = NetworkEndPoint.Parse(p_Ip, p_Port);
    }

    public void Connect()
    {
        Entity t_ConnectionRequest = m_ClientWorld.EntityManager.CreateEntity();
        m_ClientWorld.EntityManager.AddComponent<C_ConnectionRequest>(t_ConnectionRequest);
        m_ClientWorld.EntityManager.SetComponentData(t_ConnectionRequest, new C_ConnectionRequest
        {
            EndPoint = m_ServerEndpoint
        });
    }

    // Update is called once per frame
    void Update()
    {
        ug_Network.Update();

        m_ClientWorld.EntityManager.CompleteAllJobs();
        b_EndOfFrame.Update();   
    }

    private void OnDestroy()
    {
        m_ClientWorld.Dispose();
        m_ClientDriver.Dispose();
    }
}
