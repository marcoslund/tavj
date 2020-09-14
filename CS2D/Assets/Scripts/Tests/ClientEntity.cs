using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Tests;
using UnityEngine;

public class ClientEntity : MonoBehaviour
{
    public int sendPort;
    public int recvPort;
    //public Channel sendChannel;
    //public Channel recvChannel;

    public int userId;
    public int displaySeq = 0;
    public float time = 0;
    public bool isPlaying;

    public List<Snapshot> interpolationBuffer = new List<Snapshot>();
    public List<Commands> commands = new List<Commands>();
    public int minInterpolationBufferElems;

    private Dictionary<int, Transform> otherClientCubes = new Dictionary<int, Transform>();
    private Color clientColor;
    public GameObject cubePrefab;

    private ClientManager clientManager;

    public void Initialize(int sendPort, int recvPort, int userId, int minInterpolationBufferElems, Color clientColor,
        Vector3 position, Quaternion rotation, ClientManager clientManager)
    {
        this.sendPort = sendPort;
        //sendChannel = new Channel(sendPort);
        this.recvPort = recvPort;
        //recvChannel = new Channel(recvPort);
        this.userId = userId;
        this.minInterpolationBufferElems = minInterpolationBufferElems;
        this.clientColor = clientColor;
        
        Renderer rend = GetComponent<Renderer>();
        rend.material.color = clientColor;

        transform.position = position;
        transform.rotation = rotation;

        this.clientManager = clientManager;
    }

    private void Update()
    {
        var packet = clientManager.serverEntity.toClientChannels[userId].GetPacket();//recvChannel.GetPacket();

        if (packet != null) {
            var buffer = packet.buffer;

            // Deserialize
            Deserialize(buffer);
        }

        ReadClientInput();

        if (interpolationBuffer.Count >= minInterpolationBufferElems)
            isPlaying = true;
        else if (interpolationBuffer.Count <= 1)
            isPlaying = false;
        
        if (isPlaying)
        {
            time += Time.deltaTime;
            var previousTime = interpolationBuffer[0].Time;
            var nextTime = interpolationBuffer[1].Time;
            if (time >= nextTime) {
                interpolationBuffer.RemoveAt(0);
                displaySeq++;
                if (interpolationBuffer.Count < 2)
                {
                    isPlaying = false;
                    return;
                }
                previousTime = interpolationBuffer[0].Time;
                nextTime =  interpolationBuffer[1].Time;
            }
            var t =  (time - previousTime) / (nextTime - previousTime);
            Interpolate(interpolationBuffer[0], interpolationBuffer[1], t);
        }
    }
    
    private void ReadClientInput()
    {
        Commands currentCommands = new Commands(
            Input.GetKeyDown(KeyCode.UpArrow),
            Input.GetKeyDown(KeyCode.DownArrow),
            Input.GetKeyDown(KeyCode.RightArrow),
            Input.GetKeyDown(KeyCode.LeftArrow),
            Input.GetKeyDown(KeyCode.Space)
        );
        
        if (currentCommands.hasCommand())
        {
            commands.Add(currentCommands);
            // Serialize & send commands to server
            var packet = Packet.Obtain();
            SerializeCommands(packet.buffer);
            packet.buffer.Flush();

            string serverIP = "127.0.0.1";
            var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendPort);
            clientManager.serverEntity.fromClientChannels[userId].Send(packet, remoteEp);//sendChannel.Send(packet, remoteEp);

            packet.Free();
        }
    }
    
    private void Interpolate(Snapshot prevSnapshot, Snapshot nextSnapshot, float t)
    {
        var position = new Vector3();
        var rotation = new Quaternion();

        position.x = InterpolateAxis(prevSnapshot.Positions[userId].x, nextSnapshot.Positions[userId].x, t);
        position.y = InterpolateAxis(prevSnapshot.Positions[userId].y, nextSnapshot.Positions[userId].y, t);
        position.z = InterpolateAxis(prevSnapshot.Positions[userId].z, nextSnapshot.Positions[userId].z, t);
    
        rotation.w = InterpolateAxis(prevSnapshot.Rotations[userId].w, nextSnapshot.Rotations[userId].w, t);
        rotation.x = InterpolateAxis(prevSnapshot.Rotations[userId].x, nextSnapshot.Rotations[userId].x, t);
        rotation.y = InterpolateAxis(prevSnapshot.Rotations[userId].y, nextSnapshot.Rotations[userId].y, t);
        rotation.z = InterpolateAxis(prevSnapshot.Rotations[userId].z, nextSnapshot.Rotations[userId].z, t);
    
        transform.position = position;
        transform.rotation = rotation;
        
        foreach (var clientCubePair in otherClientCubes)
        {
            var clientId = clientCubePair.Key;

            if (prevSnapshot.Positions.ContainsKey(clientId))
            {
                position = new Vector3();
                rotation = new Quaternion();
            
                position.x = InterpolateAxis(prevSnapshot.Positions[clientId].x, nextSnapshot.Positions[clientId].x, t);
                position.y = InterpolateAxis(prevSnapshot.Positions[clientId].y, nextSnapshot.Positions[clientId].y, t);
                position.z = InterpolateAxis(prevSnapshot.Positions[clientId].z, nextSnapshot.Positions[clientId].z, t);
    
                rotation.w = InterpolateAxis(prevSnapshot.Rotations[clientId].w, nextSnapshot.Rotations[clientId].w, t);
                rotation.x = InterpolateAxis(prevSnapshot.Rotations[clientId].x, nextSnapshot.Rotations[clientId].x, t);
                rotation.y = InterpolateAxis(prevSnapshot.Rotations[clientId].y, nextSnapshot.Rotations[clientId].y, t);
                rotation.z = InterpolateAxis(prevSnapshot.Rotations[clientId].z, nextSnapshot.Rotations[clientId].z, t);

                var clientTransform = clientCubePair.Value;
                clientTransform.position = position;
                clientTransform.rotation = rotation;
            }
        }
    }

    private float InterpolateAxis(float currentSnapValue, float nextSnapValue, float t)
    {
        return currentSnapValue + (nextSnapValue - currentSnapValue) * t;
    }
    
    public void Deserialize(BitBuffer buffer)
    {
        PacketType messageType = (PacketType) buffer.GetByte();

        switch (messageType)
        {
            case PacketType.Snapshot:
                DeserializeSnapshot(buffer);
                break;
            case PacketType.CommandAck:
            {
                int receivedAckSequence = DeserializeAck(buffer);
                int lastAckedCommandsIndex = 0;
                foreach (var commands in commands)
                {
                    if (commands.Seq > receivedAckSequence)
                    {
                        break;
                    }
                    lastAckedCommandsIndex++;
                }
                commands.RemoveRange(0, lastAckedCommandsIndex);
                break;
            }
            case PacketType.PlayerJoined:
                DeserializePlayerJoined(buffer);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void DeserializeSnapshot(BitBuffer buffer)
    {
        var seq = buffer.GetInt();
        
        if (seq < displaySeq) return;
        
        var time = buffer.GetFloat();
        var clientCount = buffer.GetByte();

        Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
        Dictionary<int, Quaternion> rotations = new Dictionary<int, Quaternion>();

        for (int i = 0; i < clientCount; i++)
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
            
            positions.Add(clientId, position);
            rotations.Add(clientId, rotation);
        }

        Snapshot snapshot = new Snapshot(seq, time, positions, rotations);
        int bufferIndex;
        for (bufferIndex = 0; bufferIndex < interpolationBuffer.Count; bufferIndex++)
        {
            if(interpolationBuffer[bufferIndex].Seq > seq)
                break;
        }
        interpolationBuffer.Insert(bufferIndex, snapshot);
    }

    public void SerializeCommands(BitBuffer buffer)
    {
        foreach (Commands commands in commands)
        {
            buffer.PutInt(commands.Seq);
            buffer.PutInt(commands.Up ? 1 : 0);
            buffer.PutInt(commands.Down ? 1 : 0);
            buffer.PutInt(commands.Right ? 1 : 0);
            buffer.PutInt(commands.Left ? 1 : 0);
            buffer.PutInt(commands.Space ? 1 : 0);
        }
    }
    
    private int DeserializeAck(BitBuffer buffer)
    {
        return buffer.GetInt();
    }
    
    private void DeserializePlayerJoined(BitBuffer buffer)
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
        
        InitializeConnectedPlayer(clientId, position, rotation);
    }
    
    private void OnDestroy() {
        //sendChannel.Disconnect();
        //recvChannel.Disconnect();
    }

    public void InitializeConnectedPlayer(int connectedPlayerId, Vector3 position, Quaternion rotation)
    {
        GameObject newClient = Instantiate(cubePrefab, position, rotation);
        newClient.transform.position = position;
        newClient.transform.rotation = rotation;
        newClient.GetComponent<Renderer>().material.color = clientColor;
        
        otherClientCubes.Add(connectedPlayerId, newClient.transform);
    }
}
