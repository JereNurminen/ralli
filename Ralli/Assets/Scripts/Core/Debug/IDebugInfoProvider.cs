public interface IDebugInfoProvider
{
    int Priority { get; }
    string DisplayName { get; }
    bool IsVisible { get; }

    void BuildDebugInfo(DebugPanelBuilder builder);
}
