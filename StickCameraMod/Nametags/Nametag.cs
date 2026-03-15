using StickCameraMod.Core;
using StickCameraMod.Core.Interface;
using StickCameraMod.Utils;
using GorillaLocomotion;
using TMPro;
using UnityEngine;

namespace StickCameraMod.Nametags;

public class Nametag : MonoBehaviour
{
    private bool _showTpTag = true;

    private VRRig associatedRig;

    private NametagComponents firstPersonNametag;

    private bool hasInit;

    private Transform nametagParent;

    private string            platform = "[STANDALONE]";
    private NametagComponents thirdPersonNametag;

    public bool ShowTpTag
    {
        get => _showTpTag;

        set
        {
            if (value == _showTpTag)
                return;

            _showTpTag = value;

            if (hasInit)
                thirdPersonNametag.Nametag.gameObject.SetActive(value);
        }
    }

    private void Awake()
    {
        nametagParent = new GameObject("NametagParent").transform;
        nametagParent.SetParent(transform);
        nametagParent.localPosition = new Vector3(0f, 0.5f, 0f);

        associatedRig = GetComponent<VRRig>();

        switch (associatedRig.isLocal)
        {
            case true:
                platform = "[PC]";

                break;

            case false:
                firstPersonNametag = SetUpNametagComponents(true);

                break;
        }

        thirdPersonNametag = SetUpNametagComponents(false);

        if (!ShowTpTag)
            thirdPersonNametag.Nametag.gameObject.SetActive(false);

        hasInit = true;
    }

    private void LateUpdate()
    {
        int    fpsActual = associatedRig.isLocal ? (int)(1f / Time.unscaledDeltaTime) : associatedRig.fps;
        string colour    = fpsActual > 60 ? fpsActual > 72 ? "green" : "orange" : "red";
        string fps       = $"<color={colour}>{fpsActual}</color> FPS";

        // Tagged indicator
        bool isTagged = associatedRig.IsTagged();
        string tagStatus = isTagged ? " <color=#FF5533>[IT]</color>" : "";

        // Distance from camera
        string distText = "";
        if (GUIHandler.Instance != null && GUIHandler.Instance.ShowPlayerDistance &&
            CameraHandler.Instance != null)
        {
            float dist = Vector3.Distance(transform.position, CameraHandler.Instance.transform.position);
            distText = $"\n<size=60%>{dist:F1}m away</size>";
        }

        if (ShowTpTag)
        {
            thirdPersonNametag.Nametag.LookAt(Plugin.Instance.PCCamera);
            thirdPersonNametag.Nametag.Rotate(0f, 180f, 0f);
            thirdPersonNametag.FPSText.text = fps;
            thirdPersonNametag.NameText.color = isTagged
                ? new Color(1f, 0.33f, 0f, 1f)
                : associatedRig.playerColor;

            if (thirdPersonNametag.ExtraText != null)
                thirdPersonNametag.ExtraText.text = tagStatus + distText;
        }

        if (!associatedRig.isLocal)
        {
            firstPersonNametag.Nametag.LookAt(GTPlayer.Instance.headCollider.transform);
            firstPersonNametag.Nametag.Rotate(0f, 180f, 0f);
            firstPersonNametag.FPSText.text = fps;
            firstPersonNametag.NameText.color = isTagged
                ? new Color(1f, 0.33f, 0f, 1f)
                : associatedRig.playerColor;

            if (firstPersonNametag.ExtraText != null)
                firstPersonNametag.ExtraText.text = tagStatus + distText;
        }
    }

    private void OnEnable()
    {
        if (!hasInit)
            return;

        nametagParent.gameObject.SetActive(true);
        RigUtils.OnRigNameChange += OnNameUpdate;
        UpdatePlayerPlatform();
    }

    private void OnDisable()
    {
        if (!hasInit)
            return;

        nametagParent.gameObject.SetActive(false);
        RigUtils.OnRigNameChange -= OnNameUpdate;
    }

    private void OnDestroy() => Destroy(nametagParent.gameObject);

    public void UpdatePlayerPlatform()
    {
        platform = associatedRig.isLocal ? "[PC]" : GetPlayerPlatform();

        thirdPersonNametag.PlatformText.text = platform;
        if (!associatedRig.isLocal) firstPersonNametag.PlatformText.text = platform;

        Color colour = platform == "[STANDALONE]"
                               ? NametagHandler.Instance.StandaloneColour
                               : NametagHandler.Instance.SteamColour;

        thirdPersonNametag.PlatformText.color = colour;
        if (!associatedRig.isLocal) firstPersonNametag.PlatformText.color = colour;
    }

    private NametagComponents SetUpNametagComponents(bool firstPerson)
    {
        NametagComponents nametagComponents = new()
        {
                Nametag = Instantiate(NametagHandler.Instance.NametagPrefab, nametagParent).transform,
        };

        nametagComponents.Nametag.gameObject.name         = firstPerson ? "FirstPersonNametag" : "ThirdPersonNametag";
        nametagComponents.Nametag.transform.localPosition = Vector3.zero;

        nametagComponents.NameText     = nametagComponents.Nametag.Find("Name").GetComponent<TextMeshProUGUI>();
        nametagComponents.PlatformText = nametagComponents.Nametag.Find("Platform").GetComponent<TextMeshProUGUI>();
        nametagComponents.FPSText      = nametagComponents.Nametag.Find("FPS").GetComponent<TextMeshProUGUI>();

        nametagComponents.NameText.text = associatedRig.Creator != null
                                                  ? associatedRig.Creator.NickName
                                                  : associatedRig.playerText1.text;

        // Set player name color to their gorilla color
        nametagComponents.NameText.color = associatedRig.playerColor;

        // Create extra text element for tagged status + distance
        GameObject extraObj = new GameObject("Extra");
        extraObj.transform.SetParent(nametagComponents.Nametag, false);
        nametagComponents.ExtraText = extraObj.AddComponent<TextMeshProUGUI>();
        nametagComponents.ExtraText.fontSize = nametagComponents.FPSText.fontSize;
        nametagComponents.ExtraText.alignment = TextAlignmentOptions.Center;
        nametagComponents.ExtraText.color = Color.white;
        nametagComponents.ExtraText.enableWordWrapping = false;

        if (Plugin.Instance.CasterFontBold != null)
        {
            nametagComponents.ExtraText.font = Plugin.Instance.CasterFontBold;
            nametagComponents.ExtraText.fontSharedMaterial = new Material(Plugin.Instance.TMP_DistanceField);
        }

        RectTransform extraRect = extraObj.GetComponent<RectTransform>();
        RectTransform fpsRect = nametagComponents.FPSText.GetComponent<RectTransform>();
        extraRect.anchoredPosition = fpsRect.anchoredPosition + new Vector2(0, -fpsRect.sizeDelta.y - 2);
        extraRect.sizeDelta = fpsRect.sizeDelta;

        SetLayer(firstPerson, nametagComponents.Nametag);

        return nametagComponents;
    }

    private void OnNameUpdate(VRRig rig, string name)
    {
        if (rig != associatedRig)
            return;

        if (!associatedRig.isLocal)
            firstPersonNametag.NameText.text = name;

        thirdPersonNametag.NameText.text = name;
    }

    private void SetLayer(bool firstPerson, Transform trans)
    {
        foreach (Transform child in trans)
            SetLayer(firstPerson, child);

        trans.gameObject.layer = firstPerson ? UnityLayer.FirstPersonOnly.GetIndex() : UnityLayer.MirrorOnly.GetIndex();
    }

    private string GetPlayerPlatform()
    {
        string concat = string.Concat(associatedRig._playerOwnedCosmetics).ToLower();

        if (concat.Contains("s. first login")) return "[STEAM]";
        if (concat.Contains("first login") || concat.Contains("game-purchase") ||
            associatedRig.creator.GetPlayerRef().CustomProperties.Count > 1) return "[PC]";

        if (platform is "[PC]" or "[STEAM]") return platform;

        return "[STANDALONE]";
    }

    private struct NametagComponents
    {
        public Transform Nametag;

        public TextMeshProUGUI NameText;
        public TextMeshProUGUI PlatformText;
        public TextMeshProUGUI FPSText;
        public TextMeshProUGUI ExtraText;
    }
}