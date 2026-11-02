using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

// 1000 敌人 HD-2D 压测驱动：
//  1) 纯渲染阶段（timeScale=0）：只跑 LateUpdate 的矩阵构建/排序/实例化绘制
//  2) 全量阶段（timeScale=1）：Burst 移动 + 空间哈希 + 渲染
// 采样帧时间、FrameTimingManager 的 CPU 主线程/渲染线程/GPU 耗时，
// 以及 ProfilerRecorder 的 Draw Calls / SetPass / Batches。
public class EnemyPerfStress : MonoBehaviour
{
    [Header("压测规模")]
    public int enemyCount = 1000;
    [Tooltip("敌人在相机前方矩形范围内生成；近端/远端半宽会自动展开")]
    public float nearZ = 2f;
    public float farZ = 22f;
    public float nearHalfWidth = 7f;
    public float farHalfWidth = 24f;

    [Header("采样时间轴（秒）")]
    public float staticWarmup = 2f;
    public float staticMeasure = 7f;
    public float liveWarmup = 1.5f;
    public float liveMeasure = 7f;

    [Header("输出")]
    public bool takeScreenshots = true;
    [Tooltip("留空则写到仓库根目录 Proto25D/")]
    public string outputDir = "";

    // 0 NormalA / 1 NormalB / 2 Fast / 3 Tank / 6 Elite。
    // 不含 Exploder（会自爆消失，导致数量掉到 960）、Ranged（16x16 贴图无 alpha）与 Boss。
    static readonly int[] TypeTable = { 0, 1, 2, 3, 6 };
    static readonly float[] TypeWeights = { 0.40f, 0.28f, 0.20f, 0.06f, 0.06f };

    enum Phase { StaticWarmup, StaticMeasure, LiveWarmup, LiveMeasure, Report }

    Phase phase;
    float phaseTime;
    float reportShotTimer = -1f;
    bool started;

    EnemySimulation sim;
    readonly int[] spawnedByType = new int[8];

    class Stats
    {
        public readonly List<float> frameMs = new List<float>(4096);
        public double sumCpuMain, sumCpuRender, sumGpu;
        public int cpuSamples, gpuSamples;
        public double sumDraw, sumSetPass, sumBatches;
        public int drawSamples;
        public int drawMax, setPassMax, batchesMax;
        public double sumEnemyDraw, sumShadowDraw, sumInstances;
        public int enemyDrawSamples, enemyDrawMax, shadowDrawMax, instancesMax;
        public int frameStart;
    }

    readonly Stats staticStats = new Stats();
    readonly Stats liveStats = new Stats();

    ProfilerRecorder drawCallsRec;
    ProfilerRecorder setPassRec;
    ProfilerRecorder batchesRec;
    ProfilerRecorder mainThreadRec;
    ProfilerRecorder renderThreadRec;

    string report = "";
    string profilerInfo = "";
    string OutputDir => string.IsNullOrEmpty(outputDir)
        ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Proto25D"))
        : outputDir;

    void Start()
    {
        Time.timeScale = 1f;
        // 编辑器里关掉 vsync 才能看到真实渲染上限；结果同时给出 ms，便于换算 60/120 帧预算
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        sim = EnemySimulation.Instance;
        if (sim == null)
        {
            Debug.LogError("[Perf] 场景里没有 EnemySimulation");
            enabled = false;
            return;
        }

        SpawnEnemies();
        StartRecorders();

        // 先测纯渲染：timeScale=0 时 Job 的 dt=0，但矩阵构建/排序/DrawMeshInstanced 全量执行
        Time.timeScale = 0f;
        phase = Phase.StaticWarmup;
        phaseTime = 0f;
        started = true;

        Debug.Log($"[Perf] 已生成 {SimAlive()} 个敌人，屏幕 {Screen.width}x{Screen.height}，开始压测");
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        DisposeRecorders();
    }

    void SpawnEnemies()
    {
        for (int i = 0; i < enemyCount; i++)
        {
            // 两个无理数错开，保证位置/类型分布均匀且可复现
            float u = Mathf.Repeat(i * 0.61803398875f, 1f);
            float v = Mathf.Repeat(i * 0.75487766624f, 1f);
            float z = Mathf.Lerp(nearZ, farZ, v);
            float halfW = Mathf.Lerp(nearHalfWidth, farHalfWidth, (z - nearZ) / Mathf.Max(0.01f, farZ - nearZ));
            float x = Mathf.Lerp(-halfW, halfW, u);

            int type = PickType(i);
            sim.Spawn(type, new Vector2(x, z));
            if ((uint)type < (uint)spawnedByType.Length) spawnedByType[type]++;
        }
    }

    static int PickType(int i)
    {
        float r = Mathf.Repeat(i * 0.38196601125f, 1f);
        float acc = 0f;
        for (int t = 0; t < TypeWeights.Length; t++)
        {
            acc += TypeWeights[t];
            if (r < acc) return TypeTable[t];
        }
        return TypeTable[TypeTable.Length - 1];
    }

    ProfilerRecorder Open(string name, ProfilerCategory fallback, out string where)
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (d.Name != name) continue;
            var rec = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default);
            if (rec.Valid)
            {
                where = $"{d.Category.Name}/{d.Name}/{d.UnitType}";
                return rec;
            }
            rec.Dispose();
        }

        var r = ProfilerRecorder.StartNew(fallback, name, 1);
        where = r.Valid ? $"{fallback.Name}/{name}" : $"missing:{fallback.Name}/{name}";
        return r;
    }

    void StartRecorders()
    {
        drawCallsRec = Open("Draw Calls", ProfilerCategory.Render, out var d1);
        setPassRec = Open("SetPass Calls", ProfilerCategory.Render, out var d2);
        batchesRec = Open("Batches", ProfilerCategory.Render, out var d3);
        mainThreadRec = Open("Main Thread", ProfilerCategory.Internal, out var d4);
        renderThreadRec = Open("Render Thread", ProfilerCategory.Internal, out var d5);

        // 把可用计数器名写进报告，方便定位不同 Unity 版本下的命名差异
        var sb = new StringBuilder();
        sb.AppendLine($"- Draw Calls recorder: {d1}");
        sb.AppendLine($"- SetPass recorder: {d2}");
        sb.AppendLine($"- Batches recorder: {d3}");
        sb.AppendLine($"- Main Thread recorder: {d4}");
        sb.AppendLine($"- Render Thread recorder: {d5}");

        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        int shown = 0;
        foreach (var h in handles)
        {
            var desc = ProfilerRecorderHandle.GetDescription(h);
            string n = desc.Name;
            if (n.IndexOf("Draw", StringComparison.OrdinalIgnoreCase) < 0 &&
                n.IndexOf("Batch", StringComparison.OrdinalIgnoreCase) < 0 &&
                n.IndexOf("SetPass", StringComparison.OrdinalIgnoreCase) < 0 &&
                n.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) < 0) continue;
            sb.AppendLine($"  - {desc.Category.Name} :: {n} [{desc.UnitType}]");
            if (++shown > 24) break;
        }
        profilerInfo = sb.ToString();

        if (!drawCallsRec.Valid)
            Debug.LogWarning("[Perf] 场景总 Draw Calls 指标不可用，改用 EnemySimRenderer 自报的敌人 draw call 数");
    }

    void DisposeRecorders()
    {
        drawCallsRec.Dispose();
        setPassRec.Dispose();
        batchesRec.Dispose();
        mainThreadRec.Dispose();
        renderThreadRec.Dispose();
    }

    void Update()
    {
        if (!started) return;

        phaseTime += Time.unscaledDeltaTime;

        switch (phase)
        {
            case Phase.StaticWarmup:
                if (phaseTime >= staticWarmup) BeginMeasure(staticStats, Phase.StaticMeasure);
                break;

            case Phase.StaticMeasure:
                Sample(staticStats);
                if (phaseTime >= staticMeasure)
                {
                    Capture($"08_{enemyCount}_enemies_static.png");
                    // 切到全量仿真；此时敌人仍保持初始分布
                    Time.timeScale = 1f;
                    BeginPhase(Phase.LiveWarmup);
                }
                break;

            case Phase.LiveWarmup:
                if (phaseTime >= liveWarmup) BeginMeasure(liveStats, Phase.LiveMeasure);
                break;

            case Phase.LiveMeasure:
                Sample(liveStats);
                if (phaseTime >= liveMeasure)
                {
                    Capture($"09_{enemyCount}_enemies_live.png");
                    Time.timeScale = 0f;
                    phase = Phase.Report;
                    // 截图是异步的：先等 0.3s 再显示报告叠加层，否则 09 会被覆盖
                    reportShotTimer = 0.6f;
                }
                break;

            case Phase.Report:
                if (reportShotTimer > 0f)
                {
                    reportShotTimer -= Time.unscaledDeltaTime;
                    if (string.IsNullOrEmpty(report) && reportShotTimer <= 0.3f)
                    {
                        report = BuildReport();
                        Debug.Log(report);
                        WriteReport();
                    }
                    if (reportShotTimer <= 0f)
                    {
                        Capture($"10_{enemyCount}_enemies_report.png");
                        reportShotTimer = -1f;
                        Debug.Log("[Perf] 压测完成");
                    }
                }
                break;
        }
    }

    void BeginPhase(Phase next)
    {
        phase = next;
        phaseTime = 0f;
    }

    void BeginMeasure(Stats s, Phase next)
    {
        s.frameMs.Clear();
        s.sumCpuMain = s.sumCpuRender = s.sumGpu = 0;
        s.cpuSamples = s.gpuSamples = 0;
        s.sumDraw = s.sumSetPass = s.sumBatches = 0;
        s.drawSamples = 0;
        s.drawMax = s.setPassMax = s.batchesMax = 0;
        s.sumEnemyDraw = s.sumShadowDraw = s.sumInstances = 0;
        s.enemyDrawSamples = 0;
        s.enemyDrawMax = s.shadowDrawMax = s.instancesMax = 0;
        s.frameStart = Time.frameCount;
        BeginPhase(next);
    }

    void Sample(Stats s)
    {
        float dtMs = Time.unscaledDeltaTime * 1000f;
        if (dtMs > 0f) s.frameMs.Add(dtMs);

        FrameTimingManager.CaptureFrameTimings();
        var timing = new FrameTiming[1];
        if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
        {
            s.sumCpuMain += timing[0].cpuMainThreadFrameTime;
            s.sumCpuRender += timing[0].cpuRenderThreadFrameTime;
            s.cpuSamples++;
            if (timing[0].gpuFrameTime > 0f)
            {
                s.sumGpu += timing[0].gpuFrameTime;
                s.gpuSamples++;
            }
        }

        if (drawCallsRec.Valid)
        {
            int dc = (int)drawCallsRec.LastValue;
            int sp = setPassRec.Valid ? (int)setPassRec.LastValue : 0;
            int ba = batchesRec.Valid ? (int)batchesRec.LastValue : 0;
            s.sumDraw += dc;
            s.sumSetPass += sp;
            s.sumBatches += ba;
            if (dc > s.drawMax) s.drawMax = dc;
            if (sp > s.setPassMax) s.setPassMax = sp;
            if (ba > s.batchesMax) s.batchesMax = ba;
            s.drawSamples++;
        }

        if (sim != null)
        {
            int ed = sim.LastEnemyDrawCalls;
            int sd = sim.LastShadowDrawCalls;
            int inst = sim.LastRenderedInstances;
            s.sumEnemyDraw += ed;
            s.sumShadowDraw += sd;
            s.sumInstances += inst;
            if (ed > s.enemyDrawMax) s.enemyDrawMax = ed;
            if (sd > s.shadowDrawMax) s.shadowDrawMax = sd;
            if (inst > s.instancesMax) s.instancesMax = inst;
            s.enemyDrawSamples++;
        }
    }

    int SimAlive() => sim != null ? sim.AliveCount : 0;

    void Capture(string name)
    {
        if (!takeScreenshots) return;
        try
        {
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, name);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Perf] 截图: " + path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Perf] 截图失败: " + e.Message);
        }
    }

    void WriteReport()
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(Path.Combine(OutputDir, $"perf_{enemyCount}_enemies.md"), report, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Perf] 报告写入失败: " + e.Message);
        }
    }

    string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# HD-2D {enemyCount} 敌人性能实测");
        sb.AppendLine();
        sb.AppendLine($"- 时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- Unity: {Application.unityVersion}");
        sb.AppendLine($"- 分辨率: {Screen.width}x{Screen.height}  (vsync=0, targetFrameRate=-1)");
        sb.AppendLine($"- GPU: {SystemInfo.graphicsDeviceName}");
        sb.AppendLine($"- CPU: {SystemInfo.processorType}");
        sb.AppendLine($"- 敌人数量: {SimAlive()}  (请求 {enemyCount})");
        sb.AppendLine($"- 类型分布: " + DescribeSpawns());
        sb.AppendLine();

        AppendStats(sb, "阶段 A：纯渲染（timeScale=0，静止分布）", staticStats);
        AppendStats(sb, "阶段 B：全量仿真（timeScale=1，Burst 移动 + 空间哈希 + 渲染）", liveStats);

        sb.AppendLine("## Profiler 计数器诊断");
        sb.AppendLine(profilerInfo);
        sb.AppendLine();

        sb.AppendLine("## 结论提示");
        sb.AppendLine("- Draw Calls 主要由贴图批次数决定：每种敌人贴图 1~2 个实例化 draw call，阴影额外 1~2 个。");
        sb.AppendLine("- 若 GPU 耗时明显高于 CPU 主线程，瓶颈在填充率/overdraw（精灵重叠、移轴模糊、Bloom）。");
        sb.AppendLine("- 编辑器内 FrameTimingManager 的 GPU 值可能为 0（受平台/设置限制），以 Profiler 的 GPU 模块为准。");

        return sb.ToString();
    }

    string DescribeSpawns()
    {
        var names = new[] { "NormalA", "NormalB", "Fast", "Tank", "Exploder", "Ranged", "Elite", "Boss" };
        var parts = new List<string>();
        for (int i = 0; i < spawnedByType.Length && i < names.Length; i++)
            if (spawnedByType[i] > 0) parts.Add($"{names[i]}={spawnedByType[i]}");
        return string.Join(", ", parts);
    }

    void AppendStats(StringBuilder sb, string title, Stats s)
    {
        if (s.frameMs.Count == 0)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine("无样本。");
            sb.AppendLine();
            return;
        }

        var sorted = new List<float>(s.frameMs);
        sorted.Sort();
        float worst = sorted[sorted.Count - 1];

        // 剔除 >200ms 的编辑器卡顿帧（资源导入/窗口重绘/截图），避免单次卡顿把均值拉偏
        var steady = new List<float>(sorted.Count);
        foreach (var f in sorted)
            if (f <= 200f) steady.Add(f);
        int outliers = sorted.Count - steady.Count;
        if (steady.Count == 0) steady.AddRange(sorted);

        double sum = 0;
        foreach (var f in s.frameMs) sum += f;
        double steadySum = 0;
        foreach (var f in steady) steadySum += f;
        double rawAvgMs = sum / s.frameMs.Count;
        double avgMs = steadySum / steady.Count;
        float p95 = steady[Mathf.Clamp(Mathf.FloorToInt(steady.Count * 0.95f), 0, steady.Count - 1)];
        float p99 = steady[Mathf.Clamp(Mathf.FloorToInt(steady.Count * 0.99f), 0, steady.Count - 1)];

        sb.AppendLine($"## {title}");
        sb.AppendLine($"- 样本帧数: {s.frameMs.Count}  (累计 {sum:0} ms，剔除 >200ms 卡顿帧 {outliers} 个)");
        sb.AppendLine($"- 平均 FPS（稳定帧）: {1000.0 / avgMs:0.0}");
        sb.AppendLine($"- 平均帧时间（稳定帧）: {avgMs:0.00} ms  (P95 {p95:0.00} ms, P99 {p99:0.00} ms)");
        sb.AppendLine($"- 原始平均帧时间（含卡顿）: {rawAvgMs:0.00} ms，最差帧 {worst:0.00} ms");
        sb.AppendLine($"- 1% Low FPS: {1000.0 / p99:0.0}");

        if (s.cpuSamples > 0)
            sb.AppendLine($"- CPU 主线程: {s.sumCpuMain / s.cpuSamples:0.00} ms  (FrameTimingManager, {s.cpuSamples} 样本)");
        else if (mainThreadRec.Valid)
            sb.AppendLine($"- CPU 主线程: {mainThreadRec.LastValue * 1e-6f:0.00} ms  (ProfilerRecorder 最近值)");
        else
            sb.AppendLine("- CPU 主线程: N/A");

        if (s.cpuSamples > 0)
            sb.AppendLine($"- CPU 渲染线程: {s.sumCpuRender / s.cpuSamples:0.00} ms");
        else if (renderThreadRec.Valid)
            sb.AppendLine($"- CPU 渲染线程: {renderThreadRec.LastValue * 1e-6f:0.00} ms  (ProfilerRecorder 最近值)");
        else
            sb.AppendLine("- CPU 渲染线程: N/A");

        if (s.gpuSamples > 0)
            sb.AppendLine($"- GPU: {s.sumGpu / s.gpuSamples:0.00} ms  ({s.gpuSamples} 样本)");
        else
            sb.AppendLine("- GPU: N/A（编辑器下 FrameTimingManager 未返回 GPU 时间）");

        if (s.drawSamples > 0)
        {
            sb.AppendLine($"- 场景总 Draw Calls: 平均 {s.sumDraw / s.drawSamples:0.0}, 峰值 {s.drawMax}");
            sb.AppendLine($"- 场景总 SetPass Calls: 平均 {s.sumSetPass / s.drawSamples:0.0}, 峰值 {s.setPassMax}");
            sb.AppendLine($"- 场景总 Batches: 平均 {s.sumBatches / s.drawSamples:0.0}, 峰值 {s.batchesMax}");
        }
        else
        {
            sb.AppendLine("- 场景总 Draw Calls: 指标不可用（见下方 Profiler 计数器诊断）");
        }

        if (s.enemyDrawSamples > 0)
        {
            sb.AppendLine($"- 敌人实例化 Draw Calls: 平均 {s.sumEnemyDraw / s.enemyDrawSamples:0.0}, 峰值 {s.enemyDrawMax}");
            sb.AppendLine($"- 贴地阴影 Draw Calls: 平均 {s.sumShadowDraw / s.enemyDrawSamples:0.0}, 峰值 {s.shadowDrawMax}");
            sb.AppendLine($"- 实际绘制实例数: 平均 {s.sumInstances / s.enemyDrawSamples:0.0}, 峰值 {s.instancesMax}");
        }
        sb.AppendLine();
    }

    void OnGUI()
    {
        if (phase != Phase.Report || string.IsNullOrEmpty(report)) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
        };
        style.normal.textColor = Color.white;

        var rect = new Rect(16f, 16f, Mathf.Min(760f, Screen.width - 32f), Mathf.Min(520f, Screen.height - 32f));
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, report, style);
    }
}
