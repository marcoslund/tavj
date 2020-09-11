using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ClientManager : MonoBehaviour
{
    // Channels to and from server
    public int sendBasePort = 9000;
    public int recvBasePort = 9001;
    public Channel sendChannel;
    public Channel recvChannel;

    private int clientCounter = 0;
    private Dictionary<int, float> clientConnectionsTimeouts;
    public const float ClientConnectionTimeout = 1f;
    
    public GameObject clientPrefab;
    public GameObject cubePrefab;
    
    // Start is called before the first frame update
    void Start()
    {
        sendChannel = new Channel(sendBasePort);
        recvChannel = new Channel(recvBasePort);
        
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
            SendClientConnection(clientCounter);
            clientConnectionsTimeouts.Add(clientCounter, ClientConnectionTimeout);
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

    private void SendClientConnection(int userTrackId)
    {
        var packet = Packet.Obtain();
        SerializeClientConnection(packet.buffer, userTrackId);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendBasePort);
        sendChannel.Send(packet, remoteEp);

        packet.Free();
    }
    
    public void SerializeClientConnection(BitBuffer buffer, int userTrackId) {
        buffer.PutByte((int) PacketType.Join);
        buffer.PutByte(userTrackId);
    }
    
    private void Deserialize(BitBuffer buffer)
    {
        PacketType messageType = (PacketType) buffer.GetByte();

        if (messageType == PacketType.PlayerJoinedResponse)
        {
            int userTrackId = buffer.GetByte();
            if (clientConnectionsTimeouts.ContainsKey(userTrackId))
            {
                clientConnectionsTimeouts.Remove(userTrackId);
                
                GameObject newClient = Instantiate(clientPrefab);
                ClientEntity clientEntityComponent = newClient.GetComponent<ClientEntity>();
                
                int userId = buffer.GetByte();
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
