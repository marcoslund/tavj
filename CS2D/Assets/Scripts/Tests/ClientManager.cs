using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEditor.UIElements;
using UnityEngine;

public class ClientManager : MonoBehaviour
{
    // Channels to and from server
    private const int ServerPort = 9000;
    private const int ManagerPort = 9001;
    private Channel channel;
    private string serverIp;
    private IPEndPoint serverIpEndPoint;
    
    public ServerEntity serverEntity; // TODO DELETE

    private int clientCounter = 0;
    private readonly Dictionary<int, float> clientConnectionsTimeouts = new Dictionary<int, float>();
    private const float ClientConnectionTimeout = 1f;
    
    public GameObject cubePrefab;

    public int maxClientCount = 10;
    private int usedClientLayersCount = 1;
    
    private bool createdFirstPlayer;
    [HideInInspector] public GameObject firstPlayer;
    
    // Start is called before the first frame update
    void Start()
    {
        channel	= new Channel(ManagerPort);
        serverIp = PlayerPrefs.GetString("ServerIP", "127.0.0.1");
        serverIpEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), ServerPort);
    }

    // Update is called once per frame
    void Update()
    {
        // Listen for new connection requests
        if (Input.GetKeyDown(KeyCode.C) && clientCounter < maxClientCount)
        {
            int clientId = Random.Range(0, int.MaxValue); // Collisions are unlikely
            SendClientConnection(clientId);
            clientConnectionsTimeouts.Add(clientId, ClientConnectionTimeout);
            clientCounter++;
        }

        // Listen for server packets
        var packet = channel.GetPacket();
        if (packet != null) {
            var buffer = packet.buffer;
            Deserialize(buffer);
        }

        UpdateConnectionTimeouts();
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
        PacketType messageType = (PacketType) buffer.GetByte();

        switch (messageType)
        {
            case PacketType.PlayerJoinedResponse:
            {
                int clientId = buffer.GetInt();
                if (clientConnectionsTimeouts.ContainsKey(clientId))
                    InitializeClientEntity(buffer, clientId);
                break;
            }
        }
    }

    private void InitializeClientEntity(BitBuffer buffer, int clientId)
    {
        clientConnectionsTimeouts.Remove(clientId);
                
        var newClient = Instantiate(cubePrefab, transform);
        newClient.name = $"Client-{clientId}";
        newClient.layer = LayerMask.NameToLayer($"Client {usedClientLayersCount}");
        if (!serverEntity.capsulesOn) newClient.GetComponent<Renderer>().enabled = false;
    
        var clientEntity = newClient.AddComponent<ClientEntity>();
    
        var sendPort = buffer.GetInt();
        var recvPort = buffer.GetInt();
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
    
        var clientColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
    
        clientEntity.InitializeClientEntity(sendPort, recvPort, clientId, clientTime, displaySeq, minBufferElems, clientColor, 
            position, rotation, health, usedClientLayersCount, this, !createdFirstPlayer);
        usedClientLayersCount++;

        InitializeOtherPlayerCopies(buffer, clientId, clientEntity);

        if (!createdFirstPlayer)
        {
            firstPlayer = clientEntity.gameObject;
            firstPlayer.AddComponent<FirstPersonView>();
            //firstPlayer.AddComponent<ShootManager>();
            createdFirstPlayer = true;
        }
    }

    private void InitializeOtherPlayerCopies(BitBuffer buffer, int clientId, ClientEntity clientEntity)
    {
        int connectedPlayerCount = buffer.GetByte();
        var position = new Vector3();
        var rotation = new Quaternion();
        
        for (var i = 0; i < connectedPlayerCount; i++)
        {
            clientId = buffer.GetInt();
            position = new Vector3();
            rotation = new Quaternion();

            position.x = buffer.GetFloat();
            position.y = buffer.GetFloat();
            position.z = buffer.GetFloat();
            rotation.w = buffer.GetFloat();
            rotation.x = buffer.GetFloat();
            rotation.y = buffer.GetFloat();
            rotation.z = buffer.GetFloat();

            clientEntity.InitializeConnectedPlayer(clientId, position, rotation);
        }
    }
}
