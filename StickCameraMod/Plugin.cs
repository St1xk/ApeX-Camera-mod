using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using StickCameraMod.Core;
using StickCameraMod.Core.Interface;
using StickCameraMod.Nametags;
using StickCameraMod.Patches;
using StickCameraMod.Utils;
using StickCameraMod.Version_Checking;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace StickCameraMod;

[BepInPlugin(Constants.PluginGuid, Constants.PluginName, Constants.PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public        Action OnFixedUpdate;
    public        Action OnLateUpdate;
    public        Action OnUpdate;
    public static Plugin Instance { get; private set; }

    public AssetBundle CastingBundle { get; private set; }

    public Transform PCCamera { get; private set; }

    public TMP_FontAsset CasterFontBold { get; private set; }

    public Shader TMP_DistanceField { get; private set; }

    public Dictionary<string, string> KnownMods   { get; private set; } = [];
    public Dictionary<string, string> KnownCheats { get; private set; } = [];

    private void Awake() => Instance = this;

    private void Start()
    {
        GorillaTagger.OnPlayerSpawned(OnGameInitialized);

        Harmony harmony = new(Constants.PluginGuid);
        harmony.PatchAll();

        VRRigCache.OnRigDeactivated += container =>
                                       {
                                           VRRig vrrig = container.vrrig;
                                           
                                           if (SetColourPatch.SpawnedRigs.Contains(vrrig))
                                               SetColourPatch.SpawnedRigs.Remove(vrrig);

                                           RigUtils.OnRigCached?.Invoke(vrrig);
                                       };
    }

    private void Update() => OnUpdate?.Invoke();

    private void FixedUpdate() => OnFixedUpdate?.Invoke();
    private void LateUpdate()  => OnLateUpdate?.Invoke();

    private void OnGameInitialized()
    {
        Stream bundleStream = Assembly.GetExecutingAssembly()
                                      .GetManifestResourceStream("StickCameraMod.Resources.stickcameramod");

        CastingBundle = AssetBundle.LoadFromStream(bundleStream);
        bundleStream?.Close();

        CasterFontBold = Instantiate(CastingBundle.LoadAsset<TMP_FontAsset>("JetBrainsMono-Bold SDF"));

        TMP_DistanceField              = Shader.Find("TextMeshPro/Mobile/Distance Field");
        CasterFontBold.material.shader = TMP_DistanceField;

        PCCamera = GorillaTagger.Instance.thirdPersonCamera.transform.GetChild(0);

        GameObject cameraPrefab = CastingBundle.LoadAsset<GameObject>("CardboardCamera");
        GameObject camera       = Instantiate(cameraPrefab);
        camera.AddComponent<CameraHandler>();
        camera.name = "CardboardCamera";
        Destroy(cameraPrefab);

        GameObject componentHolder = new("StickCameraMod");
        componentHolder.AddComponent<VersionChecker>();
        componentHolder.AddComponent<TagManager>();
        componentHolder.AddComponent<CoreHandler>();
        componentHolder.AddComponent<NametagHandler>();
        componentHolder.AddComponent<AutoCaster>();

        // Network endpoints removed for offline compatibility
        // StartCoroutine(LoadKnownCheatsAndMods());

        RigUtils.OnRigCosmeticsLoad += rig =>
                                       {
                                           Nametag nametag = rig.GetComponent<Nametag>();
                                           nametag?.UpdatePlayerPlatform();
                                       };
    }

    // Removed network endpoint for offline compatibility
    // private IEnumerator LoadKnownCheatsAndMods() { ... }
}