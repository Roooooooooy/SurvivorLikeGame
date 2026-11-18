using UnityEngine;

namespace RpgGame
{
    // 把整张帧动画图集切成单帧 Sprite，供动作区的普通 SpriteRenderer 使用。
    // 幸存者 EnemySimulation 是在着色器里按实例选帧，不需要这个组件。
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteSheetFrameView : MonoBehaviour
    {
        [Header("图集布局")]
        public int frameWidth = 32;
        public int frameHeight = 32;
        public int framesPerRow = 10;
        public int row = 0;
        public float pixelsPerUnit = 16f;

        [Header("播放")]
        public bool animate = true;
        public float fps = 10f;

        SpriteRenderer view;
        Sprite[] frames;
        float timer;
        int index;

        void Awake()
        {
            view = GetComponent<SpriteRenderer>();
            BuildFrames();
        }

        void BuildFrames()
        {
            if (view == null || view.sprite == null) return;

            var texture = view.sprite.texture;
            if (texture == null) return;

            int count = Mathf.Max(1, Mathf.Min(
                framesPerRow,
                texture.width / Mathf.Max(1, frameWidth)));

            frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                // Unity 纹理原点在左下角；row 0 是图集最下面一行（行走帧），
                // row 1 才是上面那行死亡动画。
                int y = row * frameHeight;
                if (y + frameHeight > texture.height) y = Mathf.Max(0, texture.height - frameHeight);

                var rect = new Rect(i * frameWidth, y, frameWidth, frameHeight);
                frames[i] = Sprite.Create(
                    texture, rect, new Vector2(0.5f, 0.5f),
                    pixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{name}_frame_{i}";
            }

            index = 0;
            view.sprite = frames[0];
        }

        void Update()
        {
            if (!animate || frames == null || frames.Length <= 1) return;

            timer += Time.deltaTime;
            float step = 1f / Mathf.Max(0.01f, fps);
            while (timer >= step)
            {
                timer -= step;
                index = (index + 1) % frames.Length;
                view.sprite = frames[index];
            }
        }

        void OnDestroy()
        {
            if (frames == null) return;

            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) Destroy(frames[i]);
        }
    }
}
