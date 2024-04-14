using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCSaveEditor.Patches;

class MenuManagerPatch
{
    private static int ButtonIndex { get; } = 4;

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
        GameObject templateButton = mainButtons.transform.GetChild(1).gameObject;
        GameObject saveFileButton = Object.Instantiate(templateButton, parent: mainButtons.transform);
        saveFileButton.name = "SaveFileButton";
        saveFileButton.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = "> Edit save files";
        // we'll put it right above the settings button
        saveFileButton.transform.position = mainButtons.transform.GetChild(ButtonIndex).transform.position;
        saveFileButton.transform.SetSiblingIndex(ButtonIndex);

        var saveFileButtonButton = saveFileButton.GetComponent<Button>();
        saveFileButtonButton.onClick.RemoveAllListeners();
        saveFileButtonButton.onClick.AddListener(ClickSaveFileButton);

        // now we push everything above the settings button (including newButton) up to avoid overlap
        // for this we get the vertical space between the Credits and Quit button as a reference
        var vertSpace = mainButtons.transform.GetChild(6).transform.position.y - mainButtons.transform.GetChild(7).transform.position.y;
        for (int i = ButtonIndex; i >= 1; i--)
        {
            mainButtons.transform.GetChild(i).transform.position += new Vector3(0, vertSpace, 0);
        }
    }

    public static void ClickSaveFileButton()
    {
        Debug.Log("Save File button was clicked.");
    }
}