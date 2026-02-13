using System;
using System.Collections.Generic;
using UnityEngine;

public class DebugOverlay : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
    [SerializeField] private bool startVisible = true;

    [Header("Refresh")]
    [SerializeField] private float refreshRateHz = 10f;
    [SerializeField] private float providerRescanInterval = 1f;

    [Header("Layout")]
    [SerializeField] private float margin = 12f;
    [SerializeField] private float panelWidth = 520f;
    [SerializeField] private int fontSize = 14;

    private readonly List<IDebugInfoProvider> providers = new List<IDebugInfoProvider>();
    private readonly DebugPanelBuilder builder = new DebugPanelBuilder();

    private bool isVisible;
    private float nextRefreshTime;
    private float nextRescanTime;
    private string cachedText = "Waiting for debug providers...";
    private Vector2 scroll;

    private GUIStyle boxStyle;
    private GUIStyle textStyle;

    private void Awake()
    {
        isVisible = startVisible;
        ScanProviders();
        RebuildText();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isVisible = !isVisible;
        }

        if (Time.unscaledTime >= nextRescanTime)
        {
            ScanProviders();
            nextRescanTime = Time.unscaledTime + Mathf.Max(0.1f, providerRescanInterval);
        }

        if (Time.unscaledTime >= nextRefreshTime)
        {
            RebuildText();
            float refreshInterval = 1f / Mathf.Max(1f, refreshRateHz);
            nextRefreshTime = Time.unscaledTime + refreshInterval;
        }
    }

    private void OnGUI()
    {
        if (!isVisible)
        {
            return;
        }

        EnsureStyles();

        float maxHeight = Screen.height - margin * 2f;
        Rect area = new Rect(margin, margin, panelWidth, maxHeight);

        GUILayout.BeginArea(area, boxStyle);
        GUILayout.Label($"Debug Overlay ({toggleKey})", textStyle);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(cachedText, textStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void EnsureStyles()
    {
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(12, 12, 10, 10),
                alignment = TextAnchor.UpperLeft
            };
        }

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = false,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
        }
    }

    private void ScanProviders()
    {
        providers.Clear();

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDebugInfoProvider provider)
            {
                providers.Add(provider);
            }
        }

        providers.Sort((a, b) =>
        {
            int priorityCompare = a.Priority.CompareTo(b.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        });
    }

    private void RebuildText()
    {
        builder.Clear();

        bool wroteAny = false;
        for (int i = 0; i < providers.Count; i++)
        {
            IDebugInfoProvider provider = providers[i];
            if (!provider.IsVisible)
            {
                continue;
            }

            if (provider is Behaviour behaviour && !behaviour.isActiveAndEnabled)
            {
                continue;
            }

            try
            {
                provider.BuildDebugInfo(builder);
                wroteAny = true;
            }
            catch (Exception ex)
            {
                builder.BeginSection($"Provider Error: {provider.DisplayName}");
                builder.AddValue("Exception", ex.Message);
                wroteAny = true;
            }
        }

        if (!wroteAny)
        {
            builder.BeginSection("Debug");
            builder.AddValue("Status", "No active debug providers");
            builder.AddValue("Hint", "Implement IDebugInfoProvider on a MonoBehaviour");
        }

        cachedText = builder.ToString();
    }
}
