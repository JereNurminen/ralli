using UnityEngine;

[RequireComponent(typeof(CarController))]
[RequireComponent(typeof(CarInputReader))]
public class VehicleDebugInfoProvider : MonoBehaviour, IDebugInfoProvider
{
    public int Priority => 100;
    public string DisplayName => "Vehicle";
    public bool IsVisible => true;

    private CarController carController;
    private CarInputReader carInput;

    private void Awake()
    {
        carController = GetComponent<CarController>();
        carInput = GetComponent<CarInputReader>();
    }

    public void BuildDebugInfo(DebugPanelBuilder builder)
    {
        builder.BeginSection("Vehicle");
        builder.AddFloat("Speed (km/h)", carController.SpeedMps * 3.6f);
        builder.AddBool("Grounded", carController.IsGrounded);
        builder.AddBool("In Reverse", carController.InReverse);
        builder.AddFloat("Steer Angle (deg)", carController.SteerAngleDegrees);
        builder.AddFloat("Drift Angle (deg)", carController.DriftAngle);
        builder.AddFloat("Overdrive", carController.OverdriveFactor);
        builder.AddFloat("Engine Heat", carController.Heat01);
        builder.AddFloat("Front Grip", carController.FrontGrip01);
        builder.AddFloat("Rear Grip", carController.RearGrip01);
        builder.AddFloat("Front Grip Usage", carController.FrontGripUsage01);
        builder.AddFloat("Rear Grip Usage", carController.RearGripUsage01);
        builder.AddFloat("Input Throttle", carInput.Throttle);
        builder.AddFloat("Input Brake", carInput.Brake);
        builder.AddFloat("Input Steer", carInput.Steer);
        builder.AddBool("Input Handbrake", carInput.Handbrake);
        builder.AddBool("Input Overdrive", carInput.Overdrive);
    }
}
