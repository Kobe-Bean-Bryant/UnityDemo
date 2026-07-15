namespace EasyPeasyTransition
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;
    using UnityEngine.Events;

    /// <summary>
    /// 全屏过渡效果管理器（持久化单例 + Overlay Canvas）。
    /// 设计要点（中文学习向）：
    /// 1) 持久化单例：Awake 中调用 DontDestroyOnLoad，整个游戏生命周期只保留一个实例；
    ///    其 Overlay Canvas 的 sortingOrder=9999 保证过渡画面始终盖在普通 UI 之上。
    /// 2) 一次性预建/缓存：所有过渡类型用到的几何（整屏面板 / 切片 / 网格 / 百叶窗 …）
    ///    都在 InitializeUI 中一次性创建并缓存为字段，运行期只做"激活 + 位移/缩放/旋转"，
    ///    避免每次过渡都 Instantiate/AddComponent，性能与 GC 都更友好。
    /// 3) 按类型选容器：每种 TransitionType 对应一个独立 Container（如 slicesContainer、
    ///    gridContainer …）。EnableContainerForType 只激活需要的那一个，
    ///    DisableAllContainers 在开始/结束时统一隐藏全部，互不干扰。
    /// 4) 单协程驱动：TransitionSequence 是唯一的状态机协程，固定三段式推进——
    ///    "覆盖(cover) → 中点回调/可选场景加载 → 揭开(uncover)"。
    /// 5) 输入阻断 + 并发保护：过渡期间把 fullScreenImage.raycastTarget 置 true 吃掉点击，
    ///    阻止玩家点到下层 UI；isTransitioning 标志位防止过渡重入。
    /// 6) ApplyAnimation：把归一化进度 t∈[0,1] 翻译成具体的 Transform 变化
    ///    （anchoredPosition / localScale / localRotation / Image.fillAmount），
    ///    是所有视觉效果的"解释器"。
    /// </summary>
    public class EasyPeasyTransition : MonoBehaviour
    {
        // ===== 过渡类型枚举：每个值对应"一种几何容器 + 一个 ApplyAnimation 分支" =====
        public enum TransitionType
        {
            Fade,
            SlideLeft,
            SlideRight,
            SlideTop,
            SlideBottom,
            VerticalSlices,
            Jaws,
            DiamondSpin,
            HorizontalBlinds,
            ZoomInOut,
            TheatreCurtains,
            Checkerboard,
            DiagonalBlinds,
            VerticalBlinds,
            CornerWipe,
            RandomHorizontalStrips,
            ZipWipe,
            AlternatingSlices,
            SpiralGrid,
            SpinningLayers,
            ClockWipe,
            MultiLayerSlide,
            PixelDissolve,
            CameraShutter,
            BouncingBars,
            ConcentricSquares,
            Crosshatch,
            Pinwheel,
            Dominoes,
            FoldingColumns
        }

        // ===== 单例与生命周期：懒加载 + 跨场景持久化，任意时刻只存在一个实例 =====
        private static EasyPeasyTransition instance;

        // 公共入口（属性）：首次访问时若场景里没有实例，则自动新建一个隐藏 GameObject 并挂载本脚本。
        public static EasyPeasyTransition Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<EasyPeasyTransition>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("EasyPeasyTransition");
                        // go.hideFlags = HideFlags.HideInHierarchy;
                        instance = go.AddComponent<EasyPeasyTransition>();
                    }
                }
                return instance;
            }
        }

        // ===== 序列化设置 + 运行期缓存的 UI 引用（一次性预建后复用，避免运行期查找/创建）=====
        // 下面先两项是 Inspector 可调参数；其后全部是 InitializeUI 预建并缓存的 Transform/Image。
        [SerializeField] private Color transitionColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        [SerializeField] private float transitionDuration = 0.5f;

        private Canvas transitionCanvas;
        private RectTransform mainContainer;

        private RectTransform fullScreenPanel;
        private Image fullScreenImage;

        private RectTransform slicesContainer;
        private RectTransform[] slices;

        private RectTransform jawsContainer;
        private RectTransform jawTop;
        private RectTransform jawBottom;

        private RectTransform diamondContainer;
        private RectTransform diamond;

        private RectTransform blindsContainer;
        private RectTransform[] blinds;

        private RectTransform curtainsContainer;
        private RectTransform curtainLeft;
        private RectTransform curtainRight;

        private RectTransform gridContainer;
        private RectTransform[] gridCells;

        private RectTransform diagonalContainer;
        private RectTransform[] diagonalBlinds;

        private RectTransform vBlindsContainer;
        private RectTransform[] vBlinds;

        private RectTransform cornerContainer;
        private RectTransform cornerPanel;

        private RectTransform randomStripsContainer;
        private RectTransform[] randomStrips;
        private float[] randomStripDelays;

        private RectTransform zipContainer;
        private RectTransform[] zipStrips;

        private RectTransform spinLayersContainer;
        private RectTransform[] spinLayers;

        private RectTransform clockPanel;
        private Image clockImage;

        private RectTransform multiLayersContainer;
        private RectTransform[] multiLayers;

        private RectTransform pixelGridContainer;
        private RectTransform[] pixelCells;
        private float[] pixelDelays;

        private RectTransform shutterContainer;
        private RectTransform[] shutterPanels;

        private RectTransform bounceContainer;
        private RectTransform[] bounceBars;

        private RectTransform concentricContainer;
        private RectTransform[] concentricSquares;

        private RectTransform crosshatchContainer;
        private RectTransform[] crosshatchBars;

        private RectTransform pinwheelContainer;
        private RectTransform[] pinwheelBlades;

        private RectTransform dominoesContainer;
        private RectTransform[] dominoBars;

        private RectTransform foldingContainer;
        private RectTransform[] foldingColumns;

        // 并发保护标志：true 表示有过渡正在播放；各公共 API 在入口处检查它以拒绝重入。
        private bool isTransitioning = false;

        // ===== 生命周期：Awake 绑定单例 + DontDestroyOnLoad 持久化 + 调用 InitializeUI 预建全部 UI =====
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeUI();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        // ===== UI 预建：仅 Awake 调用一次。搭建 Overlay Canvas，并按效果分组预生成全部几何 =====
        // 每个效果块都遵循同一套路：建 Container → SetStretch 铺满 → 循环建子 Image → 设锚点/偏移。
        private void InitializeUI()
        {
            // —— 通用画布：ScreenSpaceOverlay + sortingOrder 9999 + 1920x1080 自适应缩放 + 射线检测器 ——
            transitionCanvas = GetComponent<Canvas>();
            if (transitionCanvas == null)
            {
                transitionCanvas = gameObject.AddComponent<Canvas>();
            }
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 9999;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 根容器：所有过渡几何的父节点，铺满整屏
            mainContainer = CreateRect(transform, "MainContainer");
            SetStretch(mainContainer);

            // —— 单整屏面板：Fade / SlideLeft/Right/Top/Bottom / ZoomInOut 共用这一张全屏 Image ——
            GameObject fsObj = new GameObject("FullScreenPanel");
            fullScreenPanel = fsObj.AddComponent<RectTransform>();
            fullScreenPanel.SetParent(mainContainer, false);
            SetStretch(fullScreenPanel);
            fullScreenImage = fsObj.AddComponent<Image>();
            fullScreenImage.color = transitionColor;
            fullScreenImage.raycastTarget = false; // 默认不拦射线；仅过渡进行中临时打开以挡点击

            // —— 垂直切片组：VerticalSlices / AlternatingSlices 共用这 5 条竖条 ——
            // 容器整体放大 1.5 倍 + 倾斜 10°；配合下方"超出屏幕"的锚点范围，移动时不会露空。
            slicesContainer = CreateRect(mainContainer, "SlicesContainer");
            SetStretch(slicesContainer);
            slicesContainer.localScale = Vector3.one * 1.5f;
            slicesContainer.localRotation = Quaternion.Euler(0, 0, 10f);
            slices = new RectTransform[5];
            for (int i = 0; i < 5; i++)
            {
                GameObject go = new GameObject("Slice_" + i);
                slices[i] = go.AddComponent<RectTransform>();
                slices[i].SetParent(slicesContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                // X 方向按 0.2 步长均分 5 列；Y 方向取 [-1,2] 比屏幕高一倍，旋转/平移时仍能完全覆盖画面
                slices[i].anchorMin = new Vector2(i * 0.2f, -1f);
                slices[i].anchorMax = new Vector2((i + 1) * 0.2f, 2f);
                slices[i].offsetMin = Vector2.zero;
                slices[i].offsetMax = Vector2.zero;
            }

            // —— 双面板（上下颌）：Jaws。上颌占上半屏、下颌占下半屏，分别向上/下滑开 ——
            jawsContainer = CreateRect(mainContainer, "JawsContainer");
            SetStretch(jawsContainer);

            GameObject topGo = new GameObject("JawTop");
            jawTop = topGo.AddComponent<RectTransform>();
            jawTop.SetParent(jawsContainer, false);
            Image topImg = topGo.AddComponent<Image>();
            topImg.color = transitionColor;
            topImg.raycastTarget = false;
            jawTop.anchorMin = new Vector2(0, 0.5f); // 锚点上沿在屏幕中线：上颌占据 [0.5, 1] 的上半屏
            jawTop.anchorMax = new Vector2(1, 1);
            jawTop.offsetMin = Vector2.zero;
            jawTop.offsetMax = Vector2.zero;

            GameObject botGo = new GameObject("JawBottom");
            jawBottom = botGo.AddComponent<RectTransform>();
            jawBottom.SetParent(jawsContainer, false);
            Image botImg = botGo.AddComponent<Image>();
            botImg.color = transitionColor;
            botImg.raycastTarget = false;
            jawBottom.anchorMin = new Vector2(0, 0); // 下颌占据 [0, 0.5] 的下半屏，与上颌在中线对接
            jawBottom.anchorMax = new Vector2(1, 0.5f);
            jawBottom.offsetMin = Vector2.zero;
            jawBottom.offsetMax = Vector2.zero;

            // —— 中心菱形：DiamondSpin。从屏幕中心放大并旋转 180° ——
            diamondContainer = CreateRect(mainContainer, "DiamondContainer");
            SetStretch(diamondContainer);
            GameObject diaGo = new GameObject("Diamond");
            diamond = diaGo.AddComponent<RectTransform>();
            diamond.SetParent(diamondContainer, false);
            Image diaImg = diaGo.AddComponent<Image>();
            diaImg.color = transitionColor;
            diaImg.raycastTarget = false;
            diamond.anchorMin = new Vector2(0.5f, 0.5f); // 锚点在屏幕正中心
            diamond.anchorMax = new Vector2(0.5f, 0.5f);
            diamond.sizeDelta = new Vector2(5000f, 5000f); // 5000 远超任何分辨率，保证缩放过程中始终覆盖整屏

            // —— 水平百叶窗：HorizontalBlinds。8 条横向条带，靠 Y 轴缩放开合 ——
            blindsContainer = CreateRect(mainContainer, "BlindsContainer");
            SetStretch(blindsContainer);
            int blindsCount = 8;
            blinds = new RectTransform[blindsCount];
            float step = 1f / blindsCount; // "等分铺满"套路：每条占屏幕高度的 1/count，第 i 条落在 [i*step, (i+1)*step]
            for (int i = 0; i < blindsCount; i++)
            {
                GameObject go = new GameObject("Blind_" + i);
                blinds[i] = go.AddComponent<RectTransform>();
                blinds[i].SetParent(blindsContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                blinds[i].anchorMin = new Vector2(0f, i * step);
                blinds[i].anchorMax = new Vector2(1f, (i + 1) * step);
                blinds[i].offsetMin = Vector2.zero;
                blinds[i].offsetMax = Vector2.zero;
            }

            // —— 双面板（左右幕布）：TheatreCurtains。左半屏 + 右半屏分别向两侧拉开 ——
            curtainsContainer = CreateRect(mainContainer, "CurtainsContainer");
            SetStretch(curtainsContainer);

            GameObject cLeftGo = new GameObject("CurtainLeft");
            curtainLeft = cLeftGo.AddComponent<RectTransform>();
            curtainLeft.SetParent(curtainsContainer, false);
            Image cLeftImg = cLeftGo.AddComponent<Image>();
            cLeftImg.color = transitionColor;
            cLeftImg.raycastTarget = false;
            curtainLeft.anchorMin = new Vector2(0, 0);   // 左幕布占 [0, 0.5] 左半屏
            curtainLeft.anchorMax = new Vector2(0.5f, 1);
            curtainLeft.offsetMin = Vector2.zero;
            curtainLeft.offsetMax = Vector2.zero;

            GameObject cRightGo = new GameObject("CurtainRight");
            curtainRight = cRightGo.AddComponent<RectTransform>();
            curtainRight.SetParent(curtainsContainer, false);
            Image cRightImg = cRightGo.AddComponent<Image>();
            cRightImg.color = transitionColor;
            cRightImg.raycastTarget = false;
            curtainRight.anchorMin = new Vector2(0.5f, 0); // 右幕布占 [0.5, 1] 右半屏，与左幕布在中线对接
            curtainRight.anchorMax = new Vector2(1, 1);
            curtainRight.offsetMin = Vector2.zero;
            curtainRight.offsetMax = Vector2.zero;

            // —— 4x4 网格：Checkerboard / SpiralGrid 共用这 16 格，靠逐格缩放显现 ——
            gridContainer = CreateRect(mainContainer, "GridContainer");
            SetStretch(gridContainer);
            int gridSize = 4;
            gridCells = new RectTransform[gridSize * gridSize];
            float cellSize = 1f / gridSize; // 同样的"等分铺满"套路，二维版：每格占 cellSize×cellSize
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    int index = x * gridSize + y;
                    GameObject go = new GameObject("GridCell_" + index);
                    gridCells[index] = go.AddComponent<RectTransform>();
                    gridCells[index].SetParent(gridContainer, false);
                    Image img = go.AddComponent<Image>();
                    img.color = transitionColor;
                    img.raycastTarget = false;
                    gridCells[index].anchorMin = new Vector2(x * cellSize, y * cellSize);
                    gridCells[index].anchorMax = new Vector2((x + 1) * cellSize, (y + 1) * cellSize);
                    gridCells[index].offsetMin = Vector2.zero;
                    gridCells[index].offsetMax = Vector2.zero;
                }
            }

            // —— 对角百叶：DiagonalBlinds。容器整体旋转 45° + 放大 2.5 倍，内部仍是水平百叶 ——
            diagonalContainer = CreateRect(mainContainer, "DiagonalContainer");
            SetStretch(diagonalContainer);
            diagonalContainer.localScale = Vector3.one * 2.5f;
            diagonalContainer.localRotation = Quaternion.Euler(0, 0, 45f);
            int diagCount = 10;
            diagonalBlinds = new RectTransform[diagCount];
            float diagStep = 1f / diagCount;
            for (int i = 0; i < diagCount; i++)
            {
                GameObject go = new GameObject("DiagonalBlind_" + i);
                diagonalBlinds[i] = go.AddComponent<RectTransform>();
                diagonalBlinds[i].SetParent(diagonalContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                diagonalBlinds[i].anchorMin = new Vector2(0f, i * diagStep);
                diagonalBlinds[i].anchorMax = new Vector2(1f, (i + 1) * diagStep);
                diagonalBlinds[i].offsetMin = Vector2.zero;
                diagonalBlinds[i].offsetMax = Vector2.zero;
            }

            // —— 垂直百叶窗：VerticalBlinds。10 条竖向条带，靠 X 轴缩放开合 ——
            vBlindsContainer = CreateRect(mainContainer, "VBlindsContainer");
            SetStretch(vBlindsContainer);
            int vBlindsCount = 10;
            vBlinds = new RectTransform[vBlindsCount];
            float vStep = 1f / vBlindsCount;
            for (int i = 0; i < vBlindsCount; i++)
            {
                GameObject go = new GameObject("VBlind_" + i);
                vBlinds[i] = go.AddComponent<RectTransform>();
                vBlinds[i].SetParent(vBlindsContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                vBlinds[i].anchorMin = new Vector2(i * vStep, 0f);
                vBlinds[i].anchorMax = new Vector2((i + 1) * vStep, 1f);
                vBlinds[i].offsetMin = Vector2.zero;
                vBlinds[i].offsetMax = Vector2.zero;
            }

            // —— 角落擦除：CornerWipe。锚点在左上角、pivot 也在左上角，从角点放大覆盖整屏 ——
            cornerContainer = CreateRect(mainContainer, "CornerContainer");
            SetStretch(cornerContainer);
            GameObject cornerGo = new GameObject("CornerPanel");
            cornerPanel = cornerGo.AddComponent<RectTransform>();
            cornerPanel.SetParent(cornerContainer, false);
            Image cornerImg = cornerGo.AddComponent<Image>();
            cornerImg.color = transitionColor;
            cornerImg.raycastTarget = false;
            cornerPanel.anchorMin = new Vector2(0, 1); // 锚定到左上角顶点
            cornerPanel.anchorMax = new Vector2(0, 1);
            cornerPanel.pivot = new Vector2(0, 1);     // 缩放/旋转轴心放在左上角，于是从角点向外生长
            cornerPanel.sizeDelta = new Vector2(5000f, 5000f);

            // —— 随机水平条带：RandomHorizontalStrips。10 条横条，每条带一个随机延迟，营造"乱序"擦除 ——
            // 注意：这里的延迟只是初始值；协程每次播放该类型时还会重新随机一次（见 TransitionSequence）。
            randomStripsContainer = CreateRect(mainContainer, "RandomStripsContainer");
            SetStretch(randomStripsContainer);
            int randomCount = 10;
            randomStrips = new RectTransform[randomCount];
            randomStripDelays = new float[randomCount];
            float rStep = 1f / randomCount;
            for (int i = 0; i < randomCount; i++)
            {
                GameObject go = new GameObject("RandomStrip_" + i);
                randomStrips[i] = go.AddComponent<RectTransform>();
                randomStrips[i].SetParent(randomStripsContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                randomStrips[i].anchorMin = new Vector2(0f, i * rStep);
                randomStrips[i].anchorMax = new Vector2(1f, (i + 1) * rStep);
                randomStrips[i].offsetMin = Vector2.zero;
                randomStrips[i].offsetMax = Vector2.zero;
                randomStripDelays[i] = Random.Range(0f, 0.3f); // 每条各自的入场延迟，ApplyAnimation 里据此错峰
            }

            // —— 拉链擦除：ZipWipe。10 条横条，奇偶索引从相反方向滑入，像拉链咬合 ——
            zipContainer = CreateRect(mainContainer, "ZipContainer");
            SetStretch(zipContainer);
            int zipCount = 10;
            zipStrips = new RectTransform[zipCount];
            float zStep = 1f / zipCount;
            for (int i = 0; i < zipCount; i++)
            {
                GameObject go = new GameObject("ZipStrip_" + i);
                zipStrips[i] = go.AddComponent<RectTransform>();
                zipStrips[i].SetParent(zipContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                zipStrips[i].anchorMin = new Vector2(0f, i * zStep);
                zipStrips[i].anchorMax = new Vector2(1f, (i + 1) * zStep);
                zipStrips[i].offsetMin = Vector2.zero;
                zipStrips[i].offsetMax = Vector2.zero;
            }

            // —— 多层旋转：SpinningLayers。3 个全屏层从中心放大并各自旋转 ——
            spinLayersContainer = CreateRect(mainContainer, "SpinLayersContainer");
            SetStretch(spinLayersContainer);
            int spinCount = 3;
            spinLayers = new RectTransform[spinCount];
            for (int i = 0; i < spinCount; i++)
            {
                GameObject go = new GameObject("SpinLayer_" + i);
                spinLayers[i] = go.AddComponent<RectTransform>();
                spinLayers[i].SetParent(spinLayersContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                spinLayers[i].anchorMin = new Vector2(0.5f, 0.5f);
                spinLayers[i].anchorMax = new Vector2(0.5f, 0.5f);
                spinLayers[i].sizeDelta = new Vector2(5000f, 5000f);
            }

            // —— 时钟擦除：ClockWipe。用 Image 的 Filled 类型，按 fillAmount 从 0→1 做 Radial360 扇形扫描 ——
            GameObject clockObj = new GameObject("ClockPanel");
            clockPanel = clockObj.AddComponent<RectTransform>();
            clockPanel.SetParent(mainContainer, false);
            SetStretch(clockPanel);

            clockImage = clockObj.AddComponent<Image>();

            // 用纯白纹理生成一个 Sprite 作为填充载体（颜色由 clockImage.color 控制）
            Texture2D whiteTex = Texture2D.whiteTexture;
            clockImage.sprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), new Vector2(0.5f, 0.5f));

            clockImage.type = Image.Type.Filled;
            clockImage.fillMethod = Image.FillMethod.Radial360;
            clockImage.fillOrigin = 2; // 2 = Top，从顶部开始顺时针扫描
            clockImage.color = transitionColor;
            clockImage.raycastTarget = false;

            // —— 多层滑入：MultiLayerSlide。3 个全屏层错峰滑入，且叠加不同透明度形成层次感 ——
            multiLayersContainer = CreateRect(mainContainer, "MultiLayersContainer");
            SetStretch(multiLayersContainer);
            multiLayers = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject go = new GameObject("MultiLayer_" + i);
                multiLayers[i] = go.AddComponent<RectTransform>();
                multiLayers[i].SetParent(multiLayersContainer, false);
                SetStretch(multiLayers[i]);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
            }

            // —— 8x8 像素溶解：PixelDissolve。64 个小格各自带随机延迟逐个浮现，呈现"散点溶解"质感 ——
            // 与 RandomHorizontalStrips 一样，pixelDelays 在每次播放时会被协程重新随机。
            pixelGridContainer = CreateRect(mainContainer, "PixelGridContainer");
            SetStretch(pixelGridContainer);
            int pSize = 8;
            pixelCells = new RectTransform[pSize * pSize];
            pixelDelays = new float[pSize * pSize];
            float pStep = 1f / pSize;
            for (int x = 0; x < pSize; x++)
            {
                for (int y = 0; y < pSize; y++)
                {
                    int idx = x * pSize + y;
                    GameObject go = new GameObject("PixelCell_" + idx);
                    pixelCells[idx] = go.AddComponent<RectTransform>();
                    pixelCells[idx].SetParent(pixelGridContainer, false);
                    Image img = go.AddComponent<Image>();
                    img.color = transitionColor;
                    img.raycastTarget = false;
                    pixelCells[idx].anchorMin = new Vector2(x * pStep, y * pStep);
                    pixelCells[idx].anchorMax = new Vector2((x + 1) * pStep, (y + 1) * pStep);
                    pixelCells[idx].offsetMin = Vector2.zero;
                    pixelCells[idx].offsetMax = Vector2.zero;
                    pixelDelays[idx] = Random.Range(0f, 0.5f); // 每格独立延迟，呈现"溶解/散点"质感
                }
            }

            // —— 四向快门：CameraShutter。左右上下 4 块面板同时向中心合拢/打开 ——
            // 下面先建 4 个面板，再用 anchor 把它们分别摆到左半/右半/上半/下半屏。
            shutterContainer = CreateRect(mainContainer, "ShutterContainer");
            SetStretch(shutterContainer);
            shutterPanels = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject go = new GameObject("Shutter_" + i);
                shutterPanels[i] = go.AddComponent<RectTransform>();
                shutterPanels[i].SetParent(shutterContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
            }
            shutterPanels[0].anchorMin = new Vector2(0, 0);
            shutterPanels[0].anchorMax = new Vector2(0.5f, 1);
            shutterPanels[1].anchorMin = new Vector2(0.5f, 0);
            shutterPanels[1].anchorMax = new Vector2(1, 1);
            shutterPanels[2].anchorMin = new Vector2(0, 0.5f);
            shutterPanels[2].anchorMax = new Vector2(1, 1);
            shutterPanels[3].anchorMin = new Vector2(0, 0);
            shutterPanels[3].anchorMax = new Vector2(1, 0.5f);
            for (int i = 0; i < shutterPanels.Length; i++)
            {
                shutterPanels[i].offsetMin = Vector2.zero;
                shutterPanels[i].offsetMax = Vector2.zero;
            }

            // —— 弹跳竖条：BouncingBars。7 根竖条错峰从下方弹入，用 Overshoot 缓动出回弹感 ——
            bounceContainer = CreateRect(mainContainer, "BounceContainer");
            SetStretch(bounceContainer);
            int bCount = 7;
            bounceBars = new RectTransform[bCount];
            float bStep = 1f / bCount;
            for (int i = 0; i < bCount; i++)
            {
                GameObject go = new GameObject("BounceBar_" + i);
                bounceBars[i] = go.AddComponent<RectTransform>();
                bounceBars[i].SetParent(bounceContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                bounceBars[i].anchorMin = new Vector2(i * bStep, 0);
                bounceBars[i].anchorMax = new Vector2((i + 1) * bStep, 1);
                bounceBars[i].offsetMin = Vector2.zero;
                bounceBars[i].offsetMax = Vector2.zero;
            }

            // —— 同心方块：ConcentricSquares。4 个居中方块逐层放大 + 轻微旋转 + 递增透明度 ——
            concentricContainer = CreateRect(mainContainer, "ConcentricContainer");
            SetStretch(concentricContainer);
            int cCount = 4;
            concentricSquares = new RectTransform[cCount];
            for (int i = 0; i < cCount; i++)
            {
                GameObject go = new GameObject("Concentric_" + i);
                concentricSquares[i] = go.AddComponent<RectTransform>();
                concentricSquares[i].SetParent(concentricContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                concentricSquares[i].anchorMin = new Vector2(0.5f, 0.5f);
                concentricSquares[i].anchorMax = new Vector2(0.5f, 0.5f);
                concentricSquares[i].sizeDelta = new Vector2(5000f, 5000f);
            }

            // —— 十字网格：Crosshatch。前 5 条横向 + 后 5 条竖向，组成"网"状擦除 ——
            crosshatchContainer = CreateRect(mainContainer, "CrosshatchContainer");
            SetStretch(crosshatchContainer);
            int chCount = 10;
            crosshatchBars = new RectTransform[chCount];
            for (int i = 0; i < chCount; i++)
            {
                GameObject go = new GameObject("Crosshatch_" + i);
                crosshatchBars[i] = go.AddComponent<RectTransform>();
                crosshatchBars[i].SetParent(crosshatchContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;

                if (i < 5)
                {
                    float stepH = 1f / 5f;
                    crosshatchBars[i].anchorMin = new Vector2(0, i * stepH);
                    crosshatchBars[i].anchorMax = new Vector2(1, (i + 1) * stepH);
                }
                else
                {
                    float stepV = 1f / 5f;
                    int vIdx = i - 5;
                    crosshatchBars[i].anchorMin = new Vector2(vIdx * stepV, 0);
                    crosshatchBars[i].anchorMax = new Vector2((vIdx + 1) * stepV, 1);
                }
                crosshatchBars[i].offsetMin = Vector2.zero;
                crosshatchBars[i].offsetMax = Vector2.zero;
            }

            // —— 风车：Pinwheel。4 片"叶子"锚定屏幕中心，按 90° 间隔旋转入场 ——
            pinwheelContainer = CreateRect(mainContainer, "PinwheelContainer");
            SetStretch(pinwheelContainer);
            pinwheelBlades = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject go = new GameObject("PinwheelBlade_" + i);
                pinwheelBlades[i] = go.AddComponent<RectTransform>();
                pinwheelBlades[i].SetParent(pinwheelContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                pinwheelBlades[i].anchorMin = new Vector2(0.5f, 0.5f); // 锚定屏幕中心
                pinwheelBlades[i].anchorMax = new Vector2(0.5f, 0.5f);
                pinwheelBlades[i].sizeDelta = new Vector2(3000f, 3000f);
                pinwheelBlades[i].pivot = Vector2.zero; // 旋转轴心放在锚点(屏幕中心)位置，于是绕中心旋转
            }

            // —— 多米诺：Dominoes。10 根竖条，pivot 在底边中点，按 Y 轴缩放从底部"长出" ——
            dominoesContainer = CreateRect(mainContainer, "DominoesContainer");
            SetStretch(dominoesContainer);
            int dCount = 10;
            dominoBars = new RectTransform[dCount];
            float dStep = 1f / dCount;
            for (int i = 0; i < dCount; i++)
            {
                GameObject go = new GameObject("DominoBar_" + i);
                dominoBars[i] = go.AddComponent<RectTransform>();
                dominoBars[i].SetParent(dominoesContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                dominoBars[i].anchorMin = new Vector2(i * dStep, 0);
                dominoBars[i].anchorMax = new Vector2((i + 1) * dStep, 1);
                dominoBars[i].offsetMin = Vector2.zero;
                dominoBars[i].offsetMax = Vector2.zero;
                dominoBars[i].pivot = new Vector2(0.5f, 0); // 底边中点：向上生长
            }

            // —— 折叠竖条：FoldingColumns。4 根竖条，pivot 在左侧中点，按 X 轴缩放像屏风展开 ——
            foldingContainer = CreateRect(mainContainer, "FoldingContainer");
            SetStretch(foldingContainer);
            int fCount = 4;
            foldingColumns = new RectTransform[fCount];
            float fStep = 1f / fCount;
            for (int i = 0; i < fCount; i++)
            {
                GameObject go = new GameObject("FoldingCol_" + i);
                foldingColumns[i] = go.AddComponent<RectTransform>();
                foldingColumns[i].SetParent(foldingContainer, false);
                Image img = go.AddComponent<Image>();
                img.color = transitionColor;
                img.raycastTarget = false;
                foldingColumns[i].anchorMin = new Vector2(i * fStep, 0);
                foldingColumns[i].anchorMax = new Vector2((i + 1) * fStep, 1);
                foldingColumns[i].offsetMin = Vector2.zero;
                foldingColumns[i].offsetMax = Vector2.zero;
                foldingColumns[i].pivot = new Vector2(0, 0.5f); // 左边中点：向右展开
            }

            DisableAllContainers();
        }

        // ===== 构造辅助：CreateRect 统一建 GameObject+RectTransform 并挂父节点；SetStretch 把锚点拉满整屏 =====
        private RectTransform CreateRect(Transform parent, string objName)
        {
            GameObject go = new GameObject(objName);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private void SetStretch(RectTransform rt)
        {
            // anchorMin=零 / anchorMax=一 / offset 归零 = 四角拉满父节点（标准"铺满"写法）
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // 统一隐藏所有效果容器：过渡开始前清场 + 结束后收尾，保证任意时刻只有一个效果可见
        private void DisableAllContainers()
        {
            fullScreenPanel.gameObject.SetActive(false);
            slicesContainer.gameObject.SetActive(false);
            jawsContainer.gameObject.SetActive(false);
            diamondContainer.gameObject.SetActive(false);
            blindsContainer.gameObject.SetActive(false);
            curtainsContainer.gameObject.SetActive(false);
            gridContainer.gameObject.SetActive(false);
            diagonalContainer.gameObject.SetActive(false);
            vBlindsContainer.gameObject.SetActive(false);
            cornerContainer.gameObject.SetActive(false);
            randomStripsContainer.gameObject.SetActive(false);
            zipContainer.gameObject.SetActive(false);
            spinLayersContainer.gameObject.SetActive(false);
            clockPanel.gameObject.SetActive(false);
            multiLayersContainer.gameObject.SetActive(false);
            pixelGridContainer.gameObject.SetActive(false);
            shutterContainer.gameObject.SetActive(false);
            bounceContainer.gameObject.SetActive(false);
            concentricContainer.gameObject.SetActive(false);
            crosshatchContainer.gameObject.SetActive(false);
            pinwheelContainer.gameObject.SetActive(false);
            dominoesContainer.gameObject.SetActive(false);
            foldingContainer.gameObject.SetActive(false);
        }

        // ===== 公共入口：三组 API。都先做并发检查，再把参数收拢后交给同一协程 TransitionSequence =====
        // 回调时机：onTransitionHalfway 在"覆盖完成、揭开之前"触发——这正是切换场景/数据的最佳时机；
        //           onTransitionCompleted 在整段过渡彻底结束后触发。两者均可为 null。
        // duration/color 为 null 时回退到序列化字段 transitionDuration / transitionColor。

        // 仅播放过渡（不切场景）。
        public void PlayTransition(TransitionType type, float? enterDuration = null, float? exitDuration = null, Color? customColor = null, UnityAction onTransitionHalfway = null, UnityAction onTransitionCompleted = null)
        {
            if (isTransitioning) return; // 并发保护：已有过渡进行中则直接忽略
            float inDur = enterDuration ?? transitionDuration;
            float outDur = exitDuration ?? inDur; // 出场时长未指定时，回退成与入场相同
            StartCoroutine(TransitionSequence(null, type, inDur, outDur, customColor ?? transitionColor, false, onTransitionHalfway, onTransitionCompleted));
        }

        // 过渡切场景（按名字）。中点处异步加载目标场景，在画面被完全盖住期间完成切换。
        public void LoadScene(string sceneName, TransitionType type, float? enterDuration = null, float? exitDuration = null, Color? customColor = null, UnityAction onTransitionHalfway = null, UnityAction onTransitionCompleted = null)
        {
            if (isTransitioning) return;
            float inDur = enterDuration ?? transitionDuration;
            float outDur = exitDuration ?? inDur;
            StartCoroutine(TransitionSequence(sceneName, type, inDur, outDur, customColor ?? transitionColor, false, onTransitionHalfway, onTransitionCompleted));
        }

        // 过渡切场景（按 build index）。isIndex=true 标记走 int 重载的 SceneManager.LoadSceneAsync。
        public void LoadScene(int sceneIndex, TransitionType type, float? enterDuration = null, float? exitDuration = null, Color? customColor = null, UnityAction onTransitionHalfway = null, UnityAction onTransitionCompleted = null)
        {
            if (isTransitioning) return;
            float inDur = enterDuration ?? transitionDuration;
            float outDur = exitDuration ?? inDur;
            StartCoroutine(TransitionSequence(sceneIndex.ToString(), type, inDur, outDur, customColor ?? transitionColor, true, onTransitionHalfway, onTransitionCompleted));
        }

        // ===== 协程状态机：唯一的过渡驱动器。三段式 = 覆盖(cover) → 中点(回调/场景加载) → 揭开(uncover) =====
        // isEntering=true 对应"覆盖"阶段（传给 ApplyAnimation 的 t 由 0→1）；
        // isEntering=false 对应"揭开"阶段（t 先翻转成 1-t 再传入，于是视觉上是 1→0 收回）。
        private IEnumerator TransitionSequence(string sceneId, TransitionType type, float inDuration, float outDuration, Color color, bool isIndex, UnityAction onHalfway, UnityAction onCompleted)
        {
            isTransitioning = true;
            DisableAllContainers(); // 清场：先关掉所有容器，再只开需要的那一个
            UpdateColors(color);    // 把本次颜色一次性刷到所有缓存 Image 上

            // 这两个"随机延迟"类型每次播放都要重新随机，保证每次质感不同
            if (type == TransitionType.RandomHorizontalStrips)
            {
                for (int i = 0; i < randomStripDelays.Length; i++)
                {
                    randomStripDelays[i] = Random.Range(0f, 0.4f);
                }
            }
            else if (type == TransitionType.PixelDissolve)
            {
                for (int i = 0; i < pixelDelays.Length; i++)
                {
                    pixelDelays[i] = Random.Range(0f, 0.5f);
                }
            }

            EnableContainerForType(type);           // 只激活目标类型对应的容器
            fullScreenImage.raycastTarget = true;   // 打开射线拦截：过渡期间吃掉所有点击，防止误触下层 UI

            // —— 阶段一：覆盖（cover）。t 从 0 缓动到 1，套 EaseOut 让收尾干脆 ——
            float elapsed = 0f;
            while (elapsed < inDuration)
            {
                elapsed += Time.deltaTime;
                float t = inDuration > 0f ? Mathf.Clamp01(elapsed / inDuration) : 1f; // 时长为 0 时直接置 1，跳过过渡
                ApplyAnimation(type, EaseOut(t), true);
                yield return null; // 每帧推进一次
            }
            ApplyAnimation(type, 1f, true); // 收尾对齐到 t=1，避免最后一帧的浮点误差导致没盖满

            // 覆盖完成 = 过渡中点。此时屏幕已被完全盖住，是触发回调/切场景的安全时机。
            onHalfway?.Invoke();

            // —— 阶段二：中点处理（onHalfway 已触发；若指定了场景，则在此异步加载）——
            // allowSceneActivation=false 先让加载跑到 0.9(就绪) 再放开激活，
            // 确保真正的场景切换发生在画面已被完全遮住的时刻。
            if (!string.IsNullOrEmpty(sceneId))
            {
                AsyncOperation op = isIndex ? SceneManager.LoadSceneAsync(int.Parse(sceneId)) : SceneManager.LoadSceneAsync(sceneId);
                op.allowSceneActivation = false;
                while (op.progress < 0.9f) // 进度到 0.9 即表示加载完成、只差激活
                {
                    yield return null;
                }
                op.allowSceneActivation = true;
                yield return null;
            }
            else
            {
                yield return null; // 没有场景要切时，也等一帧再开始揭开，给中点回调留出执行窗口
            }

            // —— 阶段三：揭开（uncover）。把 1-t 翻转后再 EaseIn，让揭开呈"先快后慢/减速停止"的节奏 ——
            elapsed = 0f;
            while (elapsed < outDuration)
            {
                elapsed += Time.deltaTime;
                float t = outDuration > 0f ? Mathf.Clamp01(elapsed / outDuration) : 1f;
                ApplyAnimation(type, EaseIn(1f - t), false); // 注意这里传入的是 1-t
                yield return null;
            }
            ApplyAnimation(type, 0f, false); // 收尾对齐到 t=0，确保完全收回

            // —— 收尾：解除输入阻断 + 隐藏所有容器 + 释放并发锁 + 触发完成回调 ——
            fullScreenImage.raycastTarget = false;
            DisableAllContainers();
            isTransitioning = false;

            onCompleted?.Invoke();
        }

        // ===== 颜色与容器选择 =====
        // UpdateColors：把本次过渡色一次性刷到所有缓存的 Image 上——运行期改色无需重建几何。
        private void UpdateColors(Color c)
        {
            fullScreenImage.color = c;
            for (int i = 0; i < slices.Length; i++) slices[i].GetComponent<Image>().color = c;
            jawTop.GetComponent<Image>().color = c;
            jawBottom.GetComponent<Image>().color = c;
            diamond.GetComponent<Image>().color = c;
            for (int i = 0; i < blinds.Length; i++) blinds[i].GetComponent<Image>().color = c;
            curtainLeft.GetComponent<Image>().color = c;
            curtainRight.GetComponent<Image>().color = c;
            for (int i = 0; i < gridCells.Length; i++) gridCells[i].GetComponent<Image>().color = c;
            for (int i = 0; i < diagonalBlinds.Length; i++) diagonalBlinds[i].GetComponent<Image>().color = c;
            for (int i = 0; i < vBlinds.Length; i++) vBlinds[i].GetComponent<Image>().color = c;
            cornerPanel.GetComponent<Image>().color = c;
            for (int i = 0; i < randomStrips.Length; i++) randomStrips[i].GetComponent<Image>().color = c;
            for (int i = 0; i < zipStrips.Length; i++) zipStrips[i].GetComponent<Image>().color = c;
            for (int i = 0; i < spinLayers.Length; i++) spinLayers[i].GetComponent<Image>().color = c;
            clockImage.color = c;
            for (int i = 0; i < multiLayers.Length; i++) multiLayers[i].GetComponent<Image>().color = c;
            for (int i = 0; i < pixelCells.Length; i++) pixelCells[i].GetComponent<Image>().color = c;
            for (int i = 0; i < shutterPanels.Length; i++) shutterPanels[i].GetComponent<Image>().color = c;
            for (int i = 0; i < bounceBars.Length; i++) bounceBars[i].GetComponent<Image>().color = c;
            for (int i = 0; i < concentricSquares.Length; i++) concentricSquares[i].GetComponent<Image>().color = c;
            for (int i = 0; i < crosshatchBars.Length; i++) crosshatchBars[i].GetComponent<Image>().color = c;
            for (int i = 0; i < pinwheelBlades.Length; i++) pinwheelBlades[i].GetComponent<Image>().color = c;
            for (int i = 0; i < dominoBars.Length; i++) dominoBars[i].GetComponent<Image>().color = c;
            for (int i = 0; i < foldingColumns.Length; i++) foldingColumns[i].GetComponent<Image>().color = c;
        }

        // EnableContainerForType：根据 TransitionType 激活对应的那个容器。
        // 多种类型可共用同一容器（如 Fade/Slide*/Zoom 共用 fullScreenPanel，Checkerboard/SpiralGrid 共用 grid）。
        private void EnableContainerForType(TransitionType type)
        {
            switch (type)
            {
                case TransitionType.Fade:
                case TransitionType.SlideLeft:
                case TransitionType.SlideRight:
                case TransitionType.SlideTop:
                case TransitionType.SlideBottom:
                case TransitionType.ZoomInOut:
                    fullScreenPanel.gameObject.SetActive(true);
                    break;
                case TransitionType.VerticalSlices:
                case TransitionType.AlternatingSlices:
                    slicesContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.Jaws:
                    jawsContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.DiamondSpin:
                    diamondContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.HorizontalBlinds:
                    blindsContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.TheatreCurtains:
                    curtainsContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.Checkerboard:
                case TransitionType.SpiralGrid:
                    gridContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.PixelDissolve:
                    pixelGridContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.DiagonalBlinds:
                    diagonalContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.VerticalBlinds:
                    vBlindsContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.CornerWipe:
                    cornerContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.RandomHorizontalStrips:
                    randomStripsContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.ZipWipe:
                    zipContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.SpinningLayers:
                    spinLayersContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.ClockWipe:
                    clockPanel.gameObject.SetActive(true);
                    break;
                case TransitionType.MultiLayerSlide:
                    multiLayersContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.CameraShutter:
                    shutterContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.BouncingBars:
                    bounceContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.ConcentricSquares:
                    concentricContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.Crosshatch:
                    crosshatchContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.Pinwheel:
                    pinwheelContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.Dominoes:
                    dominoesContainer.gameObject.SetActive(true);
                    break;
                case TransitionType.FoldingColumns:
                    foldingContainer.gameObject.SetActive(true);
                    break;
            }
        }

        // ===== 动画派发器：把归一化进度 t∈[0,1] 翻译成具体 Transform 变化。每种 TransitionType 一个 case =====
        // sw/sh 取屏幕尺寸的 1.5 倍作为"屏外起点"，保证面板从画面外滑入时不被看见。
        // 错峰套路（贯穿多个 case）：delay = i * k;  localT = Clamp01((t - delay) / span);
        //   含义是第 i 个元素比第 0 个晚 delay 秒启动，再用 span 走完自己的动画——形成"波浪式"涌现。
        private void ApplyAnimation(TransitionType type, float t, bool isEntering)
        {
            float sw = Screen.width * 1.5f;  // 屏宽 × 1.5：用作水平方向的屏外偏移量
            float sh = Screen.height * 1.5f; // 屏高 × 1.5：用作垂直方向的屏外偏移量

            switch (type)
            {
                case TransitionType.Fade:
                    // 纯透明度过渡：alpha 直接随 t 从 0→1，位置/缩放保持默认
                    Color c = fullScreenImage.color;
                    c.a = t;
                    fullScreenImage.color = c;
                    fullScreenPanel.anchoredPosition = Vector2.zero;
                    fullScreenPanel.localScale = Vector3.one;
                    break;

                case TransitionType.SlideLeft:
                    // 从屏幕右侧(sw)滑到中心(0)：Lerp(sw, 0, t)
                    fullScreenPanel.anchoredPosition = new Vector2(Mathf.Lerp(sw, 0, t), 0);
                    fullScreenPanel.localScale = Vector3.one;
                    break;

                case TransitionType.SlideRight:
                    fullScreenPanel.anchoredPosition = new Vector2(Mathf.Lerp(-sw, 0, t), 0);
                    fullScreenPanel.localScale = Vector3.one;
                    break;

                case TransitionType.SlideTop:
                    fullScreenPanel.anchoredPosition = new Vector2(0, Mathf.Lerp(sh, 0, t));
                    fullScreenPanel.localScale = Vector3.one;
                    break;

                case TransitionType.SlideBottom:
                    fullScreenPanel.anchoredPosition = new Vector2(0, Mathf.Lerp(-sh, 0, t));
                    fullScreenPanel.localScale = Vector3.one;
                    break;

                case TransitionType.ZoomInOut:
                    fullScreenPanel.anchoredPosition = Vector2.zero;
                    fullScreenPanel.localScale = Vector3.one * t;
                    break;

                case TransitionType.VerticalSlices:
                    // 经典错峰：第 i 条延迟 i*0.1s，归一窗口 0.6；yPos 在"覆盖"时从屏外上方滑入中心
                    for (int i = 0; i < slices.Length; i++)
                    {
                        float delay = i * 0.1f;
                        float localT = Mathf.Clamp01((t - delay) / 0.6f);
                        // isEntering 决定方向：覆盖时从 +2倍屏高 滑到 0；揭开时从 0 滑到 -2倍屏高
                        float yPos = isEntering ? Mathf.Lerp(sh * 2f, 0f, localT) : Mathf.Lerp(0f, -sh * 2f, 1f - localT);
                        slices[i].anchoredPosition = new Vector2(0, yPos);
                    }
                    break;

                case TransitionType.Jaws:
                    jawTop.anchoredPosition = new Vector2(0, Mathf.Lerp(sh, 0f, t));
                    jawBottom.anchoredPosition = new Vector2(0, Mathf.Lerp(-sh, 0f, t));
                    break;

                case TransitionType.DiamondSpin:
                    diamond.localScale = Vector3.one * t;
                    diamond.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, 180f, t));
                    break;

                case TransitionType.HorizontalBlinds:
                    for (int i = 0; i < blinds.Length; i++)
                    {
                        blinds[i].localScale = new Vector3(1f, t, 1f);
                    }
                    break;

                case TransitionType.TheatreCurtains:
                    curtainLeft.anchoredPosition = new Vector2(Mathf.Lerp(-sw / 2f, 0f, t), 0);
                    curtainRight.anchoredPosition = new Vector2(Mathf.Lerp(sw / 2f, 0f, t), 0);
                    break;

                case TransitionType.Checkerboard:
                    for (int i = 0; i < gridCells.Length; i++)
                    {
                        float delay = (i % 4) * 0.08f + (i / 4) * 0.08f;
                        float localT = Mathf.Clamp01((t - delay) / 0.52f);
                        gridCells[i].localScale = Vector3.one * localT;
                    }
                    break;

                case TransitionType.DiagonalBlinds:
                    for (int i = 0; i < diagonalBlinds.Length; i++)
                    {
                        float delay = i * 0.05f;
                        float localT = Mathf.Clamp01((t - delay) / 0.5f);
                        diagonalBlinds[i].localScale = new Vector3(1f, localT, 1f);
                    }
                    break;

                case TransitionType.VerticalBlinds:
                    for (int i = 0; i < vBlinds.Length; i++)
                    {
                        vBlinds[i].localScale = new Vector3(t, 1f, 1f);
                    }
                    break;

                case TransitionType.CornerWipe:
                    cornerPanel.localScale = Vector3.one * t * 1.5f;
                    break;

                case TransitionType.RandomHorizontalStrips:
                    for (int i = 0; i < randomStrips.Length; i++)
                    {
                        float localT = Mathf.Clamp01((t - randomStripDelays[i]) / 0.6f);
                        float xPos = isEntering ? Mathf.Lerp(sw, 0f, localT) : Mathf.Lerp(0f, -sw, 1f - localT);
                        randomStrips[i].anchoredPosition = new Vector2(xPos, 0);
                    }
                    break;

                case TransitionType.ZipWipe:
                    for (int i = 0; i < zipStrips.Length; i++)
                    {
                        float delay = i * 0.03f;
                        float localT = Mathf.Clamp01((t - delay) / 0.7f);
                        float startX = (i % 2 == 0) ? sw : -sw;
                        float xPos = isEntering ? Mathf.Lerp(startX, 0f, localT) : Mathf.Lerp(0f, -startX, 1f - localT);
                        zipStrips[i].anchoredPosition = new Vector2(xPos, 0);
                    }
                    break;

                case TransitionType.AlternatingSlices:
                    float altH = Screen.height * 1.5f;
                    if (altH < 3000f) altH = 3000f;
                    for (int i = 0; i < slices.Length; i++)
                    {
                        float delay = i * 0.04f;
                        float localT = Mathf.Clamp01((t - delay) / 0.84f);
                        float dir = (i % 2 == 0) ? altH : -altH;
                        float yPos = isEntering ? Mathf.Lerp(dir, 0f, localT) : Mathf.Lerp(0f, -dir, 1f - localT);
                        slices[i].anchoredPosition = new Vector2(0, yPos);
                    }
                    break;

                case TransitionType.SpiralGrid:
                    int[] spiralOrder = { 0, 1, 2, 3, 7, 11, 15, 14, 13, 12, 8, 4, 5, 6, 10, 9 };
                    for (int i = 0; i < spiralOrder.Length; i++)
                    {
                        int cellIndex = spiralOrder[i];
                        float delay = i * 0.03f;
                        float localT = Mathf.Clamp01((t - delay) / 0.55f);
                        gridCells[cellIndex].localScale = Vector3.one * localT;
                    }
                    break;

                case TransitionType.SpinningLayers:
                    for (int i = 0; i < spinLayers.Length; i++)
                    {
                        float delay = i * 0.1f;
                        float localT = Mathf.Clamp01((t - delay) / 0.8f);
                        spinLayers[i].localScale = Vector3.one * localT;
                        float mult = (i % 2 == 0) ? 1f : -1f;
                        spinLayers[i].localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, 120f * (i + 1) * mult, localT));
                    }
                    break;

                case TransitionType.ClockWipe:
                    clockImage.fillAmount = t; // 扇形填充量随 t 从 0 扫到 1（依赖 InitializeUI 里设的 Radial360/Top 起点）
                    break;

                case TransitionType.MultiLayerSlide:
                    for (int i = 0; i < multiLayers.Length; i++)
                    {
                        float delay = i * 0.15f;
                        float localT = Mathf.Clamp01((t - delay) / 0.7f);
                        float xPos = isEntering ? Mathf.Lerp(sw, 0f, localT) : Mathf.Lerp(0f, -sw, 1f - localT);
                        multiLayers[i].anchoredPosition = new Vector2(xPos, 0);

                        Color baseCol = multiLayers[i].GetComponent<Image>().color;
                        baseCol.a = (i == multiLayers.Length - 1) ? 1f : 0.4f + (i * 0.2f);
                        multiLayers[i].GetComponent<Image>().color = baseCol;
                    }
                    break;

                case TransitionType.PixelDissolve:
                    for (int i = 0; i < pixelCells.Length; i++)
                    {
                        float localT = Mathf.Clamp01((t - pixelDelays[i]) / 0.5f);
                        pixelCells[i].localScale = Vector3.one * localT;
                    }
                    break;

                case TransitionType.CameraShutter:
                    shutterPanels[0].anchoredPosition = new Vector2(Mathf.Lerp(-sw / 2f, 0f, t), 0);
                    shutterPanels[1].anchoredPosition = new Vector2(Mathf.Lerp(sw / 2f, 0f, t), 0);
                    shutterPanels[2].anchoredPosition = new Vector2(0, Mathf.Lerp(sh / 2f, 0f, t));
                    shutterPanels[3].anchoredPosition = new Vector2(0, Mathf.Lerp(-sh / 2f, 0f, t));
                    break;

                case TransitionType.BouncingBars:
                    // 入场用 Overshoot 出回弹感；出场退化为 localT^2（二次缓动），避免收回时还在弹
                    for (int i = 0; i < bounceBars.Length; i++)
                    {
                        float delay = i * 0.05f;
                        float localT = Mathf.Clamp01((t - delay) / 0.7f);
                        float easeVal = isEntering ? Overshoot(localT) : localT * localT;
                        float yPos = isEntering ? Mathf.Lerp(sh, 0f, easeVal) : Mathf.Lerp(0f, -sh, 1f - easeVal);
                        bounceBars[i].anchoredPosition = new Vector2(0, yPos);
                    }
                    break;

                case TransitionType.ConcentricSquares:
                    for (int i = 0; i < concentricSquares.Length; i++)
                    {
                        float delay = i * 0.1f;
                        float localT = Mathf.Clamp01((t - delay) / 0.7f);
                        concentricSquares[i].localScale = Vector3.one * localT;
                        float rotDir = (i % 2 == 0) ? 1f : -1f;
                        concentricSquares[i].localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, 45f * rotDir, localT));

                        Color baseCol = concentricSquares[i].GetComponent<Image>().color;
                        baseCol.a = 0.25f * (i + 1);
                        concentricSquares[i].GetComponent<Image>().color = baseCol;
                    }
                    break;

                case TransitionType.Crosshatch:
                    for (int i = 0; i < crosshatchBars.Length; i++)
                    {
                        float delay = (i % 5) * 0.06f;
                        float localT = Mathf.Clamp01((t - delay) / 0.76f);
                        if (i < 5)
                        {
                            float xPos = isEntering ? Mathf.Lerp(sw, 0f, localT) : Mathf.Lerp(0f, -sw, 1f - localT);
                            crosshatchBars[i].anchoredPosition = new Vector2(xPos, 0);
                        }
                        else
                        {
                            float yPos = isEntering ? Mathf.Lerp(-sh, 0f, localT) : Mathf.Lerp(0f, sh, 1f - localT);
                            crosshatchBars[i].anchoredPosition = new Vector2(0, yPos);
                        }
                    }
                    break;

                case TransitionType.Pinwheel:
                    for (int i = 0; i < pinwheelBlades.Length; i++)
                    {
                        float localT = Mathf.Clamp01(t);
                        pinwheelBlades[i].localScale = Vector3.one * localT;
                        pinwheelBlades[i].localRotation = Quaternion.Euler(0, 0, (i * 90f) + Mathf.Lerp(90f, 0f, localT));
                    }
                    break;

                case TransitionType.Dominoes:
                    for (int i = 0; i < dominoBars.Length; i++)
                    {
                        float delay = i * 0.04f;
                        float localT = Mathf.Clamp01((t - delay) / 0.64f);
                        float easeVal = isEntering ? Overshoot(localT) : localT * localT;
                        dominoBars[i].localScale = new Vector3(1f, easeVal, 1f);
                    }
                    break;

                case TransitionType.FoldingColumns:
                    for (int i = 0; i < foldingColumns.Length; i++)
                    {
                        float delay = i * 0.08f;
                        float localT = Mathf.Clamp01((t - delay) / 0.76f);
                        foldingColumns[i].localScale = new Vector3(localT, 1f, 1f);
                    }
                    break;
            }
        }

        // ===== 缓动函数（纯数学、无副作用）。统一把线性 t 映射成更有"手感"的非线性进度 =====

        // EaseOut：1-(1-t)^3，开头快、收尾慢——用于"覆盖"阶段，让面板干脆盖下。
        private float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        // EaseIn：t^3。就 EaseIn 本身（自变量 0→1）而言是"开头慢、收尾快"。
        // 但揭开阶段传的是 EaseIn(1-t)：自变量 1-t 由 1 递减到 0，值便从 1 快速衰减、末段缓停，
        // 所以视觉呈"先快后慢/减速停止"的收回节奏（与覆盖用的 EaseOut 对称，都是干脆启动、缓停收尾）。
        private float EaseIn(float t)
        {
            return t * t * t;
        }

        // Overshoot：带 1.70158 回弹系数的三次缓动，会略微冲过 1 再回落——用于弹跳/多米诺的回弹质感。
        private float Overshoot(float t)
        {
            if (t == 0f) return 0f;
            if (t == 1f) return 1f;
            float s = 1.70158f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
