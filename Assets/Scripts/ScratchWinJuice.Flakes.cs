using UnityEngine;
using UnityEngine.UI;

// Cosmetic, bounded pool. All Images are authored in ScratchToRiches.unity.
// Independent lifetimes preserve a trail without restarting older particles.
public sealed partial class ScratchWinJuice
{
    private struct Flake
    {
        public bool alive, gold;
        public int priority, batch;
        public float born, life, scale, rotation, spin;
        public Vector3 origin;
        public Vector2 drift;
        public Color tint;
    }

    private Flake[] particles;
    private readonly float[] nextTrace = new float[5];
    private int serial, batchId;
    private static readonly Color[] FoilColors =
    {
        new Color(.68f,.72f,.70f), new Color(.98f,.97f,.91f), new Color(.84f,.86f,.82f)
    };
    private static readonly Color[] TitanColors =
    {
        new Color(1f,.77f,.28f), new Color(.96f,.59f,.16f), new Color(1f,.90f,.53f)
    };

    private void InitializeFlakes()
    {
        particles = new Flake[scratchFlakes.Length];
        ClearFlakes();
    }

    private void ClearFlakes()
    {
        if (particles == null) return;
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i] = default;
            if (scratchFlakes[i] != null) scratchFlakes[i].gameObject.SetActive(false);
        }
        System.Array.Clear(nextTrace,0,nextTrace.Length);
    }

    private static Vector3 FoilWorld(RectTransform cell, Vector2 uv)
    {
        Rect r = cell.rect;
        return cell.TransformPoint(new Vector3(Mathf.Lerp(r.xMin,r.xMax,uv.x),
            Mathf.Lerp(r.yMin,r.yMax,uv.y),0f));
    }

    public void OnScratchMaterialRemoved(RectTransform cell, Vector2? main,
        Vector2? left, Vector2? right, Vector2? fanLeft, Vector2? fanRight,
        float radiusScale, bool titan)
    {
        if (!isActiveAndEnabled || cell == null || particles == null) return;
        float width = Mathf.Clamp(radiusScale, 1f, 3f);
        int batch = ++batchId;
        int primary = EmitTrace(cell, main, 0, titan ? 3 : 2, width, titan, batch, 0);
        int parallel = EmitTrace(cell, left, 1, titan ? 2 : 1, width, titan, batch, -1) +
            EmitTrace(cell, right, 2, titan ? 2 : 1, width, titan, batch, 1);
        int fan = EmitTrace(cell, fanLeft, 3, 1, width, titan, batch, -1) +
            EmitTrace(cell, fanRight, 4, 1, width, titan, batch, 1);
#if UNITY_EDITOR
        if (ScratchGameFeelPrefs.Active && primary + parallel + fan > 0)
        {
            qaTrailBursts++;
            if (parallel > 0) qaParallelBursts++;
            if (fan > 0) qaFanBursts++;
            if (titan) qaTitanTrails++;
        }
#endif
    }

    private int EmitTrace(RectTransform cell, Vector2? uv, int channel, int count,
        float width, bool titan, int batch, int direction)
    {
        float now = Time.unscaledTime;
        if (!uv.HasValue || now < nextTrace[channel]) return 0;
        nextTrace[channel] = now + (channel == 0 ? .075f : .10f);
        Vector3 world = FoilWorld(cell, uv.Value);
        int emitted = 0;
        for (int i = 0; i < count; i++)
        {
            float angle = (serial + 1) * 2.399963f;
            Vector2 drift = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                (titan ? 29f : 17f) * Mathf.Lerp(1f,1.65f,(width-1f)*.5f);
            drift.x += direction * (channel >= 3 ? 13f : 7f);
            if (Spawn(world, drift, titan ? .31f : .24f,
                titan ? .62f : Mathf.Lerp(.37f,.54f,(width-1f)*.5f),
                titan, titan ? 1 : 0, batch)) emitted++;
        }
        return emitted;
    }

    public void OnTitanActivated(RectTransform cell, Vector2 uv)
    {
        if (!isActiveAndEnabled || cell == null || particles == null) return;
        // Immediate impulse, independent of subsequent pointer motion.
        System.Array.Clear(nextTrace,0,nextTrace.Length);
        int batch = ++batchId;
        int emitted = Burst(FoilWorld(cell,uv), 10, 55f, .40f, .78f, true, 2, batch);
#if UNITY_EDITOR
        if (ScratchGameFeelPrefs.Active && emitted > 0) qaTitanActivations++;
#endif
    }

    public void OnCellRevealed(RectTransform cell, bool titan = false)
    {
        if (!isActiveAndEnabled || cell == null || particles == null) return;
        int emitted = Burst(cell.TransformPoint(cell.rect.center), titan ? 10 : 7,
            titan ? 58f : 40f, titan ? .41f : .34f, titan ? .82f : .69f,
            titan, 3, ++batchId);
#if UNITY_EDITOR
        if (ScratchGameFeelPrefs.Active && emitted > 0) qaRevealBursts++;
#endif
    }

    private int Burst(Vector3 world, int count, float reach, float lifetime,
        float size, bool gold, int priority, int batch)
    {
        int emitted = 0;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + .25f) * Mathf.PI * 2f / count;
            Vector2 drift = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) *
                reach * (i % 2 == 0 ? 1f : .78f);
            if (Spawn(world,drift,lifetime,size,gold,priority,batch)) emitted++;
        }
        return emitted;
    }

    private bool Spawn(Vector3 world, Vector2 drift, float life, float size,
        bool gold, int priority, int batch)
    {
        float now = Time.unscaledTime;
        int slot = -1, weakest = int.MaxValue;
        float oldest = float.PositiveInfinity;
        for (int i = 0; i < particles.Length; i++)
        {
            Image candidate = scratchFlakes[i];
            if (candidate == null || candidate.rectTransform.parent == null) continue;
            Flake p = particles[i];
            if (!p.alive || now >= p.born + p.life) { slot = i; break; }
            // A new low-priority trail cannot overwrite a reveal or Titan impulse.
            if (p.priority > priority || p.batch == batch) continue;
            if (p.priority < weakest || (p.priority == weakest && p.born < oldest))
            { slot = i; weakest = p.priority; oldest = p.born; }
        }
        if (slot < 0) return false;
        Image image = scratchFlakes[slot];
        int n = serial++;
        Color tint = (gold ? TitanColors : FoilColors)[n % 3];
        particles[slot] = new Flake
        {
            alive = true, gold = gold, priority = priority, batch = batch, born = now,
            life = life * (n % 3 == 0 ? .88f : 1f), scale = size * (n % 3 == 0 ? .78f : 1f),
            rotation = n * 47f % 360f, spin = (n % 2 == 0 ? 1f : -1f) * 130f,
            origin = image.rectTransform.parent.InverseTransformPoint(world), drift = drift, tint = tint
        };
        image.raycastTarget = false;
        image.gameObject.SetActive(true);
        RenderFlake(slot, now);
        return true;
    }

    private void RenderFlake(int index, float now)
    {
        Flake p = particles[index];
        Image image = scratchFlakes[index];
        if (image == null) { particles[index].alive = false; return; }
        float t = Mathf.Clamp01((now-p.born)/p.life);
        if (!p.alive || t >= 1f)
        {
            particles[index].alive = false;
            image.gameObject.SetActive(false);
            return;
        }
        float travel = 1f - (1f-t)*(1f-t);
        RectTransform rt = image.rectTransform;
        rt.localPosition = p.origin + (Vector3)(p.drift*travel + Vector2.down*17f*t*t);
        rt.localRotation = Quaternion.Euler(0f,0f,p.rotation+p.spin*t);
        rt.localScale = Vector3.one * p.scale * Mathf.Lerp(1f,.45f,t);
        Color tint = p.tint;
        tint.a = (p.gold ? .93f : .77f) * (1f-t)*(1f-t);
        image.color = tint;
    }

    private void TickFlakes(float now)
    {
        if (particles == null) return;
#if UNITY_EDITOR
        int firstTrail = -1, gold = 0;
        bool overlap = false;
#endif
        for (int i = 0; i < particles.Length; i++)
        {
            if (!particles[i].alive) continue;
            RenderFlake(i,now);
#if UNITY_EDITOR
            if (!particles[i].alive || !scratchFlakes[i].gameObject.activeInHierarchy ||
                scratchFlakes[i].color.a <= .05f) continue;
            if (particles[i].gold) gold++;
            if (particles[i].priority < 2)
            {
                if (firstTrail == -1) firstTrail = particles[i].batch;
                else if (firstTrail != particles[i].batch) overlap = true;
            }
#endif
        }
#if UNITY_EDITOR
        if (ScratchGameFeelPrefs.Active)
        {
            if (overlap) qaOverlapFrames++;
            if (gold > 0) qaGoldFrames++;
            qaPeakGold = Mathf.Max(qaPeakGold,gold);
        }
#endif
    }

#if UNITY_EDITOR
    private int qaTrailBursts, qaParallelBursts, qaFanBursts, qaTitanTrails, qaTitanActivations;
    private int qaRevealBursts, qaOverlapFrames, qaGoldFrames, qaPeakGold;
    public int QaTrailBursts => qaTrailBursts;
    public int QaParallelBursts => qaParallelBursts;
    public int QaFanBursts => qaFanBursts;
    public int QaTitanTrails => qaTitanTrails;
    public int QaTitanActivations => qaTitanActivations;
    public int QaRevealBursts => qaRevealBursts;
    public int QaOverlapFrames => qaOverlapFrames;
    public int QaGoldFrames => qaGoldFrames;
    public int QaPeakGold => qaPeakGold;

    public void QaResetFlakeMetrics()
    {
        if (!ScratchGameFeelPrefs.Active) return;
        qaTrailBursts = qaParallelBursts = qaFanBursts = qaTitanTrails = qaTitanActivations = 0;
        qaRevealBursts = qaOverlapFrames = qaGoldFrames = qaPeakGold = 0;
    }
#endif
}
