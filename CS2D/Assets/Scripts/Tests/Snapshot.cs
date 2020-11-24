using System;
using System.Collections.Generic;
using UnityEngine;

public class Snapshot : IComparable<Snapshot>
{
    private int seq;
    private float time;
    private Dictionary<int, Vector3> positions;
    private Dictionary<int, Quaternion> rotations;

    public Snapshot(int seq, float time, Dictionary<int, Vector3> positions, Dictionary<int, Quaternion> rotations)
    {
        this.seq = seq;
        this.time = time;
        this.positions = positions;
        this.rotations = rotations;
    }

    public int CompareTo(Snapshot other)
    {
        return seq.CompareTo(other.seq);
    }

    public int Seq => seq;

    public float Time => time;

    public Dictionary<int, Vector3> Positions => positions;

    public Dictionary<int, Quaternion> Rotations => rotations;

    public override string ToString()
    {
        var result = $"Seq: {seq}, Time: {time}, ";
        foreach (var pos in positions)
        {
            result += $"Player {pos.Key}: Position ({pos.Value.x}, {pos.Value.y}, {pos.Value.z}) Rotation: {rotations[pos.Key]}, ";
        }

        return result;
    }
}