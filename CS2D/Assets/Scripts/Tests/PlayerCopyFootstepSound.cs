using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCopyFootstepSound : MonoBehaviour
{
    public PlayerCopyManager playerCopyManager;
    
    private void PlayFootstep() // Called as animation event
    {
        playerCopyManager.PlayFootstep();
    }
}
