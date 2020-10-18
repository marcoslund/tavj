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

    public int clientId;
    public int displaySeq = 0; // Current min frame being used for interpolation
    public float clientTime = 0;
    public bool isPlaying;

    public List<Snapshot> interpolationBuffer = new List<Snapshot>();
    public int minInterpolationBufferElems;

    private Commands commandsToSend = new Commands();
    public List<Commands> unAckedCommands = new List<Commands>();
    private List<Commands> predictionCommands = new List<Commands>();
    //private CharacterController predictionCopy;

    private Dictionary<int, Transform> otherPlayers = new Dictionary<int, Transform>();
    private Color clientColor;

    private CharacterController _characterController;
    private int clientLayer;
    [HideInInspector] public float currentSpeed;
    public float walkingSpeed = 5.0f;
    /*public float jumpHeight = 1.0f;
    public float jumpSpeed = 10.0f;
    public float gravityValue = -9.81f;*/
    
    private ClientManager clientManager;
    private Channel channel;

    public void InitializeClientEntity(int sendPort, int recvPort, int clientId, float clientTime, int displaySeq, 
        int minInterpolationBufferElems, Color clientColor, Vector3 position, Quaternion rotation, int clientLayer, 
        /*GameObject predictionCopy,*/ ClientManager clientManager)
    {
        this.sendPort = sendPort;
        //sendChannel = new Channel(sendPort);
        this.recvPort = recvPort;
        //recvChannel = new Channel(recvPort);
        this.clientId = clientId;
        this.clientTime = clientTime;
        this.displaySeq = displaySeq;
        this.minInterpolationBufferElems = minInterpolationBufferElems;
        this.clientColor = clientColor;
        var rend = GetComponent<Renderer>();
        rend.material.color = clientColor;
        transform.position = position;
        transform.rotation = rotation;
        _characterController = GetComponent<CharacterController>();
        this.clientLayer = clientLayer;
        this.clientManager = clientManager;

        currentSpeed = walkingSpeed;

        //this.predictionCopy = predictionCopy.GetComponent<CharacterController>();
        //predictionCopy.GetComponent<Renderer>().enabled = false;
        //predictionCopy.transform.position = position;
        //predictionCopy.transform.rotation = rotation;
        //Physics.IgnoreCollision(_characterController, this.predictionCopy);

        channel = clientManager.serverEntity.toClientChannels[clientId]; // TO DELETE
    }

    private void FixedUpdate()
    {
        if (commandsToSend.hasCommand())
        {
            MovePlayer(new List<Commands>() {commandsToSend});
            unAckedCommands.Add(new Commands(commandsToSend));
            predictionCommands.Add(new Commands(commandsToSend));
            SendCommands(unAckedCommands);
            commandsToSend.Seq++;
        }
    }

    private void Update()
    {
        // Check for incoming packets
        var packet = channel.GetPacket();//recvChannel.GetPacket();
        if (packet != null) {
            var buffer = packet.buffer;
            DeserializeBuffer(buffer);
        }
        
        // Check if stored snapshot count is valid
        if (interpolationBuffer.Count >= minInterpolationBufferElems)
            isPlaying = true;
        else if (interpolationBuffer.Count <= 1)
            isPlaying = false;

        if (isPlaying)
        {
            clientTime += Time.deltaTime;
            ReadInput();
            UpdateInterpolationBuffer();
            if (!isPlaying) return;
            Interpolate(interpolationBuffer[0], interpolationBuffer[1]);
        }
    }

    private void UpdateInterpolationBuffer()
    {
        // Remove header snapshot when time advances
        var nextTime = interpolationBuffer[1].Time;
        if (clientTime >= nextTime) {
            interpolationBuffer.RemoveAt(0);
            displaySeq++;
            if (interpolationBuffer.Count < 2)
                isPlaying = false;
        }
    }

    // Update 'commandsToSend' variable if new input is read
    private void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.W))
            commandsToSend.Up = true;
        else if (Input.GetKeyUp(KeyCode.W) || (commandsToSend.Up && !Input.GetKey(KeyCode.W))) // TO DELETE SECOND CHECK
            commandsToSend.Up = false;
        
        if (Input.GetKeyDown(KeyCode.S))
            commandsToSend.Down = true;
        else if (Input.GetKeyUp(KeyCode.S) || (commandsToSend.Down && !Input.GetKey(KeyCode.S))) // TO DELETE SECOND CHECK
            commandsToSend.Down = false;
        
        if (Input.GetKeyDown(KeyCode.A))
            commandsToSend.Left = true;
        else if (Input.GetKeyUp(KeyCode.A) || (commandsToSend.Left && !Input.GetKey(KeyCode.A))) // TO DELETE SECOND CHECK
            commandsToSend.Left = false;

        if (Input.GetKeyDown(KeyCode.D))
            commandsToSend.Right = true;
        else if (Input.GetKeyUp(KeyCode.D) || (commandsToSend.Right && !Input.GetKey(KeyCode.D))) // TO DELETE SECOND CHECK
            commandsToSend.Right = false;
    }
    
    private void Interpolate(Snapshot prevSnapshot, Snapshot nextSnapshot)
    {
        var t =  (clientTime - prevSnapshot.Time) / (nextSnapshot.Time - prevSnapshot.Time);
        
        foreach (var playerTransformPair in otherPlayers)
        {
            var playerId = playerTransformPair.Key;

            if (!prevSnapshot.Positions.ContainsKey(playerId)) continue;
            
            var position = new Vector3();
            var rotation = new Quaternion();
            
            position.x = InterpolateAxis(prevSnapshot.Positions[playerId].x, nextSnapshot.Positions[playerId].x, t);
            position.y = InterpolateAxis(prevSnapshot.Positions[playerId].y, nextSnapshot.Positions[playerId].y, t);
            position.z = InterpolateAxis(prevSnapshot.Positions[playerId].z, nextSnapshot.Positions[playerId].z, t);
    
            rotation.w = InterpolateAxis(prevSnapshot.Rotations[playerId].w, nextSnapshot.Rotations[playerId].w, t);
            rotation.x = InterpolateAxis(prevSnapshot.Rotations[playerId].x, nextSnapshot.Rotations[playerId].x, t);
            rotation.y = InterpolateAxis(prevSnapshot.Rotations[playerId].y, nextSnapshot.Rotations[playerId].y, t);
            rotation.z = InterpolateAxis(prevSnapshot.Rotations[playerId].z, nextSnapshot.Rotations[playerId].z, t);

            var playerTransform = playerTransformPair.Value;
            playerTransform.position = position;
            playerTransform.rotation = rotation;
        }
    }

    private float InterpolateAxis(float currentSnapValue, float nextSnapValue, float t)
    {
        return currentSnapValue + (nextSnapValue - currentSnapValue) * t;
    }
    
    public void DeserializeBuffer(BitBuffer buffer)
    {
        PacketType messageType = (PacketType) buffer.GetByte();

        switch (messageType)
        {
            case PacketType.Snapshot:
                DeserializeSnapshot(buffer);
                break;
            case PacketType.CommandsAck:
            {
                var receivedAckSequence = DeserializeAck(buffer);
                RemoveAckedCommands(receivedAckSequence);
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
        var recvFrameSeq = buffer.GetInt();
        
        if (recvFrameSeq < displaySeq) return; // Check if received snapshot is old
        
        var time = buffer.GetFloat();
        var recvCmdSeq = buffer.GetInt();
        var playerCount = buffer.GetByte();

        var positions = new Dictionary<int, Vector3>();
        var rotations = new Dictionary<int, Quaternion>();

        for (int i = 0; i < playerCount; i++)
        {
            var position = new Vector3();
            var rotation = new Quaternion();
            
            var playerId = buffer.GetInt();
            position.x = buffer.GetFloat();
            position.y = buffer.GetFloat();
            position.z = buffer.GetFloat();
            rotation.w = buffer.GetFloat();
            rotation.x = buffer.GetFloat();
            rotation.y = buffer.GetFloat();
            rotation.z = buffer.GetFloat();
            
            positions.Add(playerId, position);
            rotations.Add(playerId, rotation);

            if (playerId == clientId)
                Conciliate(recvCmdSeq, position);
        }
        
        var snapshot = new Snapshot(recvFrameSeq, time, positions, rotations);
        StoreSnapshot(snapshot, recvFrameSeq);
    }

    private void StoreSnapshot(Snapshot snapshot, int recvFrameSeq)
    {
        int bufferIndex;
        for (bufferIndex = 0; bufferIndex < interpolationBuffer.Count; bufferIndex++)
        {
            if(interpolationBuffer[bufferIndex].Seq > recvFrameSeq)
                break;
        }
        interpolationBuffer.Insert(bufferIndex, snapshot);
    }

    private void Conciliate(int recvCmdSeq, Vector3 position)
    {
        // Delete saved command sequences based on received sequence number
        int toRemoveCmdIndex = 0;
        foreach (var commands in predictionCommands)
        {
            if (commands.Seq > recvCmdSeq) break;
            toRemoveCmdIndex++;
        }
        predictionCommands.RemoveRange(0, toRemoveCmdIndex);
                
        // Conciliate local state with received snapshot data
        transform.position = position;
        Vector3 move = Vector3.zero;
        foreach (var commands in predictionCommands)
        {
            move.x += commands.GetHorizontal() * Time.fixedDeltaTime * walkingSpeed;
            move.z += commands.GetVertical() * Time.fixedDeltaTime * walkingSpeed;
        }
        _characterController.Move(move);
    }

    private void MovePlayer(/*float velocity,*/ List<Commands> commandsList) // TODO ADD GRAVITY
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
            move.x += commands.GetHorizontal() * Time.fixedDeltaTime * walkingSpeed;
            move.z += commands.GetVertical() * Time.fixedDeltaTime * walkingSpeed;
            
            /*if (commands.Space && _characterController.isGrounded && canJump)
            {
                velocity += jumpSpeed;//Mathf.Sqrt(jumpHeight * -3.0f * gravityValue); TOO SMALL
                move.y += velocity * Time.fixedDeltaTime;
                canJump = false;
            }*/
        }
        //Debug.Log(move+ " " + commandsToSend);
        
        _characterController.Move(move);
        //playerVelocitiesY[clientId] = velocity;
    }
    
    // Serialize & send commands to server
    private void SendCommands(List<Commands> commandsList)
    {
        var packet = Packet.Obtain();
        SerializeCommands(packet.buffer, commandsList);
        packet.buffer.Flush();

        var serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendPort);
        clientManager.serverEntity.fromClientChannels[clientId].Send(packet, remoteEp);//sendChannel.Send(packet, remoteEp);

        packet.Free();
    }

    /*
     * -- FORMAT --
     * Packet Type (byte)
     * Client ID (int)
     * Commands Count (int)
     * (Commands...)
     */
    private void SerializeCommands(BitBuffer buffer, List<Commands> commandsList)
    {
        buffer.PutByte((int) PacketType.Commands);
        buffer.PutInt(clientId);
        buffer.PutInt(unAckedCommands.Count);
        foreach (Commands commands in commandsList)
        {
            buffer.PutInt(commands.Seq);
            buffer.PutByte(commands.Up ? 1 : 0);
            buffer.PutByte(commands.Down ? 1 : 0);
            buffer.PutByte(commands.Left ? 1 : 0);
            buffer.PutByte(commands.Right ? 1 : 0);
        }
    }
    
    private int DeserializeAck(BitBuffer buffer)
    {
        //Debug.Log("RECV ACK - UNACKED COMMANDS " + unAckedCommands.Count);
        return buffer.GetInt();
    }

    private void RemoveAckedCommands(int receivedAckSequence)
    {
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
    }
    
    private void DeserializePlayerJoined(BitBuffer buffer)
    {
        var newPlayerId = buffer.GetInt();
            
        var position = new Vector3();
        var rotation = new Quaternion();
            
        position.x = buffer.GetFloat();
        position.y = buffer.GetFloat();
        position.z = buffer.GetFloat();
        rotation.w = buffer.GetFloat();
        rotation.x = buffer.GetFloat();
        rotation.y = buffer.GetFloat();
        rotation.z = buffer.GetFloat();
        
        InitializeConnectedPlayer(newPlayerId, position, rotation);
        
        SendPlayerJoinedAck(newPlayerId);
    }

    public void InitializeConnectedPlayer(int connectedPlayerId, Vector3 position, Quaternion rotation)
    {
        var newClient = (GameObject) Instantiate(Resources.Load("CopyCube"), position, rotation, transform);
        newClient.name = $"ClientCube-{connectedPlayerId}";
        newClient.layer = LayerMask.NameToLayer($"Client {clientLayer}");
        newClient.transform.position = position;
        newClient.transform.rotation = rotation;
        newClient.GetComponent<Renderer>().material.color = clientColor;
        
        otherPlayers.Add(connectedPlayerId, newClient.transform);
    }
    
    private void SendPlayerJoinedAck(int newPlayerId)
    {
        var packet = Packet.Obtain();
        SerializePlayerJoinedAck(packet.buffer, newPlayerId);
        packet.buffer.Flush();

        string serverIP = "127.0.0.1";
        var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), sendPort);
        clientManager.serverEntity.fromClientChannels[clientId].Send(packet, remoteEp);//sendChannel.Send(packet, remoteEp); // TODO FIX

        packet.Free();
    }

    /*
     * -- FORMAT --
     * Packet Type (byte)
     * Client ID (int)
     * New Player ID (int)
     */
    private void SerializePlayerJoinedAck(BitBuffer buffer, int newPlayerId)
    {
        buffer.PutByte((int) PacketType.PlayerJoinedAck);
        buffer.PutInt(clientId);
        buffer.PutInt(newPlayerId);
    }
    
    private void OnDestroy() {
        //sendChannel.Disconnect();
        //recvChannel.Disconnect();
    }
}
