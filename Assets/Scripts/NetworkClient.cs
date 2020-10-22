using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;

    public GameObject cube;

    private GameObject playerController;
    private PlayerUpdateMsg playerUpdateMsg;
    private Dictionary<string, GameObject> otherPlayers;

    
    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);

        otherPlayers = new Dictionary<string, GameObject>();


        playerUpdateMsg = new PlayerUpdateMsg();
        playerUpdateMsg.player.cubeColor = new Color(
            UnityEngine.Random.Range(0.0f, 1.0f), 
            UnityEngine.Random.Range(0.0f, 1.0f), 
            UnityEngine.Random.Range(0.0f, 1.0f));

        playerController = Instantiate(cube);
        playerController.AddComponent<PlayerControls>();
        playerUpdateMsg.player.cubPos = playerController.transform.position;
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");

        //// Example to send a handshake message:
         HandshakeMsg m = new HandshakeMsg();
         m.player.id = m_Connection.InternalId.ToString();
         SendToServer(JsonUtility.ToJson(m));
    }

    IEnumerator SendUpdateToServer()
    {
        while (true)
        {
            if (playerController)
            {
                playerUpdateMsg.player.cubPos = playerController.transform.position;
                SendToServer(JsonUtility.ToJson(playerUpdateMsg));
                Debug.Log("Position: " +playerController.transform.position);
            }
            yield return new WaitForSeconds(1);
        }
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                Debug.Log("Handshake message received!");
                break;

            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player update message received!");
                break;

            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
                break;
            
            case Commands.PLAYER_LOGIN:
                PlayerLoginMsg loginMsg = JsonUtility.FromJson<PlayerLoginMsg>(recMsg);
                playerUpdateMsg.player.id = loginMsg.loginID;
                HandshakeMsg m = new HandshakeMsg();
                m.player = playerUpdateMsg.player;
                m.player.id = loginMsg.loginID;
                SendToServer(JsonUtility.ToJson(m));
                otherPlayers.Add(m.player.id, playerController);
                Debug.Log("ID: "+loginMsg.loginID);
                break;

            case Commands.PLAYER_DISCONNECT:
                PlayerDisconnectMsg discMsg = JsonUtility.FromJson<PlayerDisconnectMsg>(recMsg);
                GameObject player = otherPlayers[discMsg.discID];
                otherPlayers.Remove(discMsg.discID);
                Destroy(player);
                Debug.Log("Disconnect: "+discMsg.discID);
                break;

            default:
                Debug.Log("Unrecognized message received!");
                break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }

    void UpdateServer(ServerUpdateMsg updateMsg)
    {
        foreach (NetworkObjects.NetworkPlayer player in updateMsg.players)
        {
            if (!otherPlayers.ContainsKey(player.id) && otherPlayers.Count < updateMsg.players.Count)
            {
                GameObject newPlayer = Instantiate(cube);
                newPlayer.transform.position = player.cubPos;
                otherPlayers.Add(player.id, newPlayer);
            }
            else if (player.id != playerUpdateMsg.player.id)
            {
                if (otherPlayers.ContainsKey(player.id))
                {
                    otherPlayers[player.id].transform.position = player.cubPos;
                }
            }
        }
    }

    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }
}