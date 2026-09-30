using UnityEngine;

[RequireComponent(typeof(RoadStreamGenerator))]
public class RoadDebugInfoProvider : MonoBehaviour, IDebugInfoProvider
{
    public int Priority => 200;
    public string DisplayName => "Road";
    public bool IsVisible => true;

    private RoadStreamGenerator road;

    private void Awake()
    {
        road = GetComponent<RoadStreamGenerator>();
    }

    public void BuildDebugInfo(DebugPanelBuilder builder)
    {
        builder.BeginSection("Road");
        builder.AddInt("Seed", road.GetSeed());
    }
}
