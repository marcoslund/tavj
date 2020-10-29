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
    public float jumpSpeed = 10.0f;*/
    public float gravityValue = -9.81f;
    private float velocityY;

    private int shotSeq = 1;
    public List<Shot> unAckedShots = new List<Shot>();
    public int health;
    
    private ClientManager clientManager;
    private Channel channel;

    public void InitializeClientEntity(int sendPort, int recvPort, int clientId, float clientTime, int displaySeq, 
        int minInterpolationBufferElems, Color clientColor, Vector3 position, Quaternion rotation, int health, int clientLayer, 
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
        this.health = health;
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
        commandsToSend.RotationY = transform.rotation.eulerAngles.y;
        MovePlayer(commandsToSend);
        unAckedCommands.Add(new Commands(commandsToSend));
        predictionCommands.Add(new Commands(commandsToSend));
        SendCommands(unAckedCommands);
        commandsToSend.Seq++;
        
        /*if (commandsToSend.hasCommand())
        {
            MovePlayer(commandsToSend);
            unAckedCommands.Add(new Commands(commandsToSend));
            predictionCommands.Add(new Commands(commandsToSend));
            SendCommands(unAckedCommands);
            commandsToSend.Seq++;
        }
        else
        {
            ApplyGravity();
        }*/
    }

    private void Update()
    {
        // Check for incoming packets
        var packet = channel.GetPacket();//recvChannel.GetPacket();
        while (packet != null) {
            var buffer = packet.buffer;
            DeserializeBuffer(buffer);
            packet = channel.GetPacket();
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
                //Debug.Log("RECV ACK " + receivedAckSequence +  " - UNACKED COMMANDS " + unAckedCommands.Count);
                if (unAckedCommands.Count > 15)
                {
                    clientManager.ERROR = true;
                }
                break;
            }
            case PacketType.PlayerJoined:
                DeserializePlayerJoined(buffer);
                break;
            case PacketType.PlayerShotAck:
                // REMOVE FROM ACCUM SHOT LIST LIKE COMMANDS
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
        health = buffer.GetInt();
        var recvCmdSeq = buffer.GetInt();
        var recvVelY = buffer.GetFloat();
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
                Conciliate(recvCmdSeq, position, recvVelY);
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

    private void Conciliate(int recvCmdSeq, Vector3 position, float recvVelY)
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
        var currentRotationY = transform.rotation.eulerAngles.y;
        transform.position = position;
        foreach (var commands in predictionCommands)
        {
            Vector3 move = Vector3.zero;
            move.x += commands.GetHorizontal() * Time.fixedDeltaTime * walkingSpeed;
            move.z += commands.GetVertical() * Time.fixedDeltaTime * walkingSpeed;
            transform.rotation = Quaternion.Euler(0, commands.RotationY, 0);
            move = transform.TransformDirection(move);
            
            if (!_characterController.isGrounded)
            {
                recvVelY += gravityValue * Time.fixedDeltaTime;
                move.y = velocityY * Time.fixedDeltaTime;
            }
            
            _characterController.Move(move);
        }
        velocityY = recvVelY;
        transform.rotation = Quaternion.Euler(0, currentRotationY, 0);
    }

    private void MovePlayer(Commands commands)
    {
        Vector3 move = Vector3.zero;
        /*bool canJump = false;*/
        
        move.x = commands.GetHorizontal() * Time.fixedDeltaTime * walkingSpeed;
        move.z = commands.GetVertical() * Time.fixedDeltaTime * walkingSpeed;
        move = transform.TransformDirection(move);
        
        if (!_characterController.isGrounded)
        {
            velocityY += gravityValue * Time.fixedDeltaTime;
            move.y = velocityY * Time.fixedDeltaTime;
        }
        else
        {
            velocityY = gravityValue * Time.fixedDeltaTime;
            move.y = gravityValue * Time.fixedDeltaTime;
            //canJump = true;
        }
        
        /*if (commands.Space && _characterController.isGrounded && canJump)
        {
            velocity += jumpSpeed;//Mathf.Sqrt(jumpHeight * -3.0f * gravityValue); TOO SMALL
            move.y += velocity * Time.fixedDeltaTime;
            canJump = false;
        }*/
        
        _characterController.Move(move);
    }
    
    /*private void ApplyGravity()
    {
        Vector3 move = Vector3.zero;
        
        if (!_characterController.isGrounded)
        {
            velocityY += gravityValue * Time.fixedDeltaTime;
            move.y = velocityY * Time.fixedDeltaTime;
        }
        else
        {
            velocityY = gravityValue * Time.fixedDeltaTime;
            move.y = gravityValue * Time.fixedDeltaTime;
        }
        
        _characterController.Move(move);
    }*/
    
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
    private void SerializeCommands(BitBuffer buffer, List<Commands> commandsList) // TODO SEND ROTATION
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
            buffer.PutFloat(commands.RotationY);
        }
        //Debug.Log("SENDING COMMANDS TO SERVER UP TO " + commandsToSend.Seq);
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
        newClient.name = $"{connectedPlayerId}";
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

    public void SendPlayerShotMessage(string shotPlayerIdStr)
    {
        var shotPlayerId = Int32.Parse(shotPlayerIdStr);
        unAckedShots.Add(new Shot(shotSeq, shotPlayerId));
        shotSeq++;
        
        var packet = Packet.Obtain();
        SerializePlayerShot(packet.buffer);
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
     * Shots Count (int)
     * (Shots...)
     */
    private void SerializePlayerShot(BitBuffer buffer)
    {
        buffer.PutByte((int) PacketType.PlayerShot);
        buffer.PutInt(clientId);
        buffer.PutInt(unAckedShots.Count);
        foreach (Shot shot in unAckedShots)
        {
            buffer.PutInt(shot.Seq);
            buffer.PutInt(shot.ShotPlayerId);
        }
    }
}
