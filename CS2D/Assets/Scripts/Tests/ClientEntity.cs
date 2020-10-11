using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public List<Commands> unAckedCommands = new List<Commands>();
    public int minInterpolationBufferElems;

    private int commandSeq = 1;
    private List<Commands> predictionCommands = new List<Commands>();
    private Dictionary<int, Vector3> positionHistory = new Dictionary<int, Vector3>(); // (Frame number, Position)
    private const float PredictionEpsilon = 0.01f;

    private Dictionary<int, Transform> otherClientCubes = new Dictionary<int, Transform>();
    private Color clientColor;

    private ClientManager clientManager;

    private CharacterController _characterController;
    private int clientLayer;
    public float playerSpeed = 2.0f;
    public float jumpHeight = 1.0f;
    public float jumpSpeed = 10.0f;
    public float gravityValue = -9.81f;

    public void Initialize(int sendPort, int recvPort, int userId, float time, int minInterpolationBufferElems,
        Color clientColor, Vector3 position, Quaternion rotation, int clientLayer, ClientManager clientManager)
    {
        this.sendPort = sendPort;
        //sendChannel = new Channel(sendPort);
        this.recvPort = recvPort;
        //recvChannel = new Channel(recvPort);
        this.userId = userId;
        this.time = time;
        this.minInterpolationBufferElems = minInterpolationBufferElems;
        this.clientColor = clientColor;
        
        Renderer rend = GetComponent<Renderer>();
        rend.material.color = clientColor;

        transform.position = position;
        transform.rotation = rotation;

        _characterController = GetComponent<CharacterController>();
        this.clientLayer = clientLayer;

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

        if (interpolationBuffer.Count >= minInterpolationBufferElems)
            isPlaying = true;
        else if (interpolationBuffer.Count <= 1)
            isPlaying = false;
        
        if (isPlaying)
        {
            ReadClientInput();
            
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
            commandSeq,
            Input.GetAxisRaw("Vertical"),
            Input.GetAxisRaw("Horizontal"),
            Input.GetKeyDown(KeyCode.Space)
        );
        
        if (currentCommands.hasCommand())
        {
            Debug.Log("CREATED COMMANDS WITH SEQ " + commandSeq);
            MovePlayer(new List<Commands>() {currentCommands});
            unAckedCommands.Add(currentCommands);
            predictionCommands.Add(currentCommands);
            // Serialize & send commands to server
            var packet = Packet.Obtain();
            SerializeCommands(packet.buffer);
            packet.buffer.Flush();

            string serverIP = "127.0.0.1";
            var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendPort);
            clientManager.serverEntity.fromClientChannels[userId].Send(packet, remoteEp);//sendChannel.Send(packet, remoteEp);

            packet.Free();
            
            positionHistory.Add(commandSeq, transform.position);
            commandSeq++;
        }
    }
    
    private void Interpolate(Snapshot prevSnapshot, Snapshot nextSnapshot, float t)
    {
        var position = new Vector3();
        var rotation = new Quaternion();

        /*position.x = InterpolateAxis(prevSnapshot.Positions[userId].x, nextSnapshot.Positions[userId].x, t);
        position.y = InterpolateAxis(prevSnapshot.Positions[userId].y, nextSnapshot.Positions[userId].y, t);
        position.z = InterpolateAxis(prevSnapshot.Positions[userId].z, nextSnapshot.Positions[userId].z, t);
    
        rotation.w = InterpolateAxis(prevSnapshot.Rotations[userId].w, nextSnapshot.Rotations[userId].w, t);
        rotation.x = InterpolateAxis(prevSnapshot.Rotations[userId].x, nextSnapshot.Rotations[userId].x, t);
        rotation.y = InterpolateAxis(prevSnapshot.Rotations[userId].y, nextSnapshot.Rotations[userId].y, t);
        rotation.z = InterpolateAxis(prevSnapshot.Rotations[userId].z, nextSnapshot.Rotations[userId].z, t);
    
        transform.position = position;
        transform.rotation = rotation;*/
        
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
            case PacketType.CommandsAck:
            {
                int receivedAckSequence = DeserializeAck(buffer);
                int lastAckedCommandsIndex = 0;
                foreach (var commands in unAckedCommands)
                {
                    if (commands.Seq > receivedAckSequence)
                    {
                        break;
                    }
                    lastAckedCommandsIndex++;
                }
                unAckedCommands.RemoveRange(0, lastAckedCommandsIndex);
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
        var recvCmdSeq = buffer.GetInt();
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

            if (clientId == userId && predictionCommands.Count > 0)
            {
                // Delete saved command sequences based on received sequence number
                int toRemoveCmdIndex = 0;
                foreach (var commands in predictionCommands)
                {
                    if (commands.Seq > recvCmdSeq) break;
                    toRemoveCmdIndex++;
                }
                predictionCommands.RemoveRange(0, toRemoveCmdIndex);
                
                Debug.Log("CHECKING POSITION EPSILON... " + recvCmdSeq);
                foreach (var x in positionHistory)
                {
                    Debug.Log($"{x.Key} ({x.Value.x} {x.Value.y} {x.Value.z})");
                }
                
                // Check if received position differs much from predicted position
                if (Vector3.Distance(position, positionHistory[recvCmdSeq]) > PredictionEpsilon)
                {
                    Debug.Log("Applying prediction correction... distance: " + Vector3.Distance(position, positionHistory[recvCmdSeq]) + " recv position: (" + 
                              position.x + " " + position.y + " " + position.z + ")");
                    transform.position = position;
                    MovePlayer(predictionCommands);
                }

                positionHistory = positionHistory.Where(kvp => kvp.Key > recvCmdSeq)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
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
    
    private void MovePlayer(/*float velocity,*/ List<Commands> commandsList)
    {
        Vector3 move = Vector3.zero;
        /*bool canJump = false;
        
        if (!_characterController.isGrounded)
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
        
        foreach (var commands in commandsList)
        {
            move.x += commands.Horizontal * Time.fixedDeltaTime * playerSpeed;
            move.z += commands.Vertical * Time.fixedDeltaTime * playerSpeed;
            
            /*if (commands.Space && _characterController.isGrounded && canJump)
            {
                velocity += jumpSpeed;//Mathf.Sqrt(jumpHeight * -3.0f * gravityValue); TOO SMALL
                move.y += velocity * Time.fixedDeltaTime;
                canJump = false;
            }*/
            
        }
        
        _characterController.Move(move);
        //playerVelocitiesY[clientId] = velocity;
    }

    public void SerializeCommands(BitBuffer buffer)
    {
        buffer.PutByte((int) PacketType.Commands);//buffer.PutInt((int) PacketType.Commands);
        buffer.PutInt(userId);
        buffer.PutInt(unAckedCommands.Count);
        foreach (Commands commands in unAckedCommands)
        {
            buffer.PutInt(commands.Seq);
            /*buffer.PutInt(commands.Up ? 1 : 0);
            buffer.PutInt(commands.Down ? 1 : 0);
            buffer.PutInt(commands.Right ? 1 : 0);
            buffer.PutInt(commands.Left ? 1 : 0);*/
            buffer.PutFloat(commands.Vertical);
            buffer.PutFloat(commands.Horizontal);
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
        
        SendPlayerJoinedAck(clientId);
    }

    private void OnDestroy() {
        //sendChannel.Disconnect();
        //recvChannel.Disconnect();
    }

    public void InitializeConnectedPlayer(int connectedPlayerId, Vector3 position, Quaternion rotation)
    {
        GameObject newClient = (GameObject) Instantiate(Resources.Load("CopyCube"), position, rotation, transform);
        newClient.name = $"ClientCube-{connectedPlayerId}";
        newClient.layer = LayerMask.NameToLayer($"Client {clientLayer}");
        newClient.transform.position = position;
        newClient.transform.rotation = rotation;
        newClient.GetComponent<Renderer>().material.color = clientColor;
        
        otherClientCubes.Add(connectedPlayerId, newClient.transform);
    }
    
    private void SendPlayerJoinedAck(int clientId)
    {
        var packet = Packet.Obtain();
        SerializePlayerJoinedAck(packet.buffer, clientId);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendPort);
        clientManager.serverEntity.fromClientChannels[userId].Send(packet, remoteEp);//sendChannel.Send(packet, remoteEp);

        packet.Free();
    }

    private void SerializePlayerJoinedAck(BitBuffer buffer, int clientId)
    {
        buffer.PutInt((int) PacketType.PlayerJoinedAck);
        buffer.PutInt(userId);
        buffer.PutInt(clientId);
    }
}
