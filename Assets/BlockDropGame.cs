#if UNITY_EDITOR
using Oculus.Interaction.Editor.Generated;
#endif 

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;


public class BlockDropGame : MonoBehaviour
{
    public enum ControlScheme
    {
        OneHandedRight,
        TwoHandedSplit
    }

    [Header("Options")]
    public ControlScheme controlScheme = ControlScheme.OneHandedRight;

    [Header("Landing")]
    public float lockDelay = 0.2f;
    private float lockTimer = 0f;
    private bool touchingGround = false;

    [Header("Materials")]
    public Material blockMaterial;        // Mat_DichopticCube (both eyes, L stronger)
    public Material ghostMaterial;        // Mat_LeftOnlyGhost (left-eye only)

    [Header("Colours")]
    public Color[] blockColors = new Color[]
    {
        Color.red,
        Color.blue,
        Color.yellow,
        Color.green,
        new Color(0.7f, 0.2f, 1f, 1f) // purple-ish
    };

    [Header("Clear Pulse FX")]
    public float pulseDuration = 0.18f;
    public float pulseScaleMultiplier = 1.6f;

    [Header("Unstable Brick FX")]
    public float unstableShimmerSpeed = 6f;
    public float unstableShimmerAmount = 0.12f;

    [Header("Unstable Bricks")]
    [Range(0f, 1f)] public float unstableSpawnChance = 0.15f;
    public float unstableColorInterval = 1.8f;

    [Header("Matching")]
    public int matchMin = 4;   // set to 3 later if you want harder


    public bool randomizeColors = true;


    [Header("Quest Controller Input (OVRInput)")]
    public bool enableOVRInput = true;
    public float stickDeadzone = 0.6f;
    public float moveRepeatDelay = 0.18f;   // how fast it repeats when you hold the stick


    [Header("Left-eye-only rendering")]
    public string leftOnlyLayerName = "LeftOnly";
    private int leftOnlyLayer;

    [Header("UI")]
    public TextMesh scoreText;
    public TextMesh debugText;

    [Header("Front End UI")]
    public GameObject startScreenRoot;
    public TextMeshPro titleText;
    public TextMeshPro highScoreText;
    public TextMeshPro startButtonText;
    public TextMeshPro optionsButtonText;

    private int highScore = 0;

    private enum GameState
    {
        StartMenu,
        Playing
    }

    private GameState gameState = GameState.StartMenu;
    
    [Header("Game Over / Restart")]
    public bool requireRestartButton = true;
    public TextMesh gameOverText;   // optional: assign in Inspector (can be null)


    [Header("Grid")]
    public int width = 6;
    public int height = 10;
    public float cellSize = 0.22f;

    [Header("Timing")]
    public float dropInterval = 0.6f;     // seconds per step down

    [Header("Spawn")]
    public int spawnRow = 9;              // top row index (height - 1)
    public float boardLocalY = 0.0f;      // shift entire grid up/down if needed

    [Header("Progress")]
    public int score = 0;

    [Header("Combo Chains")]
    public int comboScoreStep = 5;
    public float comboPulseGrowth = 0.2f;

    // Grid stores placed blocks (not the current falling one)
    private Transform[,] grid;
    private int[,] colorGrid;
    private float lastStickMoveTime = -999f;
    private InputAction rightStickAction;
    private InputAction rightTriggerAction;
    private bool triggerWasDown = false;
    private InputAction rightGripAction;
    private InputAction leftStickAction;
    private InputAction leftTriggerAction;
    private InputAction rightButtonAAction;

    // Current falling single-block piece
    private int curX;
    private int curY;
    private Transform curBlockA;
    private Transform curBlockB;
    private int curColorIndex = 0;
    private int colorIndexA = 0;
    private int colorIndexB = 0;
    private int rotState = 0;   // 0=down,1=right,2=up,3=left (B relative to A)
    private int bOffsetX = 0;
    private int bOffsetY = -1;
    private Vector3 baseScaleA;
    private Vector3 baseScaleB;

    private int currentChainLevel = 0;

    // Ghost landing preview
    private Transform ghostBlock;

    private float dropTimer = 0f;
    private bool isGameOver = false;

    private bool unstableA = false;
    private bool unstableB = false;
    private Coroutine unstableRoutineA;
    private Coroutine unstableRoutineB;
    private Transform haloA;
    private Transform haloB;


    bool CanMoveDown()
    {
        int ax = Ax();
        int ay = Ay();
        int bx = Bx();
        int by = By();

        int aBelowY = ay - 1;
        int bBelowY = by - 1;

    // If either would go below the board, can't move
        if (aBelowY < 0 || bBelowY < 0) return false;

    // Check below A unless it's currently occupied by B (when B is directly below A)
        bool aBlocked =
            grid[ax, aBelowY] != null &&
            !(ax == bx && aBelowY == by);

    // Check below B unless it's currently occupied by A (when A is directly below B)
        bool bBlocked =
            grid[bx, bBelowY] != null &&
            !(bx == ax && bBelowY == ay);

        return !aBlocked && !bBlocked;
    }
   


    void Start()
    {
        grid = new Transform[width, height];
        colorGrid = new int[width, height];

// Optional: fill with -1 to mean "empty"
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                colorGrid[x, y] = -1;
            }
        }


        leftOnlyLayer = LayerMask.NameToLayer(leftOnlyLayerName);
        if (leftOnlyLayer == -1)
        {
            Debug.LogError("LeftOnly layer not found. Create a layer named 'LeftOnly' in Project Settings.");
        }
        
        isGameOver = false;
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        LoadHighScore();
        ShowStartScreen();
    }


    void UpdateScoreText()
    {
    if (scoreText != null)
        scoreText.text = "Score: " + score;
    }

    int Ax() => curX;
    int Ay() => curY;

    int Bx() => curX + bOffsetX;
    int By() => curY + bOffsetY;

    void LoadHighScore()
    {
        highScore = PlayerPrefs.GetInt("DrAmblyo_HighScore", 0);
    }

    void SaveHighScore()
    {
        PlayerPrefs.SetInt("DrAmblyo_HighScore", highScore);
        PlayerPrefs.Save();
    }

    void ShowStartScreen()
    {
        gameState = GameState.StartMenu;
        isGameOver = false;

        ResetBoard();

        if (startScreenRoot != null) startScreenRoot.SetActive(true);
        if (titleText != null) titleText.text = "Dr Amblyo";
        if (highScoreText != null) highScoreText.text = "High Score: " + highScore;
        if (startButtonText != null) startButtonText.text = "START";
        if (optionsButtonText != null) optionsButtonText.text = "OPTIONS";
    }

    void StartGame()
    {
        gameState = GameState.Playing;

        if (startScreenRoot != null) startScreenRoot.SetActive(false);

        ResetGame();
    }

    void TryRotateCapsule()
    {
        if (curBlockA == null || curBlockB == null) return;

        int nextState = (rotState + 1) % 4;

    // offsets for B relative to A: down, right, up, left
        int[] ox = { 0, 1, 0, -1 };
        int[] oy = { -1, 0, 1, 0 };

        int nbx = curX + ox[nextState];
        int nby = curY + oy[nextState];

    // bounds
        if (nbx < 0 || nbx >= width || nby < 0 || nby >= height) return;

    // occupancy (ignore current A/B cells)
        int ax = curX, ay = curY;
        int cbx = Bx(), cby = By();

        bool cellIsOccupied =
            grid[nbx, nby] != null &&
            !(nbx == ax && nby == ay) &&
            !(nbx == cbx && nby == cby);

        if (cellIsOccupied) return;

    // apply new orientation
        rotState = nextState;
        bOffsetX = ox[rotState];
        bOffsetY = oy[rotState];

        PlaceCapsule();
        UpdateGhost();

    // reset lock delay if you’re using it
        touchingGround = false;
        lockTimer = 0f;
    }

    
      void ClearAllMatchesWithChains()
    {
        int chainLevel = 0;
        bool clearedSomething = true;

        while (clearedSomething)
        {
            clearedSomething = false;

            bool[,] visited = new bool[width, height];
            int clearedThisPass = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y] == null) continue;
                    if (visited[x, y]) continue;

                    int targetColor = colorGrid[x, y];
                    if (targetColor < 0) continue;

                    var group = new System.Collections.Generic.List<Vector2Int>();
                    FloodFillSameColor(x, y, targetColor, visited, group);

                    if (group.Count >= matchMin)
                    {
                        clearedSomething = true;
                        chainLevel++;

                        foreach (var p in group)
                        {
                            if (grid[p.x, p.y] != null)
                            {
                                SpawnClearPulse(grid[p.x, p.y].localPosition, colorGrid[p.x, p.y], chainLevel);
                                Destroy(grid[p.x, p.y].gameObject);
                                grid[p.x, p.y] = null;
                            }

                            colorGrid[p.x, p.y] = -1;
                        }

                        int groupScore = group.Count * 5;
                        int comboBonus = Mathf.Max(0, chainLevel - 1) * comboScoreStep;
                        score += groupScore + comboBonus;
                        clearedThisPass += group.Count;
                    }
                }
            }

            if (clearedSomething)
            {
                currentChainLevel = chainLevel;
                UpdateScoreText();
                ApplyGravity();
            }
        }

        currentChainLevel = 0;
    }

void HandleOVRInput()
{
    if (isGameOver)
    {
    // Restart with A / Start (OVR path)
        if (!requireRestartButton ||
            OVRInput.GetDown(OVRInput.Button.One) ||
            OVRInput.GetDown(OVRInput.Button.Start))
        {
            ResetGame();
        }
        return;
    }       
 // Move with LEFT thumbstick (PrimaryThumbstick is typically the left controller)
    Vector2 stick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

       

    if (debugText != null)
        {
            debugText.text = $"StickX: {stick.x:0.00}\nTrig: {OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger):0.00}";
        }

    float now = Time.time;

    if (now - lastStickMoveTime >= moveRepeatDelay)
    {
        if (stick.x <= -stickDeadzone)
        {
            TryMoveHorizontal(-1);
            lastStickMoveTime = now;
        }
        else if (stick.x >= stickDeadzone)
        {
            TryMoveHorizontal(1);
            lastStickMoveTime = now;
        }
    }
}  
void Update()
{
    // --- START MENU STATE ---
    if (gameState == GameState.StartMenu)
    {
        if (enableOVRInput &&
            (OVRInput.GetDown(OVRInput.Button.One) ||
             OVRInput.GetDown(OVRInput.Button.Start)))
        {
            StartGame();
            return;
        }

                if ((rightGripAction != null && rightGripAction.WasPressedThisFrame()) ||
            (rightButtonAAction != null && rightButtonAAction.WasPressedThisFrame()))
        {
            StartGame();
            return;
        }

        return;
    }

    // --- GAME OVER STATE ---
    if (isGameOver)
    {
        return;
    }

    // --- GAMEPLAY ---


        // Read input based on selected control scheme
    Vector2 stick = Vector2.zero;
    float trigger = 0f;
    bool rotatePressed = false;

    if (controlScheme == ControlScheme.OneHandedRight)
    {
        stick = rightStickAction != null
            ? rightStickAction.ReadValue<Vector2>()
            : Vector2.zero;

        trigger = rightTriggerAction != null
            ? rightTriggerAction.ReadValue<float>()
            : 0f;

        rotatePressed = rightGripAction != null && rightGripAction.WasPressedThisFrame();
    }
    else if (controlScheme == ControlScheme.TwoHandedSplit)
    {
        stick = leftStickAction != null
            ? leftStickAction.ReadValue<Vector2>()
            : Vector2.zero;

        trigger = leftTriggerAction != null
            ? leftTriggerAction.ReadValue<float>()
            : 0f;

        rotatePressed =
            (rightGripAction != null && rightGripAction.WasPressedThisFrame()) ||
            (rightButtonAAction != null && rightButtonAAction.WasPressedThisFrame());
    }

    if (rotatePressed)
    {
        TryRotateCapsule();
    }

        if (debugText != null)
    {
        debugText.text =
            $"Scheme: {controlScheme}\n" +
            $"FRAME: {Time.frameCount}\n" +
            $"Stick X: {stick.x:0.00}\n" +
            $"Trig: {trigger:0.00}";
    }
    

    // Falling logic
    if (curBlockA == null || curBlockB == null) return;

// If we CAN move down, do timed falling
    if (CanMoveDown())
    {
        touchingGround = false;
        lockTimer = 0f;

        dropTimer += Time.deltaTime;
        if (dropTimer >= dropInterval)
        {
            dropTimer = 0f;
            StepDownOneRow();
        }
    }
    else
    {
    // We are touching ground -> lock delay counts EVERY frame
        touchingGround = true;
        lockTimer += Time.deltaTime;

        if (lockTimer >= lockDelay)
        {
            touchingGround = false;
            lockTimer = 0f;
            dropTimer = 0f;
            LockPiece();
        }
    } 

    

    // Horizontal movement (right stick)
    float now = Time.time;
    if (now - lastStickMoveTime >= moveRepeatDelay)
    {
        if (stick.x <= -stickDeadzone)
        {
            TryMoveHorizontal(-1);
            lastStickMoveTime = now;
        }
        else if (stick.x >= stickDeadzone)
        {
            TryMoveHorizontal(1);
            lastStickMoveTime = now;
        }
    }

    // Hard drop (right trigger)
    if (trigger > 0.8f)
    {
        if (!triggerWasDown)
        {
            HardDrop();
            triggerWasDown = true;
        }
    }
    else
    {
        triggerWasDown = false;
    }
    UpdateUnstableVisuals();

    // Keep ghost aligned
    UpdateGhost();
}

void OnEnable()
{
    rightStickAction = new InputAction("RightStick", InputActionType.Value);
    rightStickAction.AddBinding("<XRController>{RightHand}/thumbstick");
    rightStickAction.AddBinding("<OculusTouchController>{RightHand}/thumbstick");
    rightStickAction.Enable();

    rightTriggerAction = new InputAction("RightTrigger", InputActionType.Value);
    rightTriggerAction.AddBinding("<XRController>{RightHand}/trigger");
    rightTriggerAction.AddBinding("<OculusTouchController>{RightHand}/trigger");
    rightTriggerAction.Enable();

    rightGripAction = new InputAction("RightGrip", InputActionType.Button);
    rightGripAction.AddBinding("<XRController>{RightHand}/gripPressed");
    rightGripAction.AddBinding("<OculusTouchController>{RightHand}/gripPressed");
    rightGripAction.Enable();

    leftStickAction = new InputAction("LeftStick", InputActionType.Value);
    leftStickAction.AddBinding("<XRController>{LeftHand}/thumbstick");
    leftStickAction.AddBinding("<OculusTouchController>{LeftHand}/thumbstick");
    leftStickAction.Enable();

    leftTriggerAction = new InputAction("LeftTrigger", InputActionType.Value);
    leftTriggerAction.AddBinding("<XRController>{LeftHand}/trigger");
    leftTriggerAction.AddBinding("<OculusTouchController>{LeftHand}/trigger");
    leftTriggerAction.Enable();

    rightButtonAAction = new InputAction("RightButtonA", InputActionType.Button);
    rightButtonAAction.AddBinding("<XRController>{RightHand}/primaryButton");
    rightButtonAAction.AddBinding("<OculusTouchController>{RightHand}/primaryButton");
    rightButtonAAction.Enable();

  
}

void OnDisable()
{
    rightStickAction?.Disable();  rightStickAction?.Dispose();
    rightTriggerAction?.Disable(); rightTriggerAction?.Dispose();
    rightGripAction?.Disable();   rightGripAction?.Dispose();
    leftStickAction?.Disable(); leftStickAction?.Dispose();
    leftTriggerAction?.Disable(); leftTriggerAction?.Dispose();
    rightButtonAAction?.Disable(); rightButtonAAction?.Dispose();
}
void PlaceCapsule()
{
    if (curBlockA == null || curBlockB == null) return;

    SetBlockToCell(curBlockA, curX, curY);
    SetBlockToCell(curBlockB, Bx(), By());
}

void EnsureGhostExists()
{
    if (ghostBlock != null) return;

    ghostBlock = CreateBlock("GhostBlock", ghostMaterial);

    // Flatten ghost (tweak size to taste)
    ghostBlock.localScale = new Vector3(0.3f, 0.05f, 0.3f);

    // Left-only layer
    if (leftOnlyLayer != -1)
        ghostBlock.gameObject.layer = leftOnlyLayer;
}

    void SpawnNewPiece()
    {
        Debug.Log($"SpawnNewPiece() called. curBlockA={(curBlockA!=null)} curBlockB={(curBlockB!=null)}");

    // Safety: never allow a new piece while one is active
        if (curBlockA != null || curBlockB != null)
        {
            Debug.LogWarning("SpawnNewPiece called while a piece is still active. Ignoring.");
            return;
        }

    // Spawn in a random column
        curX = Random.Range(0, width);
        curY = Mathf.Clamp(spawnRow, 1, height - 1); // MUST be at least 1 because we need space below

    // Pick colours for each half
        if (randomizeColors && blockColors != null && blockColors.Length > 0)
        {
            colorIndexA = Random.Range(0, blockColors.Length);
            colorIndexB = Random.Range(0, blockColors.Length);

        // Avoid identical capsule colours (optional, but recommended)
            if (blockColors.Length > 1)
            {
                int safety = 0;
                while (colorIndexB == colorIndexA && safety < 10)
                {
                    colorIndexB = Random.Range(0, blockColors.Length);
                    safety++;
                }
            }
        }
        else
        {
            colorIndexA = 0;
            colorIndexB = 0;
        }

    // Check spawn cells are empty
        if (grid[curX, curY] != null || grid[curX, curY - 1] != null)
        {
            GameOver();
            return;
        }


    // Create capsule blocks
        curBlockA = CreateBlock("ActiveBlockA", blockMaterial);
        curBlockB = CreateBlock("ActiveBlockB", blockMaterial);

        baseScaleA = curBlockA.localScale;
        baseScaleB = curBlockB.localScale; 

    // Apply colours
        ApplyColorToBlock(curBlockA, colorIndexA);
        ApplyColorToBlock(curBlockB, colorIndexB);

        Debug.Log($"Spawn capsule colours A={colorIndexA}, B={colorIndexB}");
                
        StartUnstableIfNeeded();

    // Start vertical by default
// Start vertical by default (B below A)
        rotState = 0;
        bOffsetX = 0;
        bOffsetY = -1;


    // Place them
        PlaceCapsule();

    // Ensure ghost exists
        EnsureGhostExists();
        UpdateGhost();
    }
    void SpawnClearPulse(Vector3 localPos, int colorIndex, int chainLevel)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ClearPulseFX";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        
        float comboScale = 1f + Mathf.Max(0, chainLevel - 1) * comboPulseGrowth;
        go.transform.localScale = Vector3.one * cellSize * comboScale;

        Destroy(go.GetComponent<Collider>());

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = new Material(blockMaterial);

            Color c = blockColors[Mathf.Clamp(colorIndex, 0, blockColors.Length - 1)];
            if (r.material.HasProperty("_BaseColor"))
                r.material.SetColor("_BaseColor", c);
        }

        StartCoroutine(AnimateClearPulse(go.transform, r));
    }

    IEnumerator AnimateClearPulse(Transform pulse, Renderer r)
    {
        if (pulse == null) yield break;

        Vector3 startScale = Vector3.one * cellSize;
        Vector3 endScale = startScale * pulseScaleMultiplier;

        float t = 0f;
        while (t < pulseDuration)
        {
            if (pulse == null) yield break;

            float u = t / pulseDuration;
            pulse.localScale = Vector3.Lerp(startScale, endScale, u);

            if (r != null && r.material != null && r.material.HasProperty("_BaseColor"))
            {
                Color c = r.material.GetColor("_BaseColor");
                c.a = Mathf.Lerp(1f, 0f, u);
                r.material.SetColor("_BaseColor", c);
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (pulse != null)
            Destroy(pulse.gameObject);
    }

    void ApplyColorToBlock(Transform block, int colorIndex)
    {
        if (block == null) return;
        if (blockColors == null || blockColors.Length == 0) return;

        Renderer r = block.GetComponent<Renderer>();
        if (r == null) return;

        Material m = r.material;
        if (m == null) return;

        Color c = blockColors[Mathf.Clamp(colorIndex, 0, blockColors.Length - 1)];

        if (m.HasProperty("_BaseColor"))
        {
            m.SetColor("_BaseColor", c);
        }
        else
        {
            Debug.LogWarning($"Shader '{m.shader.name}' has no _BaseColor property");
        }
}

    void StopUnstableCoroutines()
    {
        if (unstableRoutineA != null)
        {
            StopCoroutine(unstableRoutineA);
            unstableRoutineA = null;
        }

        if (unstableRoutineB != null)
        {
            StopCoroutine(unstableRoutineB);
            unstableRoutineB = null;
        }

        unstableA = false;
        unstableB = false;

        if (curBlockA != null) curBlockA.localScale = Vector3.one * cellSize;
        if (curBlockB != null) curBlockB.localScale = Vector3.one * cellSize;

        if (haloA != null) Destroy(haloA.gameObject);
        if (haloB != null) Destroy(haloB.gameObject);
    }

    IEnumerator CycleActiveBlockColor(Transform block, bool isA)
    {
        while (block != null)
        {
            yield return new WaitForSeconds(unstableColorInterval);

            if (block == null) yield break;

            int next = Random.Range(0, blockColors.Length);

            if (isA)
            {
                colorIndexA = next;
                ApplyColorToBlock(block, colorIndexA);
            }
            else
            {
                colorIndexB = next;
                ApplyColorToBlock(block, colorIndexB);
            }
        }
    }

    void StartUnstableIfNeeded()
    {
        StopUnstableCoroutines();

        unstableA = Random.value < unstableSpawnChance;
        unstableB = Random.value < unstableSpawnChance;

       if (unstableA && curBlockA != null)
        {
            haloA = CreateHalo();
            unstableRoutineA = StartCoroutine(CycleActiveBlockColor(curBlockA, true));
        }

        if (unstableB && curBlockB != null)
        {
            haloB = CreateHalo();
            unstableRoutineB = StartCoroutine(CycleActiveBlockColor(curBlockB, false));
        }
    }

void StepDownOneRow()
{
    curY -= 1;
    PlaceCapsule();
    UpdateGhost();
}

void LockPiece()
{
    if (curBlockA == null || curBlockB == null) return;

    StopUnstableCoroutines();

    int ax = Ax();
    int ay = Ay();
    int bx = Bx();
    int by = By();

    if (!InBounds(ax, ay) || !InBounds(bx, by))
    {
        curBlockA = null;
        curBlockB = null;
        GameOver();
        return;
    }

    // Place A
    grid[ax, ay] = curBlockA;
    colorGrid[ax, ay] = colorIndexA;

    // Place B
    grid[bx, by] = curBlockB;
    colorGrid[bx, by] = colorIndexB;
        // GAME OVER if any locked block is in the top row (or beyond)
    if (ay >= height - 1 || by >= height - 1)
    {
        curBlockA = null;
        curBlockB = null;
        GameOver();
        return;
    }


    curBlockA = null;
    curBlockB = null;

    ApplyGravity();  // always settle unsupported blocks
    ClearAllMatchesWithChains();
    UpdateGhost();

    // Score for placing 2 blocks
    score += 2;
    UpdateScoreText();


    SpawnNewPiece();
}

void GameOver()
{
    isGameOver = true;

    touchingGround = false;
    lockTimer = 0f;
    dropTimer = 0f;
    triggerWasDown = false;

    if (score > highScore)
    {
        highScore = score;
        SaveHighScore();
    }

    ShowStartScreen();
}

    void TryClearMatchesFrom(int startX, int startY)
    {
        if (!InBounds(startX, startY)) return;
        if (grid[startX, startY] == null) return;

        int targetColor = colorGrid[startX, startY];
        if (targetColor < 0) return;

        bool[,] visited = new bool[width, height];
        System.Collections.Generic.List<Vector2Int> group = new System.Collections.Generic.List<Vector2Int>();

        FloodFillSameColor(startX, startY, targetColor, visited, group);

        if (group.Count >= matchMin)
        {
        // Clear them
            foreach (var p in group)
            {
                if (grid[p.x, p.y] != null)
                {
                    Destroy(grid[p.x, p.y].gameObject);
                    grid[p.x, p.y] = null;
                }
                colorGrid[p.x, p.y] = -1;
            }

        // Score bonus for clears
            score += group.Count * 5;
            UpdateScoreText();
            Debug.Log($"Cleared group size {group.Count} (color {targetColor}). Score: {score}");
        }
    }
    void FloodFillSameColor(int x, int y, int targetColor, bool[,] visited, System.Collections.Generic.List<Vector2Int> group)
    {
        if (!InBounds(x, y)) return;
        if (visited[x, y]) return;
        if (grid[x, y] == null) return;
        if (colorGrid[x, y] != targetColor) return;

        visited[x, y] = true;
        group.Add(new Vector2Int(x, y));

        FloodFillSameColor(x + 1, y, targetColor, visited, group);
        FloodFillSameColor(x - 1, y, targetColor, visited, group);
        FloodFillSameColor(x, y + 1, targetColor, visited, group);
        FloodFillSameColor(x, y - 1, targetColor, visited, group);
    }
    bool InBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
    void ApplyGravity()
{
    bool moved = true;

    // Repeat until no blocks move (usually 1–2 passes)
    while (moved)
    {
        moved = false;

        for (int x = 0; x < width; x++)
        {
            for (int y = 1; y < height; y++) // start at 1 because we look at y-1
            {
                if (grid[x, y] == null) continue;
                if (grid[x, y - 1] != null) continue;

                // Move block down by one cell
                grid[x, y - 1] = grid[x, y];
                colorGrid[x, y - 1] = colorGrid[x, y];

                grid[x, y] = null;
                colorGrid[x, y] = -1;

                SetBlockToCell(grid[x, y - 1], x, y - 1);

                moved = true;
            }
        }
    }
}



    void ClearFullRows()
    {
        for (int y = 0; y < height; y++)
        {
            bool full = true;
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] == null)
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                ClearRow(y);
                DropRowsAbove(y);
                y--; // recheck this row index after dropping
                score += 10; // bonus for clearing a row
                UpdateScoreText();
                Debug.Log("Row cleared! Score: " + score);
            }
        }
    }

    void ClearRow(int y)
    {
        for (int x = 0; x < width; x++)
        {
            if (grid[x, y] != null)
            {
                Destroy(grid[x, y].gameObject);
                grid[x, y] = null;
            }
        }
    }

    void DropRowsAbove(int clearedY)
    {
        for (int y = clearedY + 1; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] != null)
                {
                    // Move block down one row
                    grid[x, y - 1] = grid[x, y];
                    grid[x, y] = null;

                    SetBlockToCell(grid[x, y - 1], x, y - 1);
                }
            }
        }
    }

void UpdateGhost()
{
    if (ghostBlock == null || curBlockA == null || curBlockB == null) return;

    int ax = Ax();
    int ay = Ay();
    int bx = Bx();
    int by = By();

    // Find how far down we can drop (in grid steps) without collision
    int drop = 0;
    while (true)
    {
        int nextAy = ay - (drop + 1);
        int nextBy = by - (drop + 1);

        // would go below board?
        if (nextAy < 0 || nextBy < 0) break;

        // would collide with existing blocks (ignore current A/B cells)
        bool aHits =
            grid[ax, nextAy] != null &&
            !(ax == bx && nextAy == by);

        bool bHits =
            grid[bx, nextBy] != null &&
            !(bx == ax && nextBy == ay);

        if (aHits || bHits) break;

        drop++;
    }

    // Put ghost at the landing position of A (consistent reference)
    SetBlockToCell(ghostBlock, ax, ay - drop);

    // Fade based on distance
    float alpha = Mathf.Clamp01(drop / 6f);
    Renderer r = ghostBlock.GetComponent<Renderer>();
    if (r != null && r.material.HasProperty("_Alpha"))
        r.material.SetFloat("_Alpha", alpha);

    ghostBlock.gameObject.SetActive(drop != 0);
}

    void UpdateUnstableVisuals()
    {
        if (curBlockA != null)
        {
            if (unstableA)
            {
                float s = 1f + Mathf.Sin(Time.time * unstableShimmerSpeed) * unstableShimmerAmount;
                curBlockA.localScale = baseScaleA * s;
            }
            else
            {
                curBlockA.localScale = baseScaleA;
            }
        }

        if (curBlockB != null)
        {
            if (unstableB)
            {
                float s = 1f + Mathf.Sin((Time.time * unstableShimmerSpeed) + 1.2f) * unstableShimmerAmount;
                curBlockB.localScale = baseScaleB * s;
            }
            else
            {
                curBlockB.localScale = baseScaleB;
            }
        }

        if (haloA != null && curBlockA != null)
            haloA.localPosition = curBlockA.localPosition;

        if (haloB != null && curBlockB != null)
            haloB.localPosition = curBlockB.localPosition;
    }

    void ResetBoard()
    {
        StopUnstableCoroutines();
        
        // Destroy all placed blocks
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null)
                {
                    Destroy(grid[x, y].gameObject);
                    grid[x, y] = null;
                }
                colorGrid[x,y] = -1;
            }
        }

        // Destroy active
        if (curBlockA != null) Destroy(curBlockA.gameObject);
        if (curBlockB != null) Destroy(curBlockB.gameObject);
        curBlockA = null;
        curBlockB = null;


        score = 0;

                // Reset runtime state
        isGameOver = false;
        touchingGround = false;
        lockTimer = 0f;
        dropTimer = 0f;
        triggerWasDown = false;
        lastStickMoveTime = -999f;

        if (gameOverText != null) gameOverText.gameObject.SetActive(false);

        UpdateScoreText();
                if (startScreenRoot != null && gameState == GameState.Playing)
            startScreenRoot.SetActive(false);

        Debug.Log("Board reset. Score: 0");
    }

    void ResetGame()
    {
        ResetBoard();
        SpawnNewPiece();
    }

    Transform CreateHalo()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);

        go.name = "UnstableHalo";
        go.transform.SetParent(transform, false);

        Destroy(go.GetComponent<Collider>());

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = new Material(ghostMaterial);
        }

        if (leftOnlyLayer != -1)
            go.layer = leftOnlyLayer;

        go.transform.localScale = Vector3.one * cellSize * 1.45f;

        return go.transform;
    }

    Transform CreateBlock(string name, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * cellSize;

        Destroy(go.GetComponent<Collider>());

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(mat); // unique per block
        }

        return go.transform;
    }


    void SetBlockToCell(Transform t, int x, int y)
    {
        if (t == null) return;

        // Convert grid coords to local position (centered horizontally)
        float x0 = -(width - 1) * 0.5f * cellSize;
        float localX = x0 + x * cellSize;
        float localY = boardLocalY + y * cellSize;

        t.localPosition = new Vector3(localX, localY, 0f);
    }
    void TryMoveHorizontal(int dx)
    {
        if (curBlockA == null || curBlockB == null) return;

        int nextX = curX + dx;
        if (nextX < 0 || nextX >= width) return;

        int nextAx = nextX;
        int nextAy = curY;

        int nextBx = nextX + bOffsetX;
        int nextBy = curY + bOffsetY;


// Bounds check for B when horizontal
        if (nextBx < 0 || nextBx >= width) return;
        if (nextBy < 0 || nextBy >= height) return;

// Are target cells free?
        if (grid[nextAx, nextAy] != null) return;
        if (grid[nextBx, nextBy] != null) return;

        curX = nextX;
        PlaceCapsule();
        UpdateGhost();

        touchingGround = false;
        lockTimer = 0f;
    }
    void HardDrop()
    {
        if (curBlockA == null || curBlockB == null) return;


    


// For a vertical capsule, bottom block is at (curY - 1).
// We find the landing position for the BOTTOM block.

    while (CanMoveDown())
    {   
        curY -= 1;
    }
    PlaceCapsule();
    UpdateGhost();
    LockPiece();

    
    }
  
}
