using System.Globalization;
using System.Text;
using UnityEngine;

public class DebugPanelBuilder
{
    private readonly StringBuilder stringBuilder = new StringBuilder(2048);

    public void Clear()
    {
        stringBuilder.Clear();
    }

    public void BeginSection(string sectionName)
    {
        if (stringBuilder.Length > 0)
        {
            stringBuilder.AppendLine();
        }

        stringBuilder.Append('[').Append(sectionName).AppendLine("]");
    }

    public void AddValue(string label, string value)
    {
        stringBuilder.Append(label).Append(": ").AppendLine(value);
    }

    public void AddFloat(string label, float value, string format = "F2")
    {
        AddValue(label, value.ToString(format, CultureInfo.InvariantCulture));
    }

    public void AddInt(string label, int value)
    {
        AddValue(label, value.ToString(CultureInfo.InvariantCulture));
    }

    public void AddBool(string label, bool value)
    {
        AddValue(label, value ? "Yes" : "No");
    }

    public void AddVector3(string label, Vector3 value, string format = "F2")
    {
        AddValue(
            label,
            string.Format(
                CultureInfo.InvariantCulture,
                "({0}, {1}, {2})",
                value.x.ToString(format, CultureInfo.InvariantCulture),
                value.y.ToString(format, CultureInfo.InvariantCulture),
                value.z.ToString(format, CultureInfo.InvariantCulture)
            )
        );
    }

    public override string ToString()
    {
        return stringBuilder.ToString();
    }
}
