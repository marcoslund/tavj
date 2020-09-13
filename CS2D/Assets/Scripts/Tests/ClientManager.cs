using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEditor.UIElements;
using UnityEngine;

public class ClientManager : MonoBehaviour
{
    // Channels to and from server
    private int sendPort = 9000;
    private int recvPort = 9001;
    private Channel sendChannel;
    private Channel recvChannel;

    private int clientCounter = 0;
    private Dictionary<int, float> clientConnectionsTimeouts;
    public const float ClientConnectionTimeout = 1f;
    
    public GameObject clientPrefab;
    public GameObject cubePrefab;
    
    // Start is called before the first frame update
    void Start()
    {
        sendChannel = new Channel(sendPort);
        recvChannel = new Channel(recvPort);
        
        clientConnectionsTimeouts = new Dictionary<int, float>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            // Connect to server to create new client entity
            // Make packet, send to server with timeout,
            // wait for response with userID and channel ports, and then instantiate cube (with associated color),
            // which will recieve snapshots separately
            
            int userId = Random.Range(0, int.MaxValue); // Collisions are unlikely for the time being
            SendClientConnection(userId);
            clientConnectionsTimeouts.Add(userId, ClientConnectionTimeout);
            clientCounter++;
        }
        
        var packet = recvChannel.GetPacket();
        if (packet != null) {
            var buffer = packet.buffer;

            // Deserialize
            Deserialize(buffer);
        }

        foreach (var clientConnectionPair in clientConnectionsTimeouts)
        {
            var remainingTime = clientConnectionPair.Value - Time.deltaTime;
            // Check if timeout has been reached
            if (remainingTime <= 0)
            {
                SendClientConnection(clientConnectionPair.Key);
                clientConnectionsTimeouts[clientConnectionPair.Key] = ClientConnectionTimeout;
            }
        }
    }

    private void SendClientConnection(int userId)
    {
        var packet = Packet.Obtain();
        SerializeClientConnection(packet.buffer, userId);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendPort);
        sendChannel.Send(packet, remoteEp);

        packet.Free();
    }
    
    public void SerializeClientConnection(BitBuffer buffer, int userId) {
        buffer.PutByte((int) PacketType.Join);
        buffer.PutByte(userId);
    }
    
    private void Deserialize(BitBuffer buffer)
    {
        PacketType messageType = (PacketType) buffer.GetByte();

        if (messageType == PacketType.PlayerJoinedResponse)
        {
            int userId = buffer.GetInt();
            if (clientConnectionsTimeouts.ContainsKey(userId))
            {
                clientConnectionsTimeouts.Remove(userId);
                
                GameObject newClient = Instantiate(clientPrefab);
                ClientEntity clientEntityComponent = newClient.GetComponent<ClientEntity>();
                
                int sendPort = buffer.GetByte();
                int recvPort = buffer.GetByte();
                int minBufferElems = buffer.GetByte();
                var position = new Vector3();
                var rotation = new Quaternion();
                
                position.x = buffer.GetFloat();
                position.y = buffer.GetFloat();
                position.z = buffer.GetFloat();
                rotation.w = buffer.GetFloat();
                rotation.x = buffer.GetFloat();
                rotation.y = buffer.GetFloat();
                rotation.z = buffer.GetFloat();
                
                Color clientColor = new Color(
                    Random.Range(0f, 1f), 
                    Random.Range(0f, 1f), 
                    Random.Range(0f, 1f)
                );
                
                clientEntityComponent.Initialize(sendPort, recvPort, userId, minBufferElems, clientColor);
                
                int connectedPlayerCount = buffer.GetByte();
                for (int i = 0; i < connectedPlayerCount; i++)
                {
                    userId = buffer.GetByte();
                    position = new Vector3();
                    rotation = new Quaternion();
                    
                    position.x = buffer.GetFloat();
                    position.y = buffer.GetFloat();
                    position.z = buffer.GetFloat();
                    rotation.w = buffer.GetFloat();
                    rotation.x = buffer.GetFloat();
                    rotation.y = buffer.GetFloat();
                    rotation.z = buffer.GetFloat();

                    clientEntityComponent.InitializeConnectedPlayer(cubePrefab, userId, position, rotation);
                }
            }
        }
    }
}
