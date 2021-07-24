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
using VRC.Core;
using Harmony;
using System.Reflection;
using System.Globalization;

[assembly: MelonInfo(typeof(Friend_Notes.FriendNotes), "Friend Notes", "1.0.6", "MarkViews")]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonOptionalDependencies("UIExpansionKit")]

namespace Friend_Notes {

    public class FriendNotes : MelonMod {

        private static Dictionary<string, string> notes = new Dictionary<string, string>();
        private static Dictionary<string, string> addDate = new Dictionary<string, string>();
        private static bool showNotesOnNameplates, showNotesInMenu, logDate, showDateOnNameplates;
        private static TMPro.TextMeshPro textbox;
        private static Color noteColor, dateColor;
        private static string dateFormat;

        public IEnumerator UiManagerInitializer() {
            while (VRCUiManager.prop_VRCUiManager_0 == null) yield return null;

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

            Harmony.Patch(typeof(APIUser).GetMethod("LocalAddFriend"), null, new HarmonyMethod(typeof(FriendNotes).GetMethod(nameof(OnFriend), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static void OnFriend(APIUser user) {
            setDate(user.id);
        }

        public IEnumerator delayRun(Action action, float wait) {
            yield return new WaitForSeconds(wait);
            action.Invoke();
        }

        public void updateText() {
            string userID = QuickMenu.prop_QuickMenu_0.field_Public_MenuController_0.activeUser.id;
            string note = getNote(userID) + " " + getDate(userID);

            textbox.text = note;
            textbox.fontSize = 250;
            textbox.m_fontSize = 250;
        }

        public override void OnPreferencesSaved() {
            showNotesOnNameplates = MelonPreferences.GetEntryValue<bool>("FriendNotes", "showNotesOnNameplates");
            showNotesInMenu = MelonPreferences.GetEntryValue<bool>("FriendNotes", "showNotesInMenu");
            logDate = MelonPreferences.GetEntryValue<bool>("FriendNotes", "logDate");
            showDateOnNameplates = MelonPreferences.GetEntryValue<bool>("FriendNotes", "showDateOnNameplates");
            dateFormat = MelonPreferences.GetEntryValue<string>("FriendNotes", "dateFormat");

            string noteColorStr = MelonPreferences.GetEntryValue<string>("FriendNotes", "noteColor");
            Color color;
            if (ColorUtility.TryParseHtmlString("#" + noteColorStr, out color)) noteColor = color;
            else MelonLogger.Warning("Invalid HEX color code: #" + noteColorStr);

            string dateColorStr = MelonPreferences.GetEntryValue<string>("FriendNotes", "dateColor");
            Color color2;
            if (ColorUtility.TryParseHtmlString("#" + dateColorStr, out color2)) dateColor = color2;
            else MelonLogger.Warning("Invalid HEX color code:# " + dateColorStr);
            updateNameplates();
        }

        public override void OnApplicationStart() {
            MelonPreferences.CreateCategory("FriendNotes", "Friend Notes");
            MelonPreferences.CreateEntry("FriendNotes", "showNotesOnNameplates", true, "Show notes on nameplates?");
            MelonPreferences.CreateEntry("FriendNotes", "showNotesInMenu", true, "Show notes in menu?");
            MelonPreferences.CreateEntry("FriendNotes", "logDate", true, "Log date you add friends?");
            MelonPreferences.CreateEntry("FriendNotes", "showDateOnNameplates", true, "Show date on nameplates?");
            MelonPreferences.CreateEntry("FriendNotes", "noteColor", "e6e657");
            MelonPreferences.CreateEntry("FriendNotes", "dateColor", "858585");
            MelonPreferences.CreateEntry("FriendNotes", "dateFormat", "M/d/yy - hh:mm tt");

            loadNotes();
            MelonCoroutines.Start(UiManagerInitializer());
            MelonCoroutines.Start(Initialize());
            ExpansionKitApi.RegisterWaitConditionBeforeDecorating(createButton());

            OnPreferencesSaved();
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
            string userID = player.prop_String_0;

            //ignore self
            if (userID == PlayerManager.prop_PlayerManager_0.field_Private_Player_0.prop_String_0) return;

            string note = getNote(userID);
            string date = getDate(userID);

            Transform textContainer = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Text Container");
            if (textContainer == null) return;
            Transform noteTransform = textContainer.Find("Note");
            Transform dateTransform = textContainer.Find("Date");

            if ((note == "" || !showNotesOnNameplates) && (date == "" || !showDateOnNameplates))
                return;

            GameObject noteObj;
            GameObject dateObj;
            if (noteTransform == null) {
                noteObj = GameObject.Instantiate(textContainer.Find("Sub Text").gameObject, textContainer);
                noteObj.name = "Note";

                dateObj = GameObject.Instantiate(textContainer.Find("Sub Text").gameObject, textContainer);
                dateObj.name = "Date";

                GameObject originalSubText = textContainer.Find("Sub Text").gameObject;
                RectTransform bg = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Background").GetComponent<RectTransform>();
                RectTransform glow = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Glow").GetComponent<RectTransform>();

                originalSubText.AddComponent<EnableDisableListener>().OnEnabled += () => {
                    float height = 0;
                    if (showNotesOnNameplates && getNote(userID) != "") {
                        noteObj.SetActive(true);
                        height -= 0.3f;
                    }
                    if (showDateOnNameplates && getDate(userID) != "") {
                        dateObj.SetActive(true);
                        height -= 0.3f;
                    }
                    bg.anchorMin = new Vector2(0, height);
                    glow.anchorMin = new Vector2(0, height);
                };

                originalSubText.AddComponent<EnableDisableListener>().OnDisabled += () => {
                    bg.anchorMin = new Vector2(0, 0);
                    glow.anchorMin = new Vector2(0, 0);
                    noteObj.SetActive(false);
                    dateObj.SetActive(false);
                };

            } else {
                noteObj = noteTransform.gameObject;
                dateObj = dateTransform.gameObject;
            }

            noteObj.transform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>().text = note;
            noteObj.transform.Find("Text").gameObject.SetActive(true);
            noteObj.transform.Find("Icon").GetComponent<Image>().color = noteColor;
            noteObj.transform.Find("Icon").gameObject.SetActive(true);
            noteObj.SetActive(false);

            dateObj.transform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>().text = date;
            dateObj.transform.Find("Text").gameObject.SetActive(true);
            dateObj.transform.Find("Icon").GetComponent<Image>().color = dateColor;
            dateObj.transform.Find("Icon").gameObject.SetActive(true);
            dateObj.SetActive(false);
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

        public static string getDate(string userID) {
            if (addDate.ContainsKey(userID)) {
                DateTime date;
                if (DateTime.TryParseExact(addDate[userID], "dd/MM/yyyy - hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    return date.ToString(dateFormat);
                else MelonLogger.LogWarning("failed to parse date: " + addDate[userID]);
            }
            return "";
        }

        public static void setDate(string userID) {
            if (!logDate) return;
            if (!addDate.ContainsKey(userID)) {
                addDate.Add(userID, DateTime.Now.ToString("dd/MM/yyyy - hh:mm tt"));
                File.WriteAllText("UserData/FriendNotes_addDates.txt", new JavaScriptSerializer().Serialize(addDate));
            }
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
            //load data
            try {
                notes = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText("UserData/FriendNotes.txt"));
            } catch (Exception) {}

            try {
                addDate = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText("UserData/FriendNotes_addDates.txt"));
            } catch (Exception) { }

        }

    }

}
