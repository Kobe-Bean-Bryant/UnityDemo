using System.Collections.Generic;
using UnityDemo.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BricksBreakerDemo
{
    public class GameManager : Singleton<GameManager>
    {
        public Camera Camera { get; private set; }

        private Paddle _paddle;
        private Brick[] _bricks = System.Array.Empty<Brick>();

        protected override void Awake()
        {
            base.Awake();
            transform.localScale = Vector3.one; // 防御：墙体用 localScale 缩放，确保父节点缩放为 1
            Camera = Camera.main;
            InitializeWalls();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy(); // 让 Singleton 基类清空 _instance
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            AcquireLevelReferences();
        }

        // 每次加载新关卡场景时重新获取引用（Zigurous 风格）
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Camera = Camera.main;
            RefreshWalls();
            AcquireLevelReferences();
        }

        private void AcquireLevelReferences()
        {
            _paddle = FindFirstObjectByType<Paddle>();
            _bricks = FindObjectsByType<Brick>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            if (_paddle == null || Camera == null) return;
            _paddle.SetTargetX(ReadMouseWorldX());
            _paddle.SetBallOffsetInput(ReadHorizontalInput()); // 方向键改用途：调节球位
            if (ReadSpacePressed())
                _paddle.LaunchBall();
            if (ReadRPressed())
                ResetGame();
            if (ReadBPressed())
                _paddle.RequestExtraBall();
        }

        // 重置游戏状态：清残留碎片 + 重激活砖块并下落 + 销毁所有球 + 挡板下落后生成新球
        public void ResetGame()
        {
            Brick.ClearFragments();

            foreach (var brick in _bricks)
            {
                if (brick == null) continue;
                brick.gameObject.SetActive(true);
                brick.PlaySpawnAnimation();
            }

            foreach (var ball in FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                Destroy(ball.gameObject);
            }

            _paddle.PlaySpawnAnimation();
        }

        #region 输入读取

        private float ReadHorizontalInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                float h = 0f;
                if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) h -= 1f;
                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) h += 1f;
                return h;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return Input.GetAxisRaw("Horizontal");
#else
            return 0f;
#endif
        }

        private bool ReadSpacePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.spaceKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        private bool ReadRPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.rKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }

        private bool ReadBPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.bKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return Input.GetKeyDown(KeyCode.B);
#else
            return false;
#endif
        }

        private float ReadMouseWorldX()
        {
            Vector3 screen = ReadMousePosition();
            screen.z = -Camera.transform.position.z; // 投影到 z=0 游戏平面
            return Camera.ScreenToWorldPoint(screen).x;
        }

        private Vector2 ReadMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        #endregion

        #region 边界墙管理

        public const float WallThickness = 1f;
        // 缓存每面墙的 Collider，创建后仍可移动/缩放，无需销毁重建
        private readonly Dictionary<WallSide, BoxCollider2D> _walls = new Dictionary<WallSide, BoxCollider2D>();

        /// <summary>
        /// 创建 4 面屏幕边界墙（仅初始化调用一次）。
        /// 之后如需贴合新相机 / 新分辨率，请调用 <see cref="RefreshWalls"/>。
        /// </summary>
        public void InitializeWalls()
        {
            if (Camera == null)
            {
                Debug.LogError("[GameManager] Camera 未就绪，无法创建边界");
                return;
            }

            if (!Camera.orthographic)
            {
                Debug.LogError("[GameManager] 边界 Collider 仅支持正交相机");
                return;
            }

            CreateWall(WallSide.Top);
            CreateWall(WallSide.Bottom);
            CreateWall(WallSide.Left);
            CreateWall(WallSide.Right);

            ApplyWallsLayout();
        }

        /// <summary>
        /// 手动刷新边界，使其贴合当前相机。
        /// 传入 <paramref name="newCamera"/> 可切换到新相机；不传则沿用当前 Camera。
        /// </summary>
        public void RefreshWalls(Camera newCamera = null)
        {
            if (newCamera != null)
            {
                Camera = newCamera;
            }

            if (Camera == null)
            {
                Debug.LogError("[GameManager] Camera 未就绪，无法刷新边界");
                return;
            }

            if (!Camera.orthographic)
            {
                Debug.LogError("[GameManager] 边界 Collider 仅支持正交相机");
                return;
            }

            ApplyWallsLayout();
        }

        private void CreateWall(WallSide side)
        {
            var wallObj = new GameObject($"Wall_{side}");
            wallObj.transform.SetParent(transform);

            var wall = wallObj.AddComponent<Wall>();
            wall.Side = side;

            var sr = wallObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhiteSprite();
            sr.color = Color.white;

            _walls[side] = wallObj.AddComponent<BoxCollider2D>();
        }

        // 生成 1×1 白色方块精灵（缓存复用），供墙体 SpriteRenderer 使用
        private static Sprite _whiteSprite;

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null && _whiteSprite.texture != null) return _whiteSprite;
            var tex = new Texture2D(4, 4);
            Color[] px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            return _whiteSprite;
        }

        // 计算并应用 4 面墙的位置 / 尺寸，Initialize 与 Refresh 共用
        private void ApplyWallsLayout()
        {
            if (Camera == null || _walls.Count == 0) return;

            float halfHeight = Camera.orthographicSize;
            float halfWidth = halfHeight * Camera.aspect;
            float t = WallThickness;

            // 水平墙贴屏幕内侧上/下边缘；四角与竖墙自然重叠不留缝
            LayoutWall(WallSide.Top, new Vector3(0f, halfHeight - t / 2f, 0f), new Vector2(halfWidth * 2f, t));
            LayoutWall(WallSide.Bottom, new Vector3(0f, -halfHeight + t / 2f, 0f), new Vector2(halfWidth * 2f, t));
            LayoutWall(WallSide.Left, new Vector3(-halfWidth + t / 2f, 0f, 0f), new Vector2(t, halfHeight * 2f));
            LayoutWall(WallSide.Right, new Vector3(halfWidth - t / 2f, 0f, 0f), new Vector2(t, halfHeight * 2f));
        }

        private void LayoutWall(WallSide side, Vector3 position, Vector2 size)
        {
            if (!_walls.TryGetValue(side, out var collider)) return;
            collider.transform.position = position;
            collider.transform.localScale = new Vector3(size.x, size.y, 1f);
            collider.size = Vector2.one;
        }

        #endregion
    }
}
