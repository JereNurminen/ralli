using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RailingMeshTests
{
    [Test]
    public void BuildMesh_ForValidSpan_CreatesRenderableMesh()
    {
        List<RailingFrameSample> frames = CreateFrames();

        Mesh mesh = RailingMeshBuilder.BuildMesh(frames, 0.24f, 0.28f, 0.055f, 0.5f);

        Assert.NotNull(mesh);
        Assert.Greater(mesh.vertexCount, 0);
        Assert.Greater(mesh.triangles.Length, 0);
    }

    [Test]
    public void BuildMesh_EndTaper_ReducesProfileWidthNearEnds()
    {
        List<RailingFrameSample> frames = CreateFrames();

        Mesh mesh = RailingMeshBuilder.BuildMesh(frames, 0.24f, 0.28f, 0.055f, 2f);

        Vector3[] vertices = mesh.vertices;
        float startWidth = Vector3.Distance(vertices[0], vertices[1]);
        float midWidth = Vector3.Distance(vertices[24], vertices[25]);

        Assert.Less(startWidth, midWidth);
    }

    [Test]
    public void BuildMesh_UsesCorrugatedWProfile()
    {
        List<RailingFrameSample> frames = CreateFrames();
        Mesh mesh = RailingMeshBuilder.BuildMesh(frames, 0.24f, 0.28f, 0.055f, 0f);

        Assert.NotNull(mesh);
        Assert.AreEqual(frames.Count * 10, mesh.vertexCount);
    }

    [Test]
    public void BuildRectangularColliderMesh_UsesFourPointCrossSection()
    {
        List<RailingFrameSample> frames = CreateFrames();
        Mesh mesh = RailingMeshBuilder.BuildRectangularColliderMesh(frames, 0.24f, 0.28f);

        Assert.NotNull(mesh);
        Assert.AreEqual(frames.Count * 4, mesh.vertexCount);
        Assert.Greater(mesh.triangles.Length, 0);
    }

    private static List<RailingFrameSample> CreateFrames()
    {
        return new List<RailingFrameSample>
        {
            new RailingFrameSample(0f, new Vector3(0f, 0f, 0f), Vector3.right, Vector3.up),
            new RailingFrameSample(1f, new Vector3(0f, 0f, 1f), Vector3.right, Vector3.up),
            new RailingFrameSample(2f, new Vector3(0f, 0f, 2f), Vector3.right, Vector3.up),
            new RailingFrameSample(3f, new Vector3(0f, 0f, 3f), Vector3.right, Vector3.up),
            new RailingFrameSample(4f, new Vector3(0f, 0f, 4f), Vector3.right, Vector3.up)
        };
    }
}
