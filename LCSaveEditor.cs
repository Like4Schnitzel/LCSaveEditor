using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LCSaveEditor.Patches;
using LobbyCompatibility.Attributes;
using LobbyCompatibility.Enums;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCSaveEditor;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.HardDependency)]
[LobbyCompatibility(CompatibilityLevel.ClientOnly, VersionStrictness.None)]
public class LCSaveEditor : BaseUnityPlugin
{
    public static LCSaveEditor Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }
    public static GameObject? FileEditor { get; set; }

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        Patch();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    public static void InitFileEditor()
    {
        if (FileEditor != null)
        {
            Debug.Log("Attempted to initialize FileEditor, but it already exists.");
            return;
        }

        GameObject menuContainer = GameObject.Find("MenuContainer");
        GameObject lobbyHostSettings = menuContainer.transform.Find("LobbyHostSettings").gameObject;

        Debug.Log("Initializing FileEditor");

        // use the Host Settings screen as a template
        FileEditor = Instantiate(lobbyHostSettings, parent: menuContainer.transform);
        FileEditor.name = "FileEditor";
        Transform filesPanel = FileEditor.transform.Find("FilesPanel");
        Transform hostSettingsContainer = FileEditor.transform.Find("HostSettingsContainer");
        // center the FilesPanel by copying the HostSettingsContainer's position
        filesPanel.transform.position = hostSettingsContainer.transform.position;

        // add the Back button to the FilesPanel
        Transform back = hostSettingsContainer.Find("Back");
        back.SetParent(filesPanel, worldPositionStays: false);
        // modify the function of the Back button to instead disable our new thing
        Button backButton = back.GetComponent<Button>();
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(() => { FileEditor.SetActive(false); });
        // lastly let's have it replace ChallengeMoonButton
        Transform challengeMoonButton = filesPanel.GetChild(4);
        back.position = new Vector3(
            back.position.x,
            challengeMoonButton.position.y,
            back.position.z
        );
        Destroy(challengeMoonButton.gameObject);

        // now we can remove the HostSettingsContainer we copied over
        Destroy(hostSettingsContainer.gameObject);

        // set the title lable
        filesPanel.Find("EnterAName").GetComponent<TextMeshProUGUI>().text = "Select a file:";

        // remove the delete buttons
        // and assign event handlers while we're at it
        for (int i = 1; i <= 3; i++)
        {
            Transform file = filesPanel.Find($"File{i}");
            Destroy(file.Find("DeleteButton").gameObject);

            Button fileButton = file.GetComponent<Button>();
            fileButton.onClick.RemoveAllListeners();
            // for some reason passing i to EditFile here would have it take a reference of i
            // hence, to avoid always calling EditFile(4), we make a copy of i here
            int iCopy = i;
            fileButton.onClick.AddListener( () => EditFile(iCopy) );
        }

        // remove the ChallengeLeaderboard
        Destroy(FileEditor.transform.Find("ChallengeLeaderboard").gameObject);
    }

    public static void EditFile(int index)
    {
        string filePath = $"LCSaveFile{index}";
        if (!ES3.FileExists(filePath))
        {
            Debug.Log($"{filePath} does not exist.");
            return;
        }

        JObject data = JObject.Parse(ES3.LoadRawString(filePath));
        Debug.Log(data);
    }

    internal static void Patch()
    {
        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

        Logger.LogDebug("Patching...");

        Harmony.PatchAll();
        Harmony.CreateAndPatchAll(typeof(MenuManagerPatch));

        Logger.LogDebug("Finished patching!");
    }

    internal static void Unpatch()
    {
        Logger.LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        Logger.LogDebug("Finished unpatching!");
    }
}
