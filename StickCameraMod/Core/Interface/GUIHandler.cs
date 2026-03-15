using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using StickCameraMod.Core.Interface.Panel_Handlers;
using StickCameraMod.Core.Mode_Handlers;
using StickCameraMod.Patches;
using StickCameraMod.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StickCameraMod.Core.Interface;

public class GUIHandler : Singleton<GUIHandler>
{
    public bool HasInitEventSystem;
    public bool CameraLocked;
    public bool AutoFollowEnabled = true;
    public bool ShowCrosshair = false;
    public bool ShowPlayerDistance = true;
    public int ZoomSpeed = 5;

    public TextMeshProUGUI FOVText;
    public TextMeshProUGUI NearClipText;
    public TextMeshProUGUI SmoothingText;
    public TextMeshProUGUI StatusText;

    public           Slider                        FOVSlider;
    public           Slider                        NearClipSlider;
    public           Slider                        SmoothingSlider;
    private readonly Dictionary<VRRig, GameObject> leaderboardEntries = new();

    private readonly Dictionary<VRRig, GameObject> rigButtons = new();
    private          TextMeshProUGUI               currentModeText;

    private TextMeshProUGUI currentPlayerText;

    private float           initTime;
    private TextMeshProUGUI isPlayerTaggedText;
    private Transform       leaderboard;
    private GameObject      leaderboardEntryPrefab;

    private GameObject mainPanel;

    private Camera miniMapCamera;

    private GameObject playerButtonPrefab;

    private Transform  playerContent;
    public  GameObject Canvas { get; private set; }

    private void Start()
    {
        GameObject canvasPrefab = Plugin.Instance.CastingBundle.LoadAsset<GameObject>("Canvas");
        Canvas = Instantiate(canvasPrefab);
        Destroy(canvasPrefab);
        Canvas.name = "StickCameraMod Canvas";

        Canvas.transform.Find("ClosestLava").AddComponent<ClosestTaggedHandler>();

        leaderboardEntryPrefab = Plugin.Instance.CastingBundle.LoadAsset<GameObject>("LeaderboardEntry");

        leaderboard = Canvas.transform.Find("Leaderboard");
        mainPanel   = Canvas.transform.Find("MainPanel").gameObject;

        ApplyApeXTheme();

        playerContent = mainPanel.transform.Find("Players/Viewport/Content");

        SetUpPlayerInformation(mainPanel);
        SetUpCameraSettings(mainPanel);

        playerButtonPrefab = Plugin.Instance.CastingBundle.LoadAsset<GameObject>("PlayerButton");

        if (SetColourPatch.SpawnedRigs.Contains(VRRig.LocalRig))
            OnRigSpawned(VRRig.LocalRig);

        RigUtils.OnRigSpawned      += OnRigSpawned;
        RigUtils.OnRigCached       += OnRigCached;
        RigUtils.OnRigNameChange   += UpdatePlayerName;
        RigUtils.OnMatIndexChange  += UpdatePlayerTagState;
        RigUtils.OnRigColourChange += UpdatePlayerColour;

        CoreHandler.Instance.OnCastedRigChange += OnCastedRigChange;
        CoreHandler.Instance.OnCurrentHandlerChange +=
                handlerName => currentModeText.text = $"Current Mode: {handlerName}";

        SetUpOtherPanels(mainPanel);

        Canvas.SetActive(false);

        GameObject modeButtonPrefab = Plugin.Instance.CastingBundle.LoadAsset<GameObject>("CameraModeButton");
        Transform  modeContent      = mainPanel.transform.Find("CameraModes/Viewport/Content");

        foreach (KeyValuePair<string, ModeHandlerBase> modeHandlerPair in CoreHandler.Instance.ModeHandlers)
        {
            GameObject modeButton = Instantiate(modeButtonPrefab, modeContent);
            modeButton.GetComponentInChildren<TextMeshProUGUI>().text = modeHandlerPair.Value.HandlerName;
            modeButton.GetComponent<Button>().onClick
                      .AddListener(() => CoreHandler.Instance.SetCurrentHandler(modeHandlerPair.Value.HandlerName));
        }

        CoreHandler.Instance.CurrentHandlerName = PlayerPrefs.GetString(CoreHandler.CurrentHandlerKey,
                FirstPersonModeHandler.HandlerNameStatic());

        RenderTexture miniMapRenderTexture =
                Instantiate(Plugin.Instance.CastingBundle.LoadAsset<RenderTexture>("MiniMapRenderTexture"));

        Canvas.transform.Find("MiniMap").GetComponent<RawImage>().texture = miniMapRenderTexture;

        miniMapCamera               = new GameObject("hi im a miinimap camera so cool").AddComponent<Camera>();
        miniMapCamera.fieldOfView   = 100;
        miniMapCamera.nearClipPlane = 10f;
        miniMapCamera.orthographic  = true;
        miniMapCamera.targetTexture = miniMapRenderTexture;

        initTime = Time.time;
    }

    private void Update()
    {
        if (Time.time - initTime < 5f)
            return;

        if (UnityInput.Current.GetKeyDown(KeyCode.C))
        {
            if (!HasInitEventSystem)
                InitEventSystem();
            else
                mainPanel.SetActive(!mainPanel.activeSelf);
        }

        if (UnityInput.Current.GetKeyDown(KeyCode.P))
        {
            string firstPersonHandlerName = FirstPersonModeHandler.HandlerNameStatic();
            CoreHandler.Instance.CurrentHandlerName = CoreHandler.Instance.CurrentHandlerName == firstPersonHandlerName
                                                              ? ThirdPersonHandler.HandlerNameStatic()
                                                              : firstPersonHandlerName;
        }

        if (UnityInput.Current.GetKeyDown(KeyCode.S))
        {
            string screenshotPath = $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures)}/ApeXCamera_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
            ScreenCapture.CaptureScreenshot(screenshotPath);
            Debug.Log($"Screenshot saved to {screenshotPath}");
        }

        for (int i = 0; i <= 9; i++)
        {
            KeyCode key = KeyCode.Alpha0 + i;
            if (UnityInput.Current.GetKeyDown(key) && SetColourPatch.SpawnedRigs.Count > i)
            {
                CoreHandler.Instance.CastedRig = SetColourPatch.SpawnedRigs[i];

                break;
            }
        }

        // FOV Presets
        if (UnityInput.Current.GetKeyDown(KeyCode.F1))
        {
            CoreHandler.Instance.SetFOV(Constants.FovFishEye);
            Debug.Log("FOV: Fisheye (170)");
        }
        if (UnityInput.Current.GetKeyDown(KeyCode.F2))
        {
            CoreHandler.Instance.SetFOV(Constants.FovWide);
            Debug.Log("FOV: Wide (100)");
        }
        if (UnityInput.Current.GetKeyDown(KeyCode.F3))
        {
            CoreHandler.Instance.SetFOV(Constants.FovNormal);
            Debug.Log("FOV: Normal (60)");
        }
        if (UnityInput.Current.GetKeyDown(KeyCode.F4))
        {
            CoreHandler.Instance.SetFOV(Constants.FovTelephoto);
            Debug.Log("FOV: Telephoto (30)");
        }

        // Camera Lock Toggle
        if (UnityInput.Current.GetKeyDown(KeyCode.L))
        {
            CameraLocked = !CameraLocked;
            Debug.Log($"Camera locked: {CameraLocked}");
        }

        // Auto-Follow Toggle
        if (UnityInput.Current.GetKeyDown(KeyCode.A))
        {
            AutoFollowEnabled = !AutoFollowEnabled;
            Debug.Log($"Auto-follow: {AutoFollowEnabled}");
        }

        // Crosshair Toggle
        if (UnityInput.Current.GetKeyDown(KeyCode.X))
        {
            ShowCrosshair = !ShowCrosshair;
            Debug.Log($"Crosshair: {(ShowCrosshair ? "Enabled" : "Disabled")}");
        }

        // Player Distance Toggle
        if (UnityInput.Current.GetKeyDown(KeyCode.D))
        {
            ShowPlayerDistance = !ShowPlayerDistance;
            Debug.Log($"Player distance display: {(ShowPlayerDistance ? "Enabled" : "Disabled")}");
        }

        // Zoom with Mouse Scroll
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            float scrollInput = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
            if (scrollInput != 0)
            {
                int currentFOV = (int)FOVSlider.value;
                int newFOV = Mathf.Clamp(currentFOV - (int)(scrollInput * ZoomSpeed), (int)FOVSlider.minValue, (int)FOVSlider.maxValue);
                CoreHandler.Instance.SetFOV(newFOV);
            }
        }

        // Reset Camera View
        if (UnityInput.Current.GetKeyDown(KeyCode.R))
        {
            CoreHandler.Instance.SetFOV(60);
            CoreHandler.Instance.SetNearClip(1);
            CoreHandler.Instance.SetSmoothing(18);
            Debug.Log("Camera reset to default settings");
        }
    }

    private void LateUpdate()
    {
        if (CameraHandler.Instance != null)
        {
            miniMapCamera.transform.position = CameraHandler.Instance.transform.position + Vector3.up * 17f;
            miniMapCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.up);
        }
    }

    private void OnGUI()
    {
        if (HasInitEventSystem || Time.time - initTime < 5f)
            return;

        GUI.Label(new Rect(0f, 0f, 500f, 100f), "Press 'C' to Open ApeX Camera");
    }

    private void SetUpCameraSettings(GameObject mainPanel)
    {
        Transform fovPanel = mainPanel.transform.Find("FOVPanel");
        FOVSlider = fovPanel.GetComponentInChildren<Slider>();
        FOVText   = fovPanel.GetComponentInChildren<TextMeshProUGUI>();

        CoreHandler.Instance.MaxFOV = (int)FOVSlider.maxValue;
        CoreHandler.Instance.MinFOV = (int)FOVSlider.minValue;

        FOVSlider.onValueChanged.AddListener(value => CoreHandler.Instance.SetFOV((int)value));

        Transform nearClipPanel = mainPanel.transform.Find("NearClipPanel");
        NearClipSlider = nearClipPanel.GetComponentInChildren<Slider>();
        NearClipText   = nearClipPanel.GetComponentInChildren<TextMeshProUGUI>();

        NearClipSlider.onValueChanged.AddListener(value => CoreHandler.Instance.SetNearClip((int)value));

        CoreHandler.Instance.MaxNearClip = (int)NearClipSlider.maxValue;
        CoreHandler.Instance.MinNearClip = (int)NearClipSlider.minValue;

        Transform smoothingPanel = mainPanel.transform.Find("SmoothingPanel");
        SmoothingSlider = smoothingPanel.GetComponentInChildren<Slider>();
        SmoothingText   = smoothingPanel.GetComponentInChildren<TextMeshProUGUI>();

        currentModeText = mainPanel.transform.Find("CurrentMode").GetComponent<TextMeshProUGUI>();

        CoreHandler.Instance.MaxSmoothing = (int)SmoothingSlider.maxValue;
        CoreHandler.Instance.MinSmoothing = (int)SmoothingSlider.minValue;

        SmoothingSlider.onValueChanged.AddListener(value => CoreHandler.Instance.SetSmoothing((int)value));
    }

    private void SetUpOtherPanels(GameObject mainPanel)
    {
        Transform panelButtonContent = mainPanel.transform.Find("Chin/Panels/Viewport/Content");
        Transform panels             = Canvas.transform.Find("Panels");
        Type[] panelHandlers = Assembly.GetExecutingAssembly().GetTypes()
                                       .Where(t => !t.IsAbstract || t.IsClass ||
                                                   typeof(PanelHandlerBase).IsAssignableFrom(t)).ToArray();

        foreach (Transform panel in panels)
        {
            panel.gameObject.SetActive(true);
            panel.AddComponent<DraggableUI>();
            panel.Find("Exit").GetComponent<Button>().onClick.AddListener(() => panel.gameObject.SetActive(false));
            Type chosenType =
                    panelHandlers.FirstOrDefault(type => type.Name ==
                                                         panel.gameObject.name.Replace(" ", "")[..^5] + "Handler");

            panel.gameObject.AddComponent(chosenType);
        }

        foreach (Transform panelButton in panelButtonContent)
            panelButton.GetComponent<Button>().onClick.AddListener(() =>
                                                                   {
                                                                       GameObject associatedPanel =
                                                                               panels.Find(
                                                                                       panelButton.gameObject.name +
                                                                                       "Panel").gameObject;

                                                                       associatedPanel.transform.localPosition =
                                                                               Vector3.zero;

                                                                       associatedPanel.SetActive(
                                                                               !associatedPanel.activeSelf);
                                                                   });
    }

    private void SetUpPlayerInformation(GameObject mainPanel)
    {
        Transform playerInformation = mainPanel.transform.Find("Chin/PlayerInformation");
        currentPlayerText  = playerInformation.Find("PlayerName").GetComponent<TextMeshProUGUI>();
        isPlayerTaggedText = playerInformation.Find("IsTagged").GetComponent<TextMeshProUGUI>();

        currentPlayerText.text = "No Player Selected";

        Transform moreInfo = Canvas.transform.Find("PlayerInfoPanel");
        moreInfo.AddComponent<DraggableUI>();
        moreInfo.AddComponent<MoreInfoHandler>();
        moreInfo.Find("Exit").GetComponent<Button>().onClick.AddListener(() => moreInfo.gameObject.SetActive(false));

        playerInformation.Find("MoreInfo").GetComponent<Button>().onClick.AddListener(() =>
            {
                moreInfo.localPosition = Vector3.zero;
                moreInfo.gameObject.SetActive(!moreInfo.gameObject.activeSelf);
            });
    }

    public void InitEventSystem()
    {
        HasInitEventSystem             = true;
        CoreHandler.Instance.CastedRig = VRRig.LocalRig;
        Canvas.SetActive(true);
        CameraHandler.Instance.Initialize();
        WorldSpaceHandler.Instance.SetUpRenderTexture();
        FOVSlider.onValueChanged?.Invoke(PlayerPrefs.GetInt(CoreHandler.FovKey,             100));
        NearClipSlider.onValueChanged?.Invoke(PlayerPrefs.GetInt(CoreHandler.NearClipKey,   1));
        SmoothingSlider.onValueChanged?.Invoke(PlayerPrefs.GetInt(CoreHandler.SmoothingKey, 18));
    }

    private void OnRigSpawned(VRRig rig)
    {
        GameObject button = Instantiate(playerButtonPrefab, playerContent);
        button.GetComponent<Button>().onClick.AddListener(() => CoreHandler.Instance.CastedRig = rig);
        button.GetComponentInChildren<TextMeshProUGUI>().text = rig.Creator?.NickName;
        rigButtons[rig]                                       = button;

        GameObject leaderboardEntry = Instantiate(leaderboardEntryPrefab, leaderboard);
        leaderboardEntry.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{SetColourPatch.SpawnedRigs.Count - 1}.{rig.Creator?.NickName}";

        leaderboardEntry.transform.Find("ColourPanel").GetComponent<Image>().color = rig.playerColor;
        leaderboardEntries[rig]                                                    = leaderboardEntry;
    }

    private void OnRigCached(VRRig rig)
    {
        if (rigButtons.ContainsKey(rig))
        {
            Destroy(rigButtons[rig]);
            rigButtons.Remove(rig);
        }

        if (CoreHandler.Instance.CastedRig == rig)
            CoreHandler.Instance.CastedRig = VRRig.LocalRig;

        if (leaderboardEntries.ContainsKey(rig))
        {
            Destroy(leaderboardEntries[rig]);
            leaderboardEntries.Remove(rig);
        }

        foreach (KeyValuePair<VRRig, GameObject> kvp in leaderboardEntries)
            kvp.Value.GetComponentInChildren<TextMeshProUGUI>().text =
                    $"{SetColourPatch.SpawnedRigs.IndexOf(kvp.Key)}.{(kvp.Key.Creator != null ? kvp.Key.Creator.NickName : kvp.Key.playerText1.text)}";
    }

    private void UpdatePlayerName(VRRig rig, string playerName)
    {
        if (rigButtons.TryGetValue(rig, out GameObject button))
            button.GetComponentInChildren<TextMeshProUGUI>().text = playerName;

        if (CoreHandler.Instance.CastedRig == rig)
            currentPlayerText.text =
                    $"Name: <color=#{ColorUtility.ToHtmlStringRGB(rig.playerColor)}>{playerName}</color>";

        if (leaderboardEntries.TryGetValue(rig, out GameObject entry))
            entry.GetComponentInChildren<TextMeshProUGUI>().text =
                    $"{SetColourPatch.SpawnedRigs.IndexOf(rig)}.{playerName}";
    }

    private void UpdatePlayerTagState(VRRig rig)
    {
        if (leaderboardEntries.TryGetValue(rig, out GameObject button))
            button.transform.Find("ColourPanel").GetComponent<Image>().color =
                    rig.IsTagged() ? new Color(1f, 0.3288f, 0f, 1f) : rig.playerColor;

        if (CoreHandler.Instance.CastedRig == rig)
            isPlayerTaggedText.text =
                    $"Is Tagged? {(rig.IsTagged() ? "<color=green>Yes!</color>" : "<color=red>No!</color>")}";
    }

    private void UpdatePlayerColour(VRRig rig, Color colour)
    {
        if (leaderboardEntries.TryGetValue(rig, out GameObject button))
            button.transform.Find("ColourPanel").GetComponent<Image>().color =
                    rig.IsTagged() ? new Color(1f, 0.3288f, 0f, 1f) : rig.playerColor;

        if (CoreHandler.Instance.CastedRig == rig)
        {
            string playerName = rig.Creator != null ? rig.Creator.NickName : rig.playerText1.text;
            currentPlayerText.text =
                    $"Name: <color=#{ColorUtility.ToHtmlStringRGB(rig.playerColor)}>{playerName}</color>";
        }
    }

    private void OnCastedRigChange(VRRig currentRig, VRRig lastRig)
    {
        string playerName = currentRig.Creator != null
                                    ? currentRig.Creator.NickName
                                    : currentRig.playerText1.text;

        currentPlayerText.text =
                $"Name: <color=#{ColorUtility.ToHtmlStringRGB(currentRig.playerColor)}>{playerName}</color>";

        isPlayerTaggedText.text =
                $"Is Tagged? {(currentRig.IsTagged() ? "<color=green>Yes!</color>" : "<color=red>No!</color>")}";
    }

    private void ApplyApeXTheme()
    {
        if (Canvas == null || mainPanel == null)
        {
            Debug.LogWarning("Canvas or mainPanel is null, skipping theme application");
            return;
        }

        // ── ApeX palette ──
        Color bgMain     = new Color(0.06f, 0.06f, 0.08f, 0.98f);   // near-black
        Color bgCard     = new Color(0.10f, 0.10f, 0.14f, 0.95f);   // card surfaces
        Color bgInput    = new Color(0.14f, 0.14f, 0.19f, 0.90f);   // input fields / slider bg
        Color accentGrn  = new Color(0.15f, 0.85f, 0.55f, 1f);      // mint green accent
        Color accentGrnD = new Color(0.10f, 0.55f, 0.35f, 1f);      // darker green
        Color accentOrng = new Color(1.0f, 0.55f, 0.15f, 1f);       // orange highlight
        Color textMain   = new Color(0.92f, 0.92f, 0.92f, 1f);      // primary text
        Color textDim    = new Color(0.55f, 0.55f, 0.60f, 1f);      // secondary text
        Color separator  = new Color(0.25f, 0.25f, 0.30f, 0.6f);    // divider lines
        Color headerBar  = new Color(0.15f, 0.85f, 0.55f, 0.12f);   // subtle green tint for header

        // ════════════════════════════════════════════
        //  1. RESTRUCTURE MAIN PANEL — different size & position
        // ════════════════════════════════════════════
        RectTransform mainRect = mainPanel.GetComponent<RectTransform>();
        if (mainRect != null)
        {
            mainRect.sizeDelta = new Vector2(mainRect.sizeDelta.x + 40, mainRect.sizeDelta.y + 20);
            mainRect.anchoredPosition += new Vector2(-20, 0);
        }

        // ════════════════════════════════════════════
        //  2. WIPE ALL ORIGINAL COLORS
        // ════════════════════════════════════════════
        Image[] allImages = Canvas.GetComponentsInChildren<Image>(true);
        foreach (Image img in allImages)
        {
            if (img.gameObject == Canvas.gameObject) continue;
            if (img.color.a < 0.05f) continue;

            RectTransform rt = img.GetComponent<RectTransform>();
            bool isLarge = rt != null && rt.rect.width > 60 && rt.rect.height > 60;

            // slider fills and handles are handled separately
            Slider parentSlider = img.GetComponentInParent<Slider>();
            if (parentSlider != null) continue;

            img.color = isLarge ? bgMain : bgCard;
        }

        // Main panel gets the base color
        Image mainImg = mainPanel.GetComponent<Image>();
        if (mainImg != null) mainImg.color = bgMain;

        // ════════════════════════════════════════════
        //  3. BUILD A CUSTOM HEADER BAR (replaces old title)
        // ════════════════════════════════════════════
        Transform topArea = mainPanel.transform.Find("Top");
        if (topArea != null)
        {
            RectTransform topRect = topArea.GetComponent<RectTransform>();
            if (topRect != null)
            {
                topRect.sizeDelta = new Vector2(topRect.sizeDelta.x, 50);
            }

            // Add a tinted background strip behind the title
            GameObject headerBg = new GameObject("ApeX_HeaderBg");
            headerBg.transform.SetParent(topArea, false);
            headerBg.transform.SetAsFirstSibling();
            Image headerBgImg = headerBg.AddComponent<Image>();
            headerBgImg.color = headerBar;
            RectTransform hbRect = headerBg.GetComponent<RectTransform>();
            hbRect.anchorMin = Vector2.zero;
            hbRect.anchorMax = Vector2.one;
            hbRect.offsetMin = Vector2.zero;
            hbRect.offsetMax = Vector2.zero;

            // Add a bottom accent line under the header
            GameObject headerLine = new GameObject("ApeX_HeaderLine");
            headerLine.transform.SetParent(topArea, false);
            Image lineImg = headerLine.AddComponent<Image>();
            lineImg.color = accentGrn;
            RectTransform hlRect = headerLine.GetComponent<RectTransform>();
            hlRect.anchorMin = new Vector2(0.05f, 0);
            hlRect.anchorMax = new Vector2(0.95f, 0);
            hlRect.sizeDelta = new Vector2(0, 2);
            hlRect.anchoredPosition = new Vector2(0, -2);

            // Overwrite the title text
            TextMeshProUGUI titleText = topArea.GetComponentInChildren<TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.text = "APEX";
                titleText.fontSize = titleText.fontSize * 1.4f;
                titleText.color = accentGrn;
                titleText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                titleText.characterSpacing = 12f;
                titleText.alignment = TextAlignmentOptions.Center;
            }
        }

        // ════════════════════════════════════════════
        //  4. RESTYLE SLIDER SECTIONS — add section labels & dividers
        // ════════════════════════════════════════════
        string[] sliderPanels = { "FOVPanel", "NearClipPanel", "SmoothingPanel" };
        string[] sectionLabels = { "FIELD OF VIEW", "NEAR CLIP", "SMOOTHING" };

        for (int i = 0; i < sliderPanels.Length; i++)
        {
            Transform panel = mainPanel.transform.Find(sliderPanels[i]);
            if (panel == null) continue;

            // Card background for each slider section
            Image panelImg = panel.GetComponent<Image>();
            if (panelImg != null) panelImg.color = bgCard;

            // Add padding by adjusting size
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.sizeDelta += new Vector2(0, 8);
            }

            // Add a small section label above the existing text
            GameObject sectionLabel = new GameObject($"ApeX_Label_{sliderPanels[i]}");
            sectionLabel.transform.SetParent(panel, false);
            sectionLabel.transform.SetAsFirstSibling();
            TextMeshProUGUI labelTmp = sectionLabel.AddComponent<TextMeshProUGUI>();
            labelTmp.text = sectionLabels[i];
            labelTmp.fontSize = 9;
            labelTmp.color = textDim;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.characterSpacing = 4f;
            labelTmp.alignment = TextAlignmentOptions.Center;
            if (Plugin.Instance.CasterFontBold != null)
            {
                labelTmp.font = Plugin.Instance.CasterFontBold;
                labelTmp.fontSharedMaterial = new Material(Plugin.Instance.TMP_DistanceField);
            }
            RectTransform labelRect = sectionLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 1);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.sizeDelta = new Vector2(0, 14);
            labelRect.anchoredPosition = new Vector2(0, -4);

            // Add a divider line below this section
            if (i < sliderPanels.Length - 1)
            {
                GameObject divider = new GameObject($"ApeX_Divider_{i}");
                divider.transform.SetParent(panel.parent, false);
                Image divImg = divider.AddComponent<Image>();
                divImg.color = separator;
                RectTransform divRect = divider.GetComponent<RectTransform>();
                divRect.sizeDelta = new Vector2(panelRect != null ? panelRect.sizeDelta.x * 0.85f : 200, 1);
                divRect.anchoredPosition = panelRect != null
                    ? new Vector2(panelRect.anchoredPosition.x, panelRect.anchoredPosition.y - panelRect.sizeDelta.y / 2 - 3)
                    : Vector2.zero;
            }
        }

        // ════════════════════════════════════════════
        //  5. RESTYLE SLIDERS — green fill, rounded handle
        // ════════════════════════════════════════════
        Slider[] allSliders = Canvas.GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in allSliders)
        {
            Image fillImg = slider.fillRect?.GetComponent<Image>();
            if (fillImg != null) fillImg.color = accentGrn;

            Image handleImg = slider.handleRect?.GetComponent<Image>();
            if (handleImg != null)
            {
                handleImg.color = Color.white;
                // Make handle smaller/circular
                RectTransform handleRect = handleImg.GetComponent<RectTransform>();
                if (handleRect != null)
                {
                    handleRect.sizeDelta = new Vector2(14, 14);
                }
            }

            Image bgImg = slider.transform.Find("Background")?.GetComponent<Image>();
            if (bgImg != null) bgImg.color = bgInput;
        }

        // ════════════════════════════════════════════
        //  6. RESTYLE BUTTONS — flat dark with green border look
        // ════════════════════════════════════════════
        Button[] allButtons = Canvas.GetComponentsInChildren<Button>(true);
        foreach (Button btn in allButtons)
        {
            Image btnImg = btn.GetComponent<Image>();
            if (btnImg != null) btnImg.color = bgCard;

            ColorBlock colors = btn.colors;
            colors.normalColor      = bgCard;
            colors.highlightedColor = new Color(0.15f, 0.85f, 0.55f, 0.25f); // green tint hover
            colors.pressedColor     = accentGrnD;
            colors.selectedColor    = new Color(0.15f, 0.85f, 0.55f, 0.18f);
            colors.fadeDuration     = 0.08f;
            btn.colors = colors;

            // Button text: white with smaller size
            TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.color = textMain;
                btnText.fontSize = Mathf.Max(btnText.fontSize * 0.9f, 11f);
            }

            // Add a left accent bar to each button
            GameObject btnAccent = new GameObject("ApeX_BtnAccent");
            btnAccent.transform.SetParent(btn.transform, false);
            btnAccent.transform.SetAsFirstSibling();
            Image accentImg = btnAccent.AddComponent<Image>();
            accentImg.color = accentGrn;
            RectTransform accentRect = btnAccent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0, 0.15f);
            accentRect.anchorMax = new Vector2(0, 0.85f);
            accentRect.sizeDelta = new Vector2(3, 0);
            accentRect.anchoredPosition = new Vector2(2, 0);
        }

        // ════════════════════════════════════════════
        //  7. RESTYLE ALL TEXT
        // ════════════════════════════════════════════
        TextMeshProUGUI[] allText = Canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI tmp in allText)
        {
            if (tmp.color.a < 0.05f) continue;
            // Skip the title and section labels we just created
            if (tmp.gameObject.name.StartsWith("ApeX_")) continue;
            tmp.color = textMain;
        }

        // ════════════════════════════════════════════
        //  8. RESTYLE CAMERA MODE SECTION
        // ════════════════════════════════════════════
        Transform cameraModes = mainPanel.transform.Find("CameraModes");
        if (cameraModes != null)
        {
            Image cmImg = cameraModes.GetComponent<Image>();
            if (cmImg != null) cmImg.color = bgCard;

            // Add "CAMERA MODES" header
            GameObject cmHeader = new GameObject("ApeX_CameraModesHeader");
            cmHeader.transform.SetParent(cameraModes, false);
            cmHeader.transform.SetAsFirstSibling();
            TextMeshProUGUI cmTmp = cmHeader.AddComponent<TextMeshProUGUI>();
            cmTmp.text = "CAMERA MODES";
            cmTmp.fontSize = 9;
            cmTmp.color = accentOrng;
            cmTmp.fontStyle = FontStyles.Bold;
            cmTmp.characterSpacing = 3f;
            cmTmp.alignment = TextAlignmentOptions.Center;
            if (Plugin.Instance.CasterFontBold != null)
            {
                cmTmp.font = Plugin.Instance.CasterFontBold;
                cmTmp.fontSharedMaterial = new Material(Plugin.Instance.TMP_DistanceField);
            }
            RectTransform cmRect = cmHeader.GetComponent<RectTransform>();
            cmRect.anchorMin = new Vector2(0, 1);
            cmRect.anchorMax = new Vector2(1, 1);
            cmRect.sizeDelta = new Vector2(0, 16);
            cmRect.anchoredPosition = new Vector2(0, -2);
        }

        // ════════════════════════════════════════════
        //  9. RESTYLE PLAYER LIST SECTION
        // ════════════════════════════════════════════
        Transform players = mainPanel.transform.Find("Players");
        if (players != null)
        {
            Image plImg = players.GetComponent<Image>();
            if (plImg != null) plImg.color = bgCard;

            // Add "PLAYERS" header
            GameObject plHeader = new GameObject("ApeX_PlayersHeader");
            plHeader.transform.SetParent(players, false);
            plHeader.transform.SetAsFirstSibling();
            TextMeshProUGUI plTmp = plHeader.AddComponent<TextMeshProUGUI>();
            plTmp.text = "PLAYERS";
            plTmp.fontSize = 9;
            plTmp.color = accentOrng;
            plTmp.fontStyle = FontStyles.Bold;
            plTmp.characterSpacing = 3f;
            plTmp.alignment = TextAlignmentOptions.Center;
            if (Plugin.Instance.CasterFontBold != null)
            {
                plTmp.font = Plugin.Instance.CasterFontBold;
                plTmp.fontSharedMaterial = new Material(Plugin.Instance.TMP_DistanceField);
            }
            RectTransform plRect = plHeader.GetComponent<RectTransform>();
            plRect.anchorMin = new Vector2(0, 1);
            plRect.anchorMax = new Vector2(1, 1);
            plRect.sizeDelta = new Vector2(0, 16);
            plRect.anchoredPosition = new Vector2(0, -2);
        }

        // ════════════════════════════════════════════
        //  10. RESTYLE CHIN / BOTTOM BAR
        // ════════════════════════════════════════════
        Transform chin = mainPanel.transform.Find("Chin");
        if (chin != null)
        {
            Image chinImg = chin.GetComponent<Image>();
            if (chinImg != null) chinImg.color = new Color(0.04f, 0.04f, 0.06f, 0.98f);

            // Add a top border line to separate from main content
            GameObject chinBorder = new GameObject("ApeX_ChinBorder");
            chinBorder.transform.SetParent(chin, false);
            chinBorder.transform.SetAsFirstSibling();
            Image cbImg = chinBorder.AddComponent<Image>();
            cbImg.color = separator;
            RectTransform cbRect = chinBorder.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(0.05f, 1);
            cbRect.anchorMax = new Vector2(0.95f, 1);
            cbRect.sizeDelta = new Vector2(0, 1);
            cbRect.anchoredPosition = Vector2.zero;
        }

        // ════════════════════════════════════════════
        //  11. RESTYLE CURRENT MODE TEXT
        // ════════════════════════════════════════════
        Transform currentMode = mainPanel.transform.Find("CurrentMode");
        if (currentMode != null)
        {
            TextMeshProUGUI modeTmp = currentMode.GetComponent<TextMeshProUGUI>();
            if (modeTmp != null)
            {
                modeTmp.color = accentGrn;
                modeTmp.fontStyle = FontStyles.Italic;
                modeTmp.fontSize = Mathf.Max(modeTmp.fontSize * 0.85f, 12f);
            }
        }

        // ════════════════════════════════════════════
        //  12. RESTYLE SUB-PANELS (Panels group)
        // ════════════════════════════════════════════
        Transform panels = Canvas.transform.Find("Panels");
        if (panels != null)
        {
            foreach (Transform panel in panels)
            {
                Image pImg = panel.GetComponent<Image>();
                if (pImg != null) pImg.color = bgMain;

                // Restyle the exit button on each panel
                Transform exit = panel.Find("Exit");
                if (exit != null)
                {
                    Image exitImg = exit.GetComponent<Image>();
                    if (exitImg != null) exitImg.color = new Color(0.8f, 0.15f, 0.15f, 0.9f);

                    TextMeshProUGUI exitTxt = exit.GetComponentInChildren<TextMeshProUGUI>();
                    if (exitTxt != null)
                    {
                        exitTxt.color = Color.white;
                        exitTxt.text = "X";
                        exitTxt.fontStyle = FontStyles.Bold;
                    }
                }

                // Add a top accent bar to each sub-panel
                GameObject panelTopBar = new GameObject("ApeX_PanelTopBar");
                panelTopBar.transform.SetParent(panel, false);
                panelTopBar.transform.SetAsFirstSibling();
                Image ptbImg = panelTopBar.AddComponent<Image>();
                ptbImg.color = accentGrn;
                RectTransform ptbRect = panelTopBar.GetComponent<RectTransform>();
                ptbRect.anchorMin = new Vector2(0, 1);
                ptbRect.anchorMax = new Vector2(1, 1);
                ptbRect.sizeDelta = new Vector2(0, 3);
                ptbRect.anchoredPosition = Vector2.zero;
            }
        }

        // ════════════════════════════════════════════
        //  13. RESTYLE LEADERBOARD
        // ════════════════════════════════════════════
        Transform lb = Canvas.transform.Find("Leaderboard");
        if (lb != null)
        {
            Image lbImg = lb.GetComponent<Image>();
            if (lbImg != null) lbImg.color = bgMain;
        }

        // ════════════════════════════════════════════
        //  14. RESTYLE MINIMAP
        // ════════════════════════════════════════════
        Transform miniMap = Canvas.transform.Find("MiniMap");
        if (miniMap != null)
        {
            // Add a green border around the minimap
            GameObject mmBorder = new GameObject("ApeX_MiniMapBorder");
            mmBorder.transform.SetParent(miniMap.parent, false);
            mmBorder.transform.SetSiblingIndex(miniMap.GetSiblingIndex());
            Image mmBorderImg = mmBorder.AddComponent<Image>();
            mmBorderImg.color = accentGrn;
            RectTransform mmBorderRect = mmBorder.GetComponent<RectTransform>();
            RectTransform miniMapRect = miniMap.GetComponent<RectTransform>();
            mmBorderRect.anchorMin = miniMapRect.anchorMin;
            mmBorderRect.anchorMax = miniMapRect.anchorMax;
            mmBorderRect.anchoredPosition = miniMapRect.anchoredPosition;
            mmBorderRect.sizeDelta = miniMapRect.sizeDelta + new Vector2(4, 4);
        }

        // ════════════════════════════════════════════
        //  15. ADD FOOTER BRANDING
        // ════════════════════════════════════════════
        GameObject footer = new GameObject("ApeX_Footer");
        footer.transform.SetParent(mainPanel.transform, false);
        TextMeshProUGUI footerTmp = footer.AddComponent<TextMeshProUGUI>();
        footerTmp.text = "ApeX Camera Mod  v" + Constants.PluginVersion;
        footerTmp.fontSize = 8;
        footerTmp.color = textDim;
        footerTmp.fontStyle = FontStyles.Italic;
        footerTmp.alignment = TextAlignmentOptions.Center;
        if (Plugin.Instance.CasterFontBold != null)
        {
            footerTmp.font = Plugin.Instance.CasterFontBold;
            footerTmp.fontSharedMaterial = new Material(Plugin.Instance.TMP_DistanceField);
        }
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0, 0);
        footerRect.anchorMax = new Vector2(1, 0);
        footerRect.sizeDelta = new Vector2(0, 18);
        footerRect.anchoredPosition = new Vector2(0, 6);

        // ════════════════════════════════════════════
        //  16. ADD "PLAYER INFO" SECTION HEADER
        // ════════════════════════════════════════════
        Transform playerInfo = mainPanel.transform.Find("Chin/PlayerInformation");
        if (playerInfo != null)
        {
            Image piImg = playerInfo.GetComponent<Image>();
            if (piImg != null) piImg.color = bgCard;

            GameObject piHeader = new GameObject("ApeX_PlayerInfoHeader");
            piHeader.transform.SetParent(playerInfo, false);
            piHeader.transform.SetAsFirstSibling();
            TextMeshProUGUI piTmp = piHeader.AddComponent<TextMeshProUGUI>();
            piTmp.text = "TRACKING";
            piTmp.fontSize = 8;
            piTmp.color = accentOrng;
            piTmp.fontStyle = FontStyles.Bold;
            piTmp.characterSpacing = 5f;
            piTmp.alignment = TextAlignmentOptions.Center;
            if (Plugin.Instance.CasterFontBold != null)
            {
                piTmp.font = Plugin.Instance.CasterFontBold;
                piTmp.fontSharedMaterial = new Material(Plugin.Instance.TMP_DistanceField);
            }
            RectTransform piRect = piHeader.GetComponent<RectTransform>();
            piRect.anchorMin = new Vector2(0, 1);
            piRect.anchorMax = new Vector2(1, 1);
            piRect.sizeDelta = new Vector2(0, 14);
            piRect.anchoredPosition = new Vector2(0, -1);
        }

        // ════════════════════════════════════════════
        //  17. RESTYLE PLAYER INFO PANEL (popup)
        // ════════════════════════════════════════════
        Transform playerInfoPanel = Canvas.transform.Find("PlayerInfoPanel");
        if (playerInfoPanel != null)
        {
            Image pipImg = playerInfoPanel.GetComponent<Image>();
            if (pipImg != null) pipImg.color = bgMain;

            // Top accent bar
            GameObject pipBar = new GameObject("ApeX_PlayerInfoPanelBar");
            pipBar.transform.SetParent(playerInfoPanel, false);
            pipBar.transform.SetAsFirstSibling();
            Image pipBarImg = pipBar.AddComponent<Image>();
            pipBarImg.color = accentOrng;
            RectTransform pipBarRect = pipBar.GetComponent<RectTransform>();
            pipBarRect.anchorMin = new Vector2(0, 1);
            pipBarRect.anchorMax = new Vector2(1, 1);
            pipBarRect.sizeDelta = new Vector2(0, 3);
            pipBarRect.anchoredPosition = Vector2.zero;
        }

        // Scrub all old credits
        ReplaceOldCredits();

        Debug.Log("ApeX Camera Mod theme applied!");
    }

    private void ReplaceOldCredits()
    {
        TextMeshProUGUI[] allText = Canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI tmp in allText)
        {
            if (string.IsNullOrEmpty(tmp.text)) continue;

            string original = tmp.text;
            string lower = original.ToLower();

            // If the text contains any old credit references, check what kind it is
            bool hasOldCredit = lower.Contains("hansolo") || lower.Contains("hamburbur") ||
                                lower.Contains("casting should be free") || lower.Contains("castingshouldbefree");

            if (!hasOldCredit) continue;

            // If it looks like a "made by" or credit line, replace the whole thing
            if (lower.Contains("made by") || lower.Contains("created by") ||
                lower.Contains("developed by") || lower.Contains("author") ||
                lower.Contains("credit"))
            {
                tmp.text = "Made by St1ck\nDiscord: st1ckgt";
                Debug.Log($"Replaced credit block in '{tmp.gameObject.name}'");
            }
            else
            {
                // Otherwise just swap out the names
                string replaced = original;
                replaced = replaced.Replace("HanSolo1000Falcon", "St1ck");
                replaced = replaced.Replace("HanSolo", "St1ck");
                replaced = replaced.Replace("hansolo", "St1ck");
                replaced = replaced.Replace("Casting Should Be Free", "ApeX Camera Mod");
                replaced = replaced.Replace("CastingShouldBeFree", "ApeX Camera Mod");
                replaced = replaced.Replace("castingshouldbefree", "ApeX Camera Mod");
                replaced = replaced.Replace("hamburbur", "St1ck");
                tmp.text = replaced;
                Debug.Log($"Replaced name in '{tmp.gameObject.name}'");
            }
        }
    }
}