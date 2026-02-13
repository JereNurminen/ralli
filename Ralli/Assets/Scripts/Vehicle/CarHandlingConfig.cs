using UnityEngine;

[CreateAssetMenu(menuName = "Ralli/Vehicle/Car Handling Config", fileName = "CarHandlingConfig")]
public class CarHandlingConfig : ScriptableObject
{
    [Header("Steering")]
    [Range(0f, 45f)] public float maxSteerAngle = 28f;
    public float steerResponse = 8f;

    [Header("Suspension")]
    public float wheelRadius = 0.35f;
    public float suspensionRestLength = 0.5f;
    public float springStrength = 32000f;
    public float damperStrength = 3800f;

    [Header("Tire Grip")]
    public float tireFriction = 1.2f;
    public float lateralGrip = 11f;

    [Header("Drivetrain")]
    [Range(0f, 1f)] public float rearDriveBias = 1f;
    public float maxDriveForce = 10000f;
    public float maxBrakeForce = 14000f;
    public float handbrakeForce = 17000f;

    [Header("Stability")]
    public float centerOfMassYOffset = -0.6f;
    public float yawDamping = 2.5f;
    public float antiRollStiffness = 12000f;
    public float rollDamping = 1.5f;

    [Header("Steering Assist")]
    public float topSpeedForSteerReduction = 35f;
    [Range(0.1f, 1f)] public float minSteerFactorAtTopSpeed = 0.35f;
}
