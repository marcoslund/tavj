using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShootManager : MonoBehaviour
{
    [HideInInspector] public Transform cameraMain;
    [HideInInspector] public Vector3 cameraPosition;
    [HideInInspector] public Transform raySpawn;
    
    public GameObject gun;
    private Animator handsAnimator;
    public Texture icon;
    
    public AudioSource weaponChanging;
    
    // Start is called before the first frame update
    private void Awake()
    {
        cameraMain = GameObject.FindGameObjectWithTag("MainCamera").transform;
        raySpawn = GameObject.FindGameObjectWithTag("RaySpawn").transform;
        icon = (Texture) Resources.Load("Weap_Icons/NewGun_auto_img");
        
        StartCoroutine ("SpawnWeaponUponStart");
    }

    private IEnumerator SpawnWeaponUponStart() {
        yield return new WaitForSeconds (0.5f);
        if (weaponChanging)
            weaponChanging.Play ();
        
        var resource = (GameObject) Resources.Load("Gun");
        gun = Instantiate(resource, transform.position, Quaternion.identity, transform);
        gun.layer = transform.gameObject.layer;
        handsAnimator = gun.GetComponent<GunManager>().handsAnimator;
    }
    
    /*
	 * Call this method when player dies.
	 */
    public void Die() {
        Destroy (gun);
        Destroy (this);
    }

    // Update is called once per frame
    void Update()
    {
        //WalkingSound ();
    }
    
    /*void WalkingSound(){
        if (_walkSound && _runSound) {
            if (RayCastGrounded ()) { //for walk sounsd using this because suraface is not straigh			
                if (currentSpeed > 1) {
                    //				print ("unutra sam");
                    if (maxSpeed == 3) {
                        //	print ("tu sem");
                        if (!_walkSound.isPlaying) {
                            //	print ("playam hod");
                            _walkSound.Play ();
                            _runSound.Stop ();
                        }					
                    } else if (maxSpeed == 5) {
                        //	print ("NE tu sem");

                        if (!_runSound.isPlaying) {
                            _walkSound.Stop ();
                            _runSound.Play ();
                        }
                    }
                } else {
                    _walkSound.Stop ();
                    _runSound.Stop ();
                }
            } else {
                _walkSound.Stop ();
                _runSound.Stop ();
            }
        } else {
            print ("Missing walk and running sounds.");
        }

    }*/
}
