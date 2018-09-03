﻿/*
 * UCSC Level Design Toolkit
 * 
 * Iris Qin
 * qtcdyx616@gmail.com
 * 8/17/2018
 * 
 * Released under MIT Open Source License
 * 
 * This script controls the third-person perspective camera in the game which can automatically following the character and can be rotated freely by using the joystick or mouse.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AlwaysLookAt))]

public class ThirdPersonCameraController : MonoBehaviour {

    #region inspector properties    

    [Header("Target")]
    [Tooltip("The target pivot that this camera follows and looks at. Suggest to be a child gameobject of the character and its position is at the head of the character. Name it CameraPivot.")]
    public Transform targetPivot;
    [Tooltip("The renderer that the target character uses. Drag here the object which owns the character’s renderer.")]
    public Renderer targetRender;
    [Tooltip("Offset between target and screen center. If no demand, keep it (0,0) to place the target at the screen center.")]
    public Vector2 offsetToScreenCenter;

    [Header("Camera Input")]
    [Tooltip("The camera under this manager. When you drag it here, any other cameras in the scene will be disabled.")]
    public Camera myCamera;
    [Tooltip("X-axis input sensitivity. Greatly influence rotation speed. Proposed value: 2.50.")]
    public float xSensitivity = 2.5f;
    [Tooltip("Y-axis input sensitivity. Greatly influence rotation speed. Proposed value: 2.50.")]
    public float ySensitivity = 2.5f;
    [Tooltip("Fine-tune the smooth time of rotation. A smaller value will rotate faster.")]
    [Range(0.0f, 0.2f)]
    public float rotationSmoothTime = .12f;
    [Tooltip("Minimum angle of Y-axis rotation. Determines how low the character's bottom can be seen. Proposed range: -100 ~ 0.")]
    public float yMinLimit = -80f;
    [Tooltip("Maximum angle of Y-axis rotation. Determines how top the character's head can be seen. Proposed range: 0 ~ 100.")]
    public float yMaxLimit = 40f;
    [System.Serializable]
    public struct axis {
        [Tooltip("If enabled, the positive input will send negative values to the axis, and vice versa.")]
        public bool x;
        public bool y;
    }
    public axis invertAxis;
    

    [Header("Occlusion Obstacle Detection")]
    [Tooltip("The Unity layer mask against which the collider will raycast. Propose to include: default, ground.")]
    public LayerMask collideAgainst = 0;
    [Tooltip("Obstacles with this tag will be ignored.")]
    public string ignoreTag = string.Empty;
    [Tooltip("The maximum raycast distance when checking if the line of sight to this camera's target is clear. If the setting is 0 or less, the current actual distance to target will be used.")]
    public float rayDistanceLimit = 0f;
    [Tooltip("Obstacles closer to the target than this distance will be ignored. Propose to keep it 0, because we want the camera to be in front of a wall even when the gap between is 0.")]
    public float minDistanceFromTarget = 0f;
    [Tooltip("If you want to disable automatic rotation around edge of obstacle.")]
    public bool disableAutoRotation = false;

    [Header("Character Transparency")]
    [Tooltip("When the distance is smaller than this, the character starts to become transparent. Should > maxDistTransparent. Proposed value: 0.80.")]
    public float minDistOpaque = 0.8f;
    [Tooltip("When the distance is smaller than this, the character will become completely transparent. Should < minDistOpaque. Proposed value: 0.50.")]
    public float maxDistTransparent = 0.5f;

    #endregion

    #region hide properties    

    // Distance between camera and target
    float distance;
    float rotationX;
    float rotationY;
    Vector3 currentSmoothVelocity = Vector3.zero;
    Vector3 currentPositionVelocity = Vector3.zero;
    Vector3 currentRotation;
    const float epsilon = 0.0001f;
    // This must be small but greater than 0 - reduces false results due to precision
    const float precisionSlush = 0.001f;
    float lastDistance;
    Vector2 lastOffset;
    float positionSmoothTime = .03f;
    bool currentFrameOcclusion;
    float occlusionRotationMod = 0.35f;
    bool rotatePos = false;
    bool rotateNeg = false;
    float rotationTimmer = 0.0f;
    // If you want to ignore input that controls the camera's rotation, set this from other scripts.
    [HideInInspector]
    public static bool ignoreInput = false;

    #endregion

    private void Awake()
    {
        // should execute before GameManagerScript spawnPlayer and any other functions that moves character
        distance = Vector3.Distance(transform.position, targetPivot.position);
    }

    private void Start () {
        if (targetRender == null) {
            targetRender = GameManagerScript.instance.getPlayer().GetComponentInChildren<SkinnedMeshRenderer>();
        }
        if (targetPivot == null)
        {
            targetPivot = GameManagerScript.instance.getPlayer().transform.Find("CameraPivot");
        }
        transform.LookAt(targetPivot);
        rotationX = transform.eulerAngles.y;
        rotationY = transform.eulerAngles.x;
        currentRotation = new Vector3(rotationY, rotationX, 0);
    }

    private void OnValidate()
    {
        // check whether the camera is correct when it's set and disable all other cameras in the scene.
        if (myCamera) {
            if (myCamera.transform.parent != transform) {
                print("The camera should be under this manager!");
                myCamera = null;
            }
            else {
				myCamera.transform.localPosition = new Vector3(offsetToScreenCenter.x, offsetToScreenCenter.y, 0);
                foreach (Camera cam in Camera.allCameras)
                    if (cam.name != myCamera.name)
                        cam.gameObject.SetActive(false);
            }
        }

        xSensitivity = Mathf.Max(0, xSensitivity);
        ySensitivity = Mathf.Max(0, ySensitivity);
        minDistOpaque = Mathf.Max(minDistOpaque, maxDistTransparent);
        maxDistTransparent = Mathf.Min(minDistOpaque, maxDistTransparent);
    }

    private void LateUpdate()
    {
        if (!targetPivot || !targetRender || !myCamera) {
            print("Camera manager can't find targetPivot/targetRender/myCamera !");
            return;
        } 

        if (!ignoreInput)
        {
            if (!invertAxis.x)
            {
                rotationX += Input.GetAxis("Mouse X") * xSensitivity;
            }
            else
            {
                rotationX += -Input.GetAxis("Mouse X") * xSensitivity;
            }

            if (!invertAxis.y)
            {
                rotationY -= Input.GetAxis("Mouse Y") * ySensitivity;
            }
            else
            {
                rotationY -= -Input.GetAxis("Mouse Y") * ySensitivity;
            }
            rotationY = Mathf.Clamp(rotationY, yMinLimit, yMaxLimit);
            currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(rotationY, rotationX, 0), ref currentSmoothVelocity, rotationSmoothTime);
            transform.eulerAngles = currentRotation;
        }

        // the position where the Camera Manager should be if there is no occlusion
        Vector3 calculatedPosition = targetPivot.position - transform.forward * distance;
        Debug.DrawLine(calculatedPosition, targetPivot.position, Color.green);
        // the position where the Camera should be if there is no occlusion
        Vector3 calculatedCamPos = calculatedPosition + transform.right* myCamera.transform.localPosition.x + transform.up* myCamera.transform.localPosition.y;
        Vector3 cameraPosition = CheckOcclusion(calculatedCamPos);

        // if no occlusion happens
        if (!currentFrameOcclusion) {
            transform.position = calculatedPosition;
        }
        // if there is occlusion, move not the Camera but the Camera Manager
        else
        {
            // calculate where the Camera Manager should be according to Camera's position
            Vector3 shortLine = cameraPosition - targetPivot.position;
            Vector3 longLine = calculatedPosition + myCamera.transform.localPosition - targetPivot.position;
            float displacement = shortLine.magnitude / longLine.magnitude * distance;
            Vector3 targetPosition = targetPivot.position - transform.forward * displacement;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentPositionVelocity, positionSmoothTime);
        }

        if (!disableAutoRotation) {
            OcclusionRotation();
        }

        // preserve the status before an occlusion, and adjust offset proportionally, in case the target stays outside the camera view
        if (currentFrameOcclusion)
        {
            float currentDistance = Vector3.Distance(transform.position, targetPivot.position);
            if (lastDistance > epsilon) {
                myCamera.transform.localPosition = new Vector3(currentDistance / lastDistance * lastOffset.x * 0.5f, currentDistance / lastDistance * lastOffset.y * 0.5f, 0);
            }
        }
        else {
            lastDistance = distance;
            lastOffset = offsetToScreenCenter;
        }

        // adjust target's transparency when the camera is very close to it
        float dist = Vector3.Distance(transform.position, targetPivot.position);
        if (dist < maxDistTransparent) targetRender.material.color = Color.clear;
        else if (dist > minDistOpaque) targetRender.material.color = Color.white;
        else targetRender.material.color = Color.Lerp(Color.white, Color.clear, (minDistOpaque - dist)/(minDistOpaque - maxDistTransparent));
    }

    /*
     * Requires: calculatedCamPos(Vector3)
     * Modifies: currentFrameOcclusion(bool), rotationTimmer(float), rotateNeg(bool), rotatePos(bool)
     * Returns: the position where the Camera(not Camera Manager) should be after checking occlusion
     */ 
    private Vector3 CheckOcclusion(Vector3 calculatedCamPos)
    {
        currentFrameOcclusion = false;
        Vector3 targetPos = targetPivot.position;
        Vector3 resPos = calculatedCamPos;
        Vector3 dir = calculatedCamPos - targetPos;
        float targetDistance = dir.magnitude;
        float _minDistanceFromTarget = Mathf.Max(minDistanceFromTarget, epsilon);
        if (targetDistance > _minDistanceFromTarget)
        {
            dir.Normalize();
            float rayLength = targetDistance - _minDistanceFromTarget;
            if (rayDistanceLimit > epsilon)
                rayLength = Mathf.Min(rayDistanceLimit, rayLength);
            // Make a ray that looks towards the camera, to get the most distant obstruction
            Ray ray = new Ray(calculatedCamPos - rayLength * dir, dir);
            Debug.DrawLine(calculatedCamPos - rayLength * dir, calculatedCamPos, Color.blue);
            rayLength += precisionSlush;
            float displacement = rayLength;
            if (rayLength > epsilon)
            {
                RaycastHit hitInfo;
                if (RaycastIgnoreTag(ray, out hitInfo, rayLength))
                {
                    /*
                    * If the player is currently giving no input, check to see if the camera can rotate a little bit. If
                    * the camera can rotate, then rotate it. If the camera cannot rotate or the player is giving input, 
                    * then do the regular occlusion. (Shouldn't do rotation with ground layer)
                    */
                    if (!disableAutoRotation) {
                        Vector3 negativeOffset = calculatedCamPos - myCamera.transform.right;
                        Vector3 positiveOffset = calculatedCamPos + myCamera.transform.right;
                        Vector3 negativeDir = negativeOffset - targetPivot.position;
                        Vector3 positiveDir = positiveOffset - targetPivot.position;
                        negativeDir.Normalize();
                        positiveDir.Normalize();
                        Ray negative = new Ray(negativeOffset - rayLength * negativeDir, negativeDir);
                        Ray positive = new Ray(positiveOffset - rayLength * positiveDir, positiveDir);
                        RaycastHit hitInfo2;
                        int colliderLayer = hitInfo.collider.gameObject.layer;
                        // if it's not in Ground or EnemyArea layer
                        if (colliderLayer != 9 && colliderLayer != 10 && Input.GetAxis("Mouse X") == 0) {
                            if (!RaycastIgnoreTag(negative, out hitInfo2, rayLength))
                            {
                                rotationTimmer = 0.0f;
                                rotateNeg = true;
                            }
                            else if (!RaycastIgnoreTag(positive, out hitInfo2, rayLength))
                            {
                                rotationTimmer = 0.0f;
                                rotatePos = true;
                            }
                        }
                    }

                    // if there is no auto rotation, pull camera forward in front of obstacle
                    if (!rotateNeg || !rotatePos)
                    {
                        currentFrameOcclusion = true;
                        displacement = Mathf.Max(0, hitInfo.distance - precisionSlush);
                        resPos = ray.GetPoint(displacement);
                    }
                }
            }

            // use OverlapSphere to protect camera from being too close to anything
            float radius = 1f;
            Collider[] cols = Physics.OverlapSphere(resPos, radius, collideAgainst.value);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].tag != ignoreTag)
                {
                    // pull camera forward a little bit
                    displacement -= radius;
                    currentFrameOcclusion = true;
                    break;
                }
            }

            resPos = ray.GetPoint(displacement);
            if (displacement != rayLength) {
                Debug.DrawLine(calculatedCamPos - rayLength * dir, resPos, Color.red);
            }
        }
        return resPos;
    }


    /*
     * Requires: Nothing
     * Modifies: totationTimmer(float), rotationX(float), rotateNeg(bool), and rotatePos(bool)
     * Returns: Nothing
     * 
     * This is meant to cause the camera to rotate for a set amount of time.
     */ 
    private void OcclusionRotation()
    {
        float maxTime = 0.3f; //0.3f;
        rotationTimmer += Time.deltaTime;

        if (rotateNeg && rotationTimmer <= maxTime)
        {
            rotationX -= xSensitivity * occlusionRotationMod;
        }
        else if (rotatePos && rotationTimmer <= maxTime)
        {
            rotationX += xSensitivity * occlusionRotationMod;
        }

        if(rotationTimmer > maxTime)
        {
            rotateNeg = false;
            rotatePos = false;
        }
    }

    private bool RaycastIgnoreTag(Ray ray, out RaycastHit hitInfo, float rayLength)
    {
        while (Physics.Raycast(
            ray, out hitInfo, rayLength, collideAgainst.value,
            QueryTriggerInteraction.Ignore))
        {
            if (ignoreTag.Length == 0 || !hitInfo.collider.CompareTag(ignoreTag))
                return true;

            // Pull ray origin forward in front of tagged obstacle
            Ray inverseRay = new Ray(ray.GetPoint(rayLength), -ray.direction);
            if (!hitInfo.collider.Raycast(inverseRay, out hitInfo, rayLength))
                break; // should never happen
            rayLength = hitInfo.distance - precisionSlush;
            if (rayLength < epsilon)
                break;
            ray.origin = inverseRay.GetPoint(rayLength);
        }
        return false;
    }

    public void setCameraTarget(GameObject cameraTarget)
    {
        targetPivot = cameraTarget.transform;
    }

    public Camera getCamera()
    {
        return myCamera;
    }
}
