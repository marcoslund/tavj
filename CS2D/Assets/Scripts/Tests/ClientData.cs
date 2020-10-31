using System.Collections;
using System.Collections.Generic;
using Tests;
using UnityEngine;

public class ClientData
{
    private int sendPort;
    private Channel sendChannel;
    private int recvPort;
    private Channel recvChannel;

    private Dictionary<int, float> playerJoinedTimeouts = new Dictionary<int, float>();
    
    private List<Commands> recvCommands = new List<Commands>();
    private int recvCommandSeq;
    private int recvShotSeq;
    private int health;
    
    private CharacterController controller;
    private float yVelocity;

    public int SendPort
    {
        get => sendPort;
        set => sendPort = value;
    }

    public Channel SendChannel
    {
        get => sendChannel;
        set => sendChannel = value;
    }

    public int RecvPort
    {
        get => recvPort;
        set => recvPort = value;
    }

    public Channel RecvChannel
    {
        get => recvChannel;
        set => recvChannel = value;
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
