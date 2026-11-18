using UnityEngine;

namespace RpgGame
{
    // 无 Rigidbody / Collider：预切好的真实网格按脚本轨迹散落，不参与角色物理。
    public sealed class PropFractureEffect : MonoBehaviour
    {
        public int seed = 7113;
        public float lifetime = 1.8f;
        public float crackGap = 0.085f;
        public int PieceCount => pieces != null ? pieces.Length : 0;
        public bool IsBurst { get; private set; }

        Transform[] pieces;
        Vector3[] assembled, starts, velocities, axes, scales;
        Quaternion[] rotations;
        Vector3 center, impactDirection;
        float damage, impactAt = -100f, burstAt;

        void Awake()
        {
            int count = transform.childCount;
            pieces = new Transform[count]; assembled = new Vector3[count]; starts = new Vector3[count];
            velocities = new Vector3[count]; axes = new Vector3[count]; scales = new Vector3[count];
            rotations = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                pieces[i] = transform.GetChild(i); assembled[i] = pieces[i].localPosition;
                scales[i] = pieces[i].localScale; center += assembled[i];
            }
            center /= Mathf.Max(1, count);
        }

        public void ShowCracks(float amount, Vector2 direction)
        {
            damage = Mathf.Clamp01(amount);
            impactAt = Time.time;
            impactDirection = transform.InverseTransformDirection(new Vector3(direction.x, 0f, direction.y));
        }

        public void BreakApart(Vector2 direction)
        {
            if (IsBurst) return;
            IsBurst = true; burstAt = Time.time;
            var random = new System.Random(seed);
            Vector3 force = new Vector3(direction.x, 0f, direction.y);
            for (int i = 0; i < pieces.Length; i++)
            {
                starts[i] = pieces[i].position; rotations[i] = pieces[i].rotation;
                Vector3 radial = starts[i] - transform.TransformPoint(center); radial.y = 0f;
                if (radial.sqrMagnitude < 0.001f) radial = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f);
                velocities[i] = radial.normalized * (0.6f + (float)random.NextDouble() * 1.3f) + force * 0.8f;
                velocities[i].y = 1.1f + (float)random.NextDouble() * 1.4f;
                axes[i] = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()).normalized;
            }
        }

        void Update()
        {
            if (!IsBurst)
            {
                float t = Time.time - impactAt;
                Vector3 shake = impactDirection * (Mathf.Sin(t * 45f) * Mathf.Exp(-t * 13f) * 0.05f);
                for (int i = 0; i < pieces.Length; i++)
                {
                    Vector3 radial = assembled[i] - center;
                    if (radial.sqrMagnitude < 0.001f) radial = Vector3.right;
                    pieces[i].localPosition = assembled[i] + radial.normalized * crackGap * damage + shake;
                }
                return;
            }
            float elapsed = Time.time - burstAt;
            float fade = Mathf.Clamp01((elapsed - lifetime + 0.4f) / 0.4f);
            for (int i = 0; i < pieces.Length; i++)
            {
                float h = Mathf.Max(0f, starts[i].y - 0.08f);
                float vy = velocities[i].y;
                float land = (vy + Mathf.Sqrt(vy * vy + 18f * h)) / 9f;
                float flight = Mathf.Min(elapsed, land);
                Vector3 position = starts[i] + velocities[i] * flight - Vector3.up * (4.5f * flight * flight);
                if (elapsed > land)
                {
                    float slide = (1f - Mathf.Exp(-(elapsed - land) * 7f)) / 7f;
                    position += new Vector3(velocities[i].x, 0f, velocities[i].z) * slide;
                    position.y = 0.08f;
                }
                pieces[i].position = position;
                pieces[i].rotation = Quaternion.AngleAxis(flight * (150f + i * 17f), axes[i]) * rotations[i];
                pieces[i].localScale = scales[i] * (1f - fade);
            }
            if (elapsed >= lifetime) Destroy(gameObject);
        }
    }
}
