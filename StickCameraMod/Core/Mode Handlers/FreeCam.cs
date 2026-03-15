using BepInEx;
using StickCameraMod.Core.Interface;
using StickCameraMod.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StickCameraMod.Core.Mode_Handlers;

public class FreeCam : ModeHandlerBase
{
    private float pitch;
    private float yaw;

    public override string HandlerName       => "Free Cam";
    public override bool   IsPlayerDependent => false;

    private void LateUpdate()
    {
        float baseSpeed = UnityInput.Current.GetKey(KeyCode.LeftShift) ? 30f : 10f;
        float speed     = baseSpeed * Time.deltaTime;

        Vector3 movementDir = Vector3.zero;
        float   analogScale = 1f;

        if (UnityInput.Current.GetKey(KeyCode.W)) movementDir           += CameraHandler.Instance.transform.forward;
        if (UnityInput.Current.GetKey(KeyCode.S)) movementDir           -= CameraHandler.Instance.transform.forward;
        if (UnityInput.Current.GetKey(KeyCode.A)) movementDir           -= CameraHandler.Instance.transform.right;
        if (UnityInput.Current.GetKey(KeyCode.D)) movementDir           += CameraHandler.Instance.transform.right;
        if (UnityInput.Current.GetKey(KeyCode.Space)) movementDir       += Vector3.up;
        if (UnityInput.Current.GetKey(KeyCode.LeftControl)) movementDir -= Vector3.up;

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 moveStick = pad.leftStick.ReadValue();
            float   magnitude = Mathf.Clamp01(moveStick.magnitude);

            if (magnitude > 0.001f)
            {
                Vector2 dir = moveStick.normalized;

                movementDir += CameraHandler.Instance.transform.forward * dir.y;
                movementDir += CameraHandler.Instance.transform.right   * dir.x;

                analogScale = magnitude;
            }

            float up   = pad.rightTrigger.ReadValue();
            float down = pad.leftTrigger.ReadValue();

            movementDir += Vector3.up * (up - down);
            analogScale =  Mathf.Max(analogScale, Mathf.Max(up, down));
        }

        if (movementDir.sqrMagnitude > 0f)
        {
            movementDir.Normalize();
            movementDir    *= speed * analogScale;
            targetPosition += movementDir;
        }

        bool    rotating  = false;
        Vector2 lookDelta = Vector2.zero;
        float   lookSpeed = 6f;

        if (Mouse.current.rightButton.isPressed)
        {
            lookDelta        = Mouse.current.delta.ReadValue();
            rotating         = true;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else if (pad != null)
        {
            Vector2 stickLook = pad.rightStick.ReadValue();
            if (stickLook.sqrMagnitude > 0.001f)
            {
                lookDelta = stickLook * 80f;
                lookSpeed = 3.5f;
                rotating  = true;
            }
        }

        if (rotating)
        {
            yaw   += lookDelta.x * lookSpeed * Time.deltaTime;
            pitch -= lookDelta.y * lookSpeed * Time.deltaTime;
            pitch =  Mathf.Clamp(pitch, -89f, 89f);

            targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }

        Vector3    otherTargetPos = targetPosition;
        Quaternion otherTargetRot = targetRotation;

        if (CameraHandler.Instance.SmoothingFactor > 0)
        {
            int realSmoothingFactor = GetSmoothingFactor();

            if (SnappySmoothing)
            {
                Vector3 velocity        = (targetPosition - lastPosition) / Time.deltaTime;
                Vector3 angularVelocity = targetRotation.GetAngularVelocity(lastRotation, Time.deltaTime);

                otherTargetPos = Vector3.Lerp(
                        CameraHandler.Instance.transform.position,
                        targetPosition,
                        Time.deltaTime * realSmoothingFactor * velocity.magnitude
                );

                otherTargetRot = Quaternion.Slerp(
                        CameraHandler.Instance.transform.rotation,
                        targetRotation,
                        Time.deltaTime * realSmoothingFactor * ParseAngularVelocity(angularVelocity)
                );
            }
            else
            {
                otherTargetPos = Vector3.Lerp(
                        CameraHandler.Instance.transform.position,
                        targetPosition,
                        Time.deltaTime * realSmoothingFactor
                );

                otherTargetRot = Quaternion.Slerp(
                        CameraHandler.Instance.transform.rotation,
                        targetRotation,
                        Time.deltaTime * realSmoothingFactor
                );
            }
        }

        lastRotation = CameraHandler.Instance.transform.rotation;
        lastPosition = CameraHandler.Instance.transform.position;

        CameraHandler.Instance.transform.position = otherTargetPos;
        CameraHandler.Instance.transform.rotation = otherTargetRot;
    }

    private void OnEnable()
    {
        targetPosition = CameraHandler.Instance.transform.position;
        targetRotation = CameraHandler.Instance.transform.rotation;

        Vector3 euler = CameraHandler.Instance.transform.rotation.eulerAngles;
        yaw   = euler.y;
        pitch = euler.x;
    }

    private void OnDisable() => Cursor.lockState = CursorLockMode.None;
}