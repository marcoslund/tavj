using System.Collections;
using System.Collections.Generic;
using Tests;
using UnityEngine;

public class SerializationManager
{
    public static void ServerWorldSerialize(Rigidbody rigidBody, BitBuffer buffer, int seq, float time) {
        var transform = rigidBody.transform;
        var position = transform.position;
        var rotation = transform.rotation;
        buffer.PutByte((int) PacketType.Snapshot);
        buffer.PutInt(seq);
        buffer.PutFloat(time);
        buffer.PutFloat(position.x);
        buffer.PutFloat(position.y);
        buffer.PutFloat(position.z);
        buffer.PutFloat(rotation.w);
        buffer.PutFloat(rotation.x);
        buffer.PutFloat(rotation.y);
        buffer.PutFloat(rotation.z);
    }

    public static List<Commands> ServerDeserializeInput(BitBuffer buffer)
    {
        List<Commands> totalCommands = new List<Commands>();
        
        while (buffer.HasRemaining())
        {
            int seq = buffer.GetInt();

            Commands commands = new Commands(
                seq,
                buffer.GetInt() > 0,
                buffer.GetInt() > 0,
                buffer.GetInt() > 0,
                buffer.GetInt() > 0,
                buffer.GetInt() > 0);

            totalCommands.Add(commands);
        }

        return totalCommands;
    }

    public static void ServerSerializeAck(BitBuffer buffer, int commandSequence)
    {
        buffer.PutByte((int) PacketType.Ack);
        buffer.PutInt(commandSequence);
    }
}
