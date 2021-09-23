using MelonLoader;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace Friend_Notes
{
    internal class Importer
    {
        public static FileInfo oldNotesFile = new FileInfo("UserData/FriendNotes.txt");
        public static FileInfo oldDatesFile = new FileInfo("UserData/FriendNotes_addDates.txt");

        public static void Import()
        {
            ImportOldNotes(oldNotesFile);
            ImportOldDates(oldDatesFile);
        }

        public static void ImportOldNotes(FileInfo file)
        {
            if (!file.Exists) return;
            var notes = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(oldNotesFile.ReadAllText());
            foreach (var note in notes)
            {
#if DEBUG
                MelonLogger.Msg(note.Key + ": " + note.Value);
#endif
                FriendNotes.notes.AddOrUpdate(note.Key);
                FriendNotes.notes[note.Key].Note = note.Value;
            }
            FriendNotes.saveNotes();
            MelonLogger.Warning($"Imported {notes.Count} notes from {file.FullName.Quote()}");
            file.Backup(); file.Delete();
        }

        public static void ImportOldDates(FileInfo file)
        {
            if (!file.Exists) return;
            var dates = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(file.ReadAllText());
            foreach (var date in dates)
            {
#if DEBUG
                MelonLogger.Msg(date.Key + ": " + date.Value);
#endif
                FriendNotes.notes.AddOrUpdate(date.Key);
                DateTime _date;
                if (DateTime.TryParseExact(date.Value, "dd/MM/yyyy - hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out _date))
                    FriendNotes.notes[date.Key].DateAdded = _date;
            }
            FriendNotes.saveNotes();
            MelonLogger.Warning($"Imported {dates.Count} dates from {file.FullName.Quote()}");
            file.Backup(); file.Delete();
        }
    }
}