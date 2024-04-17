using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCSaveEditor.Patches;

class MenuManagerPatch
{
    [HarmonyPatch(typeof(MenuManager), nameof(MenuManager.Start))]
    [HarmonyPostfix]
    private static void MenuManager_Start(MenuManager __instance)
    {
        GameObject mainButtons = __instance.menuButtons;
        // gotta do this because Start gets called once before the actual main menu loads
        if (mainButtons == null)
        {
            return;
        }

        // clone a button and modify the copy
        GameObject? templateButton = mainButtons.transform.Find("HostButton")?.gameObject;
        // same reason we check above
        if (templateButton == null || templateButton.transform.childCount < 2)
        {
            return;
        }
        GameObject saveFileButton = Object.Instantiate(templateButton, parent: mainButtons.transform);
        saveFileButton.name = "SaveFileButton";
        saveFileButton.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>().text = "> Edit save files";

        // we'll put it right above the settings button
        Transform settingsButton = mainButtons.transform.Find("SettingsButton");
        int ButtonIndex = settingsButton.GetSiblingIndex();
        saveFileButton.transform.position = settingsButton.position;
        saveFileButton.transform.SetSiblingIndex(ButtonIndex);

        Button saveFileButtonButton = saveFileButton.GetComponent<Button>();
        saveFileButtonButton.onClick.RemoveAllListeners();
        saveFileButtonButton.onClick.AddListener(ClickSaveFileButton);

        // now we push everything above the settings button (including newButton) up to avoid overlap
        // for this we get the vertical space between the Credits and Quit button as a reference
        float vertSpace = mainButtons.transform.Find("Credits").transform.position.y - mainButtons.transform.Find("QuitButton").transform.position.y;
        // don't move the child at index 0 since that's the HeaderImage
        for (int i = ButtonIndex; i >= 1; i--)
        {
            mainButtons.transform.GetChild(i).transform.position += new Vector3(0, vertSpace, 0);
        }

        LCSaveEditor.InitFileSelector();
        LCSaveEditor.InitFileEditScreen();
        LCSaveEditor.InitSFPropsListItem();
    }

    public static void ClickSaveFileButton()
    {
        Debug.Log("save file button pressed");
        
        LCSaveEditor.FileSelector?.SetActive(true);
    }
}