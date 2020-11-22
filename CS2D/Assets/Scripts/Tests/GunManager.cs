using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class GunManager : MonoBehaviour
{
    [HideInInspector] public FirstPersonView firstPersonView;
    //[HideInInspector] public ShootManager shootManager;

	private Transform player;
	private Transform gunPlaceHolder;
	
	[HideInInspector] public Transform mainCamera;
	private Camera mainCameraComponent;
	private Camera secondCameraComponent;

	private ClientEntity clientEntity;
	
	[Header("Shooting Setup")]
	public float roundsPerSecond;
	private float waitTillNextFire;
	
	[Header("Gun Sensitivity")]
	public float mouseSensitivity_notAiming = 4;
	public float mouseSensitivity_aiming = 1;
	public float mouseSensitivity_running = 1;
	
	[Header("Gun Positioning")]
	[HideInInspector] public Vector3 currentGunPosition;
	public Vector3 restPlacePosition;
	public Vector3 aimPlacePosition;
	public float gunAimTime = 0.1f;
	private Vector3 gunPosVelocity;
	private float cameraZoomVelocity;
	private float secondCameraZoomVelocity;
	private Vector2 gunFollowTimeVelocity;
	
	public Animator handsAnimator;
	
	private Vector3 velV;
	
	public GameObject[] muzzelFlash;
	public GameObject muzzelSpawn;
	private GameObject holdFlash;
	private GameObject holdSmoke;
	
	private float currentRecoilZPos;
	private float currentRecoilXPos;
	private float currentRecoilYPos;
	
	[HideInInspector] public float recoilAmount_z = 0.5f;
	[HideInInspector] public float recoilAmount_x = 0.5f;
	[HideInInspector] public float recoilAmount_y = 0.5f;
	[Header("Recoil Not Aiming")]
	public float recoilAmount_z_non = 0.5f;
	public float recoilAmount_x_non = 0.5f;
	public float recoilAmount_y_non = 0.5f;
	[Header("Recoil Aiming")]
	public float recoilAmount_z_ = 0.5f;
	public float recoilAmount_x_ = 0.5f;
	public float recoilAmount_y_ = 0.5f;
	[HideInInspector] public float velocity_z_recoil, velocity_x_recoil, velocity_y_recoil;
	[Header("")]
	public float recoilOverTime_z = 0.5f;
	public float recoilOverTime_x = 0.5f;
	public float recoilOverTime_y = 0.5f;

	[Header("Crosshair properties")]
	public Texture horizontal_crosshair;
	public Texture vertical_crosshair;
	public Vector2 top_pos_crosshair, bottom_pos_crosshair, left_pos_crosshair, right_pos_crosshair;
	public Vector2 size_crosshair_vertical = new Vector2(1,1), size_crosshair_horizontal = new Vector2(1,1);
	[HideInInspector]
	public Vector2 expandValues_crosshair;
	private float fadeout_value = 1;
	
	[Header("Rotation")]
	private Vector2 velocityGunRotate;
	private float gunWeightX,gunWeightY;
	public float rotationLagTime = 0f;
	private float rotationLastY;
	private float rotationDeltaY;
	private float angularVelocityY;
	private float rotationLastX;
	private float rotationDeltaX;
	private float angularVelocityX;
	public Vector2 forwardRotationAmount = Vector2.one;
	
	[Header("Gun Precision")]
	public float gunPrecision_notAiming = 200.0f;
	public float gunPrecision_aiming = 100.0f;
	public float cameraZoomRatio_notAiming = 60;
	public float cameraZoomRatio_aiming = 40;
	public float secondCameraZoomRatio_notAiming = 60;
	public float secondCameraZoomRatio_aiming = 40;
	[HideInInspector] public float gunPrecision;

	public LayerMask layerMask;
	public float shotMaxDistance = 1000000;
	RaycastHit shotRaycastHit;
	private int playerLayer;
	
	[Header("Animation names")]
	public string aimingAnimationName = "Player_AImpose";
	
	[Header("Audio Sources")]
	public AudioSource shootSoundSource;
	public static AudioSource hitMarker;

	private void Awake()
	{
		player = GameObject.FindWithTag("ClientEntity").transform;
		firstPersonView = player.GetComponent<FirstPersonView>();
		//shootManager = player.GetComponent<ShootManager>();
		mainCamera = firstPersonView.myCamera;
		mainCameraComponent = mainCamera.GetComponent<Camera>();
		secondCameraComponent = GameObject.FindGameObjectWithTag("SecondCamera").GetComponent<Camera>();
		
		clientEntity = player.GetComponent<ClientEntity>();

		hitMarker = transform.Find ("hitMarkerSound").GetComponent<AudioSource> (); //TODO CHANGE
		
		firstPersonView.mouseSensitivity_notAiming = mouseSensitivity_notAiming;
		firstPersonView.mouseSensitivity_aiming = mouseSensitivity_aiming;

		rotationLastY = firstPersonView.currentYRotation;
		rotationLastX= firstPersonView.currentCameraXRotation;
		
		playerLayer = LayerMask.NameToLayer("Client 1");
	}

	private void Start()
	{
		//layerMask = LayerMask.GetMask(LayerMask.LayerToName(transform.gameObject.layer));
	}

	private void Update()
	{
		if (clientEntity.playerDead) return;
		
		Animations();
		PositionGun();
		Shoot();
		//Sprint();
	}
	
	private void Animations()
	{
		if (!handsAnimator) return;

		//handsAnimator.SetFloat("walkSpeed", clientEntity.currentSpeed);
		handsAnimator.SetBool("Aiming", Input.GetButton("Fire2"));
	}
	
	private void PositionGun() {
		transform.position = Vector3.SmoothDamp(transform.position,
			mainCamera.position  - 
			(mainCamera.right * (currentGunPosition.x + currentRecoilXPos)) + 
			(mainCamera.up * (currentGunPosition.y+ currentRecoilYPos)) + 
			(mainCamera.forward * (currentGunPosition.z + currentRecoilZPos)),ref velV, 0);
		
		clientEntity.cameraPosition = new Vector3(currentRecoilXPos, currentRecoilYPos, 0); // shootManager...

		currentRecoilZPos = Mathf.SmoothDamp(currentRecoilZPos, 0, ref velocity_z_recoil, recoilOverTime_z);
		currentRecoilXPos = Mathf.SmoothDamp(currentRecoilXPos, 0, ref velocity_x_recoil, recoilOverTime_x);
		currentRecoilYPos = Mathf.SmoothDamp(currentRecoilYPos, 0, ref velocity_y_recoil, recoilOverTime_y);
	}

	private void Shoot() {
		if (Input.GetButton ("Fire1") && waitTillNextFire <= 0) {
			int randomNumberForMuzzelFlash = Random.Range(0,5);

			if (Physics.Raycast(transform.position, transform.forward, out shotRaycastHit, shotMaxDistance,
				layerMask))
			{
				//Debug.DrawLine(transform.position, shotRaycastHit.point);
				if (shotRaycastHit.transform.gameObject.layer == playerLayer)
				{
					var shotPlayerId = Int32.Parse(shotRaycastHit.transform.name);
					if (!clientEntity.OtherPlayerIsDead(shotPlayerId))
					{
						clientEntity.SendPlayerShotMessage(shotPlayerId);
					}
					else
					{
						Debug.Log("PLAYER ALREADY DEAD");
					}
				}
			}
			
			holdFlash = Instantiate(muzzelFlash[randomNumberForMuzzelFlash], muzzelSpawn.transform.position, 
				muzzelSpawn.transform.rotation * Quaternion.Euler(0,0,90) );
			holdFlash.transform.parent = muzzelSpawn.transform;
			
			//shootSoundSource.Play();
			RecoilMath();
			waitTillNextFire = 1;
		}
		waitTillNextFire -= roundsPerSecond * Time.deltaTime;
	}

	private void RecoilMath() {
		currentRecoilZPos -= recoilAmount_z;
		currentRecoilXPos -= (Random.value - 0.5f) * recoilAmount_x;
		currentRecoilYPos -= (Random.value - 0.5f) * recoilAmount_y;
		firstPersonView.wantedCameraXRotation -= Mathf.Abs(currentRecoilYPos * gunPrecision);
		firstPersonView.wantedYRotation -= (currentRecoilXPos * gunPrecision);		 

		expandValues_crosshair += new Vector2(6,12);
	}

	private void FixedUpdate()
	{
		if (clientEntity.playerDead) return;
		
		RotationGun ();
		
		if(Input.GetAxis("Fire2") != 0) {
			gunPrecision = gunPrecision_aiming;
			recoilAmount_x = recoilAmount_x_;
			recoilAmount_y = recoilAmount_y_;
			recoilAmount_z = recoilAmount_z_;
			currentGunPosition = Vector3.SmoothDamp(currentGunPosition, aimPlacePosition, ref gunPosVelocity, gunAimTime);
			mainCameraComponent.fieldOfView = Mathf.SmoothDamp(mainCameraComponent.fieldOfView, cameraZoomRatio_aiming, ref cameraZoomVelocity, gunAimTime);
			secondCameraComponent.fieldOfView = Mathf.SmoothDamp(secondCameraComponent.fieldOfView, secondCameraZoomRatio_aiming, ref secondCameraZoomVelocity, gunAimTime);
		} else {
			gunPrecision = gunPrecision_notAiming;
			recoilAmount_x = recoilAmount_x_non;
			recoilAmount_y = recoilAmount_y_non;
			recoilAmount_z = recoilAmount_z_non;
			currentGunPosition = Vector3.SmoothDamp(currentGunPosition, restPlacePosition, ref gunPosVelocity, gunAimTime);
			mainCameraComponent.fieldOfView = Mathf.SmoothDamp(mainCameraComponent.fieldOfView, cameraZoomRatio_notAiming, ref cameraZoomVelocity, gunAimTime);
			secondCameraComponent.fieldOfView = Mathf.SmoothDamp(secondCameraComponent.fieldOfView, secondCameraZoomRatio_notAiming, ref secondCameraZoomVelocity, gunAimTime);
		}
	}
	
	private void RotationGun() {
		rotationDeltaY = firstPersonView.currentYRotation - rotationLastY;
		rotationDeltaX = firstPersonView.currentCameraXRotation - rotationLastX;

		rotationLastY= firstPersonView.currentYRotation;
		rotationLastX= firstPersonView.currentCameraXRotation;

		angularVelocityY = Mathf.Lerp(angularVelocityY, rotationDeltaY, Time.deltaTime * 5);
		angularVelocityX = Mathf.Lerp(angularVelocityX, rotationDeltaX, Time.deltaTime * 5);

		gunWeightX = Mathf.SmoothDamp(gunWeightX, firstPersonView.currentCameraXRotation, ref velocityGunRotate.x, rotationLagTime);
		gunWeightY = Mathf.SmoothDamp(gunWeightY, firstPersonView.currentYRotation, ref velocityGunRotate.y, rotationLagTime);

		transform.rotation = Quaternion.Euler (gunWeightX + (angularVelocityX * forwardRotationAmount.x), 
			gunWeightY + (angularVelocityY * forwardRotationAmount.y), 0);
	}

	/* 
	 * Changes the max speed that player is allowed to go.
	 * Also max speed is connected to the animator which will trigger the run animation.
	 */
	/*private void Sprint() {
		if (Input.GetAxis ("Vertical") > 0 && Input.GetAxisRaw ("Fire2") == 0 && Input.GetAxisRaw ("Fire1") == 0)
		{
			if (!Input.GetKeyDown(KeyCode.LeftShift)) return;
			if (clientEntity.currentSpeed == walkingSpeed) {
				pmS.maxSpeed = runningSpeed;//sets player movement peed to max

			} else {
				pmS.maxSpeed = walkingSpeed;
			}
		} else {
			pmS.maxSpeed = walkingSpeed;
		}
	}*/

	/*public static void HitMarkerSound(){
		hitMarker.Play();
	}*/

	private void OnGUI() {
		//DrawCrosshair();
	}

	private void DrawCrosshair() {
		GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, fadeout_value);
		if(Input.GetAxis("Fire2") == 0){
			GUI.DrawTexture(new Rect(vec2(left_pos_crosshair).x + position_x(-expandValues_crosshair.x) + Screen.width/2,Screen.height/2 + vec2(left_pos_crosshair).y, vec2(size_crosshair_horizontal).x, vec2(size_crosshair_horizontal).y), vertical_crosshair);//left
			GUI.DrawTexture(new Rect(vec2(right_pos_crosshair).x + position_x(expandValues_crosshair.x) + Screen.width/2,Screen.height/2 + vec2(right_pos_crosshair).y, vec2(size_crosshair_horizontal).x, vec2(size_crosshair_horizontal).y), vertical_crosshair);//right

			GUI.DrawTexture(new Rect(vec2(top_pos_crosshair).x + Screen.width/2,Screen.height/2 + vec2(top_pos_crosshair).y + position_y(-expandValues_crosshair.y), vec2(size_crosshair_vertical).x, vec2(size_crosshair_vertical).y ), horizontal_crosshair);//top
			GUI.DrawTexture(new Rect(vec2(bottom_pos_crosshair).x + Screen.width/2,Screen.height/2 +vec2(bottom_pos_crosshair).y + position_y(expandValues_crosshair.y), vec2(size_crosshair_vertical).x, vec2(size_crosshair_vertical).y), horizontal_crosshair);//bottom
		}

	}

	private float position_x(float var)
	{
		return Screen.width * var / 100;
	}
	
	private float position_y(float var)
	{
		return Screen.height * var / 100;
	}
	
	private float size_x(float var)
	{
		return Screen.width * var / 100;
	}
	
	private float size_y(float var)
	{
		return Screen.height * var / 100;
	}
	
	private Vector2 vec2(Vector2 _vec2){
		return new Vector2(Screen.width * _vec2.x / 100, Screen.height * _vec2.y / 100);
	}
}
