using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstPersonView : MonoBehaviour
{
    [HideInInspector] public Transform myCamera;
    public float mouseSensitivity = 0;
    [HideInInspector] public float mouseSensitivity_notAiming = 10;
    [HideInInspector] public float mouseSensitivity_aiming = 2;
    [Header("Z Rotation Camera")]
    [HideInInspector] public float timer;
    [HideInInspector] public int int_timer;
    [HideInInspector] public float zRotation;
    [HideInInspector] public float wantedZ;
    [HideInInspector] public float timeSpeed = 2;
    [HideInInspector] public float timerToRotateZ;
    
    private float rotationYVelocity, cameraXVelocity;
    public float yRotationSpeed, xCameraSpeed;
    [HideInInspector] public float wantedYRotation;
    [HideInInspector] public float currentYRotation;
    [HideInInspector] public float wantedCameraXRotation;
    [HideInInspector] public float currentCameraXRotation;
    public float topAngleView = 60;
    public float bottomAngleView = -45;

    private ClientEntity clientEntity;

    private void Awake(){
        Cursor.lockState = CursorLockMode.Locked;
        myCamera = GameObject.FindGameObjectWithTag("MainCamera").transform;
        myCamera.SetParent(transform);
        myCamera.localPosition = new Vector3(0, 1, 0);

        clientEntity = GetComponent<ClientEntity>();
    }

    // Update is called once per frame
    private void Update()
    {
        if (clientEntity.playerDead) return;
        
        MouseInputMovement();
        
        if(clientEntity.speed > 1)
            HeadMovement ();
    }

    private void MouseInputMovement(){
        wantedYRotation += Input.GetAxis("Mouse X") * mouseSensitivity;
        wantedCameraXRotation -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        wantedCameraXRotation = Mathf.Clamp(wantedCameraXRotation, bottomAngleView, topAngleView);
    }

    private void HeadMovement() {
        timer += timeSpeed * Time.deltaTime;
        int_timer = Mathf.RoundToInt(timer);
        if (int_timer % 2 == 0) {
            wantedZ = -1;
        } else {
            wantedZ = 1;
        }
        zRotation = Mathf.Lerp (zRotation, wantedZ, Time.deltaTime * timerToRotateZ);
    }

    private void FixedUpdate() {
        if (clientEntity.playerDead) return;
        
        if(Input.GetAxis("Fire2") != 0) {
            mouseSensitivity = mouseSensitivity_aiming;
        }
        else {
            mouseSensitivity = mouseSensitivity_notAiming;
        }
        ApplySmooth();
    }
    
    void ApplySmooth() {
        currentYRotation = Mathf.SmoothDamp(currentYRotation, wantedYRotation, ref rotationYVelocity, yRotationSpeed);
        currentCameraXRotation = Mathf.SmoothDamp(currentCameraXRotation, wantedCameraXRotation, ref cameraXVelocity, xCameraSpeed);

        transform.rotation = Quaternion.Euler(0, currentYRotation, 0);
        myCamera.localRotation = Quaternion.Euler(currentCameraXRotation, 0, zRotation);
    }
}
