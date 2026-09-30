using UnityEngine;
using UnityEngine.Rendering.Universal;

// A cockpit mirror: a low-res camera renders into a texture shown on this panel (a quad placed
// over the mirror glass; the MirrorView shader flips it and trims it to a rounded shape).
// Tuned for gameplay first: by default the view is a fixed, stable aim you set with yaw/pitch
// and field of view. "Reflect From Eye" switches to a physically aimed mirror instead.
// The panel's back (-Z) faces the driver. Must be a child of the car.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MirrorView : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int RoundnessId = Shader.PropertyToID("_Roundness");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

    [Header("View")]
    [Tooltip("Vertical field of view in degrees. Wider shows more, like a convex mirror.")]
    [Range(5f, 120f)] [SerializeField] private float fieldOfView = 30f;
    [Tooltip("Aim relative to straight back, degrees. Positive yaw turns the view toward the car's right.")]
    [SerializeField] private float yaw;
    [Tooltip("Aim relative to level, degrees. Positive pitch looks down.")]
    [SerializeField] private float pitch;
    [Tooltip("Where the view is taken from, relative to this panel, in car axes (m). Move it outward/up to clear the bodywork.")]
    [SerializeField] private Vector3 viewOffset;
    [Tooltip("Aim like a real flat mirror from the driver's eye instead of the fixed yaw/pitch aim.")]
    [SerializeField] private bool reflectFromEye;

    [Header("Image")]
    [Tooltip("Render texture height in pixels. Width follows the panel's aspect.")]
    [SerializeField] private int resolutionHeight = 128;
    [Tooltip("Render every Nth frame (1 = every frame).")]
    [Range(1, 6)] [SerializeField] private int renderEveryNthFrame = 2;
    [SerializeField] private float nearClip = 0.1f;
    [SerializeField] private float farClip = 400f;

    [Header("Look")]
    [Tooltip("0 = rectangle, 1 = ellipse.")]
    [Range(0f, 1f)] [SerializeField] private float roundness = 0.3f;
    [Range(0f, 2f)] [SerializeField] private float brightness = 0.9f;

    private Camera mirrorCamera;
    private RenderTexture renderTexture;
    private MaterialPropertyBlock propertyBlock;
    private int framePhase;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            CreateCamera();
        }

        ApplyLook();
    }

    private void OnDisable()
    {
        if (mirrorCamera != null)
        {
            Destroy(mirrorCamera.gameObject);
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }

    private void OnValidate()
    {
        ApplyLook();
    }

    private void LateUpdate()
    {
        if (mirrorCamera == null)
        {
            return;
        }

        bool renderThisFrame = (Time.frameCount + framePhase) % Mathf.Max(1, renderEveryNthFrame) == 0;
        mirrorCamera.enabled = renderThisFrame;
        if (!renderThisFrame)
        {
            return;
        }

        Transform car = transform.parent;
        Vector3 viewPosition = transform.position + car.TransformDirection(viewOffset);
        mirrorCamera.transform.SetPositionAndRotation(viewPosition, GetViewRotation(car));
        mirrorCamera.fieldOfView = fieldOfView;
        mirrorCamera.nearClipPlane = nearClip;
        mirrorCamera.farClipPlane = farClip;
    }

    private Quaternion GetViewRotation(Transform car)
    {
        // The view looks backward, so turning toward the car's right is a negative turn for the camera.
        Quaternion aim = Quaternion.Euler(pitch, -yaw, 0f);
        Camera eye = Camera.main;
        if (!reflectFromEye || eye == null)
        {
            return car.rotation * Quaternion.Euler(0f, 180f, 0f) * aim;
        }

        Vector3 reflected = Vector3.Reflect(transform.position - eye.transform.position, -transform.forward);
        return Quaternion.LookRotation(reflected, car.up) * aim;
    }

    private void CreateCamera()
    {
        float width = Mathf.Abs(transform.lossyScale.x);
        float height = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
        int pixelHeight = Mathf.Max(16, resolutionHeight);
        int pixelWidth = Mathf.Max(16, Mathf.RoundToInt(pixelHeight * width / height));

        renderTexture = new RenderTexture(pixelWidth, pixelHeight, 16) { name = $"{name}_Texture" };
        renderTexture.Create();

        var cameraObject = new GameObject($"{name}_Camera");
        cameraObject.transform.SetParent(transform.parent, false);
        mirrorCamera = cameraObject.AddComponent<Camera>();
        mirrorCamera.targetTexture = renderTexture;
        mirrorCamera.enabled = false;

        UniversalAdditionalCameraData cameraData = mirrorCamera.GetUniversalAdditionalCameraData();
        cameraData.renderShadows = false;
        cameraData.renderPostProcessing = false;

        // Spread mirrors over different frames.
        framePhase = Mathf.Abs(GetInstanceID()) % Mathf.Max(1, renderEveryNthFrame);
    }

    private void ApplyLook()
    {
        MeshRenderer panel = GetComponent<MeshRenderer>();
        if (panel == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        panel.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture(MainTexId, renderTexture != null ? renderTexture : Texture2D.blackTexture);
        propertyBlock.SetFloat(RoundnessId, roundness);
        propertyBlock.SetFloat(BrightnessId, brightness);
        panel.SetPropertyBlock(propertyBlock);
    }
}
