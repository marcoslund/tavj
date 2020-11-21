using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCopyManager : MonoBehaviour
{
    public Animator animator;
    private const float Epsilon = 0.0001f;

    // Start is called before the first frame update
    void Start()
    {
        //animator.SetBool("Shooting", true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void MovePlayerCopy(Vector3 position, Quaternion rotation)
    {
        var transf = transform;
        SetAnimatorMovementParameters(transf.position - position);
        transf.position = position;
        transf.rotation = rotation;
    }

    public void SetAnimatorMovementParameters(Vector3 delta)
    {
        var deltaX = delta.x;
        var deltaZ = delta.z;
        Debug.Log(deltaX + " " + deltaZ);

        if(deltaX > Epsilon) animator.SetFloat("Horizontal Movement", 1);
        else if(deltaX < -Epsilon) animator.SetFloat("Horizontal Movement", -1);
        else animator.SetFloat("Horizontal Movement", 0);
        
        if(deltaZ > Epsilon) animator.SetFloat("Vertical Movement", 1);
        else if(deltaZ < -Epsilon) animator.SetFloat("Vertical Movement", -1);
        else animator.SetFloat("Vertical Movement", 0);
    }

    public void TriggerDeathAnimation()
    {
        animator.SetTrigger("Dying");
    }
}
