using System.Collections.Generic;
using UnityEngine;

public struct RailingFrameSample
{
    public readonly float s;
    public readonly Vector3 position;
    public readonly Vector3 right;
    public readonly Vector3 up;

    public RailingFrameSample(float s, Vector3 position, Vector3 right, Vector3 up)
    {
        this.s = s;
        this.position = position;
        this.right = right;
        this.up = up;
    }
}

public static class RailingMeshBuilder
{
    public static Mesh BuildMesh(List<RailingFrameSample> frames, float depthMeters, float heightMeters, float flangeThicknessMeters, float taperMeters)
    {
        if (frames == null || frames.Count < 2)
        {
            return null;
        }

        Vector2[] profile = BuildSidewaysWProfile(depthMeters, heightMeters, flangeThicknessMeters);
        int ringCount = frames.Count;
        int vertsPerRing = profile.Length;
        var vertices = new Vector3[ringCount * vertsPerRing];
        var triangles = new int[(ringCount - 1) * vertsPerRing * 6];

        float taper = Mathf.Max(0f, taperMeters);

        for (int i = 0; i < ringCount; i++)
        {
            RailingFrameSample frame = frames[i];
            Vector3 inward = frame.right.normalized;
            Vector3 up = frame.up.normalized;

            float scale = 1f;
            if (taper > 0.0001f)
            {
                float toStart = frame.s - frames[0].s;
                float toEnd = frames[ringCount - 1].s - frame.s;
                float edgeDist = Mathf.Min(toStart, toEnd);
                scale = Mathf.Clamp01(edgeDist / taper);
            }

            int baseIndex = i * vertsPerRing;
            for (int j = 0; j < vertsPerRing; j++)
            {
                Vector2 point = profile[j] * scale;
                vertices[baseIndex + j] = frame.position + inward * point.x + up * point.y;
            }
        }

        int t = 0;
        for (int i = 0; i < ringCount - 1; i++)
        {
            int a = i * vertsPerRing;
            int b = (i + 1) * vertsPerRing;

            for (int j = 0; j < vertsPerRing; j++)
            {
                int jNext = (j + 1) % vertsPerRing;
                t = AddQuad(triangles, t, a + j, a + jNext, b + j, b + jNext);
            }
        }

        Mesh mesh = new Mesh
        {
            name = "RailingMesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector2[] BuildSidewaysWProfile(float depthMeters, float heightMeters, float flangeThicknessMeters)
    {
        float depth = Mathf.Max(0.02f, depthMeters);
        float height = Mathf.Max(0.02f, heightMeters);
        float thickness = Mathf.Clamp(flangeThicknessMeters, 0.005f, height * 0.4f);
        float halfHeight = height * 0.5f;
        float halfThickness = thickness * 0.5f;

        // Base closed W corrugation (before final orientation adjustment).
        Vector2[] profile = new[]
        {
            new Vector2(0.00f * depth, 0.36f * halfHeight + halfThickness),
            new Vector2(0.25f * depth, -0.98f * halfHeight + halfThickness),
            new Vector2(0.50f * depth, 0.82f * halfHeight + halfThickness),
            new Vector2(0.75f * depth, -0.95f * halfHeight + halfThickness),
            new Vector2(1.00f * depth, 0.62f * halfHeight + halfThickness),
            new Vector2(1.00f * depth, 0.62f * halfHeight - halfThickness),
            new Vector2(0.75f * depth, -0.95f * halfHeight - halfThickness),
            new Vector2(0.50f * depth, 0.82f * halfHeight - halfThickness),
            new Vector2(0.25f * depth, -0.98f * halfHeight - halfThickness),
            new Vector2(0.00f * depth, 0.36f * halfHeight - halfThickness)
        };

        // Requested orientation: rotate the corrugation 90 degrees.
        Vector2[] rotated = RotateProfile90Degrees(profile);
        Vector2[] softened = SoftenCorners(rotated, 0.45f);
        return FitProfileToBounds(softened, depth, height);
    }

    private static Vector2[] RotateProfile90Degrees(Vector2[] points)
    {
        Vector2[] rotated = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 p = points[i];
            // 90 deg CCW in profile plane.
            rotated[i] = new Vector2(-p.y, p.x);
        }

        return rotated;
    }

    private static Vector2[] SoftenCorners(Vector2[] points, float amount)
    {
        if (points == null || points.Length < 3)
        {
            return points;
        }

        float t = Mathf.Clamp01(amount);
        Vector2[] softened = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            int prev = (i - 1 + points.Length) % points.Length;
            int next = (i + 1) % points.Length;
            Vector2 mid = (points[prev] + points[next]) * 0.5f;
            softened[i] = Vector2.Lerp(points[i], mid, t);
        }

        return softened;
    }

    private static Vector2[] FitProfileToBounds(Vector2[] points, float targetDepth, float targetHeight)
    {
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 p = points[i];
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        float width = Mathf.Max(0.0001f, maxX - minX);
        float height = Mathf.Max(0.0001f, maxY - minY);
        Vector2[] fitted = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float nx = (points[i].x - minX) / width;
            float ny = (points[i].y - minY) / height;
            fitted[i] = new Vector2(nx * targetDepth, (ny - 0.5f) * targetHeight);
        }

        return fitted;
    }

    private static int AddQuad(int[] triangles, int start, int a0, int a1, int b0, int b1)
    {
        triangles[start + 0] = a0;
        triangles[start + 1] = b0;
        triangles[start + 2] = a1;
        triangles[start + 3] = a1;
        triangles[start + 4] = b0;
        triangles[start + 5] = b1;
        return start + 6;
    }

    public static Mesh BuildRectangularColliderMesh(List<RailingFrameSample> frames, float depthMeters, float heightMeters)
    {
        if (frames == null || frames.Count < 2)
        {
            return null;
        }

        float depth = Mathf.Max(0.01f, depthMeters);
        float halfHeight = Mathf.Max(0.01f, heightMeters * 0.5f);
        Vector2[] profile = new[]
        {
            new Vector2(0f, halfHeight),
            new Vector2(depth, halfHeight),
            new Vector2(depth, -halfHeight),
            new Vector2(0f, -halfHeight)
        };

        int ringCount = frames.Count;
        int vertsPerRing = profile.Length;
        var vertices = new Vector3[ringCount * vertsPerRing];
        var triangles = new int[(ringCount - 1) * vertsPerRing * 6];

        for (int i = 0; i < ringCount; i++)
        {
            RailingFrameSample frame = frames[i];
            Vector3 inward = frame.right.normalized;
            Vector3 up = frame.up.normalized;
            int baseIndex = i * vertsPerRing;
            for (int j = 0; j < vertsPerRing; j++)
            {
                Vector2 point = profile[j];
                vertices[baseIndex + j] = frame.position + inward * point.x + up * point.y;
            }
        }

        int t = 0;
        for (int i = 0; i < ringCount - 1; i++)
        {
            int a = i * vertsPerRing;
            int b = (i + 1) * vertsPerRing;
            for (int j = 0; j < vertsPerRing; j++)
            {
                int jNext = (j + 1) % vertsPerRing;
                t = AddQuad(triangles, t, a + j, a + jNext, b + j, b + jNext);
            }
        }

        Mesh mesh = new Mesh
        {
            name = "RailingColliderMesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
