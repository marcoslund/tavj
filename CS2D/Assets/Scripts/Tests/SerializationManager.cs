using System.Collections;
using System.Collections.Generic;
using Tests;
using UnityEngine;

public class SerializationManager
{
    public static void ServerWorldSerialize(BitBuffer buffer, Dictionary<int, CharacterController> clients,
        int snapshotSeq, float serverTime, int recvCmdSeq) {
        buffer.PutByte((int) PacketType.Snapshot);
        buffer.PutInt(snapshotSeq);
        buffer.PutFloat(serverTime);
        buffer.PutInt(recvCmdSeq);
        buffer.PutByte(clients.Count);

        foreach (var client in clients)
        {
            var clientId = client.Key;
            var transform = client.Value.transform;
            var position = transform.position;
            var rotation = transform.rotation;
            
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

    public static void ServerSerializeCommandAck(BitBuffer buffer, int commandSequence)
    {
        buffer.PutByte((int) PacketType.CommandsAck);
        buffer.PutInt(commandSequence);
    }
}
