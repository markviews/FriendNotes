﻿using Harmony;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UIExpansionKit.API;
using UIExpansionKit.Components;
using UnityEngine;
using UnityEngine.UI;
using VRC;
using VRC.Core;
using VRChatUtilityKit.Utilities;

[assembly: MelonInfo(typeof(Friend_Notes.FriendNotes), "Friend Notes", "1.1.0", "MarkViews, Bluscream")]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonOptionalDependencies("UIExpansionKit", "VRChatUtilityKit")]

namespace Friend_Notes
{
    public class FriendNotes : MelonMod
    {
        public static class ModInfo
        {
            public static string Name = typeof(FriendNotes).Name;
            public static string FullName = "Friend Notes"; // typeof(FriendNotes).Assembly.GetCustomAttributes(typeof(MelonInfoAttribute), false)[1] as string;
        }

        public static FileInfo notesFile = new FileInfo("UserData/FriendNotes.json");

        public static MelonPreferences_Category cat;
        public static bool showNotesOnNameplates;        // = cat.GetEntry<bool>("showNotesOnNameplates").Value;
        public static bool showNotesInMenu;                  // = cat.GetEntry<bool>("showNotesInMenu").Value;
        public static bool logDate;                                  // = cat.GetEntry<bool>("logDate").Value;
        public static bool showDateOnNameplates;         // = cat.GetEntry<bool>("showDateOnNameplates").Value;
        public static Color noteColor;                             // = cat.GetEntry<Color>("noteColor").Value;
        public static Color dateColor;                             // = cat.GetEntry<Color>("dateColor").Value;
        public static string dateFormat;                         // = cat.GetEntry<string>("dateFormat").Value;

        public static Dictionary<string, UserNote> notes;
        public static Text textbox;

        public override void OnApplicationStart()
        {
            cat = MelonPreferences.CreateCategory(ModInfo.Name, ModInfo.FullName);
            cat.CreateEntry("showNotesOnNameplates", true, "Show notes on nameplates?");
            cat.CreateEntry("showNotesInMenu", true, "Show notes in menu?");
            cat.CreateEntry("logDate", true, "Log date you add friends?");
            cat.CreateEntry("showDateOnNameplates", true, "Show date on nameplates?");
            cat.CreateEntry("noteColor", "e6e657");
            cat.CreateEntry("dateColor", "858585");
            cat.CreateEntry("dateFormat", "M/d/yy - hh:mm tt");

            notes = loadNotes();

            Importer.Import();

            NetworkEvents.OnPlayerJoined += OnPlayerJoined;

            MelonCoroutines.Start(UiManagerInitializer());
            ExpansionKitApi.RegisterWaitConditionBeforeDecorating(createButton());

            OnPreferencesSaved();
        }

        public override void OnPreferencesSaved()
        {
            showNotesOnNameplates = MelonPreferences.GetEntryValue<bool>(cat.Identifier, "showNotesOnNameplates");
            showNotesInMenu = MelonPreferences.GetEntryValue<bool>(cat.Identifier, "showNotesInMenu");
            logDate = MelonPreferences.GetEntryValue<bool>(cat.Identifier, "logDate");
            showDateOnNameplates = MelonPreferences.GetEntryValue<bool>(cat.Identifier, "showDateOnNameplates");
            dateFormat = MelonPreferences.GetEntryValue<string>(cat.Identifier, "dateFormat");

            string noteColorStr = MelonPreferences.GetEntryValue<string>(cat.Identifier, "noteColor");
            Color color;
            if (ColorUtility.TryParseHtmlString("#" + noteColorStr, out color)) noteColor = color;
            else MelonLogger.Warning("Invalid HEX color code: #" + noteColorStr);

            string dateColorStr = MelonPreferences.GetEntryValue<string>(cat.Identifier, "dateColor");
            Color color2;
            if (ColorUtility.TryParseHtmlString("#" + dateColorStr, out color2)) dateColor = color2;
            else MelonLogger.Warning("Invalid HEX color code:# " + dateColorStr);
            updateNameplates();
        }

        public IEnumerator UiManagerInitializer()
        {
            while (VRCUiManager.prop_VRCUiManager_0 == null) yield return null;

            Transform parent = GameObject.Find("UserInterface/MenuContent/Screens/UserInfo/User Panel").transform;
            GameObject textObj = GameObject.Instantiate(parent.Find("NameText").gameObject, parent);
            textObj.transform.localPosition = new Vector3(-159, 13, -10);
            textbox = textObj.GetComponent<Text>();
            textbox.fontSize = 30;
            textbox.text = "";

            GameObject userInfo = GameObject.Find("UserInterface/MenuContent/Screens/UserInfo");

            userInfo.AddComponent<EnableDisableListener>().OnEnabled += () =>
            {
                if (!showNotesInMenu) return;

                MelonCoroutines.Start(delayRun(() =>
                {
                    updateText();
                }, 0.5f));
            };

            userInfo.AddComponent<EnableDisableListener>().OnDisabled += () =>
            {
                textbox.text = "";
            };

            Harmony.Patch(typeof(APIUser).GetMethod("LocalAddFriend"), null, new HarmonyMethod(typeof(FriendNotes).GetMethod(nameof(OnFriend), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static void OnFriend(APIUser user)
        {
            if (!logDate) return;
            notes.AddOrUpdate(user);
            notes[user.id].DateAdded = DateTime.Now;
            saveNotes();
        }

        public IEnumerator delayRun(Action action, float wait)
        {
            yield return new WaitForSeconds(wait);
            action.Invoke();
        }

        public void updateText()
        {
            if (notes.ContainsKey(VRCUtils.ActiveUserInUserInfoMenu.id))
                textbox.text = notes[VRCUtils.ActiveUserInUserInfoMenu.id].FullText;
        }

        /*
[16:25:42.817] [ERROR] Exception in IL2CPP-to-Managed trampoline, not passing it to il2cpp: System.NullReferenceException: Object reference not set to an instance of an object
  at Friend_Notes.FriendNotes.<createButton>b__20_0 () [0x00038] in <ee6d0629f7084af5925ba212c51aae71>:0
  at (wrapper dynamic-method) UnhollowerRuntimeLib.DelegateSupport.(il2cpp delegate trampoline) System.Void(intptr,UnhollowerBaseLib.Runtime.Il2CppMethodInfo*)
        */

        private IEnumerator createButton()
        {
            while (QuickMenu.prop_QuickMenu_0 == null) yield return null;

#pragma warning disable CS0618
            ExpansionKitApi.RegisterSimpleMenuButton(ExpandedMenu.UserDetailsMenu, "Edit Note", new Action(() =>
            {
                var user = VRCUtils.ActiveUserInUserInfoMenu;
                string userID = user.id;
                var noteBeforeEdit = notes.ContainsKey(userID) ? notes[userID].Note : "";
                BuiltinUiUtils.ShowInputPopup(noteBeforeEdit == "" ? "Edit Note" : "Add Note", noteBeforeEdit, InputField.InputType.Standard, false, "Confirm", (newNote, _, __) =>
                {
                    notes.AddOrUpdate(user);
                    setNote(userID, newNote);
                    updateNameplates();
                    if (showNotesInMenu) updateText();
                });
            }));
        }

        public void OnPlayerJoined(Player player)
        {
            if (player is null) return;
            updateNameplate(player);
        }

        public static void updateNameplate(Player player)
        {
            string userID = player.prop_String_0;

            if (userID == PlayerManager.prop_PlayerManager_0.field_Private_Player_0.prop_String_0) return; //ignore self

            Transform textContainer = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Text Container");
            if (textContainer == null) return;
            Transform noteTransform = textContainer.Find("Note");
            Transform dateTransform = textContainer.Find("Date");

            if (notes.ContainsKey(userID))
            {
                if ((notes[userID].HasNote || !showNotesOnNameplates) && (notes[userID].HasDate || !showDateOnNameplates)) return;
            }

            GameObject noteObj, dateObj;
            if (noteTransform == null)
            {
                noteObj = GameObject.Instantiate(textContainer.Find("Sub Text").gameObject, textContainer);
                noteObj.name = "Note";

                dateObj = GameObject.Instantiate(textContainer.Find("Sub Text").gameObject, textContainer);
                dateObj.name = "Date";

                GameObject originalSubText = textContainer.Find("Sub Text").gameObject;
                RectTransform bg = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Background").GetComponent<RectTransform>();
                RectTransform glow = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Glow").GetComponent<RectTransform>();
                RectTransform pulse = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Pulse").GetComponent<RectTransform>();
                originalSubText.AddComponent<EnableDisableListener>().OnEnabled += () =>
                {
                    float height = 0;
                    if (notes.ContainsKey(userID))
                    {
                        if (showNotesOnNameplates && notes[userID].HasNote)
                        {
                            noteObj.SetActive(true);
                            height -= 0.3f;
                        }
                        if (showDateOnNameplates && notes[userID].HasDate)
                        {
                            dateObj.SetActive(true);
                            height -= 0.3f;
                        }
                    }
                    bg.anchorMin = new Vector2(0, height);
                    glow.anchorMin = new Vector2(0, height);
                    pulse.anchorMin = new Vector2(0, height);
                };

                originalSubText.AddComponent<EnableDisableListener>().OnDisabled += () =>
                {
                    bg.anchorMin = new Vector2(0, 0);
                    glow.anchorMin = new Vector2(0, 0);
                    pulse.anchorMin = new Vector2(0, 0);
                    noteObj.SetActive(false);
                    dateObj.SetActive(false);
                };
            }
            else
            {
                noteObj = noteTransform.gameObject;
                dateObj = dateTransform.gameObject;
            }

            noteObj.transform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>().text = notes.ContainsKey(userID) && notes[userID].HasNote ? notes[userID].Note : "";
            noteObj.transform.Find("Text").gameObject.SetActive(true);
            noteObj.transform.Find("Icon").GetComponent<Image>().color = noteColor;
            noteObj.transform.Find("Icon").gameObject.SetActive(true);
            noteObj.SetActive(false);

            dateObj.transform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>().text = notes.ContainsKey(userID) ? notes[userID].DateAddedText : "";
            dateObj.transform.Find("Text").gameObject.SetActive(true);
            dateObj.transform.Find("Icon").GetComponent<Image>().color = dateColor;
            dateObj.transform.Find("Icon").gameObject.SetActive(true);
            dateObj.SetActive(false);
        }

        public static void updateNameplates()
        {
            foreach (Player player in PlayerManager.prop_PlayerManager_0.field_Private_List_1_Player_0)
            {
                if (notes.ContainsKey(player.prop_String_0)) updateNameplate(player);
            }
        }

        public static void setDate(string userID)
        {
        }

        public static void setNote(string userID, string newNote)
        {
            if (notes.ContainsKey(userID)) notes[userID].Note = newNote;
            else notes[userID] = new UserNote() { Note = newNote };

            saveNotes();

            foreach (Player player in PlayerManager.prop_PlayerManager_0.field_Private_List_1_Player_0)
            {
                if (player.prop_String_0 == userID)
                {
                    updateNameplate(player);
                    break;
                }
            }
        }

        public static void saveNotes() => notes.ToFile(notesFile);

        public static Dictionary<string, UserNote> loadNotes()
        {
            if (notesFile.Exists)
            {
                try
                {
                    notes = UserNotes.FromFile(notesFile);
                    return notes;
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to load notes from {notesFile.FullName.Quote()}:\n\t{ex.Message}");
                    notesFile.Backup(true, ".corrupt");
                }
            }
            notes = new Dictionary<string, UserNote>();
            return notes;
        }
    }
}