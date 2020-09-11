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
    public Channel sendChannel;
    public Channel recvChannel;

    public int userId;
    public int displaySeq = 0;
    public float time = 0;
    public bool isPlaying;

    public List<Snapshot> interpolationBuffer = new List<Snapshot>();
    public List<Commands> commands = new List<Commands>();
    public int minBufferElems;

    private Dictionary<int, Rigidbody> players;
    private Color clientColor;

    public void Initialize(int sendPort, int recvPort, int userId, int minBufferElems, Color clientColor)
    {
        this.sendPort = sendPort;
        sendChannel = new Channel(sendPort);
        this.recvPort = recvPort;
        recvChannel = new Channel(recvPort);
        this.userId = userId;
        this.minBufferElems = minBufferElems;
        this.clientColor = clientColor;
        
        Renderer rend = GetComponent<Renderer>();
        rend.material.color = clientColor;
    }

    private void Update()
    {
        var packet = recvChannel.GetPacket();

        if (packet != null) {
            var buffer = packet.buffer;

            // Deserialize
            Deserialize(interpolationBuffer, buffer, displaySeq, commands);
        }

        ReadClientInput();

        if (interpolationBuffer.Count >= minBufferElems)
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
            SerializeCommands(commands, packet.buffer);
            packet.buffer.Flush();

            string serverIP = "127.0.0.1";
            var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendPort);
            sendChannel.Send(packet, remoteEp);

            packet.Free();
        }
    }
    
    private void Interpolate(Snapshot prevSnapshot, Snapshot nextSnapshot, float t)
    {
        var position = new Vector3();
        var rotation = new Quaternion();

        position.x = InterpolateAxis(prevSnapshot.Position.x, nextSnapshot.Position.x, t);
        position.y = InterpolateAxis(prevSnapshot.Position.y, nextSnapshot.Position.y, t);
        position.z = InterpolateAxis(prevSnapshot.Position.z, nextSnapshot.Position.z, t);
    
        rotation.w = InterpolateAxis(prevSnapshot.Rotation.w, nextSnapshot.Rotation.w, t);
        rotation.x = InterpolateAxis(prevSnapshot.Rotation.x, nextSnapshot.Rotation.x, t);
        rotation.y = InterpolateAxis(prevSnapshot.Rotation.y, nextSnapshot.Rotation.y, t);
        rotation.z = InterpolateAxis(prevSnapshot.Rotation.z, nextSnapshot.Rotation.z, t);
    
        transform.position = position;
        transform.rotation = rotation;
    }

    private float InterpolateAxis(float currentSnapValue, float nextSnapValue, float t)
    {
        return currentSnapValue + (nextSnapValue - currentSnapValue) * t;
    }
    
    public static void Deserialize(List<Snapshot> interpolationBuffer, BitBuffer buffer, int seqCli, List<Commands> clientCommands)
    {
        PacketType messageType = (PacketType) buffer.GetByte();

        switch (messageType)
        {
            case PacketType.Snapshot:
                DeserializeSnapshot(interpolationBuffer, buffer, seqCli);
                break;
            case PacketType.Ack:
            {
                int receivedAckSequence = DeserializeAck(buffer);
                int lastAckedCommandsIndex = 0;
                foreach (var commands in clientCommands)
                {
                    if (commands.Seq > receivedAckSequence)
                    {
                        break;
                    }
                    lastAckedCommandsIndex++;
                }
                clientCommands.RemoveRange(0, lastAckedCommandsIndex);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void DeserializeSnapshot(List<Snapshot> interpolationBuffer, BitBuffer buffer, int seqCli)
    {
        var position = new Vector3();
        var rotation = new Quaternion();
        
        var seq = buffer.GetInt();
        var time = buffer.GetFloat();
        position.x = buffer.GetFloat();
        position.y = buffer.GetFloat();
        position.z = buffer.GetFloat();
        rotation.w = buffer.GetFloat();
        rotation.x = buffer.GetFloat();
        rotation.y = buffer.GetFloat();
        rotation.z = buffer.GetFloat();
        
        if (seq < seqCli) return;
        
        Snapshot snapshot = new Snapshot(seq, time, position, rotation);
        int i;
        for (i = 0; i < interpolationBuffer.Count; i++)
        {
            if(interpolationBuffer[i].Seq > seq)
                break;
        }
        interpolationBuffer.Insert(i, snapshot);
    }

    public static void SerializeCommands(List<Commands> clientCommands, BitBuffer buffer)
    {
        foreach (Commands commands in clientCommands)
        {
            buffer.PutInt(commands.Seq);
            buffer.PutInt(commands.Up ? 1 : 0);
            buffer.PutInt(commands.Down ? 1 : 0);
            buffer.PutInt(commands.Right ? 1 : 0);
            buffer.PutInt(commands.Left ? 1 : 0);
            buffer.PutInt(commands.Space ? 1 : 0);
        }
    }
    
    private static int DeserializeAck(BitBuffer buffer)
    {
        return buffer.GetInt();
    }
    
    private void OnDestroy() {
        sendChannel.Disconnect();
        recvChannel.Disconnect();
    }

    public void InitializeConnectedPlayer(GameObject cubePrefab, int connectedPlayerId, Vector3 position, Quaternion rotation)
    {
        GameObject newClient = Instantiate(cubePrefab, position, rotation);
        newClient.transform.position = position;
        newClient.transform.rotation = rotation;
        newClient.GetComponent<Renderer>().material.color = clientColor;
        
        players.Add(connectedPlayerId, newClient.GetComponent<Rigidbody>());
    }
}
