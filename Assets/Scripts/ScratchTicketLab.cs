using UnityEngine;
using TMPro;

// A scene-authored test board, deliberately without game/economy/progression managers.
[DisallowMultipleComponent]
public sealed class ScratchTicketLab : MonoBehaviour
{
    [SerializeField] private ScratchCellManager cells;
    [SerializeField] private ScratchSymbolManager symbols;
    private ScratchGameManager.TicketKind selected;
    private string status = "";
    private readonly string[] labels = { "CLASSICO 3×3", "FORTUNATO 4×4", "DORATO 5×5", "DIAMANTE 25 ESAGONI" };

    private void Start() => Select(0);

    public void Select(int index)
    {
        if (index < 0 || index > 3 || cells == null || symbols == null) return;
        selected = (ScratchGameManager.TicketKind)index;
        cells.BeginTicketVisualPreparation();
        if (!cells.ConfigureGrid(ScratchGameManager.TicketSide(selected), selected))
        { status = "Errore layout"; return; }
        cells.ResetAllCells();
        bool generated = symbols.GenerateRound(ScratchGameManager.SymbolPoolSize(selected), selected);
        bool ready = cells.EndTicketVisualPreparation();
        status = generated && ready ? "" : "Errore preparazione biglietto";
    }

    private void OnGUI()
    {
        // Test-only selection controls, outside the interactive ticket.
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        var previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1920*scale)/2,(Screen.height-1080*scale)/2,0),
            Quaternion.identity, Vector3.one*scale);
        var title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
        var label = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        GUI.Box(new Rect(45,100,395,460), "");
        GUI.Label(new Rect(65,120,365,50),"LABORATORIO BIGLIETTI",title);
        GUI.Label(new Rect(65,175,365,70),"Grattatura libera · nessun costo",label);
        for(int i=0;i<4;i++)
            if (GUI.Button(new Rect(65,245+i*65,355,52),labels[i],button)) Select(i);
        if (GUI.Button(new Rect(65,508,355,40),"NUOVO BIGLIETTO",button)) Select((int)selected);
        int revealed = 0;
        if (cells != null) foreach(var c in cells.ActiveCells) if(c.IsComplete) revealed++;
        GUI.Box(new Rect(1480,100,395,200),"");
        GUI.Label(new Rect(1500,120,350,60),labels[(int)selected],title);
        GUI.Label(new Rect(1500,180,350,90),"Grattate: "+revealed+"/"+(cells!=null?cells.CellCount:0)+"\n"+status,label);
        GUI.matrix = previous;
    }
}
