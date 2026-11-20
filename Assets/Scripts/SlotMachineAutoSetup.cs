using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Main slot machine controller. Handles spin logic, reel animations,
/// win evaluation, betting, and UI updates.
/// 
/// Setup: Use menu "Underpin Slot Game > Build Complete Slot Machine"
/// to auto-generate the full playable scene.
/// </summary>
public class SlotMachineAutoSetup : MonoBehaviour
{
    [Header("Generated References")]
    public Image[] reels = new Image[3];              // 3 reel Image components
    public Sprite[] symbols = new Sprite[4];          // 4 symbol sprites
    public Button spinButton;
    public Button betUpButton;
    public Button betDownButton;
    public Text balanceLabel;
    public Text betLabel;
    public Text messageLabel;

    [Header("Game Rules")]
    public int startingCoins = 1000;
    public int startingBet = 10;
    public int minimumBet = 10;
    public int maximumBet = 100;
    public int betStep = 10;
    public float reelSpinSeconds = 1.8f;
    public float delayBetweenStops = 0.45f;

    [Header("Smooth Reel Settings")]
    [Tooltip("Height of one symbol in pixels (must match CreateImage size)")]
    public float symbolHeight = 88f;
    [Tooltip("How many symbols are pre-built in each reel strip")]
    public int symbolsPerStrip = 8;

    [Header("Bonus: Wild Symbol")]
    [Tooltip("Index of the Wild sprite in the symbols array. Set to -1 to disable.")]
    public int wildSymbolIndex = 3;   // last symbol acts as Wild

    // Runtime state
    private int coins;
    private int bet;
    private bool spinning;

    // Each reel's scrolling strip container
    private RectTransform[] reelStrips;

    private void Start()
    {
        coins = startingCoins;
        bet = Mathf.Clamp(startingBet, minimumBet, maximumBet);

        if (spinButton != null) spinButton.onClick.AddListener(Spin);
        if (betUpButton != null) betUpButton.onClick.AddListener(IncreaseBet);
        if (betDownButton != null) betDownButton.onClick.AddListener(DecreaseBet);

        // Build scroll strips behind each reel
        BuildReelStrips();

        // Randomize starting symbols
        if (symbols != null && symbols.Length > 0)
        {
            for (int i = 0; i < reels.Length; i++)
            {
                if (reels[i] != null)
                    SetSymbol(i, Random.Range(0, symbols.Length));
            }
        }

        SetMessage("READY TO PLAY");
        UpdateLabels();
    }

    // ─────────────────────────────────────────────────────────────
    //  PUBLIC CONTROL HANDLERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>Called when SPIN button is pressed.</summary>
    private void Spin()
    {
        if (spinning) return;

        if (coins < bet)
        {
            SetMessage("NOT ENOUGH COINS");
            return;
        }

        StartCoroutine(SpinSequence());
    }

    private void IncreaseBet()
    {
        if (spinning) return;
        bet = Mathf.Min(maximumBet, bet + betStep);
        UpdateLabels();
    }

    private void DecreaseBet()
    {
        if (spinning) return;
        bet = Mathf.Max(minimumBet, bet - betStep);
        UpdateLabels();
    }

    // ─────────────────────────────────────────────────────────────
    //  SPIN LOGIC
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Full spin cycle: lock controls, deduct bet, spin 3 reels,
    /// evaluate result, unlock controls.
    /// </summary>
    private IEnumerator SpinSequence()
    {
        spinning = true;
        SetControls(false);

        coins -= bet;
        UpdateLabels();
        SetMessage("GOOD LUCK!");

        int[] result = new int[3];

        for (int reel = 0; reel < 3; reel++)
        {
            result[reel] = GetRandomSymbolIndex(reel, result);
            StartCoroutine(AnimateReel(reel, result[reel]));
            yield return new WaitForSeconds(delayBetweenStops);
        }

        yield return new WaitForSeconds(reelSpinSeconds + 0.1f);

        EvaluateResult(result);

        UpdateLabels();
        SetControls(true);
        spinning = false;
    }

    /// <summary>
    /// Weighted RNG: 25% chance to match previous reel (makes wins more frequent
    /// for better player experience). Reduces frustration and improves feel.
    /// </summary>
    private int GetRandomSymbolIndex(int reel, int[] currentResult)
    {
        if (reel > 0 && Random.value < 0.25f)
            return currentResult[reel - 1];
        return Random.Range(0, symbols.Length);
    }

    /// <summary>
    /// Checks win conditions and applies the appropriate payout.
    /// Handles Wild substitution: wild matches any symbol.
    /// </summary>
    /// <summary>
    /// Checks win conditions and applies the appropriate payout.
    /// Strict rule: 3 identical OR 2 identical + 1 Wild.
    /// </summary>
    private void EvaluateResult(int[] result)
    {
        int a = result[0];
        int b = result[1];
        int c = result[2];

        bool hasWin = false;
        int winSymbol = -1;

        bool hasWild = wildSymbolIndex >= 0 && wildSymbolIndex < symbols.Length;

        // Case 1: All three are identical (includes 3 Wilds)
        if (a == b && b == c)
        {
            hasWin = true;
            winSymbol = a;
        }
        // Case 2: Two identical + One Wild
        else if (hasWild)
        {
            // Wild is at reel 1, and reel 2 & 3 match
            if (a == wildSymbolIndex && b == c && b != wildSymbolIndex)
            {
                hasWin = true;
                winSymbol = b;
            }
            // Wild is at reel 2, and reel 1 & 3 match
            else if (b == wildSymbolIndex && a == c && a != wildSymbolIndex)
            {
                hasWin = true;
                winSymbol = a;
            }
            // Wild is at reel 3, and reel 1 & 2 match
            else if (c == wildSymbolIndex && a == b && a != wildSymbolIndex)
            {
                hasWin = true;
                winSymbol = a;
            }
        }

        // --- APPLY PAYOUT ---
        if (hasWin && winSymbol >= 0)
        {
            // Multiplier: earlier symbols pay more
            int multiplier = winSymbol == 0 ? 10 :
                             winSymbol == 1 ? 5 :
                             winSymbol == 2 ? 4 : 3;

            int prize = bet * multiplier;
            coins += prize;

            StartCoroutine(FlashWin(winSymbol));
            SetMessage("WIN! +" + prize + " COINS");
        }
        else
        {
            SetMessage("NO WIN - TRY AGAIN!");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  REEL ANIMATION (SMOOTH VERTICAL SCROLL)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Animates a single reel using a vertical scrolling strip.
    /// The strip moves down, stops at the final symbol, then snaps.
    /// </summary>
    private IEnumerator AnimateReel(int index, int finalSymbol)
    {
        if (reelStrips == null || reelStrips[index] == null)
        {
            // Fallback: simple flicker if strips not built
            float elapsedF = 0f;
            while (elapsedF < reelSpinSeconds)
            {
                SetSymbol(index, Random.Range(0, symbols.Length));
                yield return new WaitForSeconds(0.075f);
                elapsedF += 0.075f;
            }
            SetSymbol(index, finalSymbol);
            yield break;
        }

        RectTransform strip = reelStrips[index];
        float elapsed = 0f;

        // Easing curve: fast in middle, slow at start & end (natural deceleration)
        AnimationCurve ease = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),
            new Keyframe(0.5f, 0.7f, 1.2f, 1.2f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

        // Total vertical distance the strip travels (multiple full symbol heights)
        float totalScroll = symbolHeight * (symbolsPerStrip + Random.Range(2, 4));

        // Position corresponding to the "final symbol" at rest
        float finalOffset = finalSymbol * symbolHeight;

        while (elapsed < reelSpinSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / reelSpinSeconds);

            // Moving strip downward
            float scroll = totalScroll * ease.Evaluate(t);
            // Modulo so it cycles through the repeating strip smoothly
            float y = scroll % (symbolHeight * symbols.Length);

            // Blend toward the final offset as we approach the end
            if (t > 0.75f)
            {
                float blend = Mathf.InverseLerp(0.75f, 1f, t);
                y = Mathf.Lerp(y, finalOffset, blend);
            }

            strip.anchoredPosition = new Vector2(0f, y);
            yield return null;
        }

        // Snap to final position
        strip.anchoredPosition = new Vector2(0f, finalOffset);

        // Update the visible reel sprite for editor/test feedback
        SetSymbol(index, finalSymbol);
    }

    /// <summary>
    /// Brief scale pulse on the winning reel for visual feedback.
    /// </summary>
    private IEnumerator FlashWin(int winSymbol)
    {
        foreach (Image reel in reels)
        {
            if (reel == null) continue;
            Transform t = reel.transform;
            Vector3 original = t.localScale;

            // Pop bigger then shrink back
            t.localScale = original * 1.15f;
            yield return new WaitForSeconds(0.15f);
            t.localScale = original;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  REEL STRIP BUILDER (EDITOR-GENERATED, RUNTIME-SAFE)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a scrolling strip behind each reel Image. Each strip contains
    /// 2 copies of the symbol list so it can cycle seamlessly.
    /// A RectMask2D on the parent clips the strip to the reel window.
    /// </summary>
    private void BuildReelStrips()
    {
        if (reels == null || symbols == null || symbols.Length == 0) return;
        if (reelStrips != null && reelStrips.Length == reels.Length) return;

        reelStrips = new RectTransform[reels.Length];

        for (int i = 0; i < reels.Length; i++)
        {
            if (reels[i] == null) continue;

            // Mask on the reel window (parent)
            RectTransform reelRT = reels[i].rectTransform;
            if (reelRT.GetComponent<RectMask2D>() == null)
                reelRT.gameObject.AddComponent<RectMask2D>();

            // Strip container as a child
            GameObject stripGO = new GameObject("Strip", typeof(RectTransform));
            stripGO.transform.SetParent(reelRT, false);
            RectTransform stripRT = stripGO.GetComponent<RectTransform>();
            stripRT.anchorMin = new Vector2(0.5f, 1f);
            stripRT.anchorMax = new Vector2(0.5f, 1f);
            stripRT.pivot = new Vector2(0.5f, 1f);
            stripRT.anchoredPosition = Vector2.zero;
            stripRT.sizeDelta = new Vector2(symbolHeight, symbolHeight * symbols.Length * 2);

            // Populate with 2 cycles of the symbol list
            int count = symbols.Length * 2;
            for (int s = 0; s < count; s++)
            {
                Sprite sym = symbols[s % symbols.Length];
                if (sym == null) continue;

                GameObject img = new GameObject("Sym_" + s, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                img.transform.SetParent(stripRT, false);
                RectTransform irt = img.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 1f);
                irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f);
                irt.anchoredPosition = new Vector2(0f, -s * symbolHeight);
                irt.sizeDelta = new Vector2(symbolHeight, symbolHeight);

                Image ii = img.GetComponent<Image>();
                ii.sprite = sym;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
            }

            // Reel visible Image is now hidden (strip shows through)
            reels[i].enabled = false;

            reelStrips[i] = stripRT;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    private void SetSymbol(int index, int symbolIndex)
    {
        if (index < 0 || index >= reels.Length) return;
        if (reels[index] == null || symbols == null) return;
        if (symbolIndex < 0 || symbolIndex >= symbols.Length) return;
        if (symbols[symbolIndex] == null) return;

        reels[index].sprite = symbols[symbolIndex];
        reels[index].preserveAspect = true;
    }

    private void SetControls(bool enabledState)
    {
        if (spinButton != null) spinButton.interactable = enabledState;
        if (betUpButton != null) betUpButton.interactable = enabledState;
        if (betDownButton != null) betDownButton.interactable = enabledState;
    }

    private void UpdateLabels()
    {
        if (balanceLabel != null) balanceLabel.text = "COINS: " + coins;
        if (betLabel != null) betLabel.text = "BET: " + bet;
    }

    private void SetMessage(string value)
    {
        if (messageLabel != null) messageLabel.text = value;
    }

    // ─────────────────────────────────────────────────────────────
    //  EDITOR: SCENE GENERATOR
    // ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [MenuItem("Underpin Slot Game/Build Complete Slot Machine")]
    public static void BuildCompleteGame()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";

        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Sprites");
        EnsureFolder("Assets/Scripts");

        Sprite backgroundSprite = FindSprite("bg_gradient");
        Sprite machineSprite = FindSprite("slot-machine1");
        Sprite[] slotSymbols = new Sprite[]
        {
            FindSprite("slot-symbol1"),
            FindSprite("slot-symbol2"),
            FindSprite("slot-symbol3"),
            FindSprite("slot-symbol4")
        };

        if (backgroundSprite == null || machineSprite == null ||
            slotSymbols[0] == null || slotSymbols[1] == null ||
            slotSymbols[2] == null || slotSymbols[3] == null)
        {
            EditorUtility.DisplayDialog(
                "Slot Machine assets missing",
                "Required PNG files not found in Assets/Sprites.\n\n" +
                "Required: bg_gradient.png, slot-machine1.png, " +
                "slot-symbol1.png to slot-symbol4.png.",
                "OK");
            return;
        }

        // Fresh empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── CAMERA (fixes "No cameras rendering") ──
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
        Camera mainCamera = cameraObject.GetComponent<Camera>();
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.06f, 0.06f, 0.12f);
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 5;
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 0, -10);

        // ── CANVAS ──
        GameObject canvasObject = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ── BACKGROUND ──
        CreateImage("Background", canvasObject.transform, backgroundSprite,
            Vector2.zero, new Vector2(1920, 1080), true);

        // ── MACHINE ART ──
        Image machine = CreateImage("SlotMachineArtwork", canvasObject.transform,
            machineSprite, new Vector2(0, 30), new Vector2(816, 624), false);
        machine.raycastTarget = false;

        // ── REEL WINDOWS ──
        Image reel1 = CreateImage("Reel1", canvasObject.transform, slotSymbols[0],
            new Vector2(-125, -39), new Vector2(88, 88), false);
        Image reel2 = CreateImage("Reel2", canvasObject.transform, slotSymbols[1],
            new Vector2(4, -39), new Vector2(88, 88), false);
        Image reel3 = CreateImage("Reel3", canvasObject.transform, slotSymbols[2],
            new Vector2(134, -39), new Vector2(88, 88), false);

        reel1.preserveAspect = true;
        reel2.preserveAspect = true;
        reel3.preserveAspect = true;

        // ── LABELS ──
        Text balance = CreateText("BalanceText", canvasObject.transform,
            "COINS: 1000", new Vector2(-220, -390), new Vector2(260, 55), 30);
        Text bet = CreateText("BetText", canvasObject.transform,
            "BET: 10", new Vector2(0, -390), new Vector2(200, 55), 30);
        Text message = CreateText("MessageText", canvasObject.transform,
            "READY TO PLAY", new Vector2(0, -325), new Vector2(600, 55), 30);

        // ── BUTTONS ──
        Button spin = CreateButton("SpinButton", canvasObject.transform,
            "SPIN", new Vector2(0, -475), new Vector2(260, 78),
            new Color(0.93f, 0.20f, 0.08f));
        Button betDown = CreateButton("BetDownButton", canvasObject.transform,
            "- BET", new Vector2(-235, -475), new Vector2(170, 70),
            new Color(0.18f, 0.22f, 0.58f));
        Button betUp = CreateButton("BetUpButton", canvasObject.transform,
            "+ BET", new Vector2(235, -475), new Vector2(170, 70),
            new Color(0.18f, 0.22f, 0.58f));

        // ── MANAGER ──
        GameObject managerObject = new GameObject("GameManager");
        SlotMachineAutoSetup manager = managerObject.AddComponent<SlotMachineAutoSetup>();
        manager.reels = new Image[] { reel1, reel2, reel3 };
        manager.symbols = slotSymbols;
        manager.spinButton = spin;
        manager.betUpButton = betUp;
        manager.betDownButton = betDown;
        manager.balanceLabel = balance;
        manager.betLabel = bet;
        manager.messageLabel = message;
        manager.wildSymbolIndex = 3; // last symbol = Wild

        // ── EVENT SYSTEM (Input System) ──
#if UNITY_2023_1_OR_NEWER
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
#else
        EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
#endif
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        // Remove any legacy input modules
        foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
            DestroyImmediate(module);

        // Add the new Input System module
        InputSystemUIInputModule inputModule =
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        // ── SAVE ──
        EditorSceneManager.SaveScene(scene, scenePath);
        Selection.activeGameObject = managerObject;

        EditorUtility.DisplayDialog(
            "Slot Machine created!",
            "Playable scene saved at:\n" + scenePath + "\n\nPress Play to test it.",
            "Let's Play");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string folder = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static Sprite FindSprite(string fileNameWithoutExtension)
    {
        string[] guids = AssetDatabase.FindAssets(fileNameWithoutExtension + " t:Texture2D");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == fileNameWithoutExtension)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool changed = importer.textureType != TextureImporterType.Sprite ||
                                   importer.spriteImportMode != SpriteImportMode.Single ||
                                   importer.textureCompression != TextureImporterCompression.Uncompressed;
                    if (changed)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                }
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return null;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite,
        Vector2 position, Vector2 size, bool stretch)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = !stretch;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value,
        Vector2 position, Vector2 size, int fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Text text = go.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = color;

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.8f, 0.35f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        button.colors = colors;

        Text text = CreateText("Label", go.transform, label, Vector2.zero, size, 28);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.color = Color.white;
        return button;
    }
#endif
}