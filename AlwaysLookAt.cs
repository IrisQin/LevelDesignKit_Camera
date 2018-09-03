using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class AlwaysLookAt : MonoBehaviour {

    private Transform target;

    void Start () {
        target = transform.GetComponent<ThirdPersonCameraController>().target;

    }
	
	
	void Update () {
        transform.LookAt(target);
        Debug.DrawLine(transform.position, target.position, Color.green);
        
    }
}