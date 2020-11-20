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
    
    private readonly Dictionary<int, float> clientConnectionsTimeouts = new Dictionary<int, float>();
    private const float ClientConnectionTimeout = 1f;
    
    public GameObject cubePrefab;

    // Start is called before the first frame update
    void Start()
    {
        channel	= new Channel(ManagerPort);
    }

    // Update is called once per frame
    void Update()
    {
        // Listen for server packets
        var packet = channel.GetPacket();
        if (packet != null) {
            var buffer = packet.buffer;
            Deserialize(buffer);
        }

        UpdateConnectionTimeouts();
    }

    public void InitializePlayerConnection()
    {
        serverIp = PlayerPrefs.GetString("ServerIp", "127.0.0.1");
        serverIpEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), ServerPort);
        
        var clientId = Random.Range(0, int.MaxValue); // Collisions are unlikely
        SendClientConnection(clientId);
        Debug.Log("SENT CONNECTION REQUEST");
        clientConnectionsTimeouts.Add(clientId, ClientConnectionTimeout);
    }

    private void UpdateConnectionTimeouts()
    {
        var keys = clientConnectionsTimeouts.Keys.ToList();
        foreach (var clientId in keys)
        {
            var remainingTime = clientConnectionsTimeouts[clientId] - Time.deltaTime;
            // Check if timeout has been reached
            if (remainingTime <= 0)
            {
                SendClientConnection(clientId);
                Debug.Log("SENT CONNECTION REQUEST");
                clientConnectionsTimeouts[clientId] = ClientConnectionTimeout;
            }
            else
            {
                clientConnectionsTimeouts[clientId] = remainingTime;
            }
        }
    }

    private void SendClientConnection(int clientId)
    {
        var packet = Packet.Obtain();
        SerializeClientConnection(packet.buffer, clientId);
        packet.buffer.Flush();

        channel.Send(packet, serverIpEndPoint);
        packet.Free();
    }
    
    /*
     * -- FORMAT --
     * Packet Type (byte)
     * Client ID (int)
     */
    private static void SerializeClientConnection(BitBuffer buffer, int clientId) {
        buffer.PutByte((int) PacketType.Join);
        buffer.PutInt(clientId);
    }
    
    private void Deserialize(BitBuffer buffer)
    {
        var messageType = (PacketType) buffer.GetByte();

        switch (messageType)
        {
            case PacketType.PlayerJoinedResponse:
            {
                var clientId = buffer.GetInt();
                if (clientConnectionsTimeouts.ContainsKey(clientId))
                {
                    Debug.Log("RECV CONNECTION RESPONSE");
                    SavePlayerAttributes(buffer, clientId);
                    SceneManager.LoadScene("Client Game");
                }

                break;
            }
        }
    }

    private void SavePlayerAttributes(BitBuffer buffer, int clientId)
    {
        clientConnectionsTimeouts.Remove(clientId);
                
        /*var newClient = Instantiate(cubePrefab, transform);
        newClient.name = $"Client-{clientId}";
        newClient.layer = LayerMask.NameToLayer($"Client {usedClientLayersCount}"); // TODO CHANGE LAYER
        newClient.GetComponent<Renderer>().enabled = false;
    
        var clientEntity = newClient.AddComponent<ClientEntity>();*/ // TODO ADD PREFAB
    
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
    
        /*var clientColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
    
        clientEntity.InitializeClientEntity(clientPort, serverIp, serverPort, clientId, clientTime, displaySeq, minBufferElems, clientColor, 
            position, rotation, health, usedClientLayersCount, !createdFirstPlayer);
        usedClientLayersCount++;*/

        SaveConnectedPlayersAttributes(buffer/*, clientEntity*/);

        /*if (!createdFirstPlayer)
        {
            firstPlayer = clientEntity.gameObject;
            firstPlayer.AddComponent<FirstPersonView>(); // TODO ADD PREFAB
            //firstPlayer.AddComponent<ShootManager>();
            createdFirstPlayer = true;
        }*/
        
        PlayerPrefs.Save();
    }

    private void SaveConnectedPlayersAttributes(BitBuffer buffer/*, ClientEntity clientEntity*/)
    {
        var connectedPlayerCount = buffer.GetByte();
        PlayerPrefs.SetInt("ConnectedPlayerCount", connectedPlayerCount);
        
        for (var i = 1; i <= connectedPlayerCount; i++)
        {
            var clientId = buffer.GetInt();
            var position = new Vector3();
            var rotation = new Quaternion();

            position.x = buffer.GetFloat();
            position.y = buffer.GetFloat();
            position.z = buffer.GetFloat();
            rotation.w = buffer.GetFloat();
            rotation.x = buffer.GetFloat();
            rotation.y = buffer.GetFloat();
            rotation.z = buffer.GetFloat();
            
            PlayerPrefs.SetInt($"ConnectedPlayer{i}Id", clientId);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}PosX", position.x);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}PosY", position.y);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}PosZ", position.z);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}RotW", rotation.w);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}RotX", rotation.x);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}RotY", rotation.y);
            PlayerPrefs.SetFloat($"ConnectedPlayer{i}RotZ", rotation.z);

            //clientEntity.InitializeConnectedPlayer(clientId, position, rotation);
        }
    }
}
