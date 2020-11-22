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
    public const int MaxClientCount = 10; // TODO IMPLEMENT LIMIT FAILURE
    public int clientCount = 0;
    private const float PlayerJoinedTimeout = 1f;
    public int minInterpolationBufferElems = 2;
    
    public int pps = 60;
    private float sendRate;
    private float sendSnapshotAccum = 0;
    public float serverTime = 0;
    public int nextSnapshotSeq = 0; // Next snapshot to send
    
    public List<Transform> spawnPoints;

    private const float PlayerSpeed = 5.0f;
    private const float GravityValue = -9.81f;

    private const int FullPlayerHealth = 100;
    private const int ShotDamage = 10;
    private const float RespawnTime = 6f;

    // Start is called before the first frame update
    private void Awake() {
        serverIp = PlayerPrefs.GetString("ServerIp", "127.0.0.1");
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
            var transf = clientData.Controller.transform;
            var toUpdatePos = transf.position;
            MovePlayer(clientId, clientData.Controller, clientData.YVelocity, clientData.RecvCommands);
            var updatedPos = transf.position;
            clientData.PlayerCopyManager.SetAnimatorMovementParameters(toUpdatePos - updatedPos);
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
                    SendPlayerJoined(timeoutClientData, connectedPlayerId, clientData.PlayerName, clientData.Controller.transform);
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
        
        var buffer = playerConnectionPacket.buffer;

        var newClientData = DeserializeJoin(buffer);
        var newClientId = newClientData.PlayerId;
        Debug.Log($"PLAYER '{newClientData.PlayerName}' CONNECTED");
        
        clients.Add(newClientId, newClientData); // Add new client data to dictionary
        
        // Instantiate client character controller
        CharacterController controller = Instantiate(cubePrefab, transform).GetComponent<CharacterController>();
        newClientData.Controller = controller;
        //controller.GetComponent<Renderer>().material.color = serverCubesColor;
        //controller.GetComponent<Renderer>().enabled = true;
        newClientData.PlayerCopyManager = controller.GetComponent<PlayerCopyManager>();

        // Setup client ports & channels
        var serverPort = ClientBasePort + clientCount * PortsPerClient;
        var clientPort = ClientBasePort + clientCount * PortsPerClient + 1;
        newClientData.ServerPort = serverPort;
        newClientData.ClientPort = clientPort;
        newClientData.Channel = new Channel(serverPort);
        newClientData.ClientIpEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), clientPort);
        
        // Setup client transform data
        var clientTransform = controller.transform;
        var clientPosition = spawnPoints[Random.Range(0, spawnPoints.Count)].position;
        clientTransform.position = clientPosition;
        
        controller.name = $"ServerInstance-{newClientId}";
        controller.gameObject.layer = LayerMask.NameToLayer("Server");
        newClientData.Health = FullPlayerHealth;
        clientCount++;

        SendPlayerJoinedResponse(newClientId, clientTransform); // Send response to client manager
        
        foreach (var client in clients) // Send event to existing clients with timeout
        {
            var clientId = client.Key;
            var data = client.Value;
            if (clientId != newClientId)
            {
                SendPlayerJoined(data, newClientId, newClientData.PlayerName, clientTransform);
                newClientData.PlayerJoinedTimeouts.Add(clientId, PlayerJoinedTimeout);
            }
        }
    }
    
    private ClientData DeserializeJoin(BitBuffer buffer)
    {
        PacketType messageType = (PacketType) buffer.GetByte();

        if (messageType != PacketType.Join)
            throw new ArgumentException("Unknown message type received from client manager.");

        var newClientData = new ClientData();
        
        newClientData.PlayerId = buffer.GetInt();
        newClientData.PlayerName = buffer.GetString();

        return newClientData;
    }

    private void SendPlayerJoinedResponse(int newUserId, Transform newClientTransform)
    {
        var packet = Packet.Obtain();
        ServerSerializationManager.SerializePlayerJoinedResponse(packet.buffer, newUserId, clients, serverTime,
            nextSnapshotSeq, minInterpolationBufferElems, newClientTransform.position, newClientTransform.rotation,
            FullPlayerHealth, PlayerSpeed, GravityValue, clientCount);
        packet.buffer.Flush();

        connectionChannel.Send(packet, connectionIpEndPoint);

        packet.Free();
    }

    private static void SendPlayerJoined(ClientData clientData, int newUserId, string newClientName, Transform newClientTransform)
    {
        var packet = Packet.Obtain();
        ServerSerializationManager.SerializePlayerJoined(packet.buffer, newUserId, newClientName, newClientTransform.position, 
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
            
            if(shotPlayerData.IsDead) continue;
            
            shotPlayerData.Health -= ShotDamage;
            
            if (shotPlayerData.Health <= 0)
            {
                shotPlayerData.IsDead = true;
                StartCoroutine(ShowDeathAnimation(shotPlayerData, shot.ShotPlayerId));
            }
            
            foreach (var clientPair in clients)
            {
                var clientData = clientPair.Value;
                SendPlayerShotBroadcast(clientData, shooterId, shot.ShotPlayerId, shotPlayerData.Health);
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
    
    private IEnumerator ShowDeathAnimation(ClientData shotPlayerData, int shotPlayerId)
    {
        shotPlayerData.PlayerCopyManager.TriggerDeathAnimation();
        
        yield return new WaitForSeconds(RespawnTime);

        shotPlayerData.IsDead = false;
        shotPlayerData.Health = FullPlayerHealth;
        shotPlayerData.PlayerCopyManager.TriggerRespawnAnimation();
        
        var newPosition = spawnPoints[Random.Range(0, spawnPoints.Count)].position;
        shotPlayerData.Controller.transform.position = newPosition;
        
        foreach (var clientPair in clients)
        {
            var clientData = clientPair.Value;
            SendPlayerRespawnBroadcast(clientData, shotPlayerId, newPosition);
        }
    }
    
    private void SendPlayerRespawnBroadcast(ClientData clientData, int shotPlayerId, Vector3 newPosition)
    {
        var packet = Packet.Obtain();
        ServerSerializationManager.SerializePlayerRespawnBroadcast(packet.buffer, shotPlayerId, newPosition, nextSnapshotSeq);
        packet.buffer.Flush();

        clientData.Channel.Send(packet, clientData.ClientIpEndPoint);

        packet.Free();
    }
}
