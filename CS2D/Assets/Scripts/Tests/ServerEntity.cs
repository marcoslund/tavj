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

    //[SerializeField] private Rigidbody cubeRigidBody;

    [SerializeField] private Dictionary<int, Rigidbody> clientCubes = new Dictionary<int, Rigidbody>();
    private Dictionary<int, int> toClientPorts = new Dictionary<int, int>();
    private Dictionary<int, int> fromClientPorts = new Dictionary<int, int>();
    public Dictionary<int, Channel> toClientChannels = new Dictionary<int, Channel>();
    public Dictionary<int, Channel> fromClientChannels = new Dictionary<int, Channel>();

    public const int PortsPerClient = 2;
    public int sendPort = 9001;
    public int recvPort = 9000;
    public Channel sendChannel;
    public Channel recvChannel;
    private int clientBasePort = 9010;
    public int clientCount = 0;
    public int minInterpolationBufferElems = 2;
    
    public int pps = 60;
    private float sendRate;
    private float sendSnapshotAccum = 0;
    private float serverTime = 0;
    private int nextSnapshotSeq = 0; // Next snapshot to send
    
    private bool serverConnected = true;

    private Color serverCubesColor = Color.white;
    private float floorSide = 4.5f; // Hardcoded
    private float initY = 0.6f;

    // Start is called before the first frame update
    void Awake() {
        sendChannel = new Channel(sendPort);
        recvChannel = new Channel(recvPort);
        
        sendRate = 1f / pps;
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown(KeyCode.D))
        {
            serverConnected = !serverConnected;
        }

        sendSnapshotAccum += Time.deltaTime;

        if (serverConnected)
        {
            UpdateServer();
        }
    }

    private void UpdateServer()
    {
        serverTime += Time.deltaTime;

        ListenForPlayerConnections();

        foreach (var clientId in clientCubes.Keys)
        {
            // Deserialize input commands for each client
            var commandPacket = fromClientChannels[clientId].GetPacket();
            
            if (commandPacket != null) {
                var buffer = commandPacket.buffer;

                List<Commands> commandsList = SerializationManager.ServerDeserializeInput(buffer);
                var packet = Packet.Obtain();
                int receivedCommandSequence = -1;
                foreach (Commands commands in commandsList)
                {
                    receivedCommandSequence = commands.Seq;
                    ExecuteClientInput(clientCubes[clientId], commands);
                }
                SerializationManager.ServerSerializeCommandAck(packet.buffer, receivedCommandSequence);
                packet.buffer.Flush();

                string serverIP = "127.0.0.1";
                var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), toClientPorts[clientId]);
                toClientChannels[clientId].Send(packet, remoteEp);

                packet.Free();
            }
        }
        
        if (sendSnapshotAccum >= sendRate)
        {
            // Serialize & send snapshot to each client
            
            //var packet = Packet.Obtain();
            //SerializationManager.ServerWorldSerialize(clientCubes, packet.buffer, nextSnapshotSeq, serverTime); TODO OPTIMIZE
            
            foreach (var clientId in clientCubes.Keys)
            {
                SendSnapshotToClient(clientId/*, packet.buffer*/);
            }

            sendSnapshotAccum -= sendRate;
            nextSnapshotSeq++;
        }
    }

    private void SendSnapshotToClient(int clientId/*, BitBuffer snapshotBuffer*/)
    {
        var packet = Packet.Obtain();
        
        //snapshotBuffer.CopyTo(packet.buffer, snapshotBuffer.GetCurrentBitCount() * ); TODO OPTIMIZE
        
        SerializationManager.ServerWorldSerialize(packet.buffer, clientCubes, nextSnapshotSeq, serverTime);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), toClientPorts[clientId]);
        toClientChannels[clientId].Send(packet, remoteEp);

        packet.Free();
    }

    private void ExecuteClientInput(Rigidbody clientRigidbody, Commands commands)
    {
        //apply input
        if (commands.Space) {
            clientRigidbody.AddForceAtPosition(Vector3.up * 5, Vector3.zero, ForceMode.Impulse);
        }
        if (commands.Left) {
            clientRigidbody.AddForceAtPosition(Vector3.left * 5, Vector3.zero, ForceMode.Impulse);
        }
        if (commands.Right) {
            clientRigidbody.AddForceAtPosition(Vector3.right * 5, Vector3.zero, ForceMode.Impulse);
        }
        if (commands.Up) {
            clientRigidbody.AddForceAtPosition(Vector3.forward * 5, Vector3.zero, ForceMode.Impulse);
        }
        if (commands.Down) {
            clientRigidbody.AddForceAtPosition(Vector3.back * 5, Vector3.zero, ForceMode.Impulse);
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
        Rigidbody newClient = Instantiate(cubePrefab).GetComponent<Rigidbody>();
        newClient.GetComponent<Renderer>().material.color = serverCubesColor;
        clientCubes.Add(newUserId, newClient);

        int clientSendPort = clientBasePort + clientCount * PortsPerClient;
        int clientRecvPort = clientBasePort + clientCount * PortsPerClient + 1;
        fromClientPorts.Add(newUserId, clientSendPort);
        toClientPorts.Add(newUserId, clientRecvPort);
        fromClientChannels.Add(newUserId, new Channel(clientSendPort));
        toClientChannels.Add(newUserId, new Channel(clientRecvPort));
            
        float clientX = Random.Range(-floorSide, floorSide);
        float clientZ = Random.Range(-floorSide, floorSide);
        Vector3 clientPosition = new Vector3(clientX, initY, clientZ);
        Quaternion clientRotation = newClient.transform.rotation;

        newClient.transform.position = clientPosition;
            
        clientCount++;

        ////// Send PlayerJoinedResponse to client manager with timeout
        SendPlayerJoinedResponse(newUserId, clientPosition, clientRotation); // TODO ADD TIMEOUT
            
        // Send PlayerJoined to existing clients with timeout
        foreach (var clientChannelPair in toClientChannels)
        {
            var clientId = clientChannelPair.Key;
            if (clientId != newUserId)
            {
                SendPlayerJoined(toClientPorts[clientId], toClientChannels[clientId], newUserId, clientPosition, clientRotation); // TODO ADD TIMEOUT
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
        buffer.PutByte(minInterpolationBufferElems);
        buffer.PutFloat(newClientPosition.x);
        buffer.PutFloat(newClientPosition.y);
        buffer.PutFloat(newClientPosition.z);
        buffer.PutFloat(newClientRotation.w);
        buffer.PutFloat(newClientRotation.x);
        buffer.PutFloat(newClientRotation.y);
        buffer.PutFloat(newClientRotation.z);
        buffer.PutByte(clientCount - 1);
        foreach (var clientCubePair in clientCubes)
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
}
