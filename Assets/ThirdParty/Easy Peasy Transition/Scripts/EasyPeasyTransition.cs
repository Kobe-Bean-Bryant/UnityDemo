namespace EasyPeasyTransition
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;
    using UnityEngine.Events;

    public class EasyPeasyTransition : MonoBehaviour
    {
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

        private static EasyPeasyTransition instance;
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
                        go.hideFlags = HideFlags.HideInHierarchy;
                        instance = go.AddComponent<EasyPeasyTransition>();
                    }
                }
                return instance;
            }
        }

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

        private bool isTransitioning = false;

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

        private void InitializeUI()
        {
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

            mainContainer = CreateRect(transform, "MainContainer");
            SetStretch(mainContainer);

            GameObject fsObj = new GameObject("FullScreenPanel");
            fullScreenPanel = fsObj.AddComponent<RectTransform>();
            fullScreenPanel.SetParent(mainContainer, false);
            SetStretch(fullScreenPanel);
            fullScreenImage = fsObj.AddComponent<Image>();
            fullScreenImage.color = transitionColor;
            fullScreenImage.raycastTarget = false;

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
                slices[i].anchorMin = new Vector2(i * 0.2f, -1f);
                slices[i].anchorMax = new Vector2((i + 1) * 0.2f, 2f);
                slices[i].offsetMin = Vector2.zero;
                slices[i].offsetMax = Vector2.zero;
            }

            jawsContainer = CreateRect(mainContainer, "JawsContainer");
            SetStretch(jawsContainer);

            GameObject topGo = new GameObject("JawTop");
            jawTop = topGo.AddComponent<RectTransform>();
            jawTop.SetParent(jawsContainer, false);
            Image topImg = topGo.AddComponent<Image>();
            topImg.color = transitionColor;
            topImg.raycastTarget = false;
            jawTop.anchorMin = new Vector2(0, 0.5f);
            jawTop.anchorMax = new Vector2(1, 1);
            jawTop.offsetMin = Vector2.zero;
            jawTop.offsetMax = Vector2.zero;

            GameObject botGo = new GameObject("JawBottom");
            jawBottom = botGo.AddComponent<RectTransform>();
            jawBottom.SetParent(jawsContainer, false);
            Image botImg = botGo.AddComponent<Image>();
            botImg.color = transitionColor;
            botImg.raycastTarget = false;
            jawBottom.anchorMin = new Vector2(0, 0);
            jawBottom.anchorMax = new Vector2(1, 0.5f);
            jawBottom.offsetMin = Vector2.zero;
            jawBottom.offsetMax = Vector2.zero;

            diamondContainer = CreateRect(mainContainer, "DiamondContainer");
            SetStretch(diamondContainer);
            GameObject diaGo = new GameObject("Diamond");
            diamond = diaGo.AddComponent<RectTransform>();
            diamond.SetParent(diamondContainer, false);
            Image diaImg = diaGo.AddComponent<Image>();
            diaImg.color = transitionColor;
            diaImg.raycastTarget = false;
            diamond.anchorMin = new Vector2(0.5f, 0.5f);
            diamond.anchorMax = new Vector2(0.5f, 0.5f);
            diamond.sizeDelta = new Vector2(5000f, 5000f);

            blindsContainer = CreateRect(mainContainer, "BlindsContainer");
            SetStretch(blindsContainer);
            int blindsCount = 8;
            blinds = new RectTransform[blindsCount];
            float step = 1f / blindsCount;
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

            curtainsContainer = CreateRect(mainContainer, "CurtainsContainer");
            SetStretch(curtainsContainer);

            GameObject cLeftGo = new GameObject("CurtainLeft");
            curtainLeft = cLeftGo.AddComponent<RectTransform>();
            curtainLeft.SetParent(curtainsContainer, false);
            Image cLeftImg = cLeftGo.AddComponent<Image>();
            cLeftImg.color = transitionColor;
            cLeftImg.raycastTarget = false;
            curtainLeft.anchorMin = new Vector2(0, 0);
            curtainLeft.anchorMax = new Vector2(0.5f, 1);
            curtainLeft.offsetMin = Vector2.zero;
            curtainLeft.offsetMax = Vector2.zero;

            GameObject cRightGo = new GameObject("CurtainRight");
            curtainRight = cRightGo.AddComponent<RectTransform>();
            curtainRight.SetParent(curtainsContainer, false);
            Image cRightImg = cRightGo.AddComponent<Image>();
            cRightImg.color = transitionColor;
            cRightImg.raycastTarget = false;
            curtainRight.anchorMin = new Vector2(0.5f, 0);
            curtainRight.anchorMax = new Vector2(1, 1);
            curtainRight.offsetMin = Vector2.zero;
            curtainRight.offsetMax = Vector2.zero;

            gridContainer = CreateRect(mainContainer, "GridContainer");
            SetStretch(gridContainer);
            int gridSize = 4;
            gridCells = new RectTransform[gridSize * gridSize];
            float cellSize = 1f / gridSize;
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

            cornerContainer = CreateRect(mainContainer, "CornerContainer");
            SetStretch(cornerContainer);
            GameObject cornerGo = new GameObject("CornerPanel");
            cornerPanel = cornerGo.AddComponent<RectTransform>();
            cornerPanel.SetParent(cornerContainer, false);
            Image cornerImg = cornerGo.AddComponent<Image>();
            cornerImg.color = transitionColor;
            cornerImg.raycastTarget = false;
            cornerPanel.anchorMin = new Vector2(0, 1);
            cornerPanel.anchorMax = new Vector2(0, 1);
            cornerPanel.pivot = new Vector2(0, 1);
            cornerPanel.sizeDelta = new Vector2(5000f, 5000f);

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
                randomStripDelays[i] = Random.Range(0f, 0.3f);
            }

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

            GameObject clockObj = new GameObject("ClockPanel");
            clockPanel = clockObj.AddComponent<RectTransform>();
            clockPanel.SetParent(mainContainer, false);
            SetStretch(clockPanel);

            clockImage = clockObj.AddComponent<Image>();

            Texture2D whiteTex = Texture2D.whiteTexture;
            clockImage.sprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), new Vector2(0.5f, 0.5f));

            clockImage.type = Image.Type.Filled;
            clockImage.fillMethod = Image.FillMethod.Radial360;
            clockImage.fillOrigin = 2;
            clockImage.color = transitionColor;
            clockImage.raycastTarget = false;

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
                    pixelDelays[idx] = Random.Range(0f, 0.5f);
                }
            }

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
                pinwheelBlades[i].anchorMin = new Vector2(0.5f, 0.5f);
                pinwheelBlades[i].anchorMax = new Vector2(0.5f, 0.5f);
                pinwheelBlades[i].sizeDelta = new Vector2(3000f, 3000f);
                pinwheelBlades[i].pivot = Vector2.zero;
            }

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
                dominoBars[i].pivot = new Vector2(0.5f, 0);
            }

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
                foldingColumns[i].pivot = new Vector2(0, 0.5f);
            }

            DisableAllContainers();
        }

        private RectTransform CreateRect(Transform parent, string objName)
        {
            GameObject go = new GameObject(objName);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

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

        public void PlayTransition(TransitionType type, float? enterDuration = null, float? exitDuration = null, Color? customColor = null, UnityAction onTransitionHalfway = null, UnityAction onTransitionCompleted = null)
        {
            if (isTransitioning) return;
            float inDur = enterDuration ?? transitionDuration;
            float outDur = exitDuration ?? inDur;
            StartCoroutine(TransitionSequence(null, type, inDur, outDur, customColor ?? transitionColor, false, onTransitionHalfway, onTransitionCompleted));
        }

        public void LoadScene(string sceneName, TransitionType type, float? enterDuration = null, float? exitDuration = null, Color? customColor = null, UnityAction onTransitionHalfway = null, UnityAction onTransitionCompleted = null)
        {
            if (isTransitioning) return;
            float inDur = enterDuration ?? transitionDuration;
            float outDur = exitDuration ?? inDur;
            StartCoroutine(TransitionSequence(sceneName, type, inDur, outDur, customColor ?? transitionColor, false, onTransitionHalfway, onTransitionCompleted));
        }

        public void LoadScene(int sceneIndex, TransitionType type, float? enterDuration = null, float? exitDuration = null, Color? customColor = null, UnityAction onTransitionHalfway = null, UnityAction onTransitionCompleted = null)
        {
            if (isTransitioning) return;
            float inDur = enterDuration ?? transitionDuration;
            float outDur = exitDuration ?? inDur;
            StartCoroutine(TransitionSequence(sceneIndex.ToString(), type, inDur, outDur, customColor ?? transitionColor, true, onTransitionHalfway, onTransitionCompleted));
        }

        private IEnumerator TransitionSequence(string sceneId, TransitionType type, float inDuration, float outDuration, Color color, bool isIndex, UnityAction onHalfway, UnityAction onCompleted)
        {
            isTransitioning = true;
            DisableAllContainers();
            UpdateColors(color);

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

            EnableContainerForType(type);
            fullScreenImage.raycastTarget = true;

            float elapsed = 0f;
            while (elapsed < inDuration)
            {
                elapsed += Time.deltaTime;
                float t = inDuration > 0f ? Mathf.Clamp01(elapsed / inDuration) : 1f;
                ApplyAnimation(type, EaseOut(t), true);
                yield return null;
            }
            ApplyAnimation(type, 1f, true);

            onHalfway?.Invoke();

            if (!string.IsNullOrEmpty(sceneId))
            {
                AsyncOperation op = isIndex ? SceneManager.LoadSceneAsync(int.Parse(sceneId)) : SceneManager.LoadSceneAsync(sceneId);
                op.allowSceneActivation = false;
                while (op.progress < 0.9f)
                {
                    yield return null;
                }
                op.allowSceneActivation = true;
                yield return null;
            }
            else
            {
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < outDuration)
            {
                elapsed += Time.deltaTime;
                float t = outDuration > 0f ? Mathf.Clamp01(elapsed / outDuration) : 1f;
                ApplyAnimation(type, EaseIn(1f - t), false);
                yield return null;
            }
            ApplyAnimation(type, 0f, false);

            fullScreenImage.raycastTarget = false;
            DisableAllContainers();
            isTransitioning = false;

            onCompleted?.Invoke();
        }

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

        private void ApplyAnimation(TransitionType type, float t, bool isEntering)
        {
            float sw = Screen.width * 1.5f;
            float sh = Screen.height * 1.5f;

            switch (type)
            {
                case TransitionType.Fade:
                    Color c = fullScreenImage.color;
                    c.a = t;
                    fullScreenImage.color = c;
                    fullScreenPanel.anchoredPosition = Vector2.zero;
                    fullScreenPanel.localScale = Vector3.one;
                    break;

                case TransitionType.SlideLeft:
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
                    for (int i = 0; i < slices.Length; i++)
                    {
                        float delay = i * 0.1f;
                        float localT = Mathf.Clamp01((t - delay) / 0.6f);
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
                    clockImage.fillAmount = t;
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

        private float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private float EaseIn(float t)
        {
            return t * t * t;
        }

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
