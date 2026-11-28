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
        public Vector2 drift, stretch;
        public bool radialImpulse;
        public Color tint;
    }

    [Header("Authored scratch feedback tuning")]
    [SerializeField, Min(1f)] private float parallelFlakeReach = 30f;
    [SerializeField, Min(1f)] private float fanFlakeReach = 54f;
    [SerializeField, Min(1f)] private float titanImpulseReach = 104f;
    [SerializeField, Range(.1f, 1f)] private float titanImpulseLife = .58f;
    [SerializeField, Range(.2f, 2f)] private float titanImpulseScale = 1.05f;

    private Flake[] particles;
    private readonly float[] nextTrace = new float[5];
    private int serial, batchId;
    private static readonly Color[] FoilColors =
    {
        new Color(.30f,.35f,.34f), new Color(1f,.99f,.94f), new Color(.74f,.80f,.77f)
    };
    private static readonly Color[] TitanColors =
    {
        new Color(1f,.78f,.22f), new Color(.90f,.43f,.07f), new Color(1f,.95f,.63f)
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
        int parallel = EmitTrace(cell, left, 1, 2, width, titan, batch, -1) +
            EmitTrace(cell, right, 2, 2, width, titan, batch, 1);
        int fan = EmitTrace(cell, fanLeft, 3, 2, width, titan, batch, -1) +
            EmitTrace(cell, fanRight, 4, 2, width, titan, batch, 1);
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
        nextTrace[channel] = now + (channel == 0 ? .075f : .12f);
        Vector3 world = FoilWorld(cell, uv.Value);
        int emitted = 0;
        for (int i = 0; i < count; i++)
        {
            float angle = (serial + 1) * 2.399963f;
            float power = Mathf.Lerp(1f, 1.65f, (width - 1f) * .5f);
            Vector2 drift = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                (titan ? 40f : 22f) * power;
            float life = titan ? .39f : .30f;
            float size = titan ? .78f : Mathf.Lerp(.46f, .65f, (width - 1f) * .5f);
            Vector2 stretch = Vector2.one;
            // Parallel strips shed paired, aligned silver chips. Fan strips
            // diverge outward and upward in two wider, distinct branches.
            // Origins always come from actual foil removed by the mask.
            if (channel == 1 || channel == 2)
            {
                drift = new Vector2(direction * parallelFlakeReach,
                    i == 0 ? 9f : -9f) * (titan ? 1.3f : 1f);
                life = .38f;
                size = Mathf.Max(size, .70f);
                stretch = new Vector2(1.32f, .90f);
            }
            else if (channel >= 3)
            {
                Vector2 ray = new Vector2(direction * (i == 0 ? .90f : .52f),
                    i == 0 ? .44f : .85f).normalized;
                drift = ray * fanFlakeReach * (titan ? 1.25f : 1f);
                life = .44f;
                size = Mathf.Max(size, .74f);
                stretch = new Vector2(1.55f, .86f);
            }
            if (Spawn(world, drift, life, size, titan, titan ? 1 : 0, batch,
                stretch, channel, false)) emitted++;
        }
        return emitted;
    }

    public void OnTitanActivated(RectTransform cell, Vector2 uv)
    {
        if (!isActiveAndEnabled || cell == null || particles == null) return;
        // Immediate impulse, independent of subsequent pointer motion.
        System.Array.Clear(nextTrace,0,nextTrace.Length);
        int batch = ++batchId;
        // Twelve evenly spaced existing flakes make a single expanding
        // golden ring. No additional UI objects or flash layer are created.
        int emitted = Burst(FoilWorld(cell, uv), 12, titanImpulseReach,
            titanImpulseLife, titanImpulseScale, true, 2, batch, true);
#if UNITY_EDITOR
        if (ScratchGameFeelPrefs.Active && emitted > 0) qaTitanActivations++;
#endif
    }

    public void OnCellRevealed(RectTransform cell, bool titan = false)
    {
        if (!isActiveAndEnabled || cell == null || particles == null) return;
        int emitted = Burst(cell.TransformPoint(cell.rect.center), titan ? 10 : 7,
            titan ? 70f : 46f, titan ? .49f : .39f, titan ? .92f : .74f,
            titan, 3, ++batchId);
#if UNITY_EDITOR
        if (ScratchGameFeelPrefs.Active && emitted > 0) qaRevealBursts++;
#endif
    }

    private int Burst(Vector3 world, int count, float reach, float lifetime,
        float size, bool gold, int priority, int batch, bool radialImpulse = false)
    {
        int emitted = 0;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + .25f) * Mathf.PI * 2f / count;
            Vector2 drift = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) *
                reach * (radialImpulse || i % 2 == 0 ? 1f : .78f);
            if (Spawn(world, drift, lifetime, size, gold, priority, batch,
                radialImpulse ? new Vector2(1.20f, .84f) : Vector2.one, 0, radialImpulse)) emitted++;
        }
        return emitted;
    }

    private bool Spawn(Vector3 world, Vector2 drift, float life, float size,
        bool gold, int priority, int batch, Vector2 stretch, int channel, bool radialImpulse)
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
        if (!gold && channel >= 3)
            tint = n % 2 == 0 ? new Color(.99f, .94f, .77f) : new Color(.62f, .72f, .70f);
        else if (!gold && channel > 0)
            tint = n % 2 == 0 ? new Color(.95f, .98f, .96f) : new Color(.48f, .60f, .61f);
        bool directional = radialImpulse || channel > 0;
        float heading = Mathf.Atan2(drift.y, drift.x) * Mathf.Rad2Deg;
        particles[slot] = new Flake
        {
            alive = true, gold = gold, priority = priority, batch = batch, born = now,
            life = life * (radialImpulse || n % 3 != 0 ? 1f : .88f),
            scale = size * (radialImpulse || n % 3 != 0 ? 1f : .78f),
            rotation = directional ? heading : n * 47f % 360f,
            spin = (n % 2 == 0 ? 1f : -1f) * (directional ? 34f : 130f),
            origin = image.rectTransform.parent.InverseTransformPoint(world),
            drift = drift, tint = tint, stretch = stretch, radialImpulse = radialImpulse
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
        if (p.radialImpulse) travel = Mathf.Lerp(.20f, 1f, travel);
        rt.localPosition = p.origin + (Vector3)(p.drift * travel +
            Vector2.down * (p.radialImpulse ? 0f : 17f) * t * t);
        rt.localRotation = Quaternion.Euler(0f,0f,p.rotation+p.spin*t);
        // Keep fragments legible during the first half of their flight.
        // Titan's one-shot impulse has a brief size punch, not a screen flash.
        float punch = p.priority == 2 ? 1f + .20f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .28f)) : 1f;
        float scale = p.scale * punch * Mathf.Lerp(1f, .58f, t);
        rt.localScale = new Vector3(p.stretch.x * scale, p.stretch.y * scale, scale);
        Color tint = p.tint;
        float fade = 1f - Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.22f,1f,t));
        tint.a = (p.gold ? 1f : .92f) * fade;
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
