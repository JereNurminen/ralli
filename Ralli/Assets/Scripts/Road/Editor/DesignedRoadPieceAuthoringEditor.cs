using UnityEngine;
using UnityEditor;
using UnityEngine.Splines;

[CustomEditor(typeof(DesignedRoadPieceAuthoring))]
public class DesignedRoadPieceAuthoringEditor : Editor
{
    private Mesh previewMesh;
    private GameObject previewObject;
    private int lastSplineVersion = -1;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var authoring = (DesignedRoadPieceAuthoring)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh Metadata", GUILayout.Height(28)))
        {
            authoring.UpdateComputedMetadata();
            EditorUtility.SetDirty(authoring);
            UpdatePreviewMesh(authoring);
        }

        EditorGUI.BeginDisabledGroup(authoring.targetPiece == null);
        if (GUILayout.Button("Bake to ScriptableObject", GUILayout.Height(36)))
        {
            if (authoring.targetPiece == null)
            {
                EditorUtility.DisplayDialog("Bake Error", "No target DesignedRoadPiece assigned.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog(
                "Bake Designed Road Piece",
                $"This will overwrite data in '{authoring.targetPiece.name}'. Continue?",
                "Bake", "Cancel"))
            {
                BakeToAsset(authoring);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Workflow:\n" +
            "1. Draw a spline using Unity's spline tools\n" +
            "2. Assign a RoadGenerationConfig for preview\n" +
            "3. Create or assign a DesignedRoadPiece asset\n" +
            "4. Click 'Refresh Metadata' to preview\n" +
            "5. Click 'Bake' to write curves to asset",
            MessageType.Info);
    }

    private void OnEnable()
    {
        var authoring = (DesignedRoadPieceAuthoring)target;

        // Clean up any orphaned preview objects from previous sessions
        var orphan = authoring.transform.Find("__RoadPiecePreview__");
        if (orphan != null)
            DestroyImmediate(orphan.gameObject);

        if (authoring.showPreview)
            UpdatePreviewMesh(authoring);
    }

    private void OnDisable()
    {
        CleanupPreview();
    }

    private void OnSceneGUI()
    {
        var authoring = (DesignedRoadPieceAuthoring)target;
        if (!authoring.showPreview || authoring.roadConfig == null)
            return;

        // Check if spline changed by comparing version
        if (authoring.splineContainer != null && authoring.splineContainer.Spline != null)
        {
            int version = authoring.splineContainer.Spline.GetHashCode();
            if (version != lastSplineVersion)
            {
                lastSplineVersion = version;
                UpdatePreviewMesh(authoring);
            }
        }
    }

    private void BakeToAsset(DesignedRoadPieceAuthoring authoring)
    {
        var result = authoring.SampleSpline();
        if (result.arcLength < 0.01f)
        {
            Debug.LogError("[DesignedRoadPiece] Spline is too short to bake.");
            return;
        }

        Undo.RecordObject(authoring.targetPiece, "Bake Designed Road Piece");

        authoring.targetPiece.arcLength = result.arcLength;
        authoring.targetPiece.totalYawDeltaDeg = result.totalYawDeltaDeg;
        authoring.targetPiece.entryTurnRate = result.entryTurnRate;
        authoring.targetPiece.exitTurnRate = result.exitTurnRate;
        authoring.targetPiece.turnRateCurve = result.turnRateCurve;
        authoring.targetPiece.elevationCurve = result.elevationCurve;
        authoring.targetPiece.hasElevation = result.hasElevation;

        EditorUtility.SetDirty(authoring.targetPiece);
        AssetDatabase.SaveAssets();

        Debug.Log($"[DesignedRoadPiece] Baked '{authoring.targetPiece.displayName}': " +
                  $"{result.arcLength:F1}m, {result.totalYawDeltaDeg:F1}deg yaw, " +
                  $"entry={result.entryTurnRate:F3}deg/m, exit={result.exitTurnRate:F3}deg/m" +
                  (result.hasElevation ? ", has elevation" : ""));
    }

    private void UpdatePreviewMesh(DesignedRoadPieceAuthoring authoring)
    {
        if (authoring.splineContainer == null || authoring.splineContainer.Spline == null)
        {
            CleanupPreview();
            return;
        }

        if (authoring.roadConfig == null)
        {
            CleanupPreview();
            return;
        }

        var result = authoring.SampleSpline();
        if (result.samplePositions == null || result.samplePositions.Length < 2)
        {
            CleanupPreview();
            return;
        }

        Mesh mesh = BuildPreviewMesh(authoring, result);
        if (mesh == null)
        {
            CleanupPreview();
            return;
        }

        EnsurePreviewObject(authoring);
        if (previewMesh != null)
            DestroyImmediate(previewMesh);
        previewMesh = mesh;

        var mf = previewObject.GetComponent<MeshFilter>();
        mf.sharedMesh = previewMesh;
    }

    private void EnsurePreviewObject(DesignedRoadPieceAuthoring authoring)
    {
        if (previewObject != null)
            return;

        previewObject = new GameObject("__RoadPiecePreview__");
        previewObject.hideFlags = HideFlags.HideAndDontSave;
        previewObject.transform.SetParent(authoring.transform, false);
        previewObject.transform.localPosition = Vector3.zero;
        previewObject.transform.localRotation = Quaternion.identity;
        previewObject.transform.localScale = Vector3.one;

        var mf = previewObject.AddComponent<MeshFilter>();
        var mr = previewObject.AddComponent<MeshRenderer>();

        // Try to use a default material
        mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mr.sharedMaterial.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    }

    private void CleanupPreview()
    {
        if (previewObject != null)
            DestroyImmediate(previewObject);
        if (previewMesh != null)
            DestroyImmediate(previewMesh);
        previewObject = null;
        previewMesh = null;
    }

    private Mesh BuildPreviewMesh(DesignedRoadPieceAuthoring authoring, DesignedRoadPieceAuthoring.BakeResult result)
    {
        var cfg = authoring.roadConfig;
        float halfWidth = cfg.roadWidth * 0.5f;
        int sampleCount = result.samplePositions.Length;

        // Simple flat road preview: just asphalt + shoulders
        float shoulderWidth = Mathf.Max(0f, cfg.shoulderWidth);
        float totalHalfWidth = halfWidth + shoulderWidth;

        int profileCount = shoulderWidth > 0.01f ? 4 : 2;
        float[] profileLateral;
        float[] profileDrop;

        if (shoulderWidth > 0.01f)
        {
            profileLateral = new float[] { -totalHalfWidth, -halfWidth, halfWidth, totalHalfWidth };
            profileDrop = new float[] { cfg.shoulderDrop, 0f, 0f, cfg.shoulderDrop };
        }
        else
        {
            profileLateral = new float[] { -halfWidth, halfWidth };
            profileDrop = new float[] { 0f, 0f };
        }

        // Generate road frames from spline samples
        Vector3[] vertices = new Vector3[sampleCount * profileCount];
        Vector3[] normals = new Vector3[sampleCount * profileCount];
        Vector2[] uv = new Vector2[sampleCount * profileCount];

        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 pos = result.samplePositions[i];
            Vector3 tangent = result.sampleTangents[i];

            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            Vector3 up = Vector3.Cross(tangent, right).normalized;

            // Apply banking from turn rate
            float turnRate = result.sampleTurnRates[i];
            float absTurnRate = Mathf.Abs(turnRate);
            float bankAngle = 0f;
            if (absTurnRate > cfg.bankTurnRateDeadzone && cfg.maxTurnRateDegPerMeter > 0.001f)
            {
                float denom = Mathf.Max(0.001f, cfg.maxTurnRateDegPerMeter - cfg.bankTurnRateDeadzone);
                float tCurve = Mathf.Clamp01((absTurnRate - cfg.bankTurnRateDeadzone) / denom);
                tCurve = tCurve * tCurve * (3f - 2f * tCurve);
                bankAngle = -Mathf.Sign(turnRate) * Mathf.Min(cfg.maxBankAngle, cfg.bankFromCurvature) * tCurve;
            }

            if (Mathf.Abs(bankAngle) > 0.01f)
            {
                Quaternion bankRot = Quaternion.AngleAxis(bankAngle, tangent);
                up = bankRot * Vector3.up;
                right = bankRot * right;
            }

            int rowBase = i * profileCount;
            float vCoord = (result.arcLength > 0f ? i / (float)(sampleCount - 1) : 0f) * result.arcLength * 0.1f;

            for (int j = 0; j < profileCount; j++)
            {
                vertices[rowBase + j] = pos + right * profileLateral[j] - up * profileDrop[j];
                normals[rowBase + j] = up;
                uv[rowBase + j] = new Vector2(
                    profileCount <= 1 ? 0f : j / (float)(profileCount - 1),
                    vCoord);
            }
        }

        int[] triangles = new int[(sampleCount - 1) * (profileCount - 1) * 6];
        int t = 0;
        for (int i = 0; i < sampleCount - 1; i++)
        {
            int aRow = i * profileCount;
            int bRow = (i + 1) * profileCount;
            for (int j = 0; j < profileCount - 1; j++)
            {
                int a0 = aRow + j;
                int a1 = aRow + j + 1;
                int b0 = bRow + j;
                int b1 = bRow + j + 1;
                triangles[t++] = a0;
                triangles[t++] = b0;
                triangles[t++] = a1;
                triangles[t++] = a1;
                triangles[t++] = b0;
                triangles[t++] = b1;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "RoadPiecePreview",
            vertices = vertices,
            normals = normals,
            uv = uv,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }
}
