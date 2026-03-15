using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace StickCameraMod.Core.Interface;

public class WristMenuHandler : MonoBehaviour
{
    private Canvas wristCanvas;
    private bool isVisible = false;

    private void Start()
    {
        CreateWristMenu();
    }

    private void CreateWristMenu()
    {
        // Create wrist menu canvas
        GameObject canvasObj = new GameObject("WristMenuCanvas");
        wristCanvas = canvasObj.AddComponent<Canvas>();
        wristCanvas.renderMode = RenderMode.WorldSpace;

        // Scale and position the canvas
        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(400, 300);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Create panel background
        GameObject panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.15f, 0.35f, 0.95f);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Add title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panelObj.transform);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "ApeX Camera";
        titleText.fontSize = 36;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.3f, 0.6f, 0.95f, 1f);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, -30);
        titleRect.sizeDelta = new Vector2(400, 50);

        // Add info text
        GameObject infoObj = new GameObject("Info");
        infoObj.transform.SetParent(panelObj.transform);
        TextMeshProUGUI infoText = infoObj.AddComponent<TextMeshProUGUI>();
        infoText.text = "FOV: 60\nSmoothing: 18\nP - Toggle View\nS - Screenshot";
        infoText.fontSize = 20;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = Color.white;

        RectTransform infoRect = infoObj.GetComponent<RectTransform>();
        infoRect.anchoredPosition = new Vector2(0, 30);
        infoRect.sizeDelta = new Vector2(400, 150);

        canvasObj.SetActive(false);
        DontDestroyOnLoad(canvasObj);
        wristCanvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.yKey.wasPressedThisFrame)
        {
            ToggleWristMenu();
        }
    }

    private void ToggleWristMenu()
    {
        isVisible = !isVisible;
        wristCanvas.gameObject.SetActive(isVisible);

        if (isVisible)
        {
            PositionOnWrist();
        }
    }

    private void PositionOnWrist()
    {
        // Position on right hand (wrist area)
        // You may need to adjust based on your hand position tracking
        if (GorillaTagger.Instance != null && GorillaTagger.Instance.rightHandTransform != null)
        {
            wristCanvas.transform.position = GorillaTagger.Instance.rightHandTransform.position +
                                            GorillaTagger.Instance.rightHandTransform.forward * 0.1f;
            wristCanvas.transform.rotation = GorillaTagger.Instance.rightHandTransform.rotation;
        }
    }

    public void UpdateInfo(string fov, string smoothing)
    {
        TextMeshProUGUI infoText = wristCanvas.transform.Find("Panel/Info").GetComponent<TextMeshProUGUI>();
        infoText.text = $"FOV: {fov}\nSmoothing: {smoothing}\nP - Toggle View\nS - Screenshot\nY - Close Menu";
    }
}
