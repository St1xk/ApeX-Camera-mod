using System.Globalization;
using System.Linq;
using BepInEx;
using StickCameraMod.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StickCameraMod.Core.Interface.Panel_Handlers;

public class PlayerHandler : PanelHandlerBase
{
    private const string RigLerpKey        = "RigLerpingValueOooooooo";
    private const string RigLerpEnabledKey = "RigLerpingEnabled";

    private const float RigLerpBody    = 0.155f;
    private const float RigLerpFingers = 0.34f;

    private LayerMask acceptedLayers;

    private GorillaTriggerColliderHandIndicator handIndicator;
    private Camera                              pcCamera;

    private bool rigLerpingEnabled;

    protected override void Start()
    {
        SetUpRigLerpingChangerThingy();
        base.Start();
    }

    private void SetUpRigLerpingChangerThingy()
    {
        Transform sliderHolder = transform.Find("RigLerpThang");
        float     currentLerp  = PlayerPrefs.GetFloat(RigLerpKey, RigLerpBody);
        rigLerpingEnabled = PlayerPrefs.GetInt(RigLerpEnabledKey, 1) == 1;

        sliderHolder.GetComponentInChildren<Slider>().onValueChanged.AddListener(value =>
            {
                currentLerp = value;
                PlayerPrefs.SetFloat(RigLerpKey, value);
                sliderHolder.GetChild(1).GetComponent<TextMeshProUGUI>().text =
                        $"Rig Lerping: {value.ToString("F", CultureInfo.InvariantCulture)}";
            });

        sliderHolder.GetComponentInChildren<Slider>().value = currentLerp;
        sliderHolder.GetComponentInChildren<Slider>().onValueChanged?.Invoke(currentLerp);

        sliderHolder.GetChild(2).GetComponentInChildren<TextMeshProUGUI>().text =
                rigLerpingEnabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>";

        transform.Find("WASDFly").GetComponent<Button>().onClick.AddListener(() =>
                                                                             {
                                                                                 Debug.Log("Pressed");

                                                                                 bool enabledWasd =
                                                                                         transform.Find("WASDFly")
                                                                                                .GetComponentInChildren<
                                                                                                         TextMeshProUGUI>()
                                                                                                .text.Contains(
                                                                                                         "Disabled");

                                                                                 transform.Find("WASDFly")
                                                                                        .GetComponentInChildren<
                                                                                                 TextMeshProUGUI>()
                                                                                        .text = enabledWasd
                                                                                             ? "WASD Fly\n<color=green>Enabled</color>"
                                                                                             : "WASD Fly\n<color=red>Disabled</color>";

                                                                                 if (enabledWasd)
                                                                                     Plugin.Instance.OnFixedUpdate +=
                                                                                             WASDFlyFixed;
                                                                                 else
                                                                                     Plugin.Instance.OnFixedUpdate -=
                                                                                             WASDFlyFixed;

                                                                                 if (!enabledWasd)
                                                                                     Cursor.lockState =
                                                                                             CursorLockMode.None;

                                                                                 PCHeadMovementPatch.IsEnabled =
                                                                                         enabledWasd;

                                                                                 handIndicator =
                                                                                         GorillaTagger.Instance
                                                                                                .rightHandTriggerCollider
                                                                                                .GetComponent<
                                                                                                         GorillaTriggerColliderHandIndicator>();

                                                                                 acceptedLayers =
                                                                                         UnityLayerMask
                                                                                                .GorillaInteractable
                                                                                                .GetIndex();

                                                                                 pcCamera = Plugin.Instance.PCCamera
                                                                                        .GetComponent<Camera>();
                                                                             });

        sliderHolder.GetChild(2).GetComponent<Button>().onClick.AddListener(() =>
                                                                            {
                                                                                rigLerpingEnabled = !rigLerpingEnabled;
                                                                                PlayerPrefs.SetInt(RigLerpEnabledKey,
                                                                                        rigLerpingEnabled ? 1 : 0);

                                                                                sliderHolder.GetChild(2)
                                                                                       .GetComponentInChildren<
                                                                                                TextMeshProUGUI>()
                                                                                       .text = rigLerpingEnabled
                                                                                            ? "<color=green>Enabled</color>"
                                                                                            : "<color=red>Disabled</color>";
                                                                            });

        Plugin.Instance.OnFixedUpdate += () =>
                                         {
                                             if (!GorillaParent.hasInstance || VRRigCache.ActiveRigs == null)
                                                 return;

                                             if (rigLerpingEnabled)
                                                 foreach (VRRig rig in VRRigCache.ActiveRigs.Where(rig =>
                                                                      !Mathf.Approximately(rig.lerpValueBody,
                                                                              currentLerp) ||
                                                                      !Mathf.Approximately(rig.lerpValueFingers,
                                                                              currentLerp)))
                                                 {
                                                     rig.lerpValueBody    = currentLerp;
                                                     rig.lerpValueFingers = currentLerp;
                                                 }
                                             else
                                                 foreach (VRRig rig in VRRigCache.ActiveRigs.Where(rig =>
                                                                      !Mathf.Approximately(rig.lerpValueBody,
                                                                              RigLerpBody) ||
                                                                      !Mathf.Approximately(rig.lerpValueFingers,
                                                                              RigLerpFingers)))
                                                 {
                                                     rig.lerpValueBody    = RigLerpBody;
                                                     rig.lerpValueFingers = RigLerpFingers;
                                                 }
                                         };
    }

    private void WASDFlyFixed()
    {
        Transform head = GorillaTagger.Instance.headCollider.transform;

        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            head.Rotate(Vector3.up,    mouseDelta.x  * 0.08f, Space.World);
            head.Rotate(Vector3.right, -mouseDelta.y * 0.08f, Space.Self);

            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }

        Vector3 movementDirection = Vector3.zero;

        if (UnityInput.Current.GetKey(KeyCode.W)) movementDirection           += head.forward;
        if (UnityInput.Current.GetKey(KeyCode.S)) movementDirection           -= head.forward;
        if (UnityInput.Current.GetKey(KeyCode.A)) movementDirection           -= head.right;
        if (UnityInput.Current.GetKey(KeyCode.D)) movementDirection           += head.right;
        if (UnityInput.Current.GetKey(KeyCode.Space)) movementDirection       += head.up;
        if (UnityInput.Current.GetKey(KeyCode.LeftControl)) movementDirection -= head.up;

        Rigidbody rigidbody = GorillaTagger.Instance.rigidbody;

        float speed = UnityInput.Current.GetKey(KeyCode.LeftShift) ? 40f : 10f;
        rigidbody.transform.position += movementDirection.normalized * (Time.fixedDeltaTime * speed);

        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.AddForce(-Physics.gravity * rigidbody.mass);

        if (!Mouse.current.leftButton.isPressed)
            return;

        if (!Physics.Raycast(pcCamera.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit,
                    20f, acceptedLayers))
            return;

        handIndicator.transform.position = hit.point;
    }
}