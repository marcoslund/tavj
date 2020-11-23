using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ServerSerializationManager
{
    public static void ServerWorldSerialize(BitBuffer buffer, int snapshotSeq, float serverTime, int clientId, Dictionary<int, ClientData> clients)
    {
        var clientData = clients[clientId];
        
        buffer.PutByte((int) PacketType.Snapshot);
        buffer.PutInt(snapshotSeq);
        buffer.PutFloat(serverTime);
        buffer.PutInt(clientData.RecvCommandSeq);
        buffer.PutFloat(clientData.YVelocity);
        buffer.PutByte(clients.Count);

        foreach (var client in clients)
        {
            clientId = client.Key;
            var clientTransform = client.Value.Controller.transform;
            var position = clientTransform.position;
            var rotation = clientTransform.rotation;
            
            buffer.PutInt(clientId);
            buffer.PutFloat(position.x);
            buffer.PutFloat(position.y);
            buffer.PutFloat(position.z);
            buffer.PutFloat(rotation.w);
            buffer.PutFloat(rotation.x);
            buffer.PutFloat(rotation.y);
            buffer.PutFloat(rotation.z);
        }
    }
    
    public static void SerializePlayerJoinedResponse(BitBuffer buffer, int newUserId, Dictionary<int, ClientData> clients, 
        float serverTime, int nextSnapshotSeq, int minInterpolationBufferElems, Vector3 newClientPosition, 
        Quaternion newClientRotation, int health, float speed, float gravity, int clientCount)
    {
        var clientData = clients[newUserId];
        
        buffer.PutByte((int) PacketType.PlayerJoinedResponse);
        buffer.PutInt(newUserId);
        buffer.PutInt(clientData.ClientPort);
        buffer.PutInt(clientData.ServerPort);
        buffer.PutFloat(serverTime);
        buffer.PutInt(nextSnapshotSeq);
        buffer.PutByte(minInterpolationBufferElems);
        buffer.PutFloat(newClientPosition.x);
        buffer.PutFloat(newClientPosition.y);
        buffer.PutFloat(newClientPosition.z);
        buffer.PutFloat(newClientRotation.w);
        buffer.PutFloat(newClientRotation.x);
        buffer.PutFloat(newClientRotation.y);
        buffer.PutFloat(newClientRotation.z);
        buffer.PutInt(health);
        buffer.PutFloat(speed);
        buffer.PutFloat(gravity);
        buffer.PutByte(clientCount - 1);
        foreach (var clientPair in clients)
        {
            var clientId = clientPair.Key;
            if (clientId != newUserId)
            {
                var clientTransform = clientPair.Value.Controller.transform;
                var position = clientTransform.position;
                var rotation = clientTransform.rotation;
                
                buffer.PutInt(clientId);
                buffer.PutString(clientPair.Value.PlayerName);
                buffer.PutFloat(position.x);
                buffer.PutFloat(position.y);
                buffer.PutFloat(position.z);
                buffer.PutFloat(rotation.w);
                buffer.PutFloat(rotation.x);
                buffer.PutFloat(rotation.y);
                buffer.PutFloat(rotation.z);
            }
        }
    }
    
    public static void SerializePlayerJoined(BitBuffer buffer, int newUserId, string newClientName, Vector3 newClientPosition,
        Quaternion newClientRotation)
    {
        buffer.PutByte((int) PacketType.PlayerJoined);
        buffer.PutInt(newUserId);
        buffer.PutString(newClientName);
        buffer.PutFloat(newClientPosition.x);
        buffer.PutFloat(newClientPosition.y);
        buffer.PutFloat(newClientPosition.z);
        buffer.PutFloat(newClientRotation.w);
        buffer.PutFloat(newClientRotation.x);
        buffer.PutFloat(newClientRotation.y);
        buffer.PutFloat(newClientRotation.z);
    }
    
    public static void ServerSerializeCommandAck(BitBuffer buffer, int commandSequence)
    {
        buffer.PutByte((int) PacketType.CommandsAck);
        buffer.PutInt(commandSequence);
    }
    
    public static void ServerSerializeShotAck(BitBuffer buffer, int shotSequence)
    {
        buffer.PutByte((int) PacketType.PlayerShotAck);
        buffer.PutInt(shotSequence);
    }
    
    public static void SerializePlayerShotBroadcast(BitBuffer buffer, int shooterId, int shotPlayerId, int health)
    {
        buffer.PutByte((int) PacketType.PlayerShotBroadcast);
        buffer.PutInt(shooterId);
        buffer.PutInt(shotPlayerId);
        buffer.PutInt(health);
    }

    public static void SerializePlayerRespawnBroadcast(BitBuffer buffer, int shotPlayerId, Vector3 newPosition,
        int nextSnapshotSeq)
    {
        buffer.PutByte((int) PacketType.PlayerRespawn);
        buffer.PutInt(shotPlayerId);
        buffer.PutInt(nextSnapshotSeq);
        buffer.PutFloat(newPosition.x);
        buffer.PutFloat(newPosition.y);
        buffer.PutFloat(newPosition.z);
    }

    public static void SerializePlayerDisconnectBroadcast(BitBuffer buffer, int disconnectedClientId)
    {
        buffer.PutByte((int) PacketType.PlayerDisconnectBroadcast);
        buffer.PutInt(disconnectedClientId);
    }
}
