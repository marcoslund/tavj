using System.Collections;
using System.Collections.Generic;
using System.Net;
using Tests;
using UnityEngine;

public class ClientData
{
    private int clientPort;
    private int serverPort;
    private Channel channel;
    private IPEndPoint clientIpEndPoint;

    private Dictionary<int, float> playerJoinedTimeouts = new Dictionary<int, float>();
    
    private List<Commands> recvCommands = new List<Commands>();
    private int recvCommandSeq;
    private int recvShotSeq;
    private int health;
    
    private CharacterController controller;
    private float yVelocity;

    public int ClientPort
    {
        get => clientPort;
        set => clientPort = value;
    }

    public int ServerPort
    {
        get => serverPort;
        set => serverPort = value;
    }

    public Channel Channel
    {
        get => channel;
        set => channel = value;
    }

    public IPEndPoint ClientIpEndPoint
    {
        get => clientIpEndPoint;
        set => clientIpEndPoint = value;
    }

    public Dictionary<int, float> PlayerJoinedTimeouts
    {
        get => playerJoinedTimeouts;
        set => playerJoinedTimeouts = value;
    }

    public List<Commands> RecvCommands
    {
        get => recvCommands;
        set => recvCommands = value;
    }

    public int RecvCommandSeq
    {
        get => recvCommandSeq;
        set => recvCommandSeq = value;
    }

    public int RecvShotSeq
    {
        get => recvShotSeq;
        set => recvShotSeq = value;
    }

    public int Health
    {
        get => health;
        set => health = value;
    }

    public CharacterController Controller
    {
        get => controller;
        set => controller = value;
    }

    public float YVelocity
    {
        get => yVelocity;
        set => yVelocity = value;
    }
}
