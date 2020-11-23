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
    private const float Epsilon = 0.0001f;

    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        //animator.SetBool("Shooting", true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void MovePlayerCopy(Vector3 position, Quaternion rotation)
    {
        var transf = transform;
        //Debug.Log($"{transf.position.x} {transf.position.y} {transf.position.z} {position.x} {position.y} {position.z}");
        var delta = transf.position - position;
        SetAnimatorMovementParameters(delta);
        //characterController.Move(-delta);
        //Debug.Log($"{position.x} {position.y} {position.z}");
        transf.position = position;
        transf.rotation = rotation;
    }

    public void SetAnimatorMovementParameters(Vector3 delta)
    {
        var deltaX = delta.x;
        var deltaZ = delta.z;
        //Debug.Log(deltaX + " " + deltaZ);

        //if(deltaX > Epsilon) animator.SetFloat("Horizontal Movement", 1);
        //else if(deltaX < -Epsilon) animator.SetFloat("Horizontal Movement", -1);
        //else animator.SetFloat("Horizontal Movement", 0);
        
        if(deltaZ > Epsilon) animator.SetFloat("Vertical Movement", 1);
        //else if(deltaZ < -Epsilon) animator.SetFloat("Vertical Movement", -1);
        //else animator.SetFloat("Vertical Movement", 0);
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
    
    private void PlayFootstep() // Called as animation event
    {
        
    }
}
