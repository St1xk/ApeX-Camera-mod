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
    private GameObject colorPanel;
    private Image      colorPreview;
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

        SetUpColorPanel();
        ApplyApeXTheme();

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

        // Screenshot (F5 to avoid conflict with WASD fly S key)
        if (UnityInput.Current.GetKeyDown(KeyCode.F5))
        {
            string screenshotPath = $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures)}/ApeXCamera_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
            ScreenCapture.CaptureScreenshot(screenshotPath);
            Debug.Log($"Screenshot saved to {screenshotPath}");
        }

        // Quick select player by number key
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
            CoreHandler.Instance.SetFOV(Constants.FovFishEye);
        if (UnityInput.Current.GetKeyDown(KeyCode.F2))
            CoreHandler.Instance.SetFOV(Constants.FovWide);
        if (UnityInput.Current.GetKeyDown(KeyCode.F3))
            CoreHandler.Instance.SetFOV(Constants.FovNormal);
        if (UnityInput.Current.GetKeyDown(KeyCode.F4))
            CoreHandler.Instance.SetFOV(Constants.FovTelephoto);

        // Toggle player distance on nametags
        if (UnityInput.Current.GetKeyDown(KeyCode.F6))
            ShowPlayerDistance = !ShowPlayerDistance;

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
        if (UnityInput.Current.GetKeyDown(KeyCode.F7))
        {
            CoreHandler.Instance.SetFOV(60);
            CoreHandler.Instance.SetNearClip(1);
            CoreHandler.Instance.SetSmoothing(18);
        }

        // Cycle to next player
        if (UnityInput.Current.GetKeyDown(KeyCode.N))
        {
            if (SetColourPatch.SpawnedRigs.Count > 0)
            {
                int currentIndex = SetColourPatch.SpawnedRigs.IndexOf(CoreHandler.Instance.CastedRig);
                int nextIndex = (currentIndex + 1) % SetColourPatch.SpawnedRigs.Count;
                CoreHandler.Instance.CastedRig = SetColourPatch.SpawnedRigs[nextIndex];
            }
        }

        // Cycle to previous player
        if (UnityInput.Current.GetKeyDown(KeyCode.B))
        {
            if (SetColourPatch.SpawnedRigs.Count > 0)
            {
                int currentIndex = SetColourPatch.SpawnedRigs.IndexOf(CoreHandler.Instance.CastedRig);
                int prevIndex = currentIndex <= 0 ? SetColourPatch.SpawnedRigs.Count - 1 : currentIndex - 1;
                CoreHandler.Instance.CastedRig = SetColourPatch.SpawnedRigs[prevIndex];
            }
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

        // Theme the new button to match
        ThemeButton(button);

        GameObject leaderboardEntry = Instantiate(leaderboardEntryPrefab, leaderboard);
        leaderboardEntry.GetComponentInChildren<TextMeshProUGUI>().text =
                $"{SetColourPatch.SpawnedRigs.Count - 1}.{rig.Creator?.NickName}";

        leaderboardEntry.transform.Find("ColourPanel").GetComponent<Image>().color = rig.playerColor;
        leaderboardEntries[rig]                                                    = leaderboardEntry;

        // Theme the leaderboard entry background
        Image entryImg = leaderboardEntry.GetComponent<Image>();
        if (entryImg != null) entryImg.color = new Color(0.95f, 0.95f, 0.95f, 0.97f);
    }

    private void ThemeButton(GameObject buttonObj)
    {
        Color btnNormal = new Color(0.15f, 0.15f, 0.15f, 1f);
        Color btnHover  = new Color(0.30f, 0.30f, 0.30f, 1f);
        Color btnPress  = new Color(0.05f, 0.05f, 0.05f, 1f);

        Image btnImg = buttonObj.GetComponent<Image>();
        if (btnImg != null) btnImg.color = btnNormal;

        Button btn = buttonObj.GetComponent<Button>();
        if (btn != null)
        {
            ColorBlock colors = btn.colors;
            colors.normalColor      = btnNormal;
            colors.highlightedColor = btnHover;
            colors.pressedColor     = btnPress;
            colors.selectedColor    = btnHover;
            btn.colors = colors;
        }

        foreach (TextMeshProUGUI txt in buttonObj.GetComponentsInChildren<TextMeshProUGUI>(true))
            txt.color = new Color(0.95f, 0.95f, 0.95f, 1f);
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

    private void SetUpColorPanel()
    {
        // Create the color panel as a child of mainPanel so it shows with the UI
        colorPanel = new GameObject("ColorPanel");
        colorPanel.transform.SetParent(mainPanel.transform, false);

        Image panelBg = colorPanel.AddComponent<Image>();
        panelBg.color = new Color(0.90f, 0.90f, 0.90f, 0.97f);

        RectTransform panelRect = colorPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 0.5f);
        panelRect.anchorMax = new Vector2(1, 0.5f);
        panelRect.pivot = new Vector2(0, 0.5f);
        panelRect.sizeDelta = new Vector2(260, 200);
        panelRect.anchoredPosition = new Vector2(5, 0);

        // Title
        GameObject titleObj = new GameObject("ColorTitle");
        titleObj.transform.SetParent(colorPanel.transform, false);
        TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Player Color";
        titleTmp.fontSize = 18;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        titleTmp.alignment = TextAlignmentOptions.Center;
        if (Plugin.Instance.CasterFontBold != null)
        {
            titleTmp.font = Plugin.Instance.CasterFontBold;
            titleTmp.fontSharedMaterial = new Material(Plugin.Instance.TMP_DistanceField);
        }
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.sizeDelta = new Vector2(0, 30);
        titleRect.anchoredPosition = new Vector2(0, -15);

        // Color preview box
        GameObject previewObj = new GameObject("ColorPreview");
        previewObj.transform.SetParent(colorPanel.transform, false);
        colorPreview = previewObj.AddComponent<Image>();
        float savedR = PlayerPrefs.GetFloat("redValue", 0.5f);
        float savedG = PlayerPrefs.GetFloat("greenValue", 0.5f);
        float savedB = PlayerPrefs.GetFloat("blueValue", 0.5f);
        colorPreview.color = new Color(savedR, savedG, savedB, 1f);
        RectTransform previewRect = previewObj.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.5f, 1);
        previewRect.anchorMax = new Vector2(0.5f, 1);
        previewRect.sizeDelta = new Vector2(40, 40);
        previewRect.anchoredPosition = new Vector2(0, -50);

        // Create R, G, B sliders
        CreateColorSlider(colorPanel.transform, "R", new Color(0.9f, 0.2f, 0.2f, 1f), -95, savedR, value =>
        {
            PlayerPrefs.SetFloat("redValue", value);
            ApplyPlayerColor();
        });

        CreateColorSlider(colorPanel.transform, "G", new Color(0.2f, 0.8f, 0.2f, 1f), -135, savedG, value =>
        {
            PlayerPrefs.SetFloat("greenValue", value);
            ApplyPlayerColor();
        });

        CreateColorSlider(colorPanel.transform, "B", new Color(0.2f, 0.4f, 0.9f, 1f), -175, savedB, value =>
        {
            PlayerPrefs.SetFloat("blueValue", value);
            ApplyPlayerColor();
        });

    }

    private void CreateColorSlider(Transform parent, string label, Color fillColor, float yPos, float initialValue, UnityEngine.Events.UnityAction<float> onChange)
    {
        // Container
        GameObject container = new GameObject($"{label}Slider");
        container.transform.SetParent(parent, false);
        RectTransform containerRect = container.GetComponent<RectTransform>() ?? container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(1, 1);
        containerRect.sizeDelta = new Vector2(-40, 25);
        containerRect.anchoredPosition = new Vector2(10, yPos);

        // Label
        GameObject labelObj = new GameObject($"{label}Label");
        labelObj.transform.SetParent(container.transform, false);
        TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 14;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = fillColor;
        labelTmp.alignment = TextAlignmentOptions.Center;
        if (Plugin.Instance.CasterFontBold != null)
        {
            labelTmp.font = Plugin.Instance.CasterFontBold;
            labelTmp.fontSharedMaterial = new Material(Plugin.Instance.TMP_DistanceField);
        }
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0, 1);
        labelRect.sizeDelta = new Vector2(25, 0);
        labelRect.anchoredPosition = new Vector2(12, 0);

        // Slider background
        GameObject sliderObj = new GameObject($"{label}SliderControl");
        sliderObj.transform.SetParent(container.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 0);
        sliderRect.anchorMax = new Vector2(1, 1);
        sliderRect.offsetMin = new Vector2(30, 2);
        sliderRect.offsetMax = new Vector2(-5, -2);

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.75f, 0.75f, 0.75f, 0.9f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = fillColor;
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Handle
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleArea.transform, false);
        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.color = Color.white;
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(12, 0);
        handleRect.anchorMin = new Vector2(0, 0);
        handleRect.anchorMax = new Vector2(0, 1);

        // Slider component
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initialValue;
        slider.onValueChanged.AddListener(onChange);
    }

    private void ApplyPlayerColor()
    {
        float r = PlayerPrefs.GetFloat("redValue", 0.5f);
        float g = PlayerPrefs.GetFloat("greenValue", 0.5f);
        float b = PlayerPrefs.GetFloat("blueValue", 0.5f);
        PlayerPrefs.Save();
        GorillaTagger.Instance.UpdateColor(r, g, b);
        UpdateColorPreview();
    }

    private void UpdateColorPreview()
    {
        if (colorPreview != null)
        {
            float r = PlayerPrefs.GetFloat("redValue", 0.5f);
            float g = PlayerPrefs.GetFloat("greenValue", 0.5f);
            float b = PlayerPrefs.GetFloat("blueValue", 0.5f);
            colorPreview.color = new Color(r, g, b, 1f);
        }
    }

    private void ApplyApeXTheme()
    {
        if (Canvas == null || mainPanel == null) return;

        // White and black theme
        Color bgPanel   = new Color(0.95f, 0.95f, 0.95f, 0.97f);  // light grey panels
        Color bgDarker  = new Color(0.85f, 0.85f, 0.85f, 0.95f);  // slightly darker for sub-elements
        Color btnNormal = new Color(0.15f, 0.15f, 0.15f, 1f);     // dark buttons
        Color btnHover  = new Color(0.30f, 0.30f, 0.30f, 1f);     // hover
        Color btnPress  = new Color(0.05f, 0.05f, 0.05f, 1f);     // pressed
        Color sliderFill = new Color(0.10f, 0.10f, 0.10f, 1f);    // black slider fill
        Color sliderBg  = new Color(0.75f, 0.75f, 0.75f, 0.90f);  // grey slider bg
        Color textDark  = new Color(0.05f, 0.05f, 0.05f, 1f);     // black text
        Color textLight = new Color(0.95f, 0.95f, 0.95f, 1f);     // white text (for dark buttons)

        // Step 1: Recolor EVERY image to light grey (nukes all purple)
        // Skip TMP SubMeshUI and RawImage objects — recoloring them breaks text rendering
        foreach (Image img in Canvas.GetComponentsInChildren<Image>(true))
        {
            if (img.color.a < 0.1f) continue;
            if (img.gameObject.GetComponent<TMP_SubMeshUI>() != null) continue;
            if (img.gameObject.GetComponent<TextMeshProUGUI>() != null) continue;
            img.color = bgPanel;
        }

        // Step 2: Sliders - set fill, handle, background
        foreach (Slider slider in Canvas.GetComponentsInChildren<Slider>(true))
        {
            Image fillImg = slider.fillRect?.GetComponent<Image>();
            if (fillImg != null) fillImg.color = sliderFill;

            Image handleImg = slider.handleRect?.GetComponent<Image>();
            if (handleImg != null) handleImg.color = btnNormal;

            Image bgImg = slider.transform.Find("Background")?.GetComponent<Image>();
            if (bgImg != null) bgImg.color = sliderBg;
        }

        // Step 3: ALL text to black first
        foreach (TextMeshProUGUI tmp in Canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.color.a < 0.1f) continue;
            tmp.color = textDark;
        }

        // Step 4: Buttons - dark background, white text (AFTER text loop so it overrides)
        foreach (Button btn in Canvas.GetComponentsInChildren<Button>(true))
        {
            Image btnImg = btn.GetComponent<Image>();
            if (btnImg != null) btnImg.color = btnNormal;

            ColorBlock colors = btn.colors;
            colors.normalColor      = btnNormal;
            colors.highlightedColor = btnHover;
            colors.pressedColor     = btnPress;
            colors.selectedColor    = btnHover;
            btn.colors = colors;

            // Force ALL text inside this button to white
            foreach (TextMeshProUGUI btnText in btn.GetComponentsInChildren<TextMeshProUGUI>(true))
                btnText.color = textLight;
        }

        // Rename all UI text to match ApeX features
        ReplaceAllText();
    }

    private void ReplaceAllText()
    {
        HashSet<TextMeshProUGUI> alreadySet = new();

        TextMeshProUGUI[] allText = Canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI tmp in allText)
        {
            if (string.IsNullOrEmpty(tmp.text)) continue;

            string name = tmp.gameObject.name;

            // Title
            if (name == "Title" && tmp.text.ToLower().Contains("casting"))
            {
                tmp.text = "ApeX Camera Mod";
                alreadySet.Add(tmp);
                continue;
            }

            // Self promo / credits
            if (name == "SelfPromo")
            {
                tmp.text = "UI made by Hansolo1000\nMod by St1ck | Discord: st1ckgt";
                alreadySet.Add(tmp);
                continue;
            }

            // Lock interface button
            if (tmp.text.Contains("Lock") && tmp.text.Contains("Interface"))
            {
                tmp.text = "Pin\nOverlay";
                alreadySet.Add(tmp);
                continue;
            }

            // Player info title
            if (name == "Title" && tmp.text.Contains("Player Information"))
            {
                tmp.text = "Spectate Target";
                alreadySet.Add(tmp);
                continue;
            }
        }

        // General old name replacements — skip anything we already set above
        foreach (TextMeshProUGUI tmp in allText)
        {
            if (string.IsNullOrEmpty(tmp.text)) continue;
            if (alreadySet.Contains(tmp)) continue;

            string lower = tmp.text.ToLower();
            if (lower.Contains("hansolo") || lower.Contains("hamburbur") ||
                lower.Contains("casting should be free") || lower.Contains("castingshouldbefree"))
            {
                string replaced = tmp.text;
                replaced = replaced.Replace("HanSolo1000Falcon", "St1ck");
                replaced = replaced.Replace("HanSolo", "St1ck");
                replaced = replaced.Replace("hansolo", "St1ck");
                replaced = replaced.Replace("Casting Should Be Free", "ApeX Camera Mod");
                replaced = replaced.Replace("CastingShouldBeFree", "ApeX Camera Mod");
                replaced = replaced.Replace("castingshouldbefree", "ApeX Camera Mod");
                replaced = replaced.Replace("hamburbur", "St1ck");
                tmp.text = replaced;
            }
        }
    }
}