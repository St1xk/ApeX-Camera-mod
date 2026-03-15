using UnityEngine;

namespace StickCameraMod.Core.Interface.Panel_Handlers;

public class PanelHandlerBase : MonoBehaviour
{
    protected virtual void Start() => gameObject.SetActive(false);
}