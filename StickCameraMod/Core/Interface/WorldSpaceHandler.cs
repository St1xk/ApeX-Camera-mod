using System.Collections.Generic;
using StickCameraMod.Core.Mode_Handlers;
using StickCameraMod.Utils;
using GorillaLocomotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StickCameraMod.Core.Interface;

public class WorldSpaceHandler : Singleton<WorldSpaceHandler>
{
    public Camera RenderTextureCamera;

    public TextMeshProUGUI FOVText;
    public TextMeshProUGUI NearClipText;
    public TextMeshProUGUI SmoothingText;

    public GameObject Canvas;

    private float initTime;

    private bool isInterfaceLocked;

    private bool wasPressed;

    private void Start()
    {
        GameObject canvasPrefab = Plugin.Instance.CastingBundle.LoadAsset<GameObject>("InGameCanvas");
        Canvas = Instantiate(canvasPrefab);
        Destroy(canvasPrefab);
        Canvas.name = "InGameCanvas";

        SetUpCameraModes();
        SetUpCameraSettings();
        SetUpCameraLocking();
        ApplyVRTheme();

        Canvas.SetActive(false);
        initTime = Time.time;
    }

    private void Update()
    {
        if (Time.time - initTime < 5f)
            return;

        bool isPressed = ControllerInputPoller.instance.leftControllerPrimaryButton;

        if (isPressed && !wasPressed)
        {
            Canvas.SetActive(!Canvas.activeSelf);

            if (!isInterfaceLocked || CoreHandler.Instance.ModeHandlers[CoreHandler.Instance.CurrentHandlerName]
                                                 .IsPlayerDependent)
            {
                Canvas.transform.position = GTPlayer.Instance.bodyCollider.transform.position +
                                            GTPlayer.Instance.bodyCollider.transform.forward * 0.5f;

                Canvas.transform.LookAt(GTPlayer.Instance.headCollider.transform);
                Canvas.transform.Rotate(0f, 180f, 0f);
            }

            if (!GUIHandler.Instance.HasInitEventSystem)
            {
                GUIHandler.Instance.InitEventSystem();
                GUIHandler.Instance.Canvas.transform.Find("MainPanel").gameObject.SetActive(false);
            }
        }

        wasPressed = isPressed;
    }

    private void SetUpCameraLocking()
    {
        Canvas.transform.Find("MainPanel/LockButton/Collider").AddComponent<PressableButton>().OnPress = () =>
            {
                isInterfaceLocked = !isInterfaceLocked;

                if (isInterfaceLocked)
                {
                    LockCamera(CoreHandler.Instance.CurrentHandlerName);
                    CoreHandler.Instance.OnCurrentHandlerChange += LockCamera;
                }
                else
                {
                    Canvas.transform.SetParent(null);
                    Canvas.transform.position = GTPlayer.Instance.bodyCollider.transform.position +
                                                GTPlayer.Instance.bodyCollider.transform.forward * 0.5f;

                    Canvas.transform.LookAt(GTPlayer.Instance.headCollider.transform);
                    Canvas.transform.Rotate(0f, 180f, 0f);
                    CoreHandler.Instance.OnCurrentHandlerChange -= LockCamera;
                }
            };
    }

    private void LockCamera(string handlerName)
    {
        ModeHandlerBase modeHandler = CoreHandler.Instance.ModeHandlers[handlerName];

        if (modeHandler.IsPlayerDependent)
        {
            Canvas.transform.SetParent(null);
            Canvas.transform.position = GTPlayer.Instance.bodyCollider.transform.position +
                                        GTPlayer.Instance.bodyCollider.transform.forward * 0.5f;

            Canvas.transform.LookAt(GTPlayer.Instance.headCollider.transform);
            Canvas.transform.Rotate(0f, 180f, 0f);
        }
        else
        {
            Canvas.transform.SetParent(CameraHandler.Instance.transform);
            Canvas.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            Canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    public void SetUpRenderTexture()
    {
        RenderTexture renderTexture = new(1920, 1080, 24, RenderTextureFormat.ARGB32);
        renderTexture.name = "VR Render Texture";
        renderTexture.Create();
        // ^^ doing it like this because the stupid fucking assetbundle wouldnt load my render texture
        // assetbundles are so cool but for some fucking reason that bitch wouldnt load!!!!!!

        Canvas.transform.Find("MainPanel/Image").GetComponent<RawImage>().texture = renderTexture;

        RenderTextureCamera             = new GameObject("Render Texture Camera").AddComponent<Camera>();
        RenderTextureCamera.cullingMask = Plugin.Instance.PCCamera.GetComponent<Camera>().cullingMask;

        RenderTextureCamera.transform.SetParent(Plugin.Instance.PCCamera.parent, false);
        RenderTextureCamera.transform.localPosition = Vector3.zero;
        RenderTextureCamera.transform.localRotation = Quaternion.identity;

        RenderTextureCamera.targetTexture = renderTexture;
    }

    private void ApplyVRTheme()
    {
        Color bgPanel  = new Color(0.95f, 0.95f, 0.95f, 0.97f);
        Color btnColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        Color textDark = new Color(0.05f, 0.05f, 0.05f, 1f);
        Color textLight = new Color(0.95f, 0.95f, 0.95f, 1f);

        // Recolor all images (skip TMP internal images)
        foreach (Image img in Canvas.GetComponentsInChildren<Image>(true))
        {
            if (img.color.a < 0.1f) continue;
            if (img.gameObject.GetComponent<TMP_SubMeshUI>() != null) continue;
            if (img.gameObject.GetComponent<TextMeshProUGUI>() != null) continue;
            img.color = bgPanel;
        }

        // Recolor all text to black
        foreach (TextMeshProUGUI tmp in Canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.color.a < 0.1f) continue;
            tmp.color = textDark;
        }

        // Recolor VR buttons (they use colliders, not Unity UI Button)
        // The +/- buttons and mode buttons have images that should be dark
        Transform chin = Canvas.transform.Find("MainPanel/Chin");
        if (chin != null)
        {
            foreach (Image img in chin.GetComponentsInChildren<Image>(true))
            {
                if (img.color.a < 0.1f) continue;
                if (img.gameObject.GetComponent<TMP_SubMeshUI>() != null) continue;
                if (img.gameObject.GetComponent<TextMeshProUGUI>() != null) continue;
                img.color = btnColor;
            }
            foreach (TextMeshProUGUI tmp in chin.GetComponentsInChildren<TextMeshProUGUI>(true))
                tmp.color = textLight;
        }

        Transform tunables = Canvas.transform.Find("MainPanel/Tunables");
        if (tunables != null)
        {
            string[] buttonPaths = {
                "FOVPanel/MoreFOV", "FOVPanel/LessFOV",
                "NearClipPanel/MoreNearClip", "NearClipPanel/LessNearClip",
                "SmoothingPanel/MoreSmoothing", "SmoothingPanel/LessSmoothing"
            };
            foreach (string path in buttonPaths)
            {
                Transform btn = tunables.Find(path);
                if (btn == null) continue;
                Image btnImg = btn.GetComponent<Image>();
                if (btnImg != null) btnImg.color = btnColor;
                TextMeshProUGUI btnTxt = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnTxt != null) btnTxt.color = textLight;
            }
        }

        Transform lockBtn = Canvas.transform.Find("MainPanel/LockButton");
        if (lockBtn != null)
        {
            Image lockImg = lockBtn.GetComponent<Image>();
            if (lockImg != null) lockImg.color = btnColor;
            TextMeshProUGUI lockTxt = lockBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (lockTxt != null) lockTxt.color = textLight;
        }

        // Replace any old branding
        foreach (TextMeshProUGUI tmp in Canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (string.IsNullOrEmpty(tmp.text)) continue;
            string lower = tmp.text.ToLower();
            if (lower.Contains("casting should be free"))
                tmp.text = tmp.text.Replace("Casting Should Be Free", "ApeX Camera Mod");
            if (lower.Contains("castingshouldbefree"))
                tmp.text = tmp.text.Replace("CastingShouldBeFree", "ApeX Camera Mod");
            if (lower.Contains("hansolo"))
            {
                tmp.text = tmp.text.Replace("HanSolo1000Falcon", "St1ck");
                tmp.text = tmp.text.Replace("HanSolo", "St1ck");
                tmp.text = tmp.text.Replace("hansolo", "St1ck");
            }
        }
    }

    private void SetUpCameraModes()
    {
        GameObject buttonPrefab = Plugin.Instance.CastingBundle.LoadAsset<GameObject>("ModeButtonTemplate");
        Transform  modeContent  = Canvas.transform.Find("MainPanel/Chin/Content");

        foreach (KeyValuePair<string, ModeHandlerBase> modeHandlerPair in CoreHandler.Instance.ModeHandlers)
        {
            GameObject modeButton = Instantiate(buttonPrefab, modeContent);
            modeButton.GetComponentInChildren<TextMeshProUGUI>().text = modeHandlerPair.Value.HandlerName;
            modeButton.transform.Find("Collider").AddComponent<PressableButton>().OnPress +=
                    () => CoreHandler.Instance.SetCurrentHandler(modeHandlerPair.Value.HandlerName);
        }
    }

    private void SetUpCameraSettings()
    {
        Transform tunables = Canvas.transform.Find("MainPanel/Tunables");

        Transform fovPanel = tunables.Find("FOVPanel");
        FOVText = fovPanel.Find("FOVText").GetComponent<TextMeshProUGUI>();
        fovPanel.Find("MoreFOV/Collider").AddComponent<PressableButton>().OnPress += () =>
                    CoreHandler.Instance.SetFOV((int)GUIHandler.Instance.FOVSlider.value + 5);

        fovPanel.Find("LessFOV/Collider").AddComponent<PressableButton>().OnPress += () =>
                    CoreHandler.Instance.SetFOV((int)GUIHandler.Instance.FOVSlider.value - 5);

        Transform nearClipPanel = tunables.Find("NearClipPanel");
        NearClipText = nearClipPanel.Find("NearClipText").GetComponent<TextMeshProUGUI>();
        nearClipPanel.Find("MoreNearClip/Collider").AddComponent<PressableButton>().OnPress += () =>
                    CoreHandler.Instance.SetNearClip((int)GUIHandler.Instance.NearClipSlider.value + 1);

        nearClipPanel.Find("LessNearClip/Collider").AddComponent<PressableButton>().OnPress += () =>
                    CoreHandler.Instance.SetNearClip((int)GUIHandler.Instance.NearClipSlider.value - 1);

        Transform smoothingPanel = tunables.Find("SmoothingPanel");
        SmoothingText = smoothingPanel.Find("SmoothingText").GetComponent<TextMeshProUGUI>();
        smoothingPanel.Find("MoreSmoothing/Collider").AddComponent<PressableButton>().OnPress += () =>
                    CoreHandler.Instance.SetSmoothing(CameraHandler.Instance.SmoothingFactor + 1);

        smoothingPanel.Find("LessSmoothing/Collider").AddComponent<PressableButton>().OnPress += () =>
                    CoreHandler.Instance.SetSmoothing(CameraHandler.Instance.SmoothingFactor - 1);
    }
}