using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One scene-authored decorative Graphic behind all ninety square icons.
/// The radial layout is deterministic and shared by the map controller.
/// It never creates UI at runtime and never catches pointer events.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScratchRadialLinks : MaskableGraphic
{
    private static readonly Color32[] BranchColors =
    {
        new Color32(75, 160, 245, 150),
        new Color32(232, 181, 81, 150),
        new Color32(93, 213, 132, 150),
        new Color32(235, 114, 177, 150),
        new Color32(152, 129, 248, 150)
    };

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    /// <summary>Positions each branch in a 72-degree wedge around one nucleus.</summary>
    public static Vector2 PositionOf(string stableId)
    {
        if (string.IsNullOrEmpty(stableId)) return Vector2.zero;
        int branch = BranchIndex(stableId);
        int count = ScratchSkillCatalogV2.CountForBranch(branch);
        for (int i = 0; i < count; i++)
        {
            ScratchSkillCatalogV2.Entry entry = ScratchSkillCatalogV2.At(branch, i);
            if (entry != null && entry.id == stableId)
                return Position(branch, i);
        }
        return Vector2.zero;
    }

    private static int BranchIndex(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        switch (id[0])
        {
            case 'G': return 0;
            case 'C': return 1;
            case 'E': return 2;
            case 'F': return 3;
            case 'A': return 4;
            default: return 0;
        }
    }

    private static Vector2 Position(int branch, int index)
    {
        int lanes = branch == 4 ? 2 : 4;
        int ring = index / lanes + 1;
        float angle = -90f + 72f * branch +
            (index % lanes - (lanes - 1) * .5f) * 16f;
        float radius = 230f + ring * 220f;
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        DrawRing(vh, Vector2.zero, 121, 5,
            new Color32(246, 200, 112, 170), 64);
        DrawRing(vh, Vector2.zero, 138, 2,
            new Color32(246, 200, 112, 52), 64);
        for (int branch = 0; branch < 5; branch++)
        {
            int count = ScratchSkillCatalogV2.CountForBranch(branch);
            int lanes = branch == 4 ? 2 : 4;
            Color32 color = BranchColors[branch];
            for (int i = 0; i < count; i++)
            {
                ScratchSkillCatalogV2.Entry entry =
                    ScratchSkillCatalogV2.At(branch, i);
                if (entry == null) continue;
                Vector2 end = Position(branch, i);
                if (i < lanes)
                {
                    DrawLine(vh, Vector2.zero, end, 5, color);
                    continue;
                }
                // Connect to the preceding ring in the same visual lane,
                // and draw skill-specific dependency links below.
                DrawLine(vh, Position(branch, i - lanes), end, 3.5f,
                    new Color32(color.r, color.g, color.b, 110));
                string[] prerequisites = (entry.prerequisites ?? "").Split(';');
                foreach (string raw in prerequisites)
                {
                    if (string.IsNullOrEmpty(raw)) continue;
                    string[] parts = raw.Split(':');
                    if (parts.Length != 2) continue;
                    Vector2 start = PositionOf(parts[0]);
                    bool cross = BranchIndex(parts[0]) != branch;
                    DrawLine(vh, start, end, cross ? 1.2f : 2.5f,
                        new Color32(color.r, color.g, color.b, (byte)(cross ? 34 : 105)));
                }
            }
        }
    }

    private static void DrawRing(VertexHelper vh, Vector2 center,
        float radius, float width, Color32 tint, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.PI * 2f * i / segments;
            float b = Mathf.PI * 2f * (i + 1) / segments;
            Vector2 start = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Vector2 end = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
            DrawLine(vh, start, end, width, tint);
        }
    }

    private static void DrawLine(VertexHelper vh, Vector2 from, Vector2 to,
        float width, Color32 tint)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 1f) return;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
        int start = vh.currentVertCount;
        AddVert(vh, from - normal, tint);
        AddVert(vh, from + normal, tint);
        AddVert(vh, to + normal, tint);
        AddVert(vh, to - normal, tint);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void AddVert(VertexHelper vh, Vector2 pos, Color32 tint)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = pos;
        v.color = tint;
        vh.AddVert(v);
    }
}
