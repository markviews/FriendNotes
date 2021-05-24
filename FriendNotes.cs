using MelonLoader;
using UIExpansionKit.API;
using System.Collections;
using System.Web.Script.Serialization;
using System.IO;
using System.Collections.Generic;
using UnityEngine.UI;
using VRC;
using System;
using UnityEngine;
using UIExpansionKit.Components;

[assembly: MelonInfo(typeof(Friend_Notes.FriendNotes), "FriendNotes", "1.0.1", "Nola2")]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonOptionalDependencies("UIExpansionKit")]

namespace Friend_Notes {

    public class FriendNotes : MelonMod {

        private static Dictionary<string, string> notes = new Dictionary<string, string>();
        private static bool showNameplates, showNotesInMenu;
        TMPro.TextMeshPro textbox;

        public override void VRChat_OnUiManagerInit() {
            GameObject userInfo = GameObject.Find("UserInterface/MenuContent/Screens/UserInfo");
            textbox = userInfo.AddComponent<TMPro.TextMeshPro>();
            textbox.sortingOrder = 1;
            textbox.margin = new Vector4(25, 100, 0, 0);
            textbox.fontSize = 250;
            textbox.m_fontSize = 250;

            userInfo.AddComponent<EnableDisableListener>().OnEnabled += () => {
                if (!showNotesInMenu) return;

                MelonCoroutines.Start(delayRun(() => {
                    updateText();
                }, 0.5f));

            };

            userInfo.AddComponent<EnableDisableListener>().OnDisabled += () => {
                    textbox.text = "";
            };

        }

        public IEnumerator delayRun(Action action, float wait) {
            yield return new WaitForSeconds(wait);
            action.Invoke();
        }

        public void updateText() {
            string userID = QuickMenu.prop_QuickMenu_0.field_Public_MenuController_0.activeUser.id;
            string note = getNote(userID);

            textbox.text = note;
            textbox.fontSize = 250;
            textbox.m_fontSize = 250;
        }

        public override void OnPreferencesSaved() {
            showNameplates = MelonPreferences.GetEntryValue<bool>("FriendNotes", "showNotesOnNameplates");
            showNotesInMenu = MelonPreferences.GetEntryValue<bool>("FriendNotes", "showNotesInMenu");
            updateNameplates();
        }

        public override void OnApplicationStart() {
            MelonPreferences.CreateCategory("FriendNotes", "Friend Notes");
            MelonPreferences.CreateEntry("FriendNotes", "showNotesOnNameplates", true, "Show notes on nameplates?");
            MelonPreferences.CreateEntry("FriendNotes", "showNotesInMenu", true, "Show notes in menu?");

            loadNotes();
            MelonCoroutines.Start(Initialize());
            ExpansionKitApi.RegisterWaitConditionBeforeDecorating(createButton());

            showNameplates = MelonPreferences.GetEntryValue<bool>("FriendNotes", "showNotesOnNameplates");
            showNotesInMenu = MelonPreferences.GetEntryValue<bool>("FriendNotes", "showNotesInMenu");
        }

        private IEnumerator Initialize() {
            while (ReferenceEquals(NetworkManager.field_Internal_Static_NetworkManager_0, null))
                yield return null;

            NetworkManagerHooks.Initialize();
            NetworkManagerHooks.OnJoin += OnPlayerJoined;
        }

        private IEnumerator createButton() {
            while (QuickMenu.prop_QuickMenu_0 == null) yield return null;

            #pragma warning disable CS0618
            ExpansionKitApi.RegisterSimpleMenuButton(ExpandedMenu.UserDetailsMenu, "Edit Note", new Action(() => {

                string userID = QuickMenu.prop_QuickMenu_0.field_Public_MenuController_0.activeUser.id;
                string noteBeforeEdit = getNote(userID);

                BuiltinUiUtils.ShowInputPopup("Edit Note", noteBeforeEdit, InputField.InputType.Standard, false, "Confirm", (newNote, _, __) => {
                    setNote(userID, newNote);
                    updateNameplates();
                    if (showNotesInMenu) updateText();
                });

            }));
        }

        public void OnPlayerJoined(Player player) {
            if (player != null)
                updateNameplate(player);
        }

        public static void updateNameplate(Player player) {
            string userID = player.field_Private_APIUser_0.id;
            string note = getNote(userID);
            if (!showNameplates) note = "";

            Transform textContainer = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Text Container");
            if (textContainer == null) return;
            Transform subTextTransform = textContainer.Find("Note");

            if ((note == "" || !showNameplates) && subTextTransform != null) {
                subTextTransform.gameObject.active = false;
                return;
            }

            GameObject subText;
            if (subTextTransform == null) {
                subText = GameObject.Instantiate(textContainer.Find("Sub-Text").gameObject, textContainer.transform, true);
                VerticalLayoutGroup layerGroup = textContainer.GetComponent<VerticalLayoutGroup>();
                layerGroup.rectChildren.Add(subText.GetComponent<RectTransform>());
                subText.name = "Note";
            } else {
                subText = subTextTransform.gameObject;
            }
            subText.GetComponent<TMPro.TextMeshProUGUI>().text = note;
            subText.active = true;
        }

        public static void updateNameplates() {
            foreach (Player player in PlayerManager.prop_PlayerManager_0.field_Private_List_1_Player_0) {
                string userID = player.prop_String_0;
                if (notes.ContainsKey(userID)) {
                    updateNameplate(player);
                }
            }
        }

        public static string getNote(string userID) {
            if (notes.ContainsKey(userID))
                return notes[userID];
            return "";
        }

        public static void setNote(string userID, string note) {
            if (note == "") {
                if (notes.ContainsKey(userID))
                    notes.Remove(userID);
            } else {
                if (notes.ContainsKey(userID))
                    notes[userID] = note;
                else
                    notes.Add(userID, note);
            }

            File.WriteAllText("UserData/FriendNotes.txt", new JavaScriptSerializer().Serialize(notes));

            foreach (Player player in PlayerManager.prop_PlayerManager_0.field_Private_List_1_Player_0) {
                if (player.prop_String_0 == userID) {
                    updateNameplate(player);
                    break;
                }
            }

        }

        public static void loadNotes() {

            //move data if it's still in old location (this code can probably be removed after a few updates as it only applies to version 1.0)
            try {
                notes = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText("Mods/FriendNotes/notes.txt"));
                setNote("", "");
                Directory.Delete("Mods/FriendNotes", true);
                MelonLogger.Log("Moving data to userData/FriendNotes.txt");
            } catch (Exception e) {}

            //load data
            try {
                notes = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText("UserData/FriendNotes.txt"));
            } catch (Exception e) {}
        }

    }

}
