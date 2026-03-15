using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace StickCameraMod.Version_Checking;

public class VersionChecker : MonoBehaviour
{
    private void Start()
    {
        Debug.Log($"ApeX Camera Mod v{Constants.PluginVersion} loaded successfully");
        StartCoroutine(CheckGitHubVersion());
    }

    private IEnumerator CheckGitHubVersion()
    {
        yield return new WaitForSeconds(3f); // Wait 3 seconds before checking

        using (UnityWebRequest request = UnityWebRequest.Get(Constants.GitHubVersionUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string versionText = request.downloadHandler.text;
                    Debug.Log($"Latest version from GitHub: {versionText}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Could not parse version info: {e.Message}");
                }
            }
            else
            {
                // Silently fail if no internet connection
                Debug.LogWarning("Could not reach GitHub for version info");
            }
        }
    }
}