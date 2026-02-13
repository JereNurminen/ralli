using UnityEngine;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(CarInputReader))]
[RequireComponent(typeof(Rigidbody))]
public class VehicleDebugInfoProvider : MonoBehaviour, IDebugInfoProvider
{
    public int Priority => 100;
    public string DisplayName => "Vehicle";
    public bool IsVisible => true;

    private CarController carController;
    private CarInputReader carInput;
    private Rigidbody rb;

    private void Awake()
    {
        carController = GetComponent<CarController>();
        carInput = GetComponent<CarInputReader>();
        rb = GetComponent<Rigidbody>();
    }

    public void BuildDebugInfo(DebugPanelBuilder builder)
    {
        builder.BeginSection("Vehicle");
        builder.AddFloat("Speed (m/s)", carController.SpeedMps);
        builder.AddFloat("Speed (km/h)", carController.SpeedMps * 3.6f);
        builder.AddFloat("Steer Angle (deg)", carController.SteerAngleDegrees);
        builder.AddFloat("Steer Factor", carController.CurrentSteerFactor);
        builder.AddInt("Grounded Wheels", carController.GroundedWheelCount);
        builder.AddBool("Grounded", carController.IsGrounded);

        builder.AddFloat("Input Throttle", carInput.Throttle);
        builder.AddFloat("Input Brake", carInput.Brake);
        builder.AddFloat("Input Steer", carInput.Steer);
        builder.AddBool("Input Handbrake", carInput.Handbrake);

        builder.AddVector3("Velocity", rb.linearVelocity);
        builder.AddVector3("Angular Velocity", rb.angularVelocity);
    }
}
