using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws radial spokes on ONE authored scene Graphic. No prefab, node or UI
/// creation during gameplay. The eight actual hold-to-buy nodes remain authored.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchRadialLinks : MaskableGraphic
{
    private bool overview = true;
    private int branch;

    private static readonly Vector2[] OverviewHubs =
    {
        new Vector2(-360, 175), new Vector2(360, 175),
        new Vector2(-235, -65), new Vector2(235, -65), new Vector2(0, -105)
    };
    private static readonly Vector2[] FocusNodes =
    {
        new Vector2(-405, 170), new Vector2(-165, 205),
        new Vector2(165, 205), new Vector2(405, 170),
        new Vector2(-405, -45), new Vector2(-165, -100),
        new Vector2(165, -100), new Vector2(405, -45)
    };
    private static readonly Color32[] BranchColors =
    {
        new Color32(90, 171, 255, 180),
        new Color32(237, 193, 120, 180),
        new Color32(117, 223, 158, 180),
        new Color32(240, 139, 196, 180),
        new Color32(178, 154, 255, 180)
    };

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Configure(bool overviewMode, int branchIndex)
    {
        if (overview == overviewMode && branch == branchIndex) return;
        overview = overviewMode;
        branch = Mathf.Clamp(branchIndex, 0, 4);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 nucleus = new Vector2(0, 58);
        DrawRing(vh, nucleus, 108, 3.5f, new Color32(246, 199, 118, 190), 64);
        DrawRing(vh, nucleus, 119, 1.5f, new Color32(246, 199, 118, 56), 64);

        if (overview)
        {
            for (int i = 0; i < OverviewHubs.Length; i++)
            {
                DrawLine(vh, nucleus, OverviewHubs[i], 5,
                    BranchColors[i]);
                DrawRing(vh, OverviewHubs[i], 34, 2,
                    BranchColors[i], 32);
            }
        }
        else
        {
            Vector2 hub = new Vector2(0, 283);
            Color32 tint = BranchColors[Mathf.Clamp(branch, 0, 4)];
            DrawLine(vh, nucleus, hub, 6, tint);
            for (int i = 0; i < FocusNodes.Length; i++)
            {
                DrawLine(vh, nucleus, FocusNodes[i], 2.5f,
                    new Color32(tint.r, tint.g, tint.b, 70));
                DrawRing(vh, FocusNodes[i], 39, 1.5f,
                    new Color32(tint.r, tint.g, tint.b, 54), 24);
            }
        }
    }

    private static void DrawRing(VertexHelper vh, Vector2 center, float radius,
        float width, Color32 color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float first = 2f * Mathf.PI * i / segments;
            float second = 2f * Mathf.PI * (i + 1) / segments;
            Vector2 a = center + new Vector2(Mathf.Cos(first), Mathf.Sin(first)) * radius;
            Vector2 b = center + new Vector2(Mathf.Cos(second), Mathf.Sin(second)) * radius;
            DrawLine(vh, a, b, width, color);
        }
    }

    private static void DrawLine(VertexHelper vh, Vector2 from, Vector2 to,
        float width, Color32 color)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 1f) return;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
        int start = vh.currentVertCount;
        Add(vh, from - normal, color);
        Add(vh, from + normal, color);
        Add(vh, to + normal, color);
        Add(vh, to - normal, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void Add(VertexHelper vh, Vector2 position, Color32 tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint;
        vh.AddVert(vertex);
    }
}
