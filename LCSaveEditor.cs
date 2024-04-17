using System;
using System.Collections.Generic;
using System.Linq;
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
    public static GameObject? FileSelector { get; set; }
    public static GameObject? FileEditScreen { get; set; }
    public static Transform? SFPropsListContainer { get; set; }
    public static GameObject? SFPropsListItem { get; set; }

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        Patch();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    public static void InitFileSelector()
    {
        if (FileSelector != null)
        {
            Debug.Log("Attempted to initialize FileSelector, but it already exists.");
            return;
        }

        GameObject menuContainer = GameObject.Find("MenuContainer");
        GameObject lobbyHostSettings = menuContainer.transform.Find("LobbyHostSettings").gameObject;

        Debug.Log("Initializing FileSelector");

        // use the Host Settings screen as a template
        FileSelector = Instantiate(lobbyHostSettings, parent: menuContainer.transform);
        FileSelector.name = "FileSelector";
        Transform filesPanel = FileSelector.transform.Find("FilesPanel");
        Transform hostSettingsContainer = FileSelector.transform.Find("HostSettingsContainer");
        // center the FilesPanel by copying the HostSettingsContainer's position
        filesPanel.transform.position = hostSettingsContainer.transform.position;

        // add the Back button to the FilesPanel
        Transform back = hostSettingsContainer.Find("Back");
        back.SetParent(filesPanel, worldPositionStays: false);
        // modify the function of the Back button to instead disable our new thing
        Button backButton = back.GetComponent<Button>();
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(() => { FileSelector.SetActive(false); });
        // lastly let's have it replace ChallengeMoonButton
        Transform challengeMoonButton = filesPanel.Find("ChallengeMoonButton");
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
        Destroy(FileSelector.transform.Find("ChallengeLeaderboard").gameObject);
    }

    public static void InitFileEditScreen()
    {
        if (FileEditScreen != null)
        {
            Debug.Log("Attempted to initialize FileEditScreen, but it already exists.");
            return;
        }

        // we'll use a copy of lobbylist as a starting point
        GameObject menuContainer = GameObject.Find("MenuContainer");
        GameObject lobbyList = menuContainer.transform.Find("LobbyList").gameObject;

        FileEditScreen = Instantiate(lobbyList, parent: menuContainer.transform);
        FileEditScreen.name = "FileEditScreen";
        Transform listPanel = FileEditScreen.transform.Find("ListPanel");
        SFPropsListContainer = listPanel.Find("Scroll View").Find("Viewport").Find("Content");

        // removing some things we don't need
        Destroy(FileEditScreen.transform.Find("JoinCode").gameObject);
        Destroy(SFPropsListContainer.Find("ListHeader (1)").gameObject);
        for (int i = 0; i < listPanel.childCount; i++)
        {
            var child = listPanel.GetChild(i).gameObject;
            if (child.name != "ListHeader" && child.name != "Scroll View")
            {
                Destroy(child);
            }
        }

        // modifying the back button
        Transform back = FileEditScreen.transform.Find("BackToMenu");
        back.Find("Text (TMP)").GetComponent<TextMeshProUGUI>().text = "> Back to file select";
        Button backButton = back.GetComponent<Button>();
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener( () => {
            FileEditScreen.SetActive(false);
            FileSelector?.SetActive(true);
            menuContainer.SetActive(true);
        });

        // change the title text
        // this will get changed again later in the EditFile method but we should make sure it doesn't say "Servers"
        listPanel.Find("ListHeader").GetComponent<TextMeshProUGUI>().text = "File";
    }

    public static void InitSFPropsListItem()
    {
        if (SFPropsListItem != null)
        {
            Debug.Log("Attempted to initialize SFPropsListItem, but it already exists.");
        }

        // take LobbyListItem as a template
        GameObject[]? allObjects = Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[];
        GameObject lobbyListItem = Array.Find(allObjects, obj => obj.name == "LobbyListItem");

        SFPropsListItem = Instantiate(lobbyListItem);
        SFPropsListItem.name = "SFPropsListItem";
        SFPropsListItem.transform.Find("ServerName").name = "Key";
        SFPropsListItem.transform.Find("NumPlayers").name = "Value";

        // modify the button
        Transform editButton = SFPropsListItem.transform.Find("JoinButton");
        editButton.name = "EditButton";
        editButton.Find("Text (TMP)").GetComponent<TextMeshProUGUI>().text = "Edit";
        editButton.GetComponent<Button>().onClick.RemoveAllListeners();
    }

    public static void EditFile(int index)
    {
        if (FileEditScreen == null)
        {
            Debug.Log("FileEditScreen is null.");
            return;
        }
        if (SFPropsListItem == null)
        {
            Debug.Log("SFPropsListItem is null.");
            return;
        }

        string filePath = $"LCSaveFile{index}";
        if (!ES3.FileExists(filePath))
        {
            Debug.Log($"{filePath} does not exist.");
            return;
        }

        var data = JObject.Parse(ES3.LoadRawString(filePath));
        string[] keys = new string[data.Count];
        int i = 0;
        float slotPositionOffset = 0;
        foreach (var kv in data)
        {
            keys[i] = kv.Key;
            i++;

            GameObject item = Instantiate(SFPropsListItem, SFPropsListContainer);
            item.transform.Find("EditButton").GetComponent<Button>().onClick.RemoveAllListeners();
            item.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, slotPositionOffset);
            slotPositionOffset -= 42;
            item.transform.Find("Key").GetComponent<TextMeshProUGUI>().text = kv.Key;

            string valueString;
            var value = ES3.Load(kv.Key, filePath: filePath);
            if (value != null && value.GetType().IsArray)
            {
                System.Collections.IEnumerable valueEnumerable = value as System.Collections.IEnumerable;
                valueString = "{";
                foreach (var elem in valueEnumerable!)
                {
                    valueString += elem + ", ";
                }
                valueString += "}";
            }
            else
            {
                valueString = (value ?? "null").ToString();
            }
            item.transform.Find("Value").GetComponent<TextMeshProUGUI>().text = valueString;
        }

        Debug.Log($"SaveFile keys are: {String.Join(" ", keys)}");

        FileEditScreen.transform.Find("ListPanel").Find("ListHeader").GetComponent<TextMeshProUGUI>().text = $"File {index}";
        FileEditScreen.SetActive(true);
        FileSelector?.SetActive(false);
        GameObject.Find("MenuContainer")?.transform.Find("MainButtons").gameObject.SetActive(false);
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
