using UnityEngine;

namespace StickCameraMod.Version_Checking;

public class VersionChecker : MonoBehaviour
{
    private void Start()
    {
        // Version checking endpoint removed for offline compatibility
        // Network calls have been removed from this mod
        Debug.Log("ApeX Camera Mod v1.1.3 loaded successfully");
    }
}