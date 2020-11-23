using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionManager : MonoBehaviour
{
    // Channels to and from server
    private const int ServerPort = 9000;
    private const int ManagerPort = 9001; // TODO HARDCODED, CONFLICT WITH OTHER INSTANCES
    private Channel channel;
    private string serverIp;
    private IPEndPoint serverIpEndPoint;

    private int clientId;
    private string clientName;
    
    private float connectionTimeoutTimer;
    private const float ClientConnectionTimeout = 1f;
    
    //public GameObject cubePrefab;

    // Update is called once per frame
    void Update()
    {
        if (channel != null)
        {
            // Listen for server packets
            var packet = channel.GetPacket();
            if (packet != null) {
                var buffer = packet.buffer;
                Deserialize(buffer);
            }

            UpdateConnectionTimeouts();
        }
    }

    public void InitializePlayerConnection()
    {
        channel	= new Channel(ManagerPort);
        serverIp = PlayerPrefs.GetString("ServerIp", "127.0.0.1");
        serverIpEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), ServerPort);
        
        clientId = Random.Range(0, int.MaxValue); // Collisions are unlikely
        clientName = PlayerPrefs.GetString("PlayerName");
        SendClientConnection(clientId, clientName);
        Debug.Log("SENT CONNECTION REQUEST");
        
        connectionTimeoutTimer = ClientConnectionTimeout;
    }

    private void UpdateConnectionTimeouts()
    {
        var remainingTime = connectionTimeoutTimer - Time.deltaTime;
        // Check if timeout has been reached
        if (remainingTime <= 0)
        {
            SendClientConnection(clientId, clientName);
            Debug.Log("SENT CONNECTION REQUEST");
            connectionTimeoutTimer = ClientConnectionTimeout;
        }
        else
        {
            connectionTimeoutTimer = remainingTime;
        }
    }

    private void SendClientConnection(int clientId, string clientName)
    {
        var packet = Packet.Obtain();
        SerializeClientConnection(packet.buffer, clientId, clientName);
        packet.buffer.Flush();

        channel.Send(packet, serverIpEndPoint);
        packet.Free();
    }
    
    /*
     * -- FORMAT --
     * Packet Type (byte)
     * Client ID (int)
     */
    private static void SerializeClientConnection(BitBuffer buffer, int clientId, string clientName) {
        buffer.PutByte((int) PacketType.Join);
        buffer.PutInt(clientId);
        buffer.PutString(clientName);
    }
    
    private void Deserialize(BitBuffer buffer)
    {
        var messageType = (PacketType) buffer.GetByte();

        if (messageType == PacketType.PlayerJoinedResponse)
        {
            var recvClientId = buffer.GetInt();
            if (recvClientId == clientId)
            {
                SavePlayerAttributes(buffer);
                channel.Disconnect();
                SceneManager.LoadScene("Client Game");
            }
        }
    }

    private void SavePlayerAttributes(BitBuffer buffer)
    {
        var clientPort = buffer.GetInt();
        var serverPort = buffer.GetInt();
        var clientTime = buffer.GetFloat();
        var displaySeq = buffer.GetInt();
        var minBufferElems = buffer.GetByte();
        var position = new Vector3();
        var rotation = new Quaternion();
    
        position.x = buffer.GetFloat();
        position.y = buffer.GetFloat();
        position.z = buffer.GetFloat();
        rotation.w = buffer.GetFloat();
        rotation.x = buffer.GetFloat();
        rotation.y = buffer.GetFloat();
        rotation.z = buffer.GetFloat();

        var health = buffer.GetInt();
        var speed = buffer.GetFloat();
        var gravity = buffer.GetFloat();
        
        PlayerPrefs.SetInt("ClientId", clientId);
        PlayerPrefs.SetInt("ClientPort", clientPort);
        PlayerPrefs.SetInt("ServerPort", serverPort);
        PlayerPrefs.SetFloat("ClientTime", clientTime);
        PlayerPrefs.SetInt("DisplaySequence", displaySeq);
        PlayerPrefs.SetInt("MinInterpolationBufferElements", minBufferElems);
        PlayerPrefs.SetFloat("ClientPosX", position.x);
        PlayerPrefs.SetFloat("ClientPosY", position.y);
        PlayerPrefs.SetFloat("ClientPosZ", position.z);
        PlayerPrefs.SetFloat("ClientRotW", rotation.w);
        PlayerPrefs.SetFloat("ClientRotX", rotation.x);
        PlayerPrefs.SetFloat("ClientRotY", rotation.y);
        PlayerPrefs.SetFloat("ClientRotZ", rotation.z);
        PlayerPrefs.SetInt("ClientHealth", health);
        PlayerPrefs.SetFloat("PlayerSpeed", speed);
        PlayerPrefs.SetFloat("Gravity", gravity);

        SaveConnectedPlayersAttributes(buffer);
        
        PlayerPrefs.Save();
    }

    private void SaveConnectedPlayersAttributes(BitBuffer buffer)
    {
        var connectedPlayerCount = buffer.GetByte();
        PlayerPrefs.SetInt("ConnectedPlayerCount", connectedPlayerCount);
        
        for (var i = 1; i <= connectedPlayerCount; i++)
        {
            var otherClientId = buffer.GetInt();
            var otherClientName = buffer.GetString();
            var position = new Vector3();
            var rotation = new Quaternion();

            position.x = buffer.GetFloat();
            position.y = buffer.GetFloat();
            position.z = buffer.GetFloat();
            rotation.w = buffer.GetFloat();
            rotation.x = buffer.GetFloat();
            rotation.y = buffer.GetFloat();
            rotation.z = buffer.GetFloat();
            
            PlayerPrefs.SetInt($"ConnectedPlayer{i}Id", otherClientId);
            PlayerPrefs.SetString($"ConnectedPlayer{i}Name", otherClientName);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}PosX", position.x);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}PosY", position.y);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}PosZ", position.z);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}RotW", rotation.w);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}RotX", rotation.x);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}RotY", rotation.y);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}RotZ", rotation.z);
        }
    }
}
