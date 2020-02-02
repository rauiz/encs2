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
    private S_ManageClientConnections s_ManageClientConnections;

    private CommandWindow m_CommandWindow;
    private FeedbackWindow m_FeedbackWindow;
    private GameAssetDatabase m_AssetDatabase;

    void Awake()
    {
        // Client Initialization
        m_ClientDriver = new UdpNetworkDriver(new NetworkConfigParameter { connectTimeoutMS = 35000, disconnectTimeoutMS = 35000, maxConnectAttempts = 10, maxFrameTimeMS = 10 });

        m_ClientWorld = new World($"Client World #{GetInstanceID()}");

        b_EndOfFrame = m_ClientWorld.CreateSystem<B_EndOfFrame>();

        s_ManageClientConnections = m_ClientWorld.CreateSystem<S_ManageClientConnections>();
        s_ManageClientConnections.Driver = m_ClientDriver;
        s_ManageClientConnections.CreateConnectionsBarrier = b_EndOfFrame;

        S_HandleClientConnectionEvent s_HandleClientConnectionEvent = m_ClientWorld.CreateSystem<S_HandleClientConnectionEvent>();
        S_HandleTextReceived s_HandleTextReceived = m_ClientWorld.CreateSystem<S_HandleTextReceived>();
        s_HandleTextReceived.CleanupMessageBarrier = b_EndOfFrame;

        S_HandleVisorMessageReceived s_HandleVisorMessageReceived = m_ClientWorld.CreateSystem<S_HandleVisorMessageReceived>();
        s_HandleVisorMessageReceived.CleanupMessageBarrier = b_EndOfFrame;

        ug_Network = m_ClientWorld.CreateSystem<UG_Network>();
        ug_Network.AddSystemToUpdateList(s_ManageClientConnections);
        ug_Network.AddSystemToUpdateList(s_HandleClientConnectionEvent);
        ug_Network.AddSystemToUpdateList(s_HandleTextReceived);
        ug_Network.AddSystemToUpdateList(s_HandleVisorMessageReceived);        

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
        if (m_CommandWindow == null)
        {
            m_CommandWindow = FindObjectOfType<CommandWindow>();
            if (m_CommandWindow != null) { 
                m_CommandWindow.onMessageSentCallback += OnMessageSentCallback;
                m_ClientWorld.GetExistingSystem<S_HandleTextReceived>().CommandWindow = m_CommandWindow;
            }
        }

        if(m_FeedbackWindow == null)
        {
            m_FeedbackWindow = FindObjectOfType<FeedbackWindow>();
            if(m_FeedbackWindow != null)
            {
                m_ClientWorld.GetExistingSystem<S_HandleVisorMessageReceived>().FeedbackWindow = m_FeedbackWindow;
            }
        }

        if(m_AssetDatabase == null)
        {
            m_AssetDatabase = FindObjectOfType<GameAssetDatabase>();
            if(m_AssetDatabase != null)
            {
                m_ClientWorld.GetExistingSystem<S_HandleTextReceived>().GameAssetDatabase = m_AssetDatabase;                
            }
        }

        ug_Network.Update();

        m_ClientWorld.EntityManager.CompleteAllJobs();
        b_EndOfFrame.Update();   
    }

    private void OnDestroy()
    {
        m_ClientWorld.Dispose();
        m_ClientDriver.Dispose();
    }

    private string OnMessageSentCallback(string p_TypedText)
    {
        string[] s_CommandStrings = p_TypedText.Split();

        if (s_CommandStrings.Length > 2)
            return "Do not speak in long sentences. I.M.P's vocalization is dissapointing in long stretches.";
        
        if (s_CommandStrings.Length < 2)
            return "Leave it to this &#!!@#!&#! I.M.P to not complete the command.";

        string s_Command = s_CommandStrings[0];
        string s_Direction = s_CommandStrings[1];

        string s_ReturnText = p_TypedText;

        MessageFromClientTypes e_MessageType = MessageFromClientTypes.PingConnection;
        switch (s_Command.ToLower())
        {
            case "break":
                e_MessageType = MessageFromClientTypes.BreakWall;
                break;
            case "move":
                e_MessageType = MessageFromClientTypes.MovePlayer;
                break;
            case "build":
                e_MessageType = MessageFromClientTypes.RepairWall;
                break;
            case "shoot":
                e_MessageType = MessageFromClientTypes.ShootPlayer;
                break;
            default:
                s_ReturnText = "Invalid Command - I.M.P (Ignorant Moving Personality)";
                break;
        }

        switch (s_Direction.ToLower())
        {
            case "up":
                m_ClientWorld.EntityManager.GetBuffer<NMBF_RequestServer>(s_ManageClientConnections.ClientEntity)
                    .Add(new NMBF_RequestServer
                    {
                        Index = WallIndexes.Up,
                        Type = e_MessageType
                    });
                break;
            case "down":
                m_ClientWorld.EntityManager.GetBuffer<NMBF_RequestServer>(s_ManageClientConnections.ClientEntity)
                    .Add(new NMBF_RequestServer
                    {
                        Index = WallIndexes.Down,
                        Type = e_MessageType
                    });
                break;
            case "left":
                m_ClientWorld.EntityManager.GetBuffer<NMBF_RequestServer>(s_ManageClientConnections.ClientEntity)
                    .Add(new NMBF_RequestServer
                    {
                        Index = WallIndexes.Left,
                        Type = e_MessageType
                    });
                break;
            case "right":
                m_ClientWorld.EntityManager.GetBuffer<NMBF_RequestServer>(s_ManageClientConnections.ClientEntity)
                    .Add(new NMBF_RequestServer{
                        Index = WallIndexes.Right,
                        Type = e_MessageType
                    });
                break;

            default:
                s_ReturnText = "Invalid Direction -- I only know Up, Down, Left or Right";
                break;
        }

        return s_ReturnText;
    }
}
