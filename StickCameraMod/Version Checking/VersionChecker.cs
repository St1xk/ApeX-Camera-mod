using System.IO;
using System.Net.Http;
using StickCameraMod.Core;
using UnityEngine;

namespace StickCameraMod.Version_Checking;

public class VersionChecker : MonoBehaviour
{
    private void Start()
    {
        using HttpClient client = new();
        HttpResponseMessage response = client
                                      .GetAsync(
                                               "https://raw.githubusercontent.com/HanSolo1000Falcon/StickCameraMod/master/version.txt")
                                      .Result;

        response.EnsureSuccessStatusCode();
        using Stream       stream   = response.Content.ReadAsStreamAsync().Result;
        using StreamReader reader   = new(stream);
        string             contents = reader.ReadToEnd().Trim();

        string[] parts = contents.Split(";");

        Version mostUpToDateVersion = new(parts[0]);
        Version currentVersion      = new(Constants.PluginVersion);

        if (currentVersion < mostUpToDateVersion)
            CoreHandler.Instance.OnDeprecatedVersionDetected(currentVersion, mostUpToDateVersion, parts[1]);
    }
}