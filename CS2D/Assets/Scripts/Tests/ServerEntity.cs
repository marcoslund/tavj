using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using Tests;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class ServerEntity : MonoBehaviour
{

    public GameObject cubePrefab;
    
    [HideInInspector] public Dictionary<int, ClientData> clients = new Dictionary<int, ClientData>();
    
    private const int ServerPort = 9000;
    private const int ConnectionPort = 9001;
    private Channel connectionChannel;
    private string serverIp;
    private IPEndPoint connectionIpEndPoint;
    
    private const int PortsPerClient = 2;
    private const int ClientBasePort = 9010;
    
    public int clientCount = 0;
    private const float PlayerJoinedTimeout = 1f;
    public int minInterpolationBufferElems = 2;
    
    public int pps = 60;
    private float sendRate;
    private float sendSnapshotAccum = 0;
    public float serverTime = 0;
    public int nextSnapshotSeq = 0; // Next snapshot to send
    
    public List<Transform> spawnPoints;
    private readonly Color serverCubesColor = Color.white;

    private const float PlayerSpeed = 5.0f;
    private const float GravityValue = -9.81f;

    private const int FullPlayerHealth = 100;
    private const int ShotDamage = 10;

    // Start is called before the first frame update
    private void Awake() {
        serverIp = PlayerPrefs.GetString("ServerIP", "127.0.0.1");
        connectionChannel = new Channel(ServerPort);
        connectionIpEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), ConnectionPort);
        
        sendRate = 1f / pps;
    }

    // Update is called once per frame
    private void Update() {
        sendSnapshotAccum += Time.deltaTime;
        UpdateServer();
    }

    private void FixedUpdate()
    {
        foreach (var client in clients)
        {
            var clientId = client.Key;
            var clientData = client.Value;
            MovePlayer(clientId, clientData.Controller, clientData.YVelocity, clientData.RecvCommands);
        }
    }

    private void UpdateServer()
    {
        serverTime += Time.deltaTime;

        ListenForPlayerConnections();
        UpdatePlayerJoinedTimeouts();
        
        foreach (var clientDataPair in clients) // Listen for incoming packets
        {
            var clientId = clientDataPair.Key;
            var clientData = clientDataPair.Value;
            
            // Deserialize packets for each client
            var packet = clientData.Channel.GetPacket();
            
            while (packet != null) {
                var buffer = packet.buffer;
                DeserializeClientMessage(buffer, clientId);
                packet = clientData.Channel.GetPacket();
            }
        }
        
        if (sendSnapshotAccum >= sendRate) // Check if snapshot must be sent
        {
            foreach (var clientDataPair in clients)
            {
                SendSnapshotToClient(clientDataPair.Key, clientDataPair.Value);
            }
            sendSnapshotAccum -= sendRate;
            nextSnapshotSeq++;
        }
    }

    private void UpdatePlayerJoinedTimeouts()
    {
        foreach (var clientDataPair in clients)
        {
            var connectedPlayerId = clientDataPair.Key;
            var clientData = clientDataPair.Value;
            foreach (var timeoutPair in clientData.PlayerJoinedTimeouts)
            {
                var remainingTime = timeoutPair.Value - Time.deltaTime;
                if (remainingTime <= 0) // Check if timeout has been reached
                {
                    var timeoutClientId = timeoutPair.Key;
                    var timeoutClientData = clients[timeoutClientId];
                    SendPlayerJoined(timeoutClientData, connectedPlayerId, clientData.Controller.transform);
                    clientData.PlayerJoinedTimeouts[timeoutClientId] = PlayerJoinedTimeout;
                }
            }
        }
    }

    private void MovePlayer(int clientId, CharacterController controller, float velocity, List<Commands> receivedCommands)
    {
        var ctrlTransform = controller.transform;
        
        foreach (var commands in receivedCommands)
        {
            Vector3 move = Vector3.zero;
            /*bool canJump = false;*/
            move.x = commands.GetHorizontal() * Time.fixedDeltaTime * PlayerSpeed;
            move.z = commands.GetVertical() * Time.fixedDeltaTime * PlayerSpeed;
            ctrlTransform.rotation = Quaternion.Euler(0, commands.RotationY, 0);
            move = ctrlTransform.TransformDirection(move);
            
            if (!controller.isGrounded)
            {
                velocity += GravityValue * Time.fixedDeltaTime;
                move.y = velocity * Time.fixedDeltaTime;
            }
            else
            {
                velocity = GravityValue * Time.fixedDeltaTime;
                move.y = GravityValue * Time.fixedDeltaTime;
                //canJump = true;
            }
            
            /*if (commands.Space && controller.isGrounded && canJump)
            {
                velocity += jumpSpeed;//Mathf.Sqrt(jumpHeight * -3.0f * gravityValue); TOO SMALL
                move.y += velocity * Time.fixedDeltaTime;
                canJump = false;
            }*/
            
            controller.Move(move);
        }
        
        receivedCommands.Clear();
        clients[clientId].YVelocity = velocity;
    }

    private void SendSnapshotToClient(int clientId, ClientData clientData)
    {
        var packet = Packet.Obtain();
        
        ServerSerializationManager.ServerWorldSerialize(packet.buffer, nextSnapshotSeq, serverTime, clientId, clients);
        packet.buffer.Flush();

        clientData.Channel.Send(packet, clientData.ClientIpEndPoint);

        packet.Free();
    }

    private void ListenForPlayerConnections()
    {
        var playerConnectionPacket = connectionChannel.GetPacket();
        
        if (playerConnectionPacket == null) return;
        Debug.Log("PLAYER CONNECTION");
        var buffer = playerConnectionPacket.buffer;

        var newClientId = DeserializeJoin(buffer);

        clients.Add(newClientId, new ClientData()); // Add new client data to dictionary
        var clientData = clients[newClientId];
        
        // Instantiate client character controller
        CharacterController controller = Instantiate(cubePrefab, transform).GetComponent<CharacterController>();
        clientData.Controller = controller;
        controller.GetComponent<Renderer>().material.color = serverCubesColor;
        controller.GetComponent<Renderer>().enabled = true;

        // Setup client ports & channels
        var serverPort = ClientBasePort + clientCount * PortsPerClient;
        var clientPort = ClientBasePort + clientCount * PortsPerClient + 1;
        clientData.ServerPort = serverPort;
        clientData.ClientPort = clientPort;
        clientData.Channel = new Channel(serverPort);
        clientData.ClientIpEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), clientPort);
        
        // Setup client transform data
        var clientTransform = controller.transform;
        var clientPosition = spawnPoints[Random.Range(0, spawnPoints.Count)].position;
        clientTransform.position = clientPosition;
        
        controller.name = $"ServerInstance-{newClientId}";
        controller.gameObject.layer = LayerMask.NameToLayer("Server");
        clientData.Health = FullPlayerHealth;
        clientCount++;

        SendPlayerJoinedResponse(newClientId, clientTransform); // Send response to client manager
        
        foreach (var client in clients) // Send event to existing clients with timeout
        {
            var clientId = client.Key;
            var data = client.Value;
            if (clientId != newClientId)
            {
                SendPlayerJoined(data, newClientId, clientTransform);
                clientData.PlayerJoinedTimeouts.Add(clientId, PlayerJoinedTimeout);
            }
        }
    }
    
    private int DeserializeJoin(BitBuffer buffer)
    {
        PacketType messageType = (PacketType) buffer.GetByte();

        if (messageType != PacketType.Join)
            throw new ArgumentException("Unknown message type received from client manager.");
        
        var userId = buffer.GetInt();
        return userId;
    }

    private void SendPlayerJoinedResponse(int newUserId, Transform newClientTransform)
    {
        var packet = Packet.Obtain();
        ServerSerializationManager.SerializePlayerJoinedResponse(packet.buffer, newUserId, clients, serverTime,
            nextSnapshotSeq, minInterpolationBufferElems, newClientTransform.position, newClientTransform.rotation,
            FullPlayerHealth, clientCount);
        packet.buffer.Flush();

        connectionChannel.Send(packet, connectionIpEndPoint);

        packet.Free();
    }

    private static void SendPlayerJoined(ClientData clientData, int newUserId, Transform newClientTransform)
    {
        var packet = Packet.Obtain();
        ServerSerializationManager.SerializePlayerJoined(packet.buffer, newUserId, newClientTransform.position, 
            newClientTransform.rotation);
        packet.buffer.Flush();
        
        clientData.Channel.Send(packet, clientData.ClientIpEndPoint);
        
        packet.Free();
    }

    private void DeserializeClientMessage(BitBuffer buffer, int clientId)
    {
        var messageType = (PacketType) buffer.GetByte();
        
        switch (messageType)
        {
            case PacketType.Commands:
                var commandsList = DeserializeCommands(buffer);
                ProcessReceivedCommands(commandsList, clientId);
                break;
            case PacketType.PlayerJoinedAck:
                DeserializePlayerJoinedAck(buffer);
                break;
            case PacketType.PlayerShot:
                var shotsList = DeserializePlayerShot(buffer);
                ProcessReceivedShots(shotsList, clientId);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private List<Commands> DeserializeCommands(BitBuffer buffer)
    {
        var commandsList = new List<Commands>();
        var playerId = buffer.GetInt();
        var storedCommandLists = buffer.GetInt();
        var clientData = clients[playerId];
        for(var i = 0; i < storedCommandLists; i++)
        {
            var seq = buffer.GetInt();
            
            var commands = new Commands(
                seq,
                buffer.GetByte() > 0,
                buffer.GetByte() > 0,
                buffer.GetByte() > 0,
                buffer.GetByte() > 0,
                buffer.GetFloat());

            if (clientData.RecvCommandSeq < seq)
            {
                commandsList.Add(commands);
                clientData.RecvCommandSeq = seq;
            }
        }

        return commandsList;
    }
    
    private void ProcessReceivedCommands(List<Commands> commandsList, int clientId)
    {
        var clientData = clients[clientId];
        var receivedCommandSequence = -1;
        foreach (var commands in commandsList)
        {
            receivedCommandSequence = commands.Seq;
            
            clientData.RecvCommands.Add(commands);
        }
        
        var packet = Packet.Obtain();
        ServerSerializationManager.ServerSerializeCommandAck(packet.buffer, receivedCommandSequence);
        packet.buffer.Flush();

        clientData.Channel.Send(packet, clientData.ClientIpEndPoint);

        packet.Free();
    }

    private void DeserializePlayerJoinedAck(BitBuffer buffer)
    {
        var clientId = buffer.GetInt();
        var connectedPlayerId = buffer.GetInt(); // New player
        var clientData = clients[connectedPlayerId];
        clientData.PlayerJoinedTimeouts.Remove(clientId);
    }
    
    private List<Shot> DeserializePlayerShot(BitBuffer buffer)
    {
        var shotsList = new List<Shot>();
        var playerId = buffer.GetInt();
        var storedShots = buffer.GetInt();
        var clientData = clients[playerId];

        for(var i = 0; i < storedShots; i++)
        {
            var seq = buffer.GetInt();
            
            var shot = new Shot(seq, buffer.GetInt());

            if (clientData.RecvShotSeq < seq)
            {
                shotsList.Add(shot);
                clientData.RecvShotSeq = seq;
            }
        }

        return shotsList;
    }
    
    private void ProcessReceivedShots(List<Shot> shotsList, int shooterId)
    {
        var shooterData = clients[shooterId];
        var recvShotSequence = -1;
        foreach (var shot in shotsList)
        {
            recvShotSequence = shot.Seq;
            var shotPlayerData = clients[shot.ShotPlayerId];
            shotPlayerData.Health -= ShotDamage;
            // CHECK IF DEAD...
            
            foreach (var clientPair in clients)
            {
                var clientId = clientPair.Key;
                var clientData = clientPair.Value;
                if (clientId != shooterId)
                {
                    SendPlayerShotBroadcast(clientData, shooterId, shot.ShotPlayerId, shotPlayerData.Health);
                }
            }
        }
        
        var packet = Packet.Obtain();
        ServerSerializationManager.ServerSerializeShotAck(packet.buffer, recvShotSequence);
        packet.buffer.Flush();

        shooterData.Channel.Send(packet, shooterData.ClientIpEndPoint);

        packet.Free();
    }

    private void SendPlayerShotBroadcast(ClientData clientData, int shooterId, int shotPlayerId, int health)
    {
        var packet = Packet.Obtain();
        ServerSerializationManager.SerializePlayerShotBroadcast(packet.buffer, shooterId, shotPlayerId, health);
        packet.buffer.Flush();

        clientData.Channel.Send(packet, clientData.ClientIpEndPoint);

        packet.Free();
    }
}
