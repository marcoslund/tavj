using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shot
{
    private int seq;
    private int shotPlayerId;

    public Shot(int seq, int shotPlayerId)
    {
        this.seq = seq;
        this.shotPlayerId = shotPlayerId;
    }

    public int Seq => seq;

    public int ShotPlayerId => shotPlayerId;
}
