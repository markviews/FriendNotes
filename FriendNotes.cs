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

[assembly: MelonInfo(typeof(Friend_Notes.FriendNotes), "Friend Notes", "2.0.5", "MarkViews")]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonAdditionalDependencies("UIExpansionKit")]

namespace Friend_Notes {
    public class FriendNotes : MelonMod {
        public static class ModInfo {
            public static string Name = typeof(FriendNotes).Name;
            public static string FullName = "Friend Notes"; // typeof(FriendNotes).Assembly.GetCustomAttributes(typeof(MelonInfoAttribute), false)[1] as string;
        }

        public static FileInfo notesFile = new FileInfo("UserData/FriendNotes.json");

        public static MelonPreferences_Category cat;
        public static bool showNotesOnNameplates;       // = cat.GetEntry<bool>("showNotesOnNameplates").Value;
        public static bool logDate;                                   // = cat.GetEntry<bool>("logDate").Value;
        public static bool showDateOnNameplates;         // = cat.GetEntry<bool>("showDateOnNameplates").Value;
        public static bool logName;                                   // = cat.GetEntry<bool>("logName").Value;
        public static Color noteColor;                             // = cat.GetEntry<Color>("noteColor").Value;
        public static Color dateColor;                             // = cat.GetEntry<Color>("dateColor").Value;
        public static string dateFormat;                         // = cat.GetEntry<string>("dateFormat").Value;
        public static bool notesAtBioTopNotBottom;     // = cat.GetEntry<bool>("notesAtBioTopNotBottom").Value;

        public static Dictionary<string, UserNote> notes;
        public static Text bio;
        public static VRC.UI.PageUserInfo userInfoPage;

        public override void OnApplicationStart() {
            cat = MelonPreferences.CreateCategory(ModInfo.Name, ModInfo.FullName);
            cat.CreateEntry("showNotesOnNameplates", true, "Show notes on nameplates");
            cat.CreateEntry("showDateOnNameplates", true, "Show date on nameplates");
            cat.CreateEntry("logDate", true, "Log date you add friends");
            cat.CreateEntry("logName", true, "Log friend display names");
            cat.CreateEntry("notesAtBioTopNotBottom", false, "Show notes at the start of bios instead of the end");
            cat.CreateEntry("noteColor", "e6e657");
            cat.CreateEntry("dateColor", "858585");
            cat.CreateEntry("dateFormat", "M/d/yy - hh:mm tt");

            notes = loadNotes();

            Importer.Import();

            MelonCoroutines.Start(UiManagerInitializer());
            createButton();

            OnPreferencesSaved();
        }

        public override void OnPreferencesSaved() {
            showNotesOnNameplates = MelonPreferences.GetEntryValue<bool>(cat.Identifier, "showNotesOnNameplates");
            logDate = MelonPreferences.GetEntryValue<bool>(cat.Identifier, "logDate");
            logName = MelonPreferences.GetEntryValue<bool>(cat.Identifier, "logName");
            notesAtBioTopNotBottom = MelonPreferences.GetEntryValue<bool>(cat.Identifier, "notesAtBioTopNotBottom");
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

        public IEnumerator UiManagerInitializer() {
            while (VRCUiManager.prop_VRCUiManager_0 == null) yield return null;

            MelonLogger.Msg("UiManagerInitializer");

            NetworkManagerHooks.Initialize();
            NetworkManagerHooks.OnJoin += OnPlayerJoined;

            bio = GameObject.Find("UserInterface/MenuContent/Screens/UserInfo/User Panel/UserBio/Bio Scroll View/Viewport/Content/BioText").GetComponent<Text>();
            userInfoPage = GameObject.Find("UserInterface/MenuContent/Screens/UserInfo").GetComponent<VRC.UI.PageUserInfo>();
            GameObject userInfo = GameObject.Find("UserInterface/MenuContent/Screens/UserInfo");

            userInfo.AddComponent<EnableDisableListener>().OnEnabled += () => {
                MelonCoroutines.Start(waitForSocialMenu(() => {
                    updateText();
                }));
            };

            Harmony.Patch(typeof(APIUser).GetMethod("LocalAddFriend"), null, new HarmonyMethod(typeof(FriendNotes).GetMethod(nameof(OnFriend), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static void OnFriend(APIUser user) {
            if (!logDate) return;
            notes.AddOrUpdate(user);
            notes[user.id].DateAdded = DateTime.Now;
            saveNotes();
        }

        public IEnumerator waitForSocialMenu(Action action) {
            while (userInfoPage == null)
                yield return null;

            while (!userInfoPage.isActiveAndEnabled)
                yield return null;

            yield return new WaitForSeconds(0.2f);
            action.Invoke();
        }


        public IEnumerator delayRun(Action action, float wait) {
            yield return new WaitForSeconds(wait);
            action.Invoke();
        }

        public void updateText() {
            APIUser user = userInfoPage.field_Private_APIUser_0;

            if (notes.ContainsKey(user.id)) {
                bool needsBreak = false;
                // `notesAtBioTopNotBottom = false` when the bio should be first and notes last.
                if (user.bio != null && !notesAtBioTopNotBottom) {
                    bio.text = user.bio + "\n";
                    needsBreak = true;
                } else bio.text = "";

                UserNote note = notes[user.id];
                if (note.HasNote) {
                    if (needsBreak) bio.text += "\n";
                    needsBreak = true;
                    bio.text += "Note: " + note.Note;
                }

                if (note.HasDate) {
                    if (needsBreak) bio.text += "\n";
                    needsBreak = true;
                    bio.text += note.DateAddedText;
                }

                if (note.DisplayNames != null)
                    foreach (DisplayName dn in note.DisplayNames) {
                        if (user.displayName != dn.Name) {
                            if (needsBreak) bio.text += "\n";
                            needsBreak = true;
                            bio.text += "Previous name: " + dn.Name + " " + dn.Date?.ToString(dateFormat);
                        }
                    }

                if (user.bio != null && notesAtBioTopNotBottom) {
                    if (needsBreak) bio.text += "\n";
                    bio.text += "\n" + user.bio;
                }
            }

            if (logName && user.isFriend) {
                if (!notes.ContainsKey(user.id)) {
                    notes.AddOrUpdate(user);
                    saveNotes();
                } else {
                    UserNote note = notes[user.id];
                    bool newName = true;

                    if (note.DisplayNames != null)
                        foreach (DisplayName dn in note.DisplayNames) {
                            if (user.displayName == dn.Name) {
                                newName = false;
                                break;
                            }
                        }

                    if (newName) {
                        notes.AddOrUpdate(user);
                        saveNotes();
                    }

                }

            }
        }

        private void createButton() {

            ExpansionKitApi.GetExpandedMenu(ExpandedMenu.UserDetailsMenu).AddSimpleButton("Edit Note", new Action(() => {
                APIUser user = userInfoPage.field_Private_APIUser_0;
                string userID = user.id;
                var noteBeforeEdit = notes.ContainsKey(userID) ? notes[userID].Note : "";
                BuiltinUiUtils.ShowInputPopup(noteBeforeEdit == "" ? "Edit Note" : "Add Note", noteBeforeEdit, InputField.InputType.Standard, false, "Confirm", (newNote, _, __) => {
                    notes.AddOrUpdate(user);
                    setNote(userID, newNote);
                    updateNameplates();
                    updateText();
                });

            }));
        }

        public void OnPlayerJoined(Player player) {
            if (player is null) return;
            updateNameplate(player);
        }

        public static void updateNameplate(Player player) {
            if (player == null) { MelonLogger.Error("updateNameplate player null"); return; }

            string userID = player.prop_String_0;
            if (player == null) { MelonLogger.Error("updateNameplate userID null"); return; }

            if (userID == PlayerManager.prop_PlayerManager_0.field_Private_Player_0.prop_String_0) return; //ignore self

            Transform textContainer = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Text Container");
            if (textContainer == null) return;
            Transform noteTransform = textContainer.Find("Note");
            Transform dateTransform = textContainer.Find("Date");

            if (notes.ContainsKey(userID))
                if ((!notes[userID].HasNote || !showNotesOnNameplates) && (!notes[userID].HasDate || !showDateOnNameplates)) return;

            GameObject noteObj, dateObj;
            if (noteTransform == null) {
                noteObj = GameObject.Instantiate(textContainer.Find("Sub Text").gameObject, textContainer);
                noteObj.name = "Note";

                dateObj = GameObject.Instantiate(textContainer.Find("Sub Text").gameObject, textContainer);
                dateObj.name = "Date";

                GameObject originalSubText = textContainer.Find("Sub Text").gameObject;
                RectTransform bg = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Background").GetComponent<RectTransform>();
                RectTransform glow = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Glow").GetComponent<RectTransform>();
                RectTransform pulse = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Main/Pulse").GetComponent<RectTransform>();

                Transform statusLine = player.gameObject.transform.Find("Player Nameplate/Canvas/Nameplate/Contents/Status Line/");

                originalSubText.AddComponent<EnableDisableListener>().OnEnabled += () => {
                    
                    int linesAdded = 0;
                    if (notes.ContainsKey(userID)) {
                        if (showNotesOnNameplates && notes[userID].HasNote) {
                            noteObj.SetActive(true);
                            linesAdded++;
                        }
                        if (showDateOnNameplates && notes[userID].HasDate) {
                            dateObj.SetActive(true);
                            linesAdded++;
                        }
                    }
                    statusLine.transform.localPosition = new Vector3(0.0066f, -58 - (linesAdded * 18), 0f);
                    bg.anchorMin = new Vector2(0, linesAdded * -0.3f);
                    glow.anchorMin = new Vector2(0, linesAdded * -0.3f);
                    pulse.anchorMin = new Vector2(0, linesAdded * -0.3f);
                };

                originalSubText.AddComponent<EnableDisableListener>().OnDisabled += () => {
                    bg.anchorMin = new Vector2(0, 0);
                    glow.anchorMin = new Vector2(0, 0);
                    pulse.anchorMin = new Vector2(0, 0);
                    noteObj.SetActive(false);
                    dateObj.SetActive(false);
                };
            } else {
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

        public static void updateNameplates() {
            foreach (Player player in PlayerManager.prop_PlayerManager_0.field_Private_List_1_Player_0) {
                if (notes.ContainsKey(player.prop_String_0)) updateNameplate(player);
            }
        }

        public static void setNote(string userID, string newNote) {
            if (notes.ContainsKey(userID)) notes[userID].Note = newNote;
            else notes[userID] = new UserNote() { Note = newNote };

            saveNotes();

            foreach (Player player in PlayerManager.prop_PlayerManager_0.field_Private_List_1_Player_0) {
                if (player.prop_String_0 == userID) {
                    updateNameplate(player);
                    break;
                }
            }
        }

        public static void saveNotes() => notes.ToFile(notesFile);

        public static Dictionary<string, UserNote> loadNotes() {
            if (notesFile.Exists) {
                try {
                    notes = UserNotes.FromFile(notesFile);
                    return notes;
                } catch (Exception ex) {
                    MelonLogger.Error($"Failed to load notes from {notesFile.FullName.Quote()}:\n\t{ex.Message}");
                    notesFile.Backup(true, ".corrupt");
                }
            }
            notes = new Dictionary<string, UserNote>();
            return notes;
        }
    }
}