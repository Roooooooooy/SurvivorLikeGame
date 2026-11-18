using System.IO;
using UnityEditor;
using UnityEngine;
using RpgGame;

namespace RpgGameEditor
{
    // 离线预览三档 3D 刀光（不进 Play）：借用主相机渲染到 RenderTexture。
    public static class SlashVfxPreview
    {
        const int W = 1536;
        const int H = 512;

        [MenuItem("工具/角色扮演/预览 3D 刀光")]
        public static void Preview()
        {
            var library = AssetDatabase.LoadAssetAtPath<SlashVfxLibrary>(
                "Assets/Resources/FX/SlashVfxLibrary.asset");
            if (library == null)
            {
                Debug.LogError("[SlashPreview] 找不到 SlashVfxLibrary.asset");
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[SlashPreview] 场景里没有 MainCamera");
                return;
            }

            Vector3 savedPos = cam.transform.position;
            Quaternion savedRot = cam.transform.rotation;
            float savedFov = cam.fieldOfView;
            RenderTexture savedTarget = cam.targetTexture;
            CameraClearFlags savedClear = cam.clearFlags;
            Color savedBg = cam.backgroundColor;

            cam.transform.position = new Vector3(0f, 9f, -17f);
            cam.transform.rotation = Quaternion.Euler(27f, 0f, 0f);
            cam.fieldOfView = 35f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.055f, 0.07f, 1f);

            // 三段共用同一颜色；层次靠粒子层数与强度。
            Color tintColor = new Color(1f, 0.80f, 0.38f, 1f);
            float[] xs = { -5.0f, 0f, 5.0f };
            // 三档预览尺寸（与旧三段连招一致）：刀光用判定前缘，拳击用判定中心/半宽。
            float[] frontEdges = { 1.75f, 1.90f, 2.60f };
            float[] punchCenters = { 0.72f, 0.78f, 0.90f };
            float[] punchHalfWidths = { 0.65f, 0.70f, 0.75f };

            var instances = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var entry = library.Get((SlashStyle)i);
                if (entry == null || entry.prefab == null)
                {
                    Debug.LogWarning("[SlashPreview] 档位 " + i + " 未配置预制体");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
                Vector3 p = new Vector3(xs[i], 1.20f, 0f);
                float s;
                if (entry.punchMode)
                {
                    // 拳击：命中点在判定框中心，大小按判定框半宽。
                    p += new Vector3(punchCenters[i], 0f, 0f);
                    s = punchHalfWidths[i]
                        / Mathf.Max(0.01f, entry.localRadius)
                        * Mathf.Max(0.01f, entry.scaleMultiplier);

                    // 主弧在下面的粒子循环里跳过，不 Play。
                }
                else
                {
                    // 刀光：以角色为中心，外缘 = 判定框前缘。
                    s = frontEdges[i] / Mathf.Max(0.01f, entry.localRadius)
                        * Mathf.Max(0.01f, entry.scaleMultiplier);
                }

                go.transform.position = p;
                // 水平弧面 / 径向冲击：绕 Y 对准 +X，再叠加档位偏移。
                go.transform.rotation = Quaternion.Euler(0f, 90f + entry.rotationOffset, 0f);
                go.transform.localScale = Vector3.one * s;

                SetChildActive(go, "Smoke", entry.includeSmoke);
                SetChildActive(go, "Flying", entry.includeFlying);
                SetChildActive(go, "Slash Distortion", entry.includeDistortion);
                SetChildActive(go, "Impact", entry.includeImpact);
                SetChildActive(go, "Point Light", entry.includeLight);

                var light = go.transform.Find("Point Light")?.GetComponent<Light>();
                if (light != null && entry.includeLight)
                {
                    light.color = new Color(tintColor.r, tintColor.g, tintColor.b, 1f);
                    light.intensity = Mathf.Max(0f, entry.lightIntensity);
                }

                float intensity = entry.colorIntensity > 0f ? entry.colorIntensity : 7f;
                Color tint = new Color(tintColor.r * intensity, tintColor.g * intensity,
                    tintColor.b * intensity, 1f);
                var block = new MaterialPropertyBlock();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mat = r.sharedMaterial;
                    if (mat == null || !mat.HasProperty("_Color")) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor("_Color", tint);
                    r.SetPropertyBlock(block);
                    block.Clear();
                }

                float particleIntensity = Mathf.Max(0f, entry.particleIntensity);
                var rootSystem = go.GetComponent<ParticleSystem>();
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (entry.punchMode && ps == rootSystem) continue;
                    if (!ps.gameObject.activeInHierarchy) continue;
                    var main = ps.main;
                    if (ps.gameObject != go)
                    {
                        main.startSizeMultiplier *= particleIntensity;
                        main.startSpeedMultiplier *= Mathf.Lerp(1f, particleIntensity, 0.5f);
                        var emission = ps.emission;
                        emission.rateOverTimeMultiplier *= particleIntensity;
                    }
                    ps.Play(true);
                    ps.Simulate(0.20f, false, false);
                }

                instances[i] = go;
            }

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            string path = Path.Combine(Path.GetTempPath(), "slash_vfx_preview.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[SlashPreview] written " + path);

            cam.targetTexture = savedTarget;
            cam.transform.position = savedPos;
            cam.transform.rotation = savedRot;
            cam.fieldOfView = savedFov;
            cam.clearFlags = savedClear;
            cam.backgroundColor = savedBg;

            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            for (int i = 0; i < instances.Length; i++)
                if (instances[i] != null) Object.DestroyImmediate(instances[i]);
        }

        static void SetChildActive(GameObject root, string childName, bool active)
        {
            var child = root.transform.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }
    }
}
