using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCopyManager : MonoBehaviour
{
    private string playerName;
    
    private CharacterController characterController;

    public GameObject[] muzzelFlash;
    public Transform muzzelSpawn;

    private bool isDead;
    private int? respawnSnapshotSeq;
    private Vector3? respawnPosition;
    
    public Animator animator;
    private const float Epsilon = 0.00001f;
    private bool prevFrameMoved;
    private int prevFrameVertMovement;
    private int prevFrameHorizMovement;
    
    [Header("Audio Clips")]
    private AudioSource audioSource;
    public AudioClip[] walkingClips;

    public AudioClip shotClip;
    public AudioClip[] deathClips;

    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
    }

    public void MovePlayerCopy(Vector3 position, Quaternion rotation)
    {
        var transf = transform;
        var delta = transf.position - position;
        
        SetAnimatorMovementParameters(delta);
        
        characterController.Move(-delta);
        transf.rotation = rotation;
    }

    public void SetAnimatorMovementParameters(Vector3 delta)
    {
        var deltaX = delta.x;
        var deltaZ = delta.z;
        
        if (deltaX == 0 && deltaZ == 0)
        {
            if (prevFrameMoved)
            {
                animator.SetFloat("Horizontal Movement", prevFrameHorizMovement);
                animator.SetFloat("Vertical Movement", prevFrameVertMovement);
            }
            else
            {
                animator.SetFloat("Horizontal Movement", 0);
                animator.SetFloat("Vertical Movement", 0);
            }
            prevFrameMoved = false;
            return;
        }

        if (deltaX > Epsilon)
        {
            animator.SetFloat("Horizontal Movement", 1);
            prevFrameHorizMovement = 1;
            prevFrameMoved = true;
        }
        else if (deltaX < -Epsilon)
        {
            animator.SetFloat("Horizontal Movement", -1);
            prevFrameHorizMovement = -1;
            prevFrameMoved = true;
        }
        else
        {
            animator.SetFloat("Horizontal Movement", 0);
            prevFrameHorizMovement = 0;
        }

        if (deltaZ > Epsilon)
        {
            animator.SetFloat("Vertical Movement", 1);
            prevFrameVertMovement = 1;
            prevFrameMoved = true;
        }
        else if (deltaZ < -Epsilon)
        {
            animator.SetFloat("Vertical Movement", -1);
            prevFrameVertMovement = -1;
            prevFrameMoved = true;
        }
        else
        {
            animator.SetFloat("Vertical Movement", 0);
            prevFrameVertMovement = 0;
        }
    }

    public void TriggerDeathAnimation()
    {
        animator.SetTrigger("Dying");
    }

    public void TriggerRespawnAnimation()
    {
        animator.SetTrigger("Respawn");
    }

    public void MovePlayerCopyDirect(Vector3 newPosition)
    {
        characterController.Move(newPosition - transform.position);
    }

    public void ShowMuzzelFlash()
    {
        var holdFlash = Instantiate(muzzelFlash[Random.Range(0,5)], muzzelSpawn.transform.position, 
            muzzelSpawn.transform.rotation * Quaternion.Euler(0,0,90) );
        holdFlash.transform.parent = muzzelSpawn.transform;
    }

    public string PlayerName
    {
        get => playerName;
        set => playerName = value;
    }

    public bool IsDead
    {
        get => isDead;
        set => isDead = value;
    }

    public int? RespawnSnapshotSeq
    {
        get => respawnSnapshotSeq;
        set => respawnSnapshotSeq = value;
    }

    public Vector3? RespawnPosition
    {
        get => respawnPosition;
        set => respawnPosition = value;
    }
    
    public void PlayFootstep()
    {
        PlayRandomClip(walkingClips);
    }
    
    public void PlayShot()
    {
        var volume = audioSource.volume;
        audioSource.volume *= 0.5f;
        audioSource.PlayOneShot(shotClip);
        audioSource.volume = volume;
    }
    
    public void PlayDeath()
    {
        PlayRandomClip(deathClips);
    }
    
    private void PlayRandomClip(AudioClip[] audioClips)
    {
        audioSource.PlayOneShot(audioClips[Random.Range(0, audioClips.Length)]);
    }
}
