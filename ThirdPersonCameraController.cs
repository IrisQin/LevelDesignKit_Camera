using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCameraController : MonoBehaviour {

    #region inspector properties    

    public Transform target;
    public Renderer targetRender;
    public Transform camera;

    public float rotationSmoothTime = .12f;
    public float xSensitivity = 3f;
    public float ySensitivity = 3f;
    [Tooltip("Minimum of Y-axis rotation")]
    public float yMinLimit = -80f;
    [Tooltip("Maximum of Y-axis rotation")]
    public float yMaxLimit = 40f;
    [System.Serializable]
    public struct axis {
        [Tooltip("If enabled, the positive input will send negative values to the axis, and vice versa.")]
        public bool x;
        public bool y;
    }
    public axis invertAxis;
    [Tooltip("Offset between camera target and screen center.")]
    public Vector2 offset2ScreenCenter;

    [Header("Obstacle Detection")]
    [Tooltip("The Unity layer mask against which the collider will raycast")]
    public LayerMask collideAgainst = 1;
    [Tooltip("Obstacles with this tag will be ignored.  It is a good idea to set this field to the target's tag")]
    public string ignoreTag = string.Empty;
    [Tooltip("Obstacles closer to the target than this will be ignored")]
    public float minDistanceFromTarget = 0.1f;
    [Tooltip("The maximum raycast distance when checking if the line of sight to this camera's target is clear.  If the setting is 0 or less, the current actual distance to target will be used.")]
    public float rayDistanceLimit = 0f;
    [Header("Character Transparency")]
    [Tooltip("When the distance is smaller than this, the character starts to become transparent. Should > maxDistTransparent")]
    public float minDistOpaque = 0.8f;
    [Tooltip("When the distance is smaller than this, the character will become completely transparent. Should < minDistOpaque")]
    public float maxDistTransparent = 0.5f;

    #endregion

    #region hide properties    

    [HideInInspector]
    // Distance between camera and target
    float distance;
    float rotationX;
    float rotationY;
    Vector3 currentSmoothVelocity = Vector3.zero;
    Vector3 currentRotation;
    const float epsilon = 0.0001f;
    // This must be small but greater than 0 - reduces false results due to precision
    const float precisionSlush = 0.001f;
    // In current frame, an occlusion happens or not. This decides if it's necessary to adjust offset according to distance
    bool currentFrameOcclusion;
    float lastDistance;
    Vector2 lastOffset;


    #endregion

    private void Start () {
        transform.LookAt(target);
        rotationX = transform.eulerAngles.y;
        rotationY = transform.eulerAngles.x;
        distance = Vector3.Distance(transform.position,target.position);
        currentRotation = new Vector3(rotationY, rotationX, 0);
    }

    private void OnValidate()
    {
        rotationSmoothTime = Mathf.Max(0, rotationSmoothTime);
        xSensitivity = Mathf.Max(0, xSensitivity);
        ySensitivity = Mathf.Max(0, ySensitivity);
        minDistOpaque = Mathf.Max(minDistOpaque, maxDistTransparent);
        maxDistTransparent = Mathf.Min(minDistOpaque, maxDistTransparent);
    }

    private void Update()
    {
        camera.localPosition = new Vector3(offset2ScreenCenter.x,offset2ScreenCenter.y,0);
        
    }

    private void LateUpdate()
    {
        if (!target||!targetRender)
            return;


        if (!invertAxis.x) rotationX += Input.GetAxis("Mouse X") * xSensitivity;   
        else rotationX += -Input.GetAxis("Mouse X") * xSensitivity;
        if (!invertAxis.y) rotationY -= Input.GetAxis("Mouse Y") * ySensitivity;
        else rotationY -= -Input.GetAxis("Mouse Y") * ySensitivity;

        rotationY = Mathf.Clamp(rotationY, yMinLimit, yMaxLimit);
        currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(rotationY, rotationX, 0), ref currentSmoothVelocity, rotationSmoothTime);
        transform.eulerAngles = currentRotation;
        Vector3 calculatedPosition = target.position - transform.forward * distance;
        Debug.DrawLine(calculatedPosition, target.position, Color.green);
        transform.position = CheckOcclusion(calculatedPosition);

        if (currentFrameOcclusion)
        {
            float currentDistance = Vector3.Distance(transform.position, target.position);
            camera.localPosition = new Vector3(currentDistance/lastDistance * lastOffset.x, currentDistance / lastDistance * lastOffset.y, 0);
        }
        else {
            lastDistance = distance;
            lastOffset = offset2ScreenCenter;
        }
        


        float dist = Vector3.Distance(transform.position, target.position);
        //print(dist);
        if (dist < maxDistTransparent) targetRender.material.color = Color.clear;
        else if (dist > minDistOpaque) targetRender.material.color = Color.white;
        else targetRender.material.color = Color.Lerp(Color.white, Color.clear, (minDistOpaque - dist)/(minDistOpaque - maxDistTransparent));
    }

    private Vector3 CheckOcclusion(Vector3 cameraPos) {
        currentFrameOcclusion = false;
        Vector3 resPos = cameraPos;
        Vector3 dir = cameraPos - target.position;
        float targetDistance = dir.magnitude;
        float _minDistanceFromTarget = Mathf.Max(minDistanceFromTarget, epsilon);
        if (targetDistance > _minDistanceFromTarget)
        {
            dir.Normalize();
            float rayLength = targetDistance - _minDistanceFromTarget;
            if (rayDistanceLimit > epsilon)
                rayLength = Mathf.Min(rayDistanceLimit, rayLength);

            // Make a ray that looks towards the camera, to get the most distant obstruction
            Ray ray = new Ray(cameraPos - rayLength * dir, dir);
            rayLength += precisionSlush;
            if (rayLength > epsilon)
            {
                RaycastHit hitInfo;

                if (RaycastIgnoreTag(ray, out hitInfo, rayLength))
                {
                    
                    // Pull camera forward in front of obstacle
                    float adjustment = Mathf.Max(0, hitInfo.distance - precisionSlush);
                    resPos = ray.GetPoint(adjustment);
                    Debug.DrawLine(cameraPos - rayLength * dir, resPos, Color.red);
                    currentFrameOcclusion = true;
                }
            }
        }   
        return resPos;
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
}
