using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Isolated, scene-authored ticket board. No economy, persistence or progression.
[DisallowMultipleComponent]
public sealed partial class ScratchTicketLab : MonoBehaviour
{
    [SerializeField] private ScratchCellManager cells;
    [SerializeField] private ScratchSymbolManager symbols;
    [SerializeField] private int seed = 41026;
    [SerializeField, Min(0.01f)] private float strokeInterval = .035f;
    [SerializeField, Min(0f)] private float inspectionPause = 1.5f;
    [SerializeField, Range(8, 16)] private int samplesPerAxis = 12;
    [SerializeField] private bool recordAutomatically = true;
#if UNITY_EDITOR
    // Editor recorder hooks keep UnityEditor/Recorder out of the runtime assembly.
    public static System.Func<string, bool> BeginVideoTicket;
    public static System.Func<bool> VideoCheckpoint;
    public static System.Action FinishVideo;
#endif
    private ScratchGameManager.TicketKind selected;
    private readonly string[] labels = { "CLASSICO 3×3", "FORTUNATO 4×4", "DORATO 5×5", "DIAMANTE 25 ESAGONI" };
    private readonly string[] results = { "Non testato", "Non testato", "Non testato", "Non testato" };
    private Coroutine sweep;
    private PointerEventData pointer;
    private ScratchCell pressed;
    private bool ready;
    private string status = "";
    private int round;
    private int failures;
    private bool peek;
    private bool running;

    private void Start() => Select(0);
    private void OnDisable() => StopTest();

    public void Select(int index)
    {
        StopTest();
        Prepare(index, seed + round++);
    }

    private bool Prepare(int index, int ticketSeed)
    {
        ready = false;
        peek = false;
        if (index < 0 || index > 3 || cells == null || symbols == null) return false;
        selected = (ScratchGameManager.TicketKind)index;
        cells.BeginTicketVisualPreparation();
        if (!cells.ConfigureGrid(ScratchGameManager.TicketSide(selected), selected))
        { status = "Errore layout"; return false; }
        cells.ResetAllCells();
        var randomState = Random.state;
        bool generated;
        try
        {
            Random.InitState(ticketSeed);
            generated = symbols.GenerateRound(ScratchGameManager.SymbolPoolSize(selected), selected);
        }
        finally { Random.state = randomState; }
        bool prepared = cells.EndTicketVisualPreparation();
        ready = generated && prepared;
        status = ready ? "Pronto · seed " + ticketSeed : "Errore preparazione";
        Canvas.ForceUpdateCanvases();
        return ready;
    }

    private void StopTest()
    {
        if (sweep != null) StopCoroutine(sweep);
        sweep = null;
        Release();
        running = false;
        EndVideo();
    }

    private void EndVideo()
    {
#if UNITY_EDITOR
        if (recordAutomatically) FinishVideo?.Invoke();
#endif
    }

    private bool CaptureTicket(int index)
    {
#if UNITY_EDITOR
        if (recordAutomatically)
            return BeginVideoTicket != null && BeginVideoTicket(new[] { "Classico", "Fortunato", "Dorato", "Diamante" }[index]);
#endif
        return true;
    }

    private bool CaptureCheckpoint()
    {
#if UNITY_EDITOR
        if (recordAutomatically) return VideoCheckpoint != null && VideoCheckpoint();
#endif
        return true;
    }

    private void CaptureFailed()
    {
        Release();
        running = false;
        sweep = null;
        status = "Errore registrazione: controlla Console";
        EndVideo();
    }

    private void Release()
    {
        if (pressed != null && pointer != null) pressed.OnPointerUp(pointer);
        pressed = null;
        pointer = null;
    }

    private void StartTest(bool all, bool quick = false)
    {
        StopTest();
        if (EventSystem.current == null) { status = "EventSystem mancante"; return; }
        failures = 0;
        if (all) for (int i = 0; i < results.Length; i++) results[i] = "Non testato";
        running = true;
        sweep = StartCoroutine(Run(all ? 0 : (int)selected, all ? 3 : (int)selected, quick));
    }

    private IEnumerator Run(int first, int last, bool quick)
    {
        yield return null;
        for (int i = first; i <= last; i++)
        {
            results[i] = "In corso";
            if (!CaptureTicket(i))
            { results[i] = "VIDEO FAIL"; CaptureFailed(); yield break; }
            bool ok = Prepare(i, seed + i);
            // Let the new Recorder see the covered board before inspection begins.
            yield return null;
            int expected = i == 0 ? 9 : i == 1 ? 16 : 25;
            int target = quick ? 3 : expected;
            if (ok) ok = CheckFresh(expected, i);
            if (ok) yield return Snapshot("01_coperto");
            if (ok)
            {
                var firstCell = cells.ActiveCells[0];
                var raw = firstCell.GetComponentInChildren<RawImage>(true);
                TogglePeek();
                bool previewOk = firstCell.IsPreviewVisible && firstCell.TryGetVisibleSymbol(out _) &&
                    Mathf.Approximately(raw.color.a, .75f);
                if (previewOk) yield return Snapshot("02_occhio_di_falco");
                yield return new WaitForSecondsRealtime(inspectionPause);
                TogglePeek();
                ok = previewOk && CheckFresh(expected, i);
                if (!ok) Debug.LogError("LAB: " + labels[i] + " anteprima/reset non valido", this);
            }
            yield return new WaitForSecondsRealtime(inspectionPause);
            if (ok)
            {
                for (int cellIndex = 0; cellIndex < cells.ActiveCells.Length; cellIndex++)
                {
                    if (quick && cellIndex != 0 && cellIndex != expected / 2 && cellIndex != expected - 1) continue;
                    ScratchCell cell = cells.ActiveCells[cellIndex];
                    // Rotate only between cells, never during a scratch gesture.
                    if (!CaptureCheckpoint())
                    { results[i] = "VIDEO FAIL"; CaptureFailed(); yield break; }
                    yield return null;
                    yield return Scratch(cell);
                    if (!cell.IsComplete || cell.RevealedArtwork == null)
                    { ok = false; Debug.LogError("LAB: " + labels[i] + " " + cell.name + " non rivelata", this); }
                }
            }
            int revealed = CountRevealed();
            ok &= revealed == target;
            results[i] = (ok ? "PASS " : "FAIL ") + revealed + "/" + target + (quick ? " rapido" : "");
            if (!ok) failures++;
            string report = "TICKET LAB " + labels[i] + " " + results[i];
            if (ok) Debug.Log(report, this); else Debug.LogError(report, this);
            var match = symbols.Evaluate(cells.ActiveCells);
            status = results[i] + " · " + match.Tier + " · ispezione finale";
            yield return new WaitForSecondsRealtime(inspectionPause);
            yield return Snapshot("04_risultato");
        }
        status = (quick ? "Audit rapido: " : "Test completo: ") + (failures == 0 ? "PASS" : failures + " errori");
        yield return null;
        EndVideo();
        running = false;
        sweep = null;
    }

    private bool CheckFresh(int expected, int kind)
    {
        if (cells.CellCount != expected) return false;
        foreach (ScratchCell cell in cells.ActiveCells)
        {
            var raw = cell.GetComponentInChildren<RawImage>(true);
            if (raw == null || cell.IsComplete || cell.IsPreviewVisible ||
                cell.RevealedPercentage != 0f || cell.TryGetVisibleSymbol(out _) ||
                raw.color.a < .999f || raw.texture == null) return false;
            RectTransform rt = raw.rectTransform;
            Vector2 center = ScreenPoint(rt, .5f, .5f);
            if (!cell.ContainsScratchPoint(center, null)) return false;
            if (kind >= 2 && cell.ContainsScratchPoint(ScreenPoint(rt, .02f, .02f), null)) return false;
        }
        return true;
    }

    private static Vector2 ScreenPoint(RectTransform rt, float x, float y)
    {
        Rect r = rt.rect;
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(
            new Vector3(Mathf.Lerp(r.xMin,r.xMax,x), Mathf.Lerp(r.yMin,r.yMax,y),0)));
    }

    // Drive the actual scratch pointer API; do not force completion or alter the mask.
    private IEnumerator Scratch(ScratchCell cell)
    {
        var raw = cell.GetComponentInChildren<RawImage>(true);
        if (raw == null) yield break;
        var rt = raw.rectTransform;
        var ev = new PointerEventData(EventSystem.current)
        { pointerId = -10, button = PointerEventData.InputButton.Left };
        int n = Mathf.Clamp(samplesPerAxis, 8, 16);
        bool inspected = false;
        for (int y = 0; y < n && !cell.IsComplete; y++)
        {
            for (int x = 0; x < n && !cell.IsComplete; x++)
            {
                int column = y % 2 == 0 ? x : n - 1 - x;
                ev.position = ScreenPoint(rt,(column+.5f)/n,(y+.5f)/n);
                if (!cell.ContainsScratchPoint(ev.position,null)) { Release(); continue; }
                if (pressed == null)
                { pressed = cell; pointer = ev; cell.OnPointerDown(ev); }
                else cell.OnDrag(ev);
                if (!inspected && cell == cells.ActiveCells[0] && cell.RevealedPercentage >= 25f && !cell.IsComplete)
                { inspected = true; yield return Snapshot("03_grattatura_parziale"); yield return new WaitForSecondsRealtime(inspectionPause); }
                yield return new WaitForSecondsRealtime(strokeInterval);
            }
        }
        Release();
        yield return new WaitForSecondsRealtime(.12f);
    }

    private int CountRevealed()
    {
        int count = 0;
        if (cells != null) foreach (var c in cells.ActiveCells) if (c.IsComplete) count++;
        return count;
    }

    private void TogglePeek()
    {
        if (!ready || cells.CellCount == 0) return;
        var cell = cells.ActiveCells[0];
        if (cell.IsComplete) return;
        peek = !peek;
        cell.SetPreview(peek && symbols.TryGetSymbol(cell, out string label) ? label : "");
    }

    // Existing lab-only IMGUI controls. No runtime game objects are created.
    private void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        var previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1920*scale)/2,(Screen.height-1080*scale)/2,0),
            Quaternion.identity, Vector3.one*scale);
        var title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
        var label = new GUIStyle(GUI.skin.label) { fontSize = 21, wordWrap = true };
        var button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        GUI.Box(new Rect(45,100,395,735), "");
        GUI.Label(new Rect(65,120,365,45),"LABORATORIO BIGLIETTI",title);
        GUI.Label(new Rect(65,170,355,65),recordAutomatically
            ? "Grattatura libera · video automatico in Editor"
            : "Grattatura libera · nessun costo",label);
        GUI.enabled = !running;
        for (int i=0;i<4;i++)
            if (GUI.Button(new Rect(65,235+i*58,355,48),labels[i],button)) Select(i);
        if (GUI.Button(new Rect(65,475,355,48),"NUOVO BIGLIETTO",button)) Select((int)selected);
        if (GUI.Button(new Rect(65,532,355,48),peek ? "NASCONDI ANTEPRIMA" : "OCCHIO DI FALCO",button)) TogglePeek();
        if (GUI.Button(new Rect(65,590,355,48),"TEST BIGLIETTO ATTUALE",button)) StartTest(false);
        if (GUI.Button(new Rect(65,648,355,48),"TEST TUTTI I BIGLIETTI",button)) StartTest(true);
        if (GUI.Button(new Rect(65,706,355,48),"AUDIT RAPIDO (4 BIGLIETTI)",button)) StartTest(true, true);
        GUI.enabled = running;
        if (GUI.Button(new Rect(65,764,355,48),"STOP TEST",button)) { StopTest(); status = "Interrotto"; }
        GUI.enabled = true;
        GUI.Box(new Rect(1480,100,395,410),"");
        GUI.Label(new Rect(1500,120,355,60),labels[(int)selected],title);
        GUI.Label(new Rect(1500,180,350,110),"Grattate: "+CountRevealed()+"/"+(cells!=null?cells.CellCount:0)+"\n"+status,label);
        for (int i=0;i<4;i++) GUI.Label(new Rect(1500,295+i*45,355,42),labels[i].Split(' ')[0]+": "+results[i],label);
        GUI.matrix = previous;
    }
}
