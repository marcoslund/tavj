using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private const float InitY = 1.1f; //0.6f;

    private readonly Dictionary<int, List<Commands>> receivedCommands = new Dictionary<int, List<Commands>>();
    //private Dictionary<int, float> playerVelocitiesY = new Dictionary<int, float>();
    private float playerSpeed = 5.0f;
    /*public float jumpHeight = 1.0f;
    public float jumpSpeed = 10.0f;*/
    public float gravityValue = -9.81f;

    private readonly Dictionary<int, int> playerRecvCmdSeq = new Dictionary<int, int>();
    
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
            //var velocity = playerVelocitiesY[clientId];
            MovePlayer(clientId, controller, /*velocity,*/ receivedCommands[clientId]);
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
            
            if (packet != null) {
                var buffer = packet.buffer;

                DeserializeClientMessage(buffer, clientId);
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

    private void MovePlayer(int clientId, CharacterController controller, /*float velocity,*/ List<Commands> receivedCommands)
    {
        Vector3 move = Vector3.zero;
        /*bool canJump = false;
        
        if (!controller.isGrounded)
        {
            velocity += gravityValue * Time.fixedDeltaTime;
            move.y = velocity * Time.fixedDeltaTime;
        }
        else
        {
            velocity = gravityValue * Time.fixedDeltaTime;
            move.y = gravityValue * Time.fixedDeltaTime;
            canJump = true;
        }*/
        
        foreach (var commands in receivedCommands)
        {
            move.x += commands.GetHorizontal() * Time.fixedDeltaTime * playerSpeed;
            move.z += commands.GetVertical() * Time.fixedDeltaTime * playerSpeed;
            
            /*if (commands.Space && controller.isGrounded && canJump)
            {
                velocity += jumpSpeed;//Mathf.Sqrt(jumpHeight * -3.0f * gravityValue); TOO SMALL
                move.y += velocity * Time.fixedDeltaTime;
                canJump = false;
            }*/
            
        }
        
        controller.Move(move);
        
        receivedCommands.Clear();
        //playerVelocitiesY[clientId] = velocity;
    }

    private void SendSnapshotToClient(int clientId/*, BitBuffer snapshotBuffer*/)
    {
        var packet = Packet.Obtain();
        
        //snapshotBuffer.CopyTo(packet.buffer, snapshotBuffer.GetCurrentBitCount() * ); TODO OPTIMIZE
        
        ServerWorldSerialize(packet.buffer, nextSnapshotSeq, playerRecvCmdSeq[clientId]);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), toClientPorts[clientId]);
        toClientChannels[clientId].Send(packet, remoteEp);

        packet.Free();
    }
    
    private void ServerWorldSerialize(BitBuffer buffer, int snapshotSeq, int recvCmdSeq) {
        buffer.PutByte((int) PacketType.Snapshot);
        buffer.PutInt(snapshotSeq);
        buffer.PutFloat(serverTime);
        buffer.PutInt(recvCmdSeq);
        buffer.PutByte(clients.Count);

        foreach (var client in clients)
        {
            var clientId = client.Key;
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
        //playerVelocitiesY.Add(newUserId, 0);
        
        playerRecvCmdSeq.Add(newUserId, 0);
            
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
                ProcessReceivedInput(commandsList, clientId);
                break;
            case PacketType.PlayerJoinedAck:
                DeserializePlayerJoinedAck(buffer);
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
                buffer.GetByte() > 0
                /*buffer.GetFloat(),
                buffer.GetFloat(),
                buffer.GetInt() > 0*/);
            
            if(playerRecvCmdSeq[playerId] < seq)
                commandsList.Add(commands);
        }

        playerRecvCmdSeq[playerId] = seq;

        return commandsList;
    }
    
    private void ProcessReceivedInput(List<Commands> commandsList, int clientId)
    {
        /*Debug.Log("SERVER - RECV COMMANDS:");*/
        /*foreach (Commands commands in commandsList)
        {
            Debug.Log(commands);
        }*/
        int receivedCommandSequence = -1;
        foreach (Commands commands in commandsList)
        {
            receivedCommandSequence = commands.Seq;
            receivedCommands[clientId].Add(commands);
            //ExecuteClientInput(clientCubes[clientId], commands);
        }
        //Debug.Log("SERVER - SENDING ACK WITH SEQ " + receivedCommandSequence);
        
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
}
