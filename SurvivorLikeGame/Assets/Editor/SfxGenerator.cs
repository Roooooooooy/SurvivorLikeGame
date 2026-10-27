using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// W3 占位音效生成器：用正弦/噪声包络合成 6 个短 WAV（16-bit PCM / 22050Hz），
// 写入 Assets/Resources/Audio/SFX，随后由 Unity 自动生成 .meta（GUID 不手写）。
// 放在 Resources 下，打包后 AudioManager 才能用 Resources.Load 找到占位音效。
// 只用于开发期占位，正式音效直接替换同名文件即可，AudioManager 不关心来源。
public static class SfxGenerator
{
    private const int SampleRate = 22050;
    private const string OutputDir = "Assets/Resources/Audio/SFX";

    private static readonly string[] ClipNames = { "hit", "kill", "pickup", "levelup", "hurt", "shoot" };
    private static readonly System.Random noise = new System.Random(20261027);

    // ---------------- 生成入口 ----------------

    [MenuItem("Tools/Survivor/Generate W3 Placeholder SFX")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(OutputDir);

        // 命中：短促的下行扫频 + 噪声，打击感偏“闷”
        Generate("hit", 0.09f, t =>
            Chirp(320f, 120f, t, 0.09f) * 0.6f + Noise() * 0.4f);

        // 击杀：更长的下坠 + 噪声
        Generate("kill", 0.22f, t =>
            Chirp(420f, 70f, t, 0.22f) * 0.55f + Noise() * 0.45f);

        // 拾取：两声上行短音（880 → 1320Hz）
        Generate("pickup", 0.13f, t =>
        {
            const float seg = 0.065f;
            float freq = t < seg ? 880f : 1320f;
            return Sine(freq, t < seg ? t : t - seg) * 0.8f;
        });

        // 升级：C5-E5-G5-C6 琶音，每个音单独包络
        Generate("levelup", 0.32f, t =>
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float seg = 0.08f;
            int index = Mathf.Clamp((int)(t / seg), 0, notes.Length - 1);
            float local = t - index * seg;
            float freq = notes[index];
            return Sine(freq, local) * 0.7f + Sine(freq * 2f, local) * 0.15f;
        }, t =>
        {
            const float seg = 0.08f;
            int index = Mathf.Clamp((int)(t / seg), 0, 3);
            return NoteEnvelope(t - index * seg, seg);
        });

        // 受击：低频锯齿 + 噪声，强调“痛感”
        Generate("hurt", 0.2f, t =>
        {
            float saw = 2f * (t * 160f - Mathf.Floor(t * 160f)) - 1f;
            return saw * 0.5f + Sine(90f, t) * 0.25f + Noise() * 0.35f;
        });

        // 发射：极短的上行扫频
        Generate("shoot", 0.07f, t =>
            Chirp(700f, 1400f, t, 0.07f) * 0.8f + Noise() * 0.15f);

        // 同步导入，保证随后进入 Play Mode 时 AudioManager 能立刻读到 AudioClip
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[W3][SFX] 已生成 " + ClipNames.Length + " 个占位音效到 " + OutputDir);
    }

    // ---------------- 波形与 WAV 写入 ----------------

    static float Sine(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);

    // 线性扫频：f(t) = f0 + k·t，相位取积分得到 f0·t + k·t²/2
    static float Chirp(float f0, float f1, float t, float duration)
    {
        float k = (f1 - f0) / duration;
        return Mathf.Sin(2f * Mathf.PI * (f0 * t + 0.5f * k * t * t));
    }

    static float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);

    // 默认包络：4ms 起音 + 平方衰减，保证短促不爆音
    static float DecayEnvelope(float t, float duration)
    {
        const float attack = 0.004f;
        float a = Mathf.Clamp01(t / attack);
        float d = Mathf.Clamp01(1f - t / duration);
        return a * d * d;
    }

    // 琶音单音包络：快速起音后衰减
    static float NoteEnvelope(float local, float seg)
    {
        float x = Mathf.Clamp01(local / seg);
        return Mathf.Clamp01(local / 0.006f) * Mathf.Pow(1f - x, 1.5f);
    }

    static void Generate(string name, float seconds, Func<float, float> wave, Func<float, float> envelope = null)
    {
        int count = Mathf.CeilToInt(SampleRate * seconds);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float env = envelope != null ? envelope(t) : DecayEnvelope(t, seconds);
            samples[i] = Mathf.Clamp(wave(t) * env, -1f, 1f);
        }
        SaveWav(name, samples);
    }

    static void SaveWav(string name, float[] samples)
    {
        string path = Path.Combine(OutputDir, name + ".wav");
        int dataSize = samples.Length * 2;

        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);              // fmt 块大小
            writer.Write((short)1);        // PCM
            writer.Write((short)1);        // 单声道
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);  // 字节率 = 采样率 × 声道 × 位深/8
            writer.Write((short)2);        // 块对齐
            writer.Write((short)16);       // 16-bit
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            for (int i = 0; i < samples.Length; i++)
                writer.Write((short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f));
        }
    }

    // ---------------- 运行期验证（Play Mode） ----------------

    private static int verifyDelayFrames;

    [MenuItem("Tools/Survivor/Verify W3 Audio (Play Mode)")]
    public static void VerifyInPlayMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[W3][VERIFY] 请先进入 Play Mode 再执行该菜单");
            return;
        }

        Time.timeScale = 1f;
        // 等两帧让 WeaponManager.Start / 武器 Start 完成
        verifyDelayFrames = 2;
        EditorApplication.update -= TickVerify;
        EditorApplication.update += TickVerify;
    }

    static void TickVerify()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.update -= TickVerify;
            return;
        }
        if (verifyDelayFrames > 0)
        {
            verifyDelayFrames--;
            return;
        }
        EditorApplication.update -= TickVerify;
        RunVerification();
    }

    static void RunVerification()
    {
        int pass = 0, fail = 0;
        void Check(bool ok, string msg)
        {
            if (ok) { pass++; Debug.Log("[W3][VERIFY][PASS] " + msg); }
            else { fail++; Debug.LogError("[W3][VERIFY][FAIL] " + msg); }
        }

        var am = AudioManager.Instance;
        Check(am != null, "AudioManager 已懒加载创建");
        if (am == null) { FinishVerify(pass, fail); return; }

        Check(am.PoolCapacity == 10, "播放池容量为 10（实际 " + am.PoolCapacity + "）");
        Check(am.HasAllClips, "6 个占位音效全部加载成功");

        // 1) CombatEvents 订阅
        int hit0 = am.HitPlayCount;
        CombatEvents.RaiseHit(Vector2.zero, 7);
        Check(am.HitPlayCount == hit0 + 1, "CombatEvents.Hit → 命中音效计数 +1");

        int hit1 = am.HitPlayCount;
        CombatEvents.RaiseHit(Vector2.zero, 7);
        Check(am.HitPlayCount == hit1, "命中音效 0.04s 全局节流生效（同帧第二次被丢弃）");

        int kill0 = am.KillPlayCount;
        CombatEvents.RaiseKill(Vector2.one, 3);
        Check(am.KillPlayCount == kill0 + 1, "CombatEvents.Kill → 击杀音效计数 +1");

        int pickup0 = am.PickupPlayCount;
        CombatEvents.RaisePickup(Vector2.left, 2);
        Check(am.PickupPlayCount == pickup0 + 1, "CombatEvents.Pickup → 拾取音效计数 +1");

        // 2) 武器发射音效：篡改 attackTimer 后手动走一遍 Update（与运行期同一条路径）
        var weapons = UnityEngine.Object.FindObjectsByType<WeaponBase>(FindObjectsSortMode.None);
        Check(weapons.Length > 0, "场景中存在 WeaponBase（实际 " + weapons.Length + " 把）");
        int shoot0 = am.ShootPlayCount;
        for (int i = 0; i < weapons.Length; i++)
        {
            ForceWeaponUpdate(weapons[i]);
            if (am.ShootPlayCount > shoot0) break;
        }
        Check(am.ShootPlayCount > shoot0, "WeaponBase.Update → Attack 后播放发射音效");

        // 3) 玩家受击/升级音效
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Check(player != null, "场景中存在 PlayerController");
        if (player != null)
        {
            int hurt0 = am.HurtPlayCount;
            player.TakeDamage(1);
            Check(am.HurtPlayCount == hurt0 + 1, "PlayerController.TakeDamage → 受击音效");

            int lv0 = am.LevelUpPlayCount;
            player.GainExp(player.expToNextLevel);
            Check(am.LevelUpPlayCount > lv0, "PlayerController.LevelUp → 升级音效");
        }

        Debug.Log("[W3][VERIFY][STATE] total=" + am.TotalPlayCount
            + " hit=" + am.HitPlayCount
            + " kill=" + am.KillPlayCount
            + " pickup=" + am.PickupPlayCount
            + " levelup=" + am.LevelUpPlayCount
            + " hurt=" + am.HurtPlayCount
            + " shoot=" + am.ShootPlayCount
            + " activeSources=" + am.ActiveSourceCount
            + " lastClip=" + am.LastClipName);

        FinishVerify(pass, fail);
    }

    static void ForceWeaponUpdate(WeaponBase weapon)
    {
        var type = typeof(WeaponBase);
        var timer = type.GetField("attackTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        timer?.SetValue(weapon, 9999f);
        var update = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        update?.Invoke(weapon, null);
    }

    static void FinishVerify(int pass, int fail)
    {
        if (fail == 0) Debug.Log("[W3][VERIFY][DONE] 全部通过，共 " + pass + " 项");
        else Debug.LogError("[W3][VERIFY][DONE] 通过 " + pass + " 项，失败 " + fail + " 项");
    }
}
