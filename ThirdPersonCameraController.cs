using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCameraController : MonoBehaviour {

    #region inspector properties    

    public Transform target;

    public float rotationSmoothTime = .12f;
    public float xMouseSensitivity = 3f;
    public float yMouseSensitivity = 3f;
    [Tooltip("Minimum of Y-axis rotation")]
    public float yMinLimit = -80f;
    [Tooltip("Maximum of Y-axis rotation")]
    public float yMaxLimit = 40f;
    [Tooltip("Distance between camera and target. Read-only.")]
    public float distance;
    //[Tooltip("Offset between target and center of screen.")]
    //public Vector2 Offset = new Vector2(0,0);

    #endregion

    #region hide properties    

    float rotationX;
    float rotationY;
    Vector3 currentSmoothVelocity = Vector3.zero;
    Vector3 currentRotation;

    #endregion

    void Start () {
        transform.LookAt(target);
        rotationX = transform.eulerAngles.y;
        rotationY = transform.eulerAngles.x;
        distance = Vector3.Distance(transform.position,target.position);
        currentRotation = new Vector3(rotationY, rotationX, 0);
    }

    private void Update()
    {
    }

    void LateUpdate()
    {

        if (!target)
            return;

        if (Input.GetMouseButton(0))
        {
            rotationX += Input.GetAxis("Mouse X") * xMouseSensitivity;
            rotationY -= Input.GetAxis("Mouse Y") * yMouseSensitivity;
            rotationY = Mathf.Clamp(rotationY, yMinLimit, yMaxLimit);
            currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(rotationY, rotationX, 0), ref currentSmoothVelocity, rotationSmoothTime);
            transform.eulerAngles = currentRotation;
            
        }

        Vector3 newPosition = target.position - transform.forward * distance;
        transform.position = newPosition;

    }
}
