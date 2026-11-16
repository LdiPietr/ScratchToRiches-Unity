using UnityEditor;
using UnityEngine;

/// <summary>Visible M1 reset button, enabled only while playing.</summary>
[CustomEditor(typeof(ScratchCellManager))]
public sealed class ScratchCellManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Reset All Cells", GUILayout.Height(30f)))
                ((ScratchCellManager)target).ResetAllCells();
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Reset All Cells is available in Play Mode.", MessageType.Info);
    }
}
