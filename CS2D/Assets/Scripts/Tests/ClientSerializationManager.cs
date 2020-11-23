using System.Collections;
using System.Collections.Generic;
using Tests;
using UnityEngine;

public class ClientSerializationManager
{
    /*
     * -- FORMAT --
     * Packet Type (byte)
     * Client ID (int)
     * Commands Count (int)
     * (Commands...)
     */
    public static void SerializeCommands(BitBuffer buffer, List<Commands> commandsList, int clientId)
    {
        buffer.PutByte((int) PacketType.Commands);
        buffer.PutInt(clientId);
        buffer.PutInt(commandsList.Count);
        foreach (var commands in commandsList)
        {
            buffer.PutInt(commands.Seq);
            buffer.PutByte(commands.Up ? 1 : 0);
            buffer.PutByte(commands.Down ? 1 : 0);
            buffer.PutByte(commands.Left ? 1 : 0);
            buffer.PutByte(commands.Right ? 1 : 0);
            buffer.PutFloat(commands.RotationY);
        }
    }
    
    /*
     * -- FORMAT --
     * Packet Type (byte)
     * Client ID (int)
     * New Player ID (int)
     */
    public static void SerializePlayerJoinedAck(BitBuffer buffer, int newPlayerId, int clientId)
    {
        buffer.PutByte((int) PacketType.PlayerJoinedAck);
        buffer.PutInt(clientId);
        buffer.PutInt(newPlayerId);
    }
    
    /*
     * -- FORMAT --
     * Packet Type (byte)
     * Client ID (int)
     * Shots Count (int)
     * (Shots...)
     */
    public static void SerializePlayerShot(BitBuffer buffer, List<Shot> shotsList, int clientId)
    {
        buffer.PutByte((int) PacketType.PlayerShot);
        buffer.PutInt(clientId);
        buffer.PutInt(shotsList.Count);
        foreach (var shot in shotsList)
        {
            buffer.PutInt(shot.Seq);
            buffer.PutInt(shot.ShotPlayerId);
        }
    }

    /*
     * -- FORMAT --
     * Packet Type (byte)
     */
    public static void SerializePlayerDisconnect(BitBuffer buffer)
    {
        buffer.PutByte((int) PacketType.PlayerDisconnect);
    }
}
