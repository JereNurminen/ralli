using System.Collections.Generic;
using UnityEngine;

// Placeholder score display: score and multiplier, plus a short feed of recent tricks and hits.
// Plain IMGUI text until the real HUD is designed.
[RequireComponent(typeof(ScoreSystem))]
public class ScoreHud : MonoBehaviour
{
    private struct FeedLine
    {
        public string text;
        public Color color;
        public float time;
    }

    [SerializeField] private int fontSize = 28;
    [SerializeField] private float feedLifetime = 2.5f;
    [SerializeField] private int maxFeedLines = 5;

    private ScoreSystem score;
    private readonly List<FeedLine> feed = new List<FeedLine>();
    private GUIStyle bigStyle;
    private GUIStyle feedStyle;

    private void Start()
    {
        score = GetComponent<ScoreSystem>();
        score.TrickScored += (trick, points) => AddLine($"{trick}  +{points:0}", new Color(1f, 0.9f, 0.3f));
        score.MultiplierLost += (label, lost) => AddLine($"{label}  x-{lost:0.0}", new Color(1f, 0.35f, 0.3f));
    }

    private void AddLine(string text, Color color)
    {
        feed.Insert(0, new FeedLine { text = text, color = color, time = Time.time });
        if (feed.Count > maxFeedLines)
        {
            feed.RemoveAt(feed.Count - 1);
        }
    }

    private void OnGUI()
    {
        if (score == null)
        {
            return;
        }

        bigStyle ??= new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
        feedStyle ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(fontSize * 0.7f), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };

        float width = 500f;
        float x = Screen.width - width - 20f;
        float y = 20f;
        DrawShadowed(new Rect(x, y, width, fontSize + 10), $"{score.Score:N0}", Color.white, bigStyle);
        y += fontSize + 8;
        DrawShadowed(new Rect(x, y, width, fontSize + 10), $"x{score.Multiplier:0.00}", new Color(0.6f, 0.9f, 1f), bigStyle);
        y += fontSize + 16;

        for (int i = 0; i < feed.Count; i++)
        {
            float age = Time.time - feed[i].time;
            if (age > feedLifetime)
            {
                continue;
            }

            Color color = feed[i].color;
            color.a = 1f - Mathf.Clamp01((age - feedLifetime * 0.6f) / (feedLifetime * 0.4f));
            DrawShadowed(new Rect(x, y, width, fontSize), feed[i].text, color, feedStyle);
            y += feedStyle.fontSize + 6;
        }
    }

    private static void DrawShadowed(Rect rect, string text, Color color, GUIStyle style)
    {
        style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }
}
