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

    [SerializeField] private Dictionary<int, CharacterController> clients = new Dictionary<int, CharacterController>();
    private Dictionary<int, int> toClientPorts = new Dictionary<int, int>();
    private Dictionary<int, int> fromClientPorts = new Dictionary<int, int>();
    public Dictionary<int, Channel> toClientChannels = new Dictionary<int, Channel>();
    public Dictionary<int, Channel> fromClientChannels = new Dictionary<int, Channel>();
    
    private readonly Dictionary<int, Dictionary<int, float>> playerJoinedTimeouts = new Dictionary<int, Dictionary<int, float>>();
    private const float PlayerJoinedTimeout = 1f;

    private const int PortsPerClient = 2;
    public int sendPort = 9001;
    public int recvPort = 9000;
    public Channel sendChannel;
    public Channel recvChannel;
    private readonly int clientBasePort = 9010;
    public int clientCount = 0;
    public int minInterpolationBufferElems = 2;
    
    public int pps = 60;
    private float sendRate;
    private float sendSnapshotAccum = 0;
    public float serverTime = 0;
    public int nextSnapshotSeq = 0; // Next snapshot to send
    
    private bool serverConnected = true;

    private readonly Color serverCubesColor = Color.white;
    private const float FloorSide = 4.5f; // Hardcoded
    public float InitY = 1.1f; //0.6f;

    private readonly Dictionary<int, List<Commands>> receivedCommands = new Dictionary<int, List<Commands>>();
    private Dictionary<int, float> playersVelocitiesY = new Dictionary<int, float>();
    private float playerSpeed = 5.0f;
    /*public float jumpHeight = 1.0f;
    public float jumpSpeed = 10.0f;*/
    private float gravityValue = -9.81f;

    private readonly Dictionary<int, int> playerRecvCmdSeq = new Dictionary<int, int>();
    private readonly Dictionary<int, int> playerRecvShotSeq = new Dictionary<int, int>();
    
    private readonly Dictionary<int, int> playersHealth = new Dictionary<int, int>();
    public const int FullPlayerHealth = 100;
    public const int ShotDamage = 15;

    public bool capsulesOn = false;

    public ClientManager clientManager; // TODO DELETE
    
    // Start is called before the first frame update
    void Awake() {
        sendChannel = new Channel(sendPort);
        recvChannel = new Channel(recvPort);
        
        sendRate = 1f / pps;
    }

    // Update is called once per frame
    void Update() {
        /*if (Input.GetKeyDown(KeyCode.D))
        {
            serverConnected = !serverConnected;
        }*/

        sendSnapshotAccum += Time.deltaTime;

        if (serverConnected)
        {
            UpdateServer();
        }
    }

    private void FixedUpdate()
    {
        foreach (var client in clients)
        {
            var clientId = client.Key;
            var controller = client.Value;
            var velocity = playersVelocitiesY[clientId];
            MovePlayer(clientId, controller, velocity, receivedCommands[clientId]);
        }
    }

    private void UpdateServer()
    {
        serverTime += Time.deltaTime;

        ListenForPlayerConnections();
        
        foreach (var connectedClientPairs in playerJoinedTimeouts)
        {
            foreach (var timeoutPair in connectedClientPairs.Value)
            {
                var remainingTime = timeoutPair.Value - Time.deltaTime;
                // Check if timeout has been reached
                if (remainingTime <= 0)
                {
                    var timeoutClientId = timeoutPair.Key;
                    var connectedPlayerId = connectedClientPairs.Key;
                    var connectedPlayerTransform = clients[connectedPlayerId].transform;
                    SendPlayerJoined(toClientPorts[timeoutClientId], toClientChannels[timeoutClientId],
                        connectedPlayerId, connectedPlayerTransform.position, connectedPlayerTransform.rotation);
                    playerJoinedTimeouts[connectedPlayerId][timeoutClientId] = PlayerJoinedTimeout;
                }
            }
        }

        foreach (var clientId in clients.Keys)
        {
            // Deserialize packets for each client
            var packet = fromClientChannels[clientId].GetPacket();
            
            while (packet != null) {
                var buffer = packet.buffer;

                DeserializeClientMessage(buffer, clientId);
                packet = fromClientChannels[clientId].GetPacket();
            }
        }
        
        if (sendSnapshotAccum >= sendRate)
        {
            // Serialize & send snapshot to each client
            
            //var packet = Packet.Obtain();
            //SerializationManager.ServerWorldSerialize(clientCubes, packet.buffer, nextSnapshotSeq, serverTime); TODO OPTIMIZE
            
            foreach (var clientId in clients.Keys)
            {
                SendSnapshotToClient(clientId/*, packet.buffer*/);
            }

            sendSnapshotAccum -= sendRate;
            nextSnapshotSeq++;
        }
    }

    private void MovePlayer(int clientId, CharacterController controller, float velocity, List<Commands> receivedCommands)
    {
        var ctrlTransform = controller.transform;
        
        foreach (var commands in receivedCommands)
        {
            Vector3 move = Vector3.zero;
            /*bool canJump = false;*/
            move.x = commands.GetHorizontal() * Time.fixedDeltaTime * playerSpeed;
            move.z = commands.GetVertical() * Time.fixedDeltaTime * playerSpeed;
            ctrlTransform.rotation = Quaternion.Euler(0, commands.RotationY, 0);
            move = ctrlTransform.TransformDirection(move);
            
            if (!controller.isGrounded)
            {
                velocity += gravityValue * Time.fixedDeltaTime;
                move.y = velocity * Time.fixedDeltaTime;
            }
            else
            {
                velocity = gravityValue * Time.fixedDeltaTime;
                move.y = gravityValue * Time.fixedDeltaTime;
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
        playersVelocitiesY[clientId] = velocity;
    }

    private void SendSnapshotToClient(int clientId/*, BitBuffer snapshotBuffer*/)
    {
        var packet = Packet.Obtain();
        
        //snapshotBuffer.CopyTo(packet.buffer, snapshotBuffer.GetCurrentBitCount() * ); TODO OPTIMIZE
        
        ServerWorldSerialize(packet.buffer, nextSnapshotSeq, clientId);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), toClientPorts[clientId]);
        toClientChannels[clientId].Send(packet, remoteEp);

        packet.Free();
    }
    
    private void ServerWorldSerialize(BitBuffer buffer, int snapshotSeq, int clientId) {
        buffer.PutByte((int) PacketType.Snapshot);
        buffer.PutInt(snapshotSeq);
        buffer.PutFloat(serverTime);
        buffer.PutInt(playersHealth[clientId]);
        buffer.PutInt(playerRecvCmdSeq[clientId]);
        buffer.PutFloat(playersVelocitiesY[clientId]);
        buffer.PutByte(clients.Count);

        foreach (var client in clients)
        {
            clientId = client.Key;
            var clientTransform = client.Value.transform;
            var position = clientTransform.position;
            var rotation = clientTransform.rotation;
            
            buffer.PutInt(clientId);
            buffer.PutFloat(position.x);
            buffer.PutFloat(position.y);
            buffer.PutFloat(position.z);
            buffer.PutFloat(rotation.w);
            buffer.PutFloat(rotation.x);
            buffer.PutFloat(rotation.y);
            buffer.PutFloat(rotation.z);
        }
    }

    private void ListenForPlayerConnections()
    {
        var playerConnectionPacket = recvChannel.GetPacket();
        
        if (playerConnectionPacket == null) return;
        Debug.Log("CONNECTION");
        var buffer = playerConnectionPacket.buffer;

        int newUserId = DeserializeJoin(buffer);
        
        // Create cube game object
        CharacterController newClient = Instantiate(cubePrefab, transform).GetComponent<CharacterController>();
        newClient.GetComponent<Renderer>().material.color = serverCubesColor;
        if (!capsulesOn) newClient.GetComponent<Renderer>().enabled = false;
        clients.Add(newUserId, newClient);

        int clientSendPort = clientBasePort + clientCount * PortsPerClient;
        int clientRecvPort = clientBasePort + clientCount * PortsPerClient + 1;
        fromClientPorts.Add(newUserId, clientSendPort);
        toClientPorts.Add(newUserId, clientRecvPort);
        fromClientChannels.Add(newUserId, new Channel(clientSendPort));
        toClientChannels.Add(newUserId, new Channel(clientRecvPort));
            
        float clientX = Random.Range(-FloorSide, FloorSide);
        float clientZ = Random.Range(-FloorSide, FloorSide);
        Vector3 clientPosition = new Vector3(clientX, InitY, clientZ);
        Quaternion clientRotation = newClient.transform.rotation;

        newClient.transform.position = clientPosition;
        newClient.name = $"ServerCube-{newUserId}";
        newClient.gameObject.layer = LayerMask.NameToLayer("Server");
        
        receivedCommands.Add(newUserId, new List<Commands>());
        playersVelocitiesY.Add(newUserId, 0);
        
        playerRecvCmdSeq.Add(newUserId, 0);
        playerRecvShotSeq.Add(newUserId, 0);
        playersHealth.Add(newUserId, FullPlayerHealth);
            
        clientCount++;

        SendPlayerJoinedResponse(newUserId, clientPosition, clientRotation);
        
        playerJoinedTimeouts.Add(newUserId, new Dictionary<int, float>());
            
        // Send PlayerJoined to existing clients with timeout
        foreach (var clientChannelPair in toClientChannels)
        {
            var clientId = clientChannelPair.Key;
            if (clientId != newUserId)
            {
                SendPlayerJoined(toClientPorts[clientId], toClientChannels[clientId], newUserId, clientPosition, clientRotation);
                playerJoinedTimeouts[newUserId].Add(clientId, PlayerJoinedTimeout);
            }
        }
    }
    
    private int DeserializeJoin(BitBuffer buffer)
    {
        PacketType messageType = (PacketType) buffer.GetByte();

        if (messageType != PacketType.Join)
            throw new ArgumentException("Unknown message type received from client manager.");
        
        int userId = buffer.GetInt();
        return userId;
    }

    private void SendPlayerJoinedResponse(int newUserId, Vector3 newClientPosition, Quaternion newClientRotation)
    {
        var packet = Packet.Obtain();
        SerializePlayerJoinedResponse(packet.buffer, newUserId, newClientPosition, newClientRotation);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendPort);
        sendChannel.Send(packet, remoteEp);

        packet.Free();
    }

    private void SendPlayerJoined(int clientPort, Channel clientChannel, int newUserId, Vector3 newClientPosition,
        Quaternion newClientRotation)
    {
        var packet = Packet.Obtain();
        SerializePlayerJoined(packet.buffer, newUserId, newClientPosition, newClientRotation);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), clientPort);
        clientChannel.Send(packet, remoteEp);

        packet.Free();
    }

    private void SerializePlayerJoinedResponse(BitBuffer buffer, int newUserId, Vector3 newClientPosition,
        Quaternion newClientRotation)
    {
        buffer.PutByte((int) PacketType.PlayerJoinedResponse);
        buffer.PutInt(newUserId);
        buffer.PutInt(fromClientPorts[newUserId]);
        buffer.PutInt(toClientPorts[newUserId]);
        buffer.PutFloat(serverTime);
        buffer.PutInt(nextSnapshotSeq);
        buffer.PutByte(minInterpolationBufferElems);
        buffer.PutFloat(newClientPosition.x);
        buffer.PutFloat(newClientPosition.y);
        buffer.PutFloat(newClientPosition.z);
        buffer.PutFloat(newClientRotation.w);
        buffer.PutFloat(newClientRotation.x);
        buffer.PutFloat(newClientRotation.y);
        buffer.PutFloat(newClientRotation.z);
        buffer.PutInt(FullPlayerHealth);
        buffer.PutByte(clientCount - 1);
        foreach (var clientCubePair in clients)
        {
            var clientId = clientCubePair.Key;
            if (clientId != newUserId)
            {
                var clientTransform = clientCubePair.Value.transform;
                var position = clientTransform.position;
                var rotation = clientTransform.rotation;
                
                buffer.PutInt(clientId);
                buffer.PutFloat(position.x);
                buffer.PutFloat(position.y);
                buffer.PutFloat(position.z);
                buffer.PutFloat(rotation.w);
                buffer.PutFloat(rotation.x);
                buffer.PutFloat(rotation.y);
                buffer.PutFloat(rotation.z);
            }
        }
    }
    
    private void SerializePlayerJoined(BitBuffer buffer, int newUserId, Vector3 newClientPosition,
        Quaternion newClientRotation)
    {
        buffer.PutByte((int) PacketType.PlayerJoined);
        buffer.PutInt(newUserId);
        buffer.PutFloat(newClientPosition.x);
        buffer.PutFloat(newClientPosition.y);
        buffer.PutFloat(newClientPosition.z);
        buffer.PutFloat(newClientRotation.w);
        buffer.PutFloat(newClientRotation.x);
        buffer.PutFloat(newClientRotation.y);
        buffer.PutFloat(newClientRotation.z);
    }

    private void DeserializeClientMessage(BitBuffer buffer, int clientId)
    {
        PacketType messageType = (PacketType) buffer.GetByte();
        
        switch (messageType)
        {
            case PacketType.Commands:
                List<Commands> commandsList = DeserializeCommands(buffer);
                ProcessReceivedCommands(commandsList, clientId);
                break;
            case PacketType.PlayerJoinedAck:
                DeserializePlayerJoinedAck(buffer);
                break;
            case PacketType.PlayerShot:
                List<Shot> shotsList = DeserializePlayerShot(buffer);
                ProcessReceivedShots(shotsList, clientId);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private List<Commands> DeserializeCommands(BitBuffer buffer)
    {
        List<Commands> commandsList = new List<Commands>();
        int playerId = buffer.GetInt();
        int storedCommandLists = buffer.GetInt();
        int seq = 0;
        //Debug.Log("STORED CMDS: " + storedCommandLists);
        for(int i = 0; i < storedCommandLists; i++)
        {
            seq = buffer.GetInt();
            
            Commands commands = new Commands(
                seq,
                buffer.GetByte() > 0,
                buffer.GetByte() > 0,
                buffer.GetByte() > 0,
                buffer.GetByte() > 0,
                buffer.GetFloat());

            if (playerRecvCmdSeq[playerId] < seq)
            {
                commandsList.Add(commands);
                playerRecvCmdSeq[playerId] = seq;
            }
            if (clientManager.ERROR)
                ;
        }
        //Debug.Log("RECV " + storedCommandLists + " COMMANDS AT SERVER UP TO " + seq);

        return commandsList;
    }
    
    private void ProcessReceivedCommands(List<Commands> commandsList, int clientId)
    {
        int receivedCommandSequence = -1;
        foreach (Commands commands in commandsList)
        {
            receivedCommandSequence = commands.Seq;
            
            receivedCommands[clientId].Add(commands);
            //ExecuteClientInput(clientCubes[clientId], commands);
        }
        //Debug.Log("SERVER - SENDING ACK WITH SEQ " + receivedCommandSequence);
        if (clientManager.ERROR)
            ;
        var packet = Packet.Obtain();
        ServerSerializeCommandAck(packet.buffer, receivedCommandSequence);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), toClientPorts[clientId]);
        toClientChannels[clientId].Send(packet, remoteEp);

        packet.Free();
    }
    
    private void ServerSerializeCommandAck(BitBuffer buffer, int commandSequence)
    {
        //Debug.Log("SENDING ACK FROM SERVER: " + commandSequence);
        if (clientManager.ERROR)
            ;
        buffer.PutByte((int) PacketType.CommandsAck);
        buffer.PutInt(commandSequence);
    }
    
    private void DeserializePlayerJoinedAck(BitBuffer buffer)
    {
        var clientId = buffer.GetInt();
        var connectedPlayerId = buffer.GetInt();
        playerJoinedTimeouts[connectedPlayerId].Remove(clientId);
        if (playerJoinedTimeouts[connectedPlayerId].Count == 0)
        {
            playerJoinedTimeouts.Remove(connectedPlayerId);
        }
    }
    
    private List<Shot> DeserializePlayerShot(BitBuffer buffer)
    {
        List<Shot> shotsList = new List<Shot>();
        int playerId = buffer.GetInt();
        int storedShots = buffer.GetInt();
        int seq = 0;
        
        for(int i = 0; i < storedShots; i++)
        {
            seq = buffer.GetInt();
            
            Shot shot = new Shot(
                seq,
                buffer.GetInt()
            );

            if (playerRecvShotSeq[playerId] < seq)
            {
                shotsList.Add(shot);
                playerRecvShotSeq[playerId] = seq;
            }
        }

        return shotsList;
    }
    
    private void ProcessReceivedShots(List<Shot> shotsList, int shooterId)
    {
        int recvdShotSequence = -1;
        foreach (Shot shot in shotsList)
        {
            recvdShotSequence = shot.Seq;

            playersHealth[shot.ShotPlayerId] -= ShotDamage;
            // CHECK IF DEAD...
            
            foreach (var clientChannelPair in toClientChannels)
            {
                var clientId = clientChannelPair.Key;
                if (clientId != shooterId)
                {
                    SendPlayerShotBroadcast(toClientPorts[clientId], toClientChannels[clientId], shooterId, shot.ShotPlayerId);
                }
            }
        }
        
        var packet = Packet.Obtain();
        ServerSerializeShotAck(packet.buffer, recvdShotSequence);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), toClientPorts[shooterId]);
        toClientChannels[shooterId].Send(packet, remoteEp);

        packet.Free();
    }

    private void ServerSerializeShotAck(BitBuffer buffer, int shotSequence)
    {
        buffer.PutByte((int) PacketType.PlayerShotAck);
        buffer.PutInt(shotSequence);
    }
    
    private void SendPlayerShotBroadcast(int port, Channel channel, int shooterId, int shotPlayerId)
    {
        var packet = Packet.Obtain();
        SerializePlayerShotBroadcast(packet.buffer, shooterId, shotPlayerId);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), port);
        channel.Send(packet, remoteEp);

        packet.Free();
    }

    private void SerializePlayerShotBroadcast(BitBuffer buffer, int shooterId, int shotPlayerId)
    {
        buffer.PutByte((int) PacketType.PlayerShotBroadcast);
        buffer.PutInt(shooterId);
        buffer.PutInt(shotPlayerId);
    }
}
