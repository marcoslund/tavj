using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Tests;
using UnityEngine;

public class ClientEntity : MonoBehaviour
{
    public int clientPort;
    public int serverPort;
    private Channel channel;
    private IPEndPoint serverIpEndPoint;

    public int clientId;
    public int displaySeq = 0; // Current min frame being used for interpolation
    public float clientTime = 0;
    public bool isPlaying;

    public List<Snapshot> interpolationBuffer = new List<Snapshot>();
    public int minInterpolationBufferElems;

    private readonly Commands commandsToSend = new Commands();
    public List<Commands> unAckedCommands = new List<Commands>();
    private readonly List<Commands> predictionCommands = new List<Commands>();

    private readonly Dictionary<int, ClientCopyEntity> otherPlayers = new Dictionary<int, ClientCopyEntity>();
    //private Color clientColor;

    private CharacterController characterController;
    //private int clientLayer;
    [HideInInspector] public float currentSpeed;
    
    public float walkingSpeed = 5.0f; // TODO HARDCODED, RECV FROM SERVER
    public float gravityValue = -9.81f;
    private float velocityY;

    private int shotSeq = 1;
    public List<Shot> unAckedShots = new List<Shot>();
    public int health;
    private int startingHealth;
    public PlayerHealth playerHealthManager;

    [HideInInspector] public Transform cameraMain;
    [HideInInspector] public Vector3 cameraPosition;
    [HideInInspector] public Transform raySpawn;
    
    public GameObject gun;
    public Animator handsAnimator;
    public Texture icon;
    
    public AudioSource weaponChanging;

    private bool isFirstClient;

    private void Awake()
    {
        clientId = PlayerPrefs.GetInt("ClientId");
        transform.name = $"Client-{clientId}";
        
        /* Networking variables */
        clientPort = PlayerPrefs.GetInt("ClientPort");
        serverPort = PlayerPrefs.GetInt("ServerPort");
        channel = new Channel(clientPort);
        var serverIp = PlayerPrefs.GetString("ServerIp");
        serverIpEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        
        /* Client snapshot variables */
        clientTime = PlayerPrefs.GetFloat("ClientTime");
        displaySeq = PlayerPrefs.GetInt("DisplaySequence");
        minInterpolationBufferElems = PlayerPrefs.GetInt("MinInterpolationBufferElements");
        
        /* Client game variables */
        transform.position = new Vector3(
                PlayerPrefs.GetFloat("ClientPosX"),
                PlayerPrefs.GetFloat("ClientPosY"),
                PlayerPrefs.GetFloat("ClientPosZ")
            );
        transform.rotation = new Quaternion(
            PlayerPrefs.GetFloat("ClientRotW"),
            PlayerPrefs.GetFloat("ClientRotX"),
            PlayerPrefs.GetFloat("ClientRotY"),
            PlayerPrefs.GetFloat("ClientRotZ")
            );
        health = PlayerPrefs.GetInt("ClientHealth");;
        startingHealth = health;
        characterController = GetComponent<CharacterController>();
        //this.clientLayer = clientLayer;
        currentSpeed = walkingSpeed;
        
        InitializeConnectedPlayers();
        
        /* FPS variables */
        cameraMain = GameObject.FindGameObjectWithTag("MainCamera").transform;
        raySpawn = GameObject.FindGameObjectWithTag("RaySpawn").transform;
        icon = (Texture) Resources.Load("Weap_Icons/NewGun_auto_img");
        StartCoroutine ("SpawnWeaponUponStart");
    }

    private void InitializeConnectedPlayers()
    {
        var connectedPlayerCount = PlayerPrefs.GetInt("ConnectedPlayerCount");
        
        for (var i = 1; i <= connectedPlayerCount; i++)
        {
            var connectedPlayerId = PlayerPrefs.GetInt($"ConnectedPlayer{i}Id");
            var position = new Vector3();
            var rotation = new Quaternion();

            position.x = PlayerPrefs.GetFloat($"ConnectedPlayer{i}PosX");
            position.y = PlayerPrefs.GetFloat($"ConnectedPlayer{i}PosY");
            position.z = PlayerPrefs.GetFloat($"ConnectedPlayer{i}PosZ");
            rotation.w = PlayerPrefs.GetFloat($"ConnectedPlayer{i}RotW");
            rotation.x = PlayerPrefs.GetFloat($"ConnectedPlayer{i}RotX");
            rotation.y = PlayerPrefs.GetFloat($"ConnectedPlayer{i}RotY");
            rotation.z = PlayerPrefs.GetFloat($"ConnectedPlayer{i}RotZ");
            
            InitializeConnectedPlayer(connectedPlayerId, position, rotation);
        }
    }

    private void FixedUpdate()
    {
        commandsToSend.RotationY = transform.rotation.eulerAngles.y;
        var move = MovePlayer(commandsToSend);
        if (isFirstClient && handsAnimator)
            handsAnimator.SetBool("Moving", commandsToSend.isMoveCommand());
        unAckedCommands.Add(new Commands(commandsToSend));
        predictionCommands.Add(new Commands(commandsToSend));
        SendCommands(unAckedCommands);
        commandsToSend.Seq++;
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
    
    private IEnumerator SpawnWeaponUponStart() {
        yield return new WaitForSeconds (0.5f);
        if (weaponChanging)
            weaponChanging.Play ();
        
        var resource = (GameObject) Resources.Load("Gun");
        gun = Instantiate(resource, transform.position, Quaternion.identity, transform);
        gun.layer = transform.gameObject.layer;
        handsAnimator = gun.GetComponent<GunManager>().handsAnimator;
    }

    // Update 'commandsToSend' variable if new input is read
    private void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.W))
            commandsToSend.Up = true;
        else if (Input.GetKeyUp(KeyCode.W) || (commandsToSend.Up && !Input.GetKey(KeyCode.W))) // TODO DELETE SECOND CHECK
            commandsToSend.Up = false;
        
        if (Input.GetKeyDown(KeyCode.S))
            commandsToSend.Down = true;
        else if (Input.GetKeyUp(KeyCode.S) || (commandsToSend.Down && !Input.GetKey(KeyCode.S))) // TODO DELETE SECOND CHECK
            commandsToSend.Down = false;
        
        if (Input.GetKeyDown(KeyCode.A))
            commandsToSend.Left = true;
        else if (Input.GetKeyUp(KeyCode.A) || (commandsToSend.Left && !Input.GetKey(KeyCode.A))) // TODO DELETE SECOND CHECK
            commandsToSend.Left = false;

        if (Input.GetKeyDown(KeyCode.D))
            commandsToSend.Right = true;
        else if (Input.GetKeyUp(KeyCode.D) || (commandsToSend.Right && !Input.GetKey(KeyCode.D))) // TODO DELETE SECOND CHECK
            commandsToSend.Right = false;
    }
    
    private void Interpolate(Snapshot prevSnapshot, Snapshot nextSnapshot)
    {
        var t =  (clientTime - prevSnapshot.Time) / (nextSnapshot.Time - prevSnapshot.Time);
        
        foreach (var playerCopyPair in otherPlayers)
        {
            var playerId = playerCopyPair.Key;

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

            playerCopyPair.Value.MovePlayerCopy(position, rotation);
        }
    }

    private float InterpolateAxis(float currentSnapValue, float nextSnapValue, float t)
    {
        return currentSnapValue + (nextSnapValue - currentSnapValue) * t;
    }

    private void DeserializeBuffer(BitBuffer buffer)
    {
        var messageType = (PacketType) buffer.GetByte();

        switch (messageType)
        {
            case PacketType.Snapshot:
                DeserializeSnapshot(buffer);
                break;
            case PacketType.CommandsAck:
            {
                var receivedAckSequence = DeserializeCommandAck(buffer);
                RemoveAckedCommands(receivedAckSequence);
                break;
            }
            case PacketType.PlayerJoined:
                DeserializePlayerJoined(buffer);
                break;
            case PacketType.PlayerShotAck:
                var rcvdShotSequence = DeserializeShotAck(buffer);
                RemoveAckedShots(rcvdShotSequence);
                break;
            case PacketType.PlayerShotBroadcast:
                DeserializeShotBroadcast(buffer);
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
            
            if (!characterController.isGrounded)
            {
                recvVelY += gravityValue * Time.fixedDeltaTime;
                move.y = velocityY * Time.fixedDeltaTime;
            }
            
            characterController.Move(move);
        }
        velocityY = recvVelY;
        transform.rotation = Quaternion.Euler(0, currentRotationY, 0);
    }

    private Vector3 MovePlayer(Commands commands)
    {
        Vector3 move = Vector3.zero;
        /*bool canJump = false;*/
        
        move.x = commands.GetHorizontal() * Time.fixedDeltaTime * walkingSpeed;
        move.z = commands.GetVertical() * Time.fixedDeltaTime * walkingSpeed;
        move = transform.TransformDirection(move);
        
        if (!characterController.isGrounded)
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
        
        characterController.Move(move);

        return move;
    }
    
    // Serialize & send commands to server
    private void SendCommands(List<Commands> commandsList)
    {
        var packet = Packet.Obtain();
        ClientSerializationManager.SerializeCommands(packet.buffer, commandsList, clientId);
        packet.buffer.Flush();

        channel.Send(packet, serverIpEndPoint);

        packet.Free();
    }

    private static int DeserializeCommandAck(BitBuffer buffer)
    {
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
        //newClient.layer = LayerMask.NameToLayer($"Client {clientLayer}");  // TODO CHANGE LAYER
        newClient.transform.position = position;
        newClient.transform.rotation = rotation;
        //newClient.GetComponent<Renderer>().material.color = clientColor;
        
        otherPlayers.Add(connectedPlayerId, newClient.GetComponent<ClientCopyEntity>());
    }
    
    private void SendPlayerJoinedAck(int newPlayerId)
    {
        var packet = Packet.Obtain();
        ClientSerializationManager.SerializePlayerJoinedAck(packet.buffer, newPlayerId, clientId);
        packet.buffer.Flush();

        channel.Send(packet, serverIpEndPoint);

        packet.Free();
    }

    private void OnDestroy() {
        channel.Disconnect();
    }

    public void SendPlayerShotMessage(string shotPlayerIdStr)
    {
        var shotPlayerId = Int32.Parse(shotPlayerIdStr);
        unAckedShots.Add(new Shot(shotSeq, shotPlayerId));
        shotSeq++;
        
        var packet = Packet.Obtain();
        ClientSerializationManager.SerializePlayerShot(packet.buffer, unAckedShots, clientId);
        packet.buffer.Flush();

        channel.Send(packet, serverIpEndPoint);

        packet.Free();
    }

    private int DeserializeShotAck(BitBuffer buffer)
    {
        return buffer.GetInt();
    }

    private void RemoveAckedShots(int rcvdShotSequence)
    {
        int lastAckedShotIndex = 0;
        foreach (var shot in unAckedShots)
        {
            if (shot.Seq > rcvdShotSequence)
                break;
            lastAckedShotIndex++;
        }
        unAckedShots.RemoveRange(0, lastAckedShotIndex);
    }
    
    private void DeserializeShotBroadcast(BitBuffer buffer)
    {
        var shooterId = buffer.GetInt();
        var shotPlayerId = buffer.GetInt();
        var shotPlayerHealth = buffer.GetInt();

        if (shotPlayerId == clientId)
        {
            health = shotPlayerHealth;
            playerHealthManager.SetPlayerHealth(health / (float) startingHealth);
        }
        
        // TODO SHOW SHOOTING (& DEATH) ANIMATION & BLOOD, SEND ACK
    }
}
