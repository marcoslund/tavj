using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Tests;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;
using Random = System.Random;

public class SimulationTest : MonoBehaviour
{

    public GameObject clientPrefab;

    [SerializeField] private Rigidbody cubeRigidBody;

    [SerializeField] private Dictionary<int, ClientEntity> cubeClients = new Dictionary<int, ClientEntity>();

    public const int PortsPerClient = 2;
    public int sendBasePort = 9000;
    public int recvBasePort = 9001;
    public int clientCount = 0;
    
    public int pps = 10;
    private float sendRate;
    
    private float accum = 0;
    private float serverTime = 0;
    private int seq = 0; // Next snapshot to send
    private bool serverConnected;
    
    public int interpolationCount = 2;

    // Start is called before the first frame update
    void Start() {
        sendRate = 1f / pps;
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown(KeyCode.D))
        {
            serverConnected = !serverConnected;
        }

        accum += Time.deltaTime;

        if (serverConnected)
        {
            UpdateServer();
        }
    }

    private void UpdateServer()
    {
        serverTime += Time.deltaTime;

        foreach (var cubeClientPair in cubeClients)
        {
            int userID = cubeClientPair.Key;
            ClientEntity cubeClient = cubeClientPair.Value;
            var commandPacket = cubeClient.sendChannel.GetPacket();
            
            if (commandPacket != null) {
                var buffer = commandPacket.buffer;

                List<Commands> commandsList = SerializationManager.ServerDeserializeInput(buffer);
                var packet = Packet.Obtain();
                int receivedCommandSequence = -1;
                foreach (Commands commands in commandsList)
                {
                    receivedCommandSequence = commands.Seq;
                    ExecuteClientInput(commands);
                }
                SerializationManager.ServerSerializeAck(packet.buffer, receivedCommandSequence);
                packet.buffer.Flush();

                string serverIP = "127.0.0.1";
                var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), cubeClient.recvPort);
                cubeClient.recvChannel.Send(packet, remoteEp);

                packet.Free();
            }
            
            if (accum >= sendRate)
            {
                // Serialize & send snapshot to clients
                var packet = Packet.Obtain();
                SerializationManager.ServerWorldSerialize(cubeRigidBody, packet.buffer, seq, serverTime);
                packet.buffer.Flush();

                string serverIP = "127.0.0.1";
                var remoteEp = new IPEndPoint(IPAddress.Parse(serverIP), cubeClient.recvPort);
                cubeClient.recvChannel.Send(packet, remoteEp);

                packet.Free();

                accum -= sendRate;
                seq++;
            }
        }
    }
    
    private void ExecuteClientInput(Commands commands)
    {
        //apply input
        if (commands.Space) {
            cubeRigidBody.AddForceAtPosition(Vector3.up * 5, Vector3.zero, ForceMode.Impulse);
        }
        if (commands.Left) {
            cubeRigidBody.AddForceAtPosition(Vector3.left * 5, Vector3.zero, ForceMode.Impulse);
        }
        if (commands.Right) {
            cubeRigidBody.AddForceAtPosition(Vector3.right * 5, Vector3.zero, ForceMode.Impulse);
        }
        if (commands.Up) {
            cubeRigidBody.AddForceAtPosition(Vector3.forward * 5, Vector3.zero, ForceMode.Impulse);
        }
        if (commands.Down) {
            cubeRigidBody.AddForceAtPosition(Vector3.back * 5, Vector3.zero, ForceMode.Impulse);
        }
    }
}
