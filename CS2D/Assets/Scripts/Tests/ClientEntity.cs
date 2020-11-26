using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Tests;
using UnityEngine;
using Random = UnityEngine.Random;

public class ClientEntity : MonoBehaviour
{
    private int clientPort;
    private int serverPort;
    private Channel channel;
    private IPEndPoint serverIpEndPoint;

    private int clientId;
    private string clientName;
    
    private int displaySeq = 0; // Current min frame being used for interpolation
    private float clientTime = 0;
    private bool isPlaying;

    private List<Snapshot> interpolationBuffer = new List<Snapshot>();
    private int minInterpolationBufferElems;

    private readonly Commands commandsToSend = new Commands();
    private List<Commands> unAckedCommands = new List<Commands>();
    private readonly List<Commands> predictionCommands = new List<Commands>();

    private readonly Dictionary<int, PlayerCopyManager> otherPlayers = new Dictionary<int, PlayerCopyManager>();

    private CharacterController characterController;
    [HideInInspector] public float speed;
    private float gravity;
    private float velocityY;

    private int shotSeq = 1;
    private List<Shot> unAckedShots = new List<Shot>();
    private int health;
    private int startingHealth;
    private PlayerHealth playerHealthManager;
    [HideInInspector] public bool playerDead;
    private List<int> playersToRespawn = new List<int>();

    [HideInInspector] public Transform cameraMain;
    [HideInInspector] public Vector3 cameraPosition;
    [HideInInspector] public Transform raySpawn;
    
    public GameObject gun;
    public Animator handsAnimator;
    public Texture icon;
    
    public AudioSource weaponChanging;

    public GameObject thirdPersonModel;
    private Animator thirdPersonAnimator;
    private Vector3 deathCameraPosition = new Vector3(0, 2, -2);
    private float deathCameraRotationX = 35f;
    private FirstPersonView firstPersonView;

    public UIEventManager uiEventManagerventManager;
    private bool disconnectFromPauseMenu;

    [Header("Audio Clips")]
    private AudioSource audioSource;
    public AudioClip[] walkingClips;
    private float walkingClipTimer;
    private float walkingClipTimerLimit = 0.35f;

    public AudioClip shotClip;
    public AudioClip[] deathClips;

    private int latency;

    private void Awake()
    {
        clientId = PlayerPrefs.GetInt("ClientId");
        transform.name = $"Client-{clientId}";
        clientName = PlayerPrefs.GetString("PlayerName");
        
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
        playerHealthManager = GetComponent<PlayerHealth>();

        speed = PlayerPrefs.GetFloat("PlayerSpeed");
        gravity = PlayerPrefs.GetFloat("Gravity");

        characterController = GetComponent<CharacterController>();
        
        InitializeConnectedPlayers();
        
        /* FPS variables */
        cameraMain = GameObject.FindGameObjectWithTag("MainCamera").transform;
        raySpawn = GameObject.FindGameObjectWithTag("RaySpawn").transform;
        icon = (Texture) Resources.Load("Weap_Icons/NewGun_auto_img");
        StartCoroutine ("SpawnWeaponUponStart");

        thirdPersonAnimator = thirdPersonModel.GetComponent<Animator>();
        firstPersonView = GetComponent<FirstPersonView>();

        audioSource = GetComponent<AudioSource>();
    }

    private void InitializeConnectedPlayers()
    {
        var connectedPlayerCount = PlayerPrefs.GetInt("ConnectedPlayerCount");
        
        for (var i = 1; i <= connectedPlayerCount; i++)
        {
            var connectedPlayerId = PlayerPrefs.GetInt($"ConnectedPlayer{i}Id");
            var connectedPlayerName = PlayerPrefs.GetString($"ConnectedPlayer{i}Name");
            var position = new Vector3();
            var rotation = new Quaternion();

            position.x = PlayerPrefs.GetFloat($"ConnectedPlayer{i}PosX");
            position.y = PlayerPrefs.GetFloat($"ConnectedPlayer{i}PosY");
            position.z = PlayerPrefs.GetFloat($"ConnectedPlayer{i}PosZ");
            rotation.w = PlayerPrefs.GetFloat($"ConnectedPlayer{i}RotW");
            rotation.x = PlayerPrefs.GetFloat($"ConnectedPlayer{i}RotX");
            rotation.y = PlayerPrefs.GetFloat($"ConnectedPlayer{i}RotY");
            rotation.z = PlayerPrefs.GetFloat($"ConnectedPlayer{i}RotZ");
            
            InitializeConnectedPlayer(connectedPlayerId, connectedPlayerName, position, rotation);
        }
    }

    private void Start()
    {
        playerHealthManager.InitializePlayerHealth(health);
    }

    private void FixedUpdate()
    {
        commandsToSend.RotationY = transform.rotation.eulerAngles.y;
        var move = MovePlayer(commandsToSend);
        if (handsAnimator)
            handsAnimator.SetBool("Moving", commandsToSend.isMoveCommand());
        
        if (commandsToSend.isMoveCommand())
        {
            CheckWalkingClipTimer();
        }
        unAckedCommands.Add(new Commands(commandsToSend));
        predictionCommands.Add(new Commands(commandsToSend));
        SendCommands(unAckedCommands);
        commandsToSend.Seq++;
    }

    private void Update()
    {
        // Check for incoming packets
        var packet = channel.GetPacket();
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
        while (clientTime >= nextTime) {
            interpolationBuffer.RemoveAt(0);
            displaySeq++;
            if (interpolationBuffer.Count < 2)
                isPlaying = false;
            
            CheckPlayersRespawn();
            nextTime = interpolationBuffer[1].Time;
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
        if (playerDead) return;
        
        if (Input.GetKeyDown(KeyCode.W))
            commandsToSend.Up = true;
        else if (Input.GetKeyUp(KeyCode.W))
            commandsToSend.Up = false;
        
        if (Input.GetKeyDown(KeyCode.S))
            commandsToSend.Down = true;
        else if (Input.GetKeyUp(KeyCode.S))
            commandsToSend.Down = false;
        
        if (Input.GetKeyDown(KeyCode.A))
            commandsToSend.Left = true;
        else if (Input.GetKeyUp(KeyCode.A))
            commandsToSend.Left = false;

        if (Input.GetKeyDown(KeyCode.D))
            commandsToSend.Right = true;
        else if (Input.GetKeyUp(KeyCode.D))
            commandsToSend.Right = false;
        
        /* Latency addition keys */
        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            latency = 100;
        } else if (Input.GetKeyDown(KeyCode.Alpha2)) {
            latency = 200;
        } else if (Input.GetKeyDown(KeyCode.Alpha3)) {
            latency = 300;
        } else if (Input.GetKeyDown(KeyCode.Alpha4)) {
            latency = 400;
        } else if (Input.GetKeyDown(KeyCode.Alpha5)) {
            latency = 500;
        } else if (Input.GetKeyDown(KeyCode.Alpha0)) {
            latency = 0;
        }
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
            case PacketType.PlayerRespawn:
                DeserializePlayerRespawn(buffer);
                break;
            case PacketType.PlayerDisconnectBroadcast:
                DeserializePlayerDisconnectBroadcast(buffer);
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
        characterController.enabled = false;
        transform.position = position;
        characterController.enabled = true;
        foreach (var commands in predictionCommands)
        {
            Vector3 move = Vector3.zero;
            move.x += commands.GetHorizontal() * Time.fixedDeltaTime * speed;
            move.z += commands.GetVertical() * Time.fixedDeltaTime * speed;
            transform.rotation = Quaternion.Euler(0, commands.RotationY, 0);
            move = transform.TransformDirection(move);
            
            if (!characterController.isGrounded)
            {
                recvVelY += gravity * Time.fixedDeltaTime;
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
        
        move.x = commands.GetHorizontal() * Time.fixedDeltaTime * speed;
        move.z = commands.GetVertical() * Time.fixedDeltaTime * speed;
        move = transform.TransformDirection(move);
        
        if (!characterController.isGrounded)
        {
            velocityY += gravity * Time.fixedDeltaTime;
            move.y = velocityY * Time.fixedDeltaTime;
        }
        else
        {
            velocityY = gravity * Time.fixedDeltaTime;
            move.y = gravity * Time.fixedDeltaTime;
            //canJump = true;
        }
        
        /*if (commands.Space && _characterController.isGrounded && canJump)
        {
            velocity += jumpSpeed;//Mathf.Sqrt(jumpHeight * -3.0f * gravityValue);
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

        if (latency != 0)
        {
            Task.Delay(latency).ContinueWith(t => channel.Send(packet, serverIpEndPoint)).ContinueWith(t => packet.Free());
        }
        else
        {
            channel.Send(packet, serverIpEndPoint);
            packet.Free();
        }
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
        var newPlayerName = buffer.GetString();
        var position = new Vector3();
        var rotation = new Quaternion();
            
        position.x = buffer.GetFloat();
        position.y = buffer.GetFloat();
        position.z = buffer.GetFloat();
        rotation.w = buffer.GetFloat();
        rotation.x = buffer.GetFloat();
        rotation.y = buffer.GetFloat();
        rotation.z = buffer.GetFloat();
        
        InitializeConnectedPlayer(newPlayerId, newPlayerName, position, rotation);
        
        SendPlayerJoinedAck(newPlayerId);
    }

    private void InitializeConnectedPlayer(int connectedPlayerId, string connectedPlayerName, Vector3 position, Quaternion rotation)
    {
        var newClient = (GameObject) Instantiate(Resources.Load("CopyCube"), position, rotation);
        newClient.name = $"{connectedPlayerId}";
        newClient.transform.position = position;
        newClient.transform.rotation = rotation;

        var connectedPlayerManager = newClient.GetComponent<PlayerCopyManager>();
        connectedPlayerManager.PlayerName = connectedPlayerName;
        
        otherPlayers.Add(connectedPlayerId, connectedPlayerManager);
    }
    
    private void SendPlayerJoinedAck(int newPlayerId)
    {
        var packet = Packet.Obtain();
        ClientSerializationManager.SerializePlayerJoinedAck(packet.buffer, newPlayerId, clientId);
        packet.buffer.Flush();

        channel.Send(packet, serverIpEndPoint);

        packet.Free();
    }

    public bool OtherPlayerIsDead(int playerId)
    {
        if (!otherPlayers.ContainsKey(playerId)) return false;
        
        return otherPlayers[playerId].IsDead;
    }

    public void SendPlayerShotMessage(int shotPlayerId)
    {
        unAckedShots.Add(new Shot(shotSeq, shotPlayerId));
        shotSeq++;
        
        var packet = Packet.Obtain();
        ClientSerializationManager.SerializePlayerShot(packet.buffer, unAckedShots, clientId);
        packet.buffer.Flush();

        if (latency != 0)
        {
            Task.Delay(latency).ContinueWith(t => channel.Send(packet, serverIpEndPoint)).ContinueWith(t => packet.Free());
        }
        else
        {
            channel.Send(packet, serverIpEndPoint);
            packet.Free();
        }
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

        var shooterExists = otherPlayers.ContainsKey(shooterId);
        var victimExists = otherPlayers.ContainsKey(shotPlayerId);
        
        if (shotPlayerId == clientId)
        {
            health = shotPlayerHealth;
            playerHealthManager.SetPlayerHealth(health);
            if (health <= 0)
            {
                uiEventManagerventManager.ShowKillEvent(
                    shooterId,
                    shooterExists ? otherPlayers[shooterId].PlayerName : "PLAYER", 
                    shotPlayerId,
                    clientName);
                ShowOwnDeathAnimation();
            }
            if (shooterExists)
            {
                otherPlayers[shooterId].PlayShot();
            }
        } else if (shotPlayerHealth <= 0)
        {
            if (!victimExists) return;
            
            var shotPlayer = otherPlayers[shotPlayerId];
            shotPlayer.IsDead = true;
            uiEventManagerventManager.ShowKillEvent(shooterId, shooterId == clientId? clientName : 
                (shooterExists ? otherPlayers[shooterId].PlayerName : "PLAYER"), shotPlayerId, shotPlayer.PlayerName);
            shotPlayer.TriggerDeathAnimation();
            if (shooterExists)
            {
                otherPlayers[shooterId].PlayShot();
            }
            otherPlayers[shotPlayerId].PlayDeath();
        }

        if (shooterId != clientId && shooterExists)
        {
            otherPlayers[shooterId].ShowMuzzelFlash();
        }
    }

    private void ShowOwnDeathAnimation()
    {
        playerDead = true;
        gun.SetActive(false);
        firstPersonView.enabled = false;
        playerHealthManager.TogglePlayerHealth();
        thirdPersonModel.SetActive(true);
        
        var originalCameraRot = cameraMain.rotation.eulerAngles;
        cameraMain.position = transform.TransformPoint(deathCameraPosition);
        cameraMain.rotation = Quaternion.Euler(new Vector3(deathCameraRotationX, originalCameraRot.y, originalCameraRot.z));
        
        thirdPersonAnimator.SetTrigger("Dying");
        PlayDeath();
    }

    private void DeserializePlayerRespawn(BitBuffer buffer)
    {
        var playerId = buffer.GetInt();
        var respawnSnapshotSeq = buffer.GetInt();
        var respawnPosition = new Vector3(buffer.GetFloat(), buffer.GetFloat(), buffer.GetFloat());
        
        if (playerId == clientId)
        {
            RespawnSelf(respawnPosition);
        }
        else
        {
            if(!otherPlayers.ContainsKey(playerId)) return;
            
            var respawnPlayer = otherPlayers[playerId];
            if (displaySeq >= respawnSnapshotSeq)
            {
                RespawnOther(respawnPlayer, respawnPosition);
            }
            else
            {
                // To avoid wrong interpolation, save data until correct snapshot is being used
                respawnPlayer.RespawnSnapshotSeq = respawnSnapshotSeq;
                respawnPlayer.RespawnPosition = respawnPosition;
                playersToRespawn.Add(playerId);
            }
        }
    }

    private void RespawnSelf(Vector3 newPosition)
    {
        thirdPersonAnimator.SetTrigger("Respawn");
        thirdPersonModel.SetActive(false);
        gun.SetActive(true);
        firstPersonView.enabled = true;
        playerHealthManager.TogglePlayerHealth();
        playerHealthManager.SetPlayerHealth(startingHealth);
        characterController.enabled = false;
        transform.position = newPosition;
        characterController.enabled = true;

        cameraMain.position = transform.TransformPoint(new Vector3(0,1,0));
        cameraMain.rotation = Quaternion.Euler(Vector3.zero);
        playerDead = false;
    }

    private void RespawnOther(PlayerCopyManager otherPlayer, Vector3 newPosition)
    {
        otherPlayer.IsDead = false;
        otherPlayer.TriggerRespawnAnimation();
        otherPlayer.MovePlayerCopyDirect(newPosition);
    }
    
    // Called when new snapshot is switched for interpolation, for awaiting respawns
    private void CheckPlayersRespawn()
    {
        var temp = new List<int>(playersToRespawn);
        foreach (var playerId in temp)
        {
            if (!otherPlayers.ContainsKey(playerId))
            {
                playersToRespawn.Remove(playerId);
            }
            else
            {
                var otherPlayer = otherPlayers[playerId];
                if (otherPlayer.RespawnSnapshotSeq.GetValueOrDefault() <= displaySeq)
                {
                    RespawnOther(otherPlayer, otherPlayer.RespawnPosition.GetValueOrDefault());
                    otherPlayer.RespawnSnapshotSeq = null;
                    otherPlayer.RespawnPosition = null;
                    playersToRespawn.Remove(playerId);
                }
            }
        }
    }

    public void SendPlayerDisconnect()
    {
        var packet = Packet.Obtain();
        ClientSerializationManager.SerializePlayerDisconnect(packet.buffer);
        packet.buffer.Flush();

        channel.Send(packet, serverIpEndPoint);

        packet.Free();
    }
    
    private void OnDestroy() {
        if (!disconnectFromPauseMenu)
        {
            SendPlayerDisconnect();
        }
        channel.Disconnect();
    }
    
    private void DeserializePlayerDisconnectBroadcast(BitBuffer buffer)
    {
        var disconnectedPlayerId = buffer.GetInt();
        Destroy(otherPlayers[disconnectedPlayerId].gameObject);
        otherPlayers.Remove(disconnectedPlayerId);
    }

    private void PlayRandomClip(AudioClip[] audioClips)
    {
        audioSource.PlayOneShot(audioClips[Random.Range(0, audioClips.Length)]);
    }
    
    private void CheckWalkingClipTimer()
    {
        walkingClipTimer += Time.fixedDeltaTime;
        if (walkingClipTimer > walkingClipTimerLimit)
        {
            walkingClipTimer = 0;
            PlayFootstep();
        }
    }

    private void PlayFootstep()
    {
        PlayRandomClip(walkingClips);
    }

    public void PlayShot()
    {
        var volume = audioSource.volume;
        audioSource.volume *= 0.5f;
        audioSource.PlayOneShot(shotClip);
        audioSource.volume = volume;
    }

    private void PlayDeath()
    {
        PlayRandomClip(deathClips);
    }

    public int ClientId => clientId;

    public string ClientName => clientName;

    public bool DisconnectFromPauseMenu
    {
        get => disconnectFromPauseMenu;
        set => disconnectFromPauseMenu = value;
    }
}
