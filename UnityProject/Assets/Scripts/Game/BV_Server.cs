using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

public class BV_Server : MonoBehaviour
{
    private World m_ServerWorld;

    private UdpNetworkDriver m_ServerDriver;

    private UG_Network ug_ServerNetwork;
    private B_EndOfFrame b_EndOfFrame;

    public void Initialize(ushort p_Port)
    {
        // Server Initialization        
        m_ServerDriver = new UdpNetworkDriver(new INetworkParameter[0]);

        NetworkEndPoint t_ServerEndpoint = NetworkEndPoint.AnyIpv4;
        t_ServerEndpoint.Port = p_Port;
        if (m_ServerDriver.Bind(t_ServerEndpoint) != 0)
            Debug.Log($"Failed to bind to port {p_Port}");
        else
            m_ServerDriver.Listen();

        m_ServerWorld = new World("Server World");

        b_EndOfFrame = m_ServerWorld.CreateSystem<B_EndOfFrame>();

        S_ManageServerConnections s_ManageServerConnections = m_ServerWorld.CreateSystem<S_ManageServerConnections>();
        s_ManageServerConnections.Driver = m_ServerDriver;
        s_ManageServerConnections.ConcurrentDriver = m_ServerDriver.ToConcurrent();
        s_ManageServerConnections.CreateConnectionsBarrier = b_EndOfFrame;

        S_GenerateGrid s_GenerateGrid = m_ServerWorld.CreateSystem<S_GenerateGrid>(5, 5);

        S_MovePlayer s_MovePlayer = m_ServerWorld.CreateSystem<S_MovePlayer>();
        s_MovePlayer.CleanupMessageBarrier = b_EndOfFrame;

        ug_ServerNetwork = m_ServerWorld.CreateSystem<UG_Network>();
        ug_ServerNetwork.AddSystemToUpdateList(s_ManageServerConnections);
        ug_ServerNetwork.AddSystemToUpdateList(s_GenerateGrid);
        ug_ServerNetwork.AddSystemToUpdateList(s_MovePlayer);        

        ug_ServerNetwork.SortSystemUpdateList();
    }

    // Update is called once per frame
    void Update()
    {
        ug_ServerNetwork.Update();

        m_ServerWorld.EntityManager.CompleteAllJobs();
        b_EndOfFrame.Update();
    }

    private void OnDestroy()
    {
        m_ServerWorld.Dispose();
        m_ServerDriver.Dispose();
    }
}
