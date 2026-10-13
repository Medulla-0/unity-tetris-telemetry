using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    [Header("Board")]
    public RectTransform boardContainer;
    public GameObject blockPrefab;
    public GridLayoutGroup backgroundGrid;

    [Header("Hold UI")]
    public GridLayoutGroup holdGrid;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;

    private float cellSize;
    private float holdCellSize;
    private float GameSpeed;
    private bool GamePause = false;
    private bool isGameOver = false;

    private string[] BlocksData = { "I", "O", "T", "L", "J", "S", "Z" };
    private string[] BlockPool = new string[0];
    private string CurrentBlock = null;
    private string CurrentBlock_ID;
    private GameObject[] CurrentBlock_Object_Array = new GameObject[5];
    private int CurrentGrid_Center = 6;
    private Color currentBlockColor = Color.white;

    private string HeldBlock = null;
    private bool canHold = true;
    private GameObject[] holdPreviewObjects = new GameObject[4];

    private GameObject[] ghostBlock_Objects = new GameObject[4];

    private Vector2 touchStartPos;
    private float touchStartTime;
    private const float SwipeThreshold = 50f;
    private const float TapTimeThreshold = 0.3f;

    private void Start()
    {
        if (boardContainer == null)
        {
            Debug.LogError("Board Container atanmamis.");
            return;
        }
        if (backgroundGrid == null)
        {
            Debug.LogError("Background Grid atanmamis.");
            return;
        }
        if (blockPrefab == null)
        {
            Debug.LogError("Block Prefab atanmamis.");
            return;
        }

        Canvas.ForceUpdateCanvases();
        cellSize = boardContainer.rect.width / 10f;
        holdCellSize = cellSize * 0.6f;

        CreateBackgroundGrid();
        CreateGhostBlocks();
        CreateHoldGridIfNeeded();

        GameSpeed = 0.5f;
        CurrentBlock = null;
        HeldBlock = null;
        canHold = true;
        isGameOver = false;

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        StartGameLoop();
    }

    private void Update()
    {
        if (GamePause || isGameOver || string.IsNullOrEmpty(CurrentBlock)) return;

        HandleKeyboardInput();
        HandleTouchInput();
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            if (TryMove(-1, 0)) TelemetryManager.Instance?.RecordMove(-1, 0);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            if (TryMove(1, 0)) TelemetryManager.Instance?.RecordMove(1, 0);

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            TryRotate();

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            if (TryMove(0, 1)) TelemetryManager.Instance?.RecordMove(0, 1);

        if (Input.GetKeyDown(KeyCode.Space))
            HardDrop();

        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            HoldCurrentBlock();
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount <= 0) return;

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                touchStartPos = touch.position;
                touchStartTime = Time.time;
                break;

            case TouchPhase.Ended:
                Vector2 delta = touch.position - touchStartPos;
                float elapsed = Time.time - touchStartTime;

                if (delta.magnitude < SwipeThreshold && elapsed < TapTimeThreshold)
                {
                    TryRotate();
                }
                else if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    if (delta.x > SwipeThreshold && TryMove(1, 0)) TelemetryManager.Instance?.RecordMove(1, 0);
                    else if (delta.x < -SwipeThreshold && TryMove(-1, 0)) TelemetryManager.Instance?.RecordMove(-1, 0);
                }
                else if (delta.y < -SwipeThreshold)
                {
                    HardDrop();
                }
                else if (delta.y > SwipeThreshold)
                {
                    HoldCurrentBlock();
                }
                break;
        }
    }

    public void HoldCurrentBlock()
    {
        if (GamePause || isGameOver || string.IsNullOrEmpty(CurrentBlock) || !canHold) return;

        canHold = false;
        HideGhostPiece();
        ClearActiveBlockObjects();

        TelemetryManager.Instance?.RecordHold();

        if (string.IsNullOrEmpty(HeldBlock))
        {
            HeldBlock = CurrentBlock;
            CurrentBlock = null;
            UpdateHoldPreview();
            SpawnNextBlock();
        }
        else
        {
            string previousHeld = HeldBlock;
            HeldBlock = CurrentBlock;
            CurrentBlock = previousHeld;
            UpdateHoldPreview();
            SetBlock(CurrentBlock);
        }
    }

    private void ClearActiveBlockObjects()
    {
        for (int i = 0; i < CurrentBlock_Object_Array.Length; i++)
        {
            if (CurrentBlock_Object_Array[i] != null)
            {
                CurrentBlock_Object_Array[i].transform.SetParent(null);
                Destroy(CurrentBlock_Object_Array[i]);
                CurrentBlock_Object_Array[i] = null;
            }
        }
    }

    private void CreateHoldGridIfNeeded()
    {
        if (holdGrid == null)
        {
            GameObject holdPanelObj = new GameObject("HoldPanel", typeof(RectTransform), typeof(Image), typeof(Button));
            holdPanelObj.transform.SetParent(boardContainer, false);

            RectTransform panelRt = holdPanelObj.GetComponent<RectTransform>();
            float boxSize = holdCellSize * 4f + 12f;
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(6f, -6f);
            panelRt.sizeDelta = new Vector2(boxSize, boxSize);

            Image panelImg = holdPanelObj.GetComponent<Image>();
            panelImg.color = new Color(0.08f, 0.1f, 0.14f, 0.75f);

            Button holdBtn = holdPanelObj.GetComponent<Button>();
            holdBtn.onClick.AddListener(HoldCurrentBlock);

            GameObject gridObj = new GameObject("HoldGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObj.transform.SetParent(holdPanelObj.transform, false);

            RectTransform gridRt = gridObj.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.5f, 0.5f);
            gridRt.anchorMax = new Vector2(0.5f, 0.5f);
            gridRt.pivot = new Vector2(0.5f, 0.5f);
            gridRt.anchoredPosition = Vector2.zero;
            gridRt.sizeDelta = new Vector2(holdCellSize * 4f, holdCellSize * 4f);

            holdGrid = gridObj.GetComponent<GridLayoutGroup>();
        }

        while (holdGrid.transform.childCount > 0)
        {
            Transform child = holdGrid.transform.GetChild(0);
            child.SetParent(null);
            DestroyImmediate(child.gameObject);
        }

        holdGrid.cellSize = new Vector2(holdCellSize, holdCellSize);
        holdGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        holdGrid.constraintCount = 4;
        holdGrid.spacing = Vector2.zero;
        holdGrid.padding = new RectOffset(0, 0, 0, 0);

        for (int i = 0; i < 16; i++)
        {
            GameObject cell = new GameObject($"HoldCell_{i}", typeof(RectTransform));
            cell.transform.SetParent(holdGrid.transform, false);
        }

        for (int i = 0; i < 4; i++)
        {
            GameObject previewBlock = Instantiate(blockPrefab, holdGrid.transform, false);
            previewBlock.name = "HoldPreviewBlock";
            Image img = previewBlock.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = false;
            }
            previewBlock.SetActive(false);
            holdPreviewObjects[i] = previewBlock;
        }
    }

    private void UpdateHoldPreview()
    {
        if (holdGrid == null || holdGrid.transform.childCount < 16) return;

        if (string.IsNullOrEmpty(HeldBlock))
        {
            for (int i = 0; i < holdPreviewObjects.Length; i++)
            {
                if (holdPreviewObjects[i] != null)
                {
                    holdPreviewObjects[i].SetActive(false);
                }
            }
            return;
        }

        int[] cellIndices = GetHoldPreviewIndices(HeldBlock);
        Color c = GetBlockColor(HeldBlock);

        for (int i = 0; i < 4; i++)
        {
            GameObject pObj = holdPreviewObjects[i];
            if (pObj == null) continue;

            Transform targetCell = holdGrid.transform.GetChild(cellIndices[i]);
            pObj.transform.SetParent(targetCell, false);

            RectTransform rt = pObj.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(holdCellSize, holdCellSize);

            Image img = pObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = c;
            }

            pObj.SetActive(true);
        }
    }

    private int[] GetHoldPreviewIndices(string blockType)
    {
        switch (blockType)
        {
            case "I": return new int[] { 4, 5, 6, 7 };
            case "O": return new int[] { 5, 6, 9, 10 };
            case "T": return new int[] { 1, 4, 5, 6 };
            case "L": return new int[] { 2, 4, 5, 6 };
            case "J": return new int[] { 0, 4, 5, 6 };
            case "S": return new int[] { 1, 2, 4, 5 };
            case "Z": return new int[] { 0, 1, 5, 6 };
            default: return new int[] { 4, 5, 6, 7 };
        }
    }

    private void StartGameLoop()
    {
        RegeneratePool();
        SpawnNextBlock();
        StartCoroutine(GameLoop());
    }

    private IEnumerator GameLoop()
    {
        while (!GamePause && !isGameOver)
        {
            int currentLvl = (GameManager.Instance != null) ? GameManager.Instance.level : 1;
            float currentDelay = Mathf.Max(0.08f, GameSpeed - (currentLvl - 1) * 0.04f);
            yield return new WaitForSeconds(currentDelay);

            if (isGameOver || GamePause) break;

            if (BlockPool.Length <= 0)
                RegeneratePool();

            if (string.IsNullOrEmpty(CurrentBlock))
            {
                SpawnNextBlock();
            }
            else
            {
                if (!TryMove(0, 1))
                {
                    LockBlock();
                }
            }
        }
    }

    private bool TryMove(int colOffset, int rowOffset)
    {
        if (string.IsNullOrEmpty(CurrentBlock)) return false;

        int[] newIndices = new int[CurrentBlock_Object_Array.Length];

        for (int i = 0; i < CurrentBlock_Object_Array.Length; i++)
        {
            if (CurrentBlock_Object_Array[i] == null) { newIndices[i] = -1; continue; }

            int currentIndex = CurrentBlock_Object_Array[i].transform.parent.GetSiblingIndex();
            int col = currentIndex % 10;
            int row = currentIndex / 10;
            int newCol = col + colOffset;
            int newRow = row + rowOffset;

            if (newCol < 0 || newCol > 9 || newRow < 0 || newRow > 19)
                return false;

            int newIndex = newRow * 10 + newCol;
            Transform target = backgroundGrid.transform.GetChild(newIndex);

            for (int c = 0; c < target.childCount; c++)
            {
                string childName = target.GetChild(c).name;
                if (childName != CurrentBlock_ID && childName != "GhostBlock")
                    return false;
            }

            newIndices[i] = newIndex;
        }

        for (int i = 0; i < CurrentBlock_Object_Array.Length; i++)
        {
            if (CurrentBlock_Object_Array[i] != null && newIndices[i] >= 0)
            {
                CurrentBlock_Object_Array[i].transform.SetParent(
                    backgroundGrid.transform.GetChild(newIndices[i]), false);
            }
        }

        CurrentGrid_Center += rowOffset * 10 + colOffset;
        UpdateGhostPiece();

        return true;
    }

    private void TryRotate()
    {
        if (string.IsNullOrEmpty(CurrentBlock) || CurrentBlock == "O") return;

        if (TryRotateInternal())
        {
            UpdateGhostPiece();
            return;
        }

        if (TryMove(1, 0))
        {
            if (TryRotateInternal()) { UpdateGhostPiece(); return; }
            TryMove(-1, 0);
        }

        if (TryMove(-1, 0))
        {
            if (TryRotateInternal()) { UpdateGhostPiece(); return; }
            TryMove(1, 0);
        }

        if (TryMove(0, 1))
        {
            if (TryRotateInternal()) { UpdateGhostPiece(); return; }
            TryMove(0, -1);
        }
    }

    private bool TryRotateInternal()
    {
        int centerCol = CurrentGrid_Center % 10;
        int centerRow = CurrentGrid_Center / 10;

        int[] newIndices = new int[CurrentBlock_Object_Array.Length];

        for (int i = 0; i < CurrentBlock_Object_Array.Length; i++)
        {
            if (CurrentBlock_Object_Array[i] == null) { newIndices[i] = -1; continue; }

            int idx = CurrentBlock_Object_Array[i].transform.parent.GetSiblingIndex();
            int relCol = (idx % 10) - centerCol;
            int relRow = (idx / 10) - centerRow;

            int newCol = centerCol + (-relRow);
            int newRow = centerRow + relCol;

            if (newCol < 0 || newCol > 9 || newRow < 0 || newRow > 19)
                return false;

            int newIndex = newRow * 10 + newCol;
            Transform target = backgroundGrid.transform.GetChild(newIndex);

            for (int c = 0; c < target.childCount; c++)
            {
                string childName = target.GetChild(c).name;
                if (childName != CurrentBlock_ID && childName != "GhostBlock")
                    return false;
            }

            newIndices[i] = newIndex;
        }

        for (int i = 0; i < CurrentBlock_Object_Array.Length; i++)
        {
            if (CurrentBlock_Object_Array[i] != null && newIndices[i] >= 0)
            {
                CurrentBlock_Object_Array[i].transform.SetParent(
                    backgroundGrid.transform.GetChild(newIndices[i]), false);
            }
        }

        TelemetryManager.Instance?.RecordRotation();
        return true;
    }

    private void HardDrop()
    {
        if (string.IsNullOrEmpty(CurrentBlock)) return;

        TelemetryManager.Instance?.RecordHardDrop();

        while (TryMove(0, 1)) { }

        LockBlock();
    }

    private void CreateGhostBlocks()
    {
        for (int i = 0; i < 4; i++)
        {
            GameObject ghost = Instantiate(blockPrefab, boardContainer, false);
            ghost.name = "GhostBlock";
            Image img = ghost.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = false;
            }
            ghost.SetActive(false);
            ghostBlock_Objects[i] = ghost;
        }
    }

    private int CalculateDropDistance()
    {
        if (string.IsNullOrEmpty(CurrentBlock)) return 0;

        int dropDistance = 0;
        while (CanMoveBy(0, dropDistance + 1))
        {
            dropDistance++;
        }
        return dropDistance;
    }

    private bool CanMoveBy(int colOffset, int rowOffset)
    {
        for (int i = 0; i < CurrentBlock_Object_Array.Length; i++)
        {
            if (CurrentBlock_Object_Array[i] == null) continue;

            int currentIndex = CurrentBlock_Object_Array[i].transform.parent.GetSiblingIndex();
            int col = currentIndex % 10;
            int row = currentIndex / 10;
            int newCol = col + colOffset;
            int newRow = row + rowOffset;

            if (newCol < 0 || newCol > 9 || newRow < 0 || newRow > 19)
                return false;

            int newIndex = newRow * 10 + newCol;
            Transform target = backgroundGrid.transform.GetChild(newIndex);

            for (int c = 0; c < target.childCount; c++)
            {
                string childName = target.GetChild(c).name;
                if (childName != CurrentBlock_ID && childName != "GhostBlock")
                    return false;
            }
        }
        return true;
    }

    private void UpdateGhostPiece()
    {
        if (string.IsNullOrEmpty(CurrentBlock) || isGameOver || GamePause)
        {
            HideGhostPiece();
            return;
        }

        int dropDist = CalculateDropDistance();

        if (dropDist == 0)
        {
            HideGhostPiece();
            return;
        }

        int ghostIdx = 0;
        for (int i = 0; i < CurrentBlock_Object_Array.Length; i++)
        {
            if (CurrentBlock_Object_Array[i] == null) continue;

            int curIdx = CurrentBlock_Object_Array[i].transform.parent.GetSiblingIndex();
            int ghostCellIdx = curIdx + (dropDist * 10);

            if (ghostCellIdx >= 0 && ghostCellIdx < 200 && ghostIdx < ghostBlock_Objects.Length)
            {
                Transform targetCell = backgroundGrid.transform.GetChild(ghostCellIdx);
                GameObject ghost = ghostBlock_Objects[ghostIdx];

                ghost.transform.SetParent(targetCell, false);

                RectTransform rt = ghost.GetComponent<RectTransform>();
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(cellSize, cellSize);

                Image ghostImg = ghost.GetComponent<Image>();
                if (ghostImg != null)
                {
                    ghostImg.color = new Color(currentBlockColor.r, currentBlockColor.g, currentBlockColor.b, 0.30f);
                }

                ghost.SetActive(true);
                ghostIdx++;
            }
        }

        for (int i = ghostIdx; i < ghostBlock_Objects.Length; i++)
        {
            ghostBlock_Objects[i].SetActive(false);
        }
    }

    private void HideGhostPiece()
    {
        for (int i = 0; i < ghostBlock_Objects.Length; i++)
        {
            if (ghostBlock_Objects[i] != null)
            {
                ghostBlock_Objects[i].transform.SetParent(boardContainer, false);
                ghostBlock_Objects[i].SetActive(false);
            }
        }
    }

    private void LockBlock()
    {
        HideGhostPiece();
        CurrentBlock = null;
        canHold = true;

        int linesCleared = ClearFullLines();
        if (linesCleared > 0)
        {
            TelemetryManager.Instance?.RecordLinesCleared(linesCleared);
        }

        bool gameOver = false;
        for (int idx = 0; idx < 20; idx++)
        {
            Transform cell = backgroundGrid.transform.GetChild(idx);
            for (int c = 0; c < cell.childCount; c++)
            {
                if (cell.GetChild(c).name != "GhostBlock")
                {
                    gameOver = true;
                    break;
                }
            }
            if (gameOver) break;
        }

        if (gameOver)
        {
            isGameOver = true;
            GamePause = true;
            Debug.Log("Oyun bitti.");
            TelemetryManager.Instance?.CompleteSession();
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
        }
        else
        {
            TelemetryManager.Instance?.SyncLiveTelemetry();
        }
    }

    private int ClearFullLines()
    {
        int linesClearedThisTurn = 0;

        for (int row = 19; row >= 0; row--)
        {
            bool full = true;
            for (int col = 0; col < 10; col++)
            {
                int idx = row * 10 + col;
                Transform cell = backgroundGrid.transform.GetChild(idx);

                bool hasBlock = false;
                for (int c = 0; c < cell.childCount; c++)
                {
                    if (cell.GetChild(c).name != "GhostBlock")
                    {
                        hasBlock = true;
                        break;
                    }
                }

                if (!hasBlock)
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                linesClearedThisTurn++;

                for (int col = 0; col < 10; col++)
                {
                    int idx = row * 10 + col;
                    Transform cell = backgroundGrid.transform.GetChild(idx);
                    while (cell.childCount > 0)
                    {
                        Transform child = cell.GetChild(0);
                        if (child.name == "GhostBlock")
                        {
                            child.SetParent(boardContainer, false);
                            child.gameObject.SetActive(false);
                        }
                        else
                        {
                            child.SetParent(null);
                            Destroy(child.gameObject);
                        }
                    }
                }

                for (int r = row - 1; r >= 0; r--)
                {
                    for (int col = 0; col < 10; col++)
                    {
                        int fromIdx = r * 10 + col;
                        int toIdx = (r + 1) * 10 + col;
                        Transform fromCell = backgroundGrid.transform.GetChild(fromIdx);
                        Transform toCell = backgroundGrid.transform.GetChild(toIdx);

                        while (fromCell.childCount > 0)
                        {
                            Transform child = fromCell.GetChild(0);
                            if (child.name == "GhostBlock")
                            {
                                child.SetParent(boardContainer, false);
                                child.gameObject.SetActive(false);
                            }
                            else
                            {
                                child.SetParent(toCell, false);
                            }
                        }
                    }
                }

                row++;
            }
        }

        if (linesClearedThisTurn > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.ScoreSet(linesClearedThisTurn);
        }

        return linesClearedThisTurn;
    }

    private void SpawnNextBlock()
    {
        if (BlockPool.Length <= 0)
            RegeneratePool();

        CurrentBlock = BlockPool[0];
        BlockPool = BlockPool.Where((x, index) => index != 0).ToArray();
        TelemetryManager.Instance?.RecordBlockSpawn(CurrentBlock);
        SetBlock(CurrentBlock);
    }

    public void SetBlock(string BlockData)
    {
        Array.Clear(CurrentBlock_Object_Array, 0, CurrentBlock_Object_Array.Length);

        string BlockID = Guid.NewGuid().ToString();
        CurrentBlock_ID = BlockID;
        currentBlockColor = GetBlockColor(BlockData);

        if (BlockData == "I")
        {
            GameObject b1 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(4), false);
            GameObject b2 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(5), false);
            GameObject b3 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(6), false);
            GameObject b4 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(7), false);

            CurrentBlock_Object_Array[0] = b4;
            CurrentBlock_Object_Array[1] = b3;
            CurrentBlock_Object_Array[2] = b2;
            CurrentBlock_Object_Array[3] = b1;

            CurrentGrid_Center = 5;
        }
        else if (BlockData == "O")
        {
            GameObject b1 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(5), false);
            GameObject b2 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(6), false);
            GameObject b3 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(15), false);
            GameObject b4 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(16), false);

            CurrentBlock_Object_Array[0] = b4;
            CurrentBlock_Object_Array[1] = b3;
            CurrentBlock_Object_Array[2] = b2;
            CurrentBlock_Object_Array[3] = b1;

            CurrentGrid_Center = 16;
        }
        else if (BlockData == "T")
        {
            GameObject b1 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(6), false);
            GameObject b2 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(16), false);
            GameObject b3 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(15), false);
            GameObject b4 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(17), false);

            CurrentBlock_Object_Array[0] = b4;
            CurrentBlock_Object_Array[1] = b3;
            CurrentBlock_Object_Array[2] = b2;
            CurrentBlock_Object_Array[3] = b1;

            CurrentGrid_Center = 16;
        }
        else if (BlockData == "L")
        {
            GameObject b1 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(6), false);
            GameObject b2 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(16), false);
            GameObject b3 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(26), false);
            GameObject b4 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(27), false);

            CurrentBlock_Object_Array[0] = b4;
            CurrentBlock_Object_Array[1] = b3;
            CurrentBlock_Object_Array[2] = b2;
            CurrentBlock_Object_Array[3] = b1;

            CurrentGrid_Center = 16;
        }
        else if (BlockData == "J")
        {
            GameObject b1 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(6), false);
            GameObject b2 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(16), false);
            GameObject b3 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(26), false);
            GameObject b4 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(25), false);

            CurrentBlock_Object_Array[0] = b4;
            CurrentBlock_Object_Array[1] = b3;
            CurrentBlock_Object_Array[2] = b2;
            CurrentBlock_Object_Array[3] = b1;

            CurrentGrid_Center = 16;
        }
        else if (BlockData == "S")
        {
            GameObject b1 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(6), false);
            GameObject b2 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(7), false);
            GameObject b3 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(16), false);
            GameObject b4 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(15), false);

            CurrentBlock_Object_Array[0] = b4;
            CurrentBlock_Object_Array[1] = b3;
            CurrentBlock_Object_Array[2] = b2;
            CurrentBlock_Object_Array[3] = b1;

            CurrentGrid_Center = 16;
        }
        else if (BlockData == "Z")
        {
            GameObject b1 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(6), false);
            GameObject b2 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(5), false);
            GameObject b3 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(16), false);
            GameObject b4 = Instantiate(blockPrefab, backgroundGrid.transform.GetChild(17), false);

            CurrentBlock_Object_Array[0] = b4;
            CurrentBlock_Object_Array[1] = b3;
            CurrentBlock_Object_Array[2] = b2;
            CurrentBlock_Object_Array[3] = b1;

            CurrentGrid_Center = 16;
        }
        else
        {
            GamePause = true;
            Debug.LogError("Bilinmeyen blok tipi: " + BlockData);
            return;
        }

        for (int i = 0; i < CurrentBlock_Object_Array.Length; i++)
        {
            if (CurrentBlock_Object_Array[i] != null)
            {
                RectTransform rt = CurrentBlock_Object_Array[i].GetComponent<RectTransform>();
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                CurrentBlock_Object_Array[i].name = BlockID;

                Image img = CurrentBlock_Object_Array[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = currentBlockColor;
                }
            }
        }

        UpdateGhostPiece();
    }

    private Color GetBlockColor(string blockType)
    {
        switch (blockType)
        {
            case "I": return new Color(0.0f, 0.85f, 1.0f);
            case "O": return new Color(1.0f, 0.85f, 0.0f);
            case "T": return new Color(0.7f, 0.2f, 1.0f);
            case "L": return new Color(1.0f, 0.55f, 0.0f);
            case "J": return new Color(0.1f, 0.4f, 1.0f);
            case "S": return new Color(0.2f, 0.9f, 0.2f);
            case "Z": return new Color(1.0f, 0.2f, 0.2f);
            default: return Color.white;
        }
    }

    private void CreateBackgroundGrid()
    {
        while (backgroundGrid.transform.childCount > 0)
        {
            Transform child = backgroundGrid.transform.GetChild(0);
            child.SetParent(null);
            DestroyImmediate(child.gameObject);
        }

        backgroundGrid.cellSize = new Vector2(cellSize, cellSize);
        backgroundGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        backgroundGrid.constraintCount = 10;
        backgroundGrid.spacing = Vector2.zero;
        backgroundGrid.padding = new RectOffset(0, 0, 0, 0);

        for (int i = 0; i < 200; i++)
        {
            GameObject cell = new GameObject($"Cell_{i}", typeof(RectTransform));
            cell.transform.SetParent(backgroundGrid.transform, false);

            Image img = cell.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.04f);
            img.raycastTarget = false;
        }
    }

    private void RegeneratePool()
    {
        List<string> bag = new List<string>(BlocksData);

        System.Random rnd = new System.Random();
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(0, i + 1);
            string tmp = bag[i];
            bag[i] = bag[j];
            bag[j] = tmp;
        }

        BlockPool = bag.ToArray();
    }

    public void RestartGame()
    {
        StopAllCoroutines();
        HideGhostPiece();

        for (int i = 0; i < 200; i++)
        {
            Transform cell = backgroundGrid.transform.GetChild(i);
            while (cell.childCount > 0)
            {
                Transform child = cell.GetChild(0);
                if (child.name == "GhostBlock")
                {
                    child.SetParent(boardContainer, false);
                    child.gameObject.SetActive(false);
                }
                else
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
            }
        }

        Array.Clear(CurrentBlock_Object_Array, 0, CurrentBlock_Object_Array.Length);
        CurrentBlock = null;
        CurrentBlock_ID = "";
        CurrentGrid_Center = 6;
        HeldBlock = null;
        canHold = true;
        UpdateHoldPreview();
        GamePause = false;
        isGameOver = false;
        BlockPool = new string[0];

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.CompleteSession();
            TelemetryManager.Instance.StartNewSession();
        }

        if (GameManager.Instance != null)
            GameManager.Instance.ResetGame();

        StartGameLoop();
    }
}
