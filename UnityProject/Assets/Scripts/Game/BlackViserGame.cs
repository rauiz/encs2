using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UI;

public class BlackViserGame : MonoBehaviour
{
    private BV_Server m_Server;
    private BV_Client m_Client;

    public Button btn_StartServer;

    public InputField txt_ClientIpConnect;
    public Button btn_StartClient;

    // Start is called before the first frame update
    void Start()
    {
        btn_StartServer.onClick.AddListener(new UnityEngine.Events.UnityAction(() =>
        {
            m_Server = new GameObject("BV_Server").AddComponent<BV_Server>();
            m_Server.Initialize(9000);
        }));

        btn_StartClient.onClick.AddListener(new UnityEngine.Events.UnityAction(() =>
        {
            m_Client = new GameObject("BV_Client").AddComponent<BV_Client>();
            if(string.IsNullOrEmpty(txt_ClientIpConnect.text))
                m_Client.UpdateConnectionEndPoint(NetworkEndPoint.LoopbackIpv4, 9000);
            else
                m_Client.UpdateConnectionEndPoint(txt_ClientIpConnect.text, 9000);

            m_Client.Connect();
        }));
    }    
}
