using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Friend_Notes
{
    public enum BooleanStringMode
    {
        Numbers,
        TrueFalse,
        EnabledDisabled,
        OnOff,
        YesNo,
    }

    public static partial class Extensions
    {
        #region Reflection

        public static Dictionary<string, object> ToDictionary(this object instanceToConvert)
        {
            return instanceToConvert.GetType()
              .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
              .ToDictionary(
              propertyInfo => propertyInfo.Name,
              propertyInfo => Extensions.ConvertPropertyToDictionary(propertyInfo, instanceToConvert));
        }

        private static object ConvertPropertyToDictionary(PropertyInfo propertyInfo, object owner)
        {
            Type propertyType = propertyInfo.PropertyType;
            object propertyValue = propertyInfo.GetValue(owner);

            // If property is a collection don't traverse collection properties but the items instead
            if (!propertyType.Equals(typeof(string)) && (typeof(ICollection<>).Name.Equals(propertyValue.GetType().BaseType.Name) || typeof(Collection<>).Name.Equals(propertyValue.GetType().BaseType.Name)))
            {
                var collectionItems = new List<Dictionary<string, object>>();
                var count = (int)propertyType.GetProperty("Count").GetValue(propertyValue);
                PropertyInfo indexerProperty = propertyType.GetProperty("Item");

                // Convert collection items to dictionary
                for (var index = 0; index < count; index++)
                {
                    object item = indexerProperty.GetValue(propertyValue, new object[] { index });
                    PropertyInfo[] itemProperties = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

                    if (itemProperties.Any())
                    {
                        Dictionary<string, object> dictionary = itemProperties
                          .ToDictionary(
                            subtypePropertyInfo => subtypePropertyInfo.Name,
                            subtypePropertyInfo => Extensions.ConvertPropertyToDictionary(subtypePropertyInfo, item));
                        collectionItems.Add(dictionary);
                    }
                }

                return collectionItems;
            }

            // If property is a string stop traversal (ignore that string is a char[])
            if (propertyType.IsPrimitive || propertyType.Equals(typeof(string)))
            {
                return propertyValue;
            }

            PropertyInfo[] properties = propertyType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            if (properties.Any())
            {
                return properties.ToDictionary(
                                    subtypePropertyInfo => subtypePropertyInfo.Name,
                                    subtypePropertyInfo => (object)Extensions.ConvertPropertyToDictionary(subtypePropertyInfo, propertyValue));
            }

            return propertyValue;
        }

        #endregion Reflection

        #region DateTime

        public static bool ExpiredSince(this DateTime dateTime, int minutes)
        {
            return (dateTime - DateTime.Now).TotalMinutes < minutes;
        }

        public static TimeSpan StripMilliseconds(this TimeSpan time)
        {
            return new TimeSpan(time.Days, time.Hours, time.Minutes, time.Seconds);
        }

        #endregion DateTime

        #region DirectoryInfo

        public static DirectoryInfo Combine(this Environment.SpecialFolder specialFolder, params string[] paths) => Combine(new DirectoryInfo(Environment.GetFolderPath(specialFolder)), paths);

        public static FileInfo CombineFile(this Environment.SpecialFolder specialFolder, params string[] paths) => CombineFile(new DirectoryInfo(Environment.GetFolderPath(specialFolder)), paths);

        public static DirectoryInfo Combine(this DirectoryInfo dir, params string[] paths)
        {
            var final = dir.FullName;
            foreach (var path in paths)
            {
                final = Path.Combine(final, path.ReplaceInvalidFileNameChars());
            }
            return new DirectoryInfo(final);
        }

        public static FileInfo CombineFile(this DirectoryInfo dir, params string[] paths)
        {
            var final = dir.FullName;
            foreach (var path in paths)
            {
                final = Path.Combine(final, path);
            }
            return new FileInfo(final);
        }

        public static string PrintablePath(this FileSystemInfo file) => file.FullName.Replace(@"\\", @"\");

        #endregion DirectoryInfo

        #region FileInfo

        public static FileInfo Backup(this FileInfo file, bool overwrite = true, string extension = ".bak")
        {
            return file.CopyTo(file.FullName + extension, overwrite);
        }

        public static FileInfo Combine(this FileInfo file, params string[] paths)
        {
            var final = file.DirectoryName;
            foreach (var path in paths)
            {
                final = Path.Combine(final, path);
            }
            return new FileInfo(final);
        }

        public static string FileNameWithoutExtension(this FileInfo file)
        {
            return Path.GetFileNameWithoutExtension(file.Name);
        }

        /*public static string Extension(this FileInfo file) {
            return Path.GetExtension(file.Name);
        }*/

        public static void AppendLine(this FileInfo file, string line)
        {
            try
            {
                if (!file.Exists) file.Create();
                File.AppendAllLines(file.FullName, new string[] { line });
            }
            catch { }
        }

        public static void WriteAllText(this FileInfo file, string text)
        {
            file.Directory.Create();
            if (!file.Exists) file.Create().Close();
            File.WriteAllText(file.FullName, text);
        }

        public static string ReadAllText(this FileInfo file) => File.ReadAllText(file.FullName);

        public static List<string> ReadAllLines(this FileInfo file) => File.ReadAllLines(file.FullName).ToList();

        #endregion FileInfo

        #region Object

        public static string ToJSON(this object obj, bool indented = true)
        {
            // return JsonConvert.SerializeObject(obj, (indented ? Formatting.Indented : Formatting.None), new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, DateFormatString = "yyyy-MM-dd hh:mm:ss"}) ; // , new JsonConverter[] { new StringEnumConverter() }
            return JsonConvert.SerializeObject(obj, (indented ? Formatting.Indented : Formatting.None), new JsonConverter[] { new StringEnumConverter() });
        }

        #endregion Object

        #region String

        public static string GetDigits(this string input)
        {
            return new string(input.Where(char.IsDigit).ToArray());
        }

        public static string Format(this string input, params string[] args)
        {
            return string.Format(input, args);
        }

        public static IEnumerable<string> SplitToLines(this string input)
        {
            if (input == null)
            {
                yield break;
            }

            using (System.IO.StringReader reader = new System.IO.StringReader(input))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        public static string ToTitleCase(this string source, string langCode = "en-US")
        {
            return new CultureInfo(langCode, false).TextInfo.ToTitleCase(source);
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        public static bool IsNullOrEmpty(this string source)
        {
            return string.IsNullOrEmpty(source);
        }

        public static bool IsNullOrWhiteSpace(this string source)
        {
            return string.IsNullOrWhiteSpace(source);
        }

        public static string[] Split(this string source, string split, int count = -1, StringSplitOptions options = StringSplitOptions.None)
        {
            if (count != -1) return source.Split(new string[] { split }, count, options);
            return source.Split(new string[] { split }, options);
        }

        public static string Remove(this string Source, string Replace)
        {
            return Source.Replace(Replace, string.Empty);
        }

        public static string ReplaceLastOccurrence(this string Source, string Find, string Replace)
        {
            int place = Source.LastIndexOf(Find);
            if (place == -1)
                return Source;
            string result = Source.Remove(place, Find.Length).Insert(place, Replace);
            return result;
        }

        public static string Ext(this string text, string extension)
        {
            return text + "." + extension;
        }

        public static string Quote(this string text)
        {
            return SurroundWith(text, "\"");
        }

        public static string Enclose(this string text)
        {
            return SurroundWith(text, "(", ")");
        }

        public static string Brackets(this string text)
        {
            return SurroundWith(text, "[", "]");
        }

        public static string SurroundWith(this string text, string surrounds)
        {
            return surrounds + text + surrounds;
        }

        public static string SurroundWith(this string text, string starts, string ends)
        {
            return starts + text + ends;
        }

        public static string RemoveInvalidFileNameChars(this string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        public static string ReplaceInvalidFileNameChars(this string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }

        #endregion String

        #region Int

        public static int Percentage(this int total, int part)
        {
            return (int)((double)part / total * 100);
        }

        #endregion Int

        #region Dict

        public static void AddSafe(this IDictionary<string, string> dictionary, string key, string value)
        {
            if (!dictionary.ContainsKey(key))
                dictionary.Add(key, value);
        }

        #endregion Dict

        #region List

        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
   => self.Select((item, index) => (item, index));

        public static string Join(this List<string> strings, string separator)
        {
            return string.Join(separator, strings);
        }

        public static T PopFirst<T>(this IEnumerable<T> list) => list.ToList().PopAt(0);

        public static T PopLast<T>(this IEnumerable<T> list) => list.ToList().PopAt(list.Count() - 1);

        public static T PopAt<T>(this List<T> list, int index)
        {
            T r = list.ElementAt<T>(index);
            list.RemoveAt(index);
            return r;
        }

        #endregion List

        #region Task

        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    return default(TResult);
                }
            }
        }

        #endregion Task

        #region bool

        public static string ToString(this bool value, BooleanStringMode mode = BooleanStringMode.TrueFalse, bool capitalize = true)
        {
            string str;
            switch (mode)
            {
                case BooleanStringMode.Numbers:
                    str = value ? "1" : "0"; break;
                case BooleanStringMode.TrueFalse:
                    str = value ? "True" : "False"; break;
                case BooleanStringMode.EnabledDisabled:
                    str = value ? "Enabled" : "Disabled"; break;
                case BooleanStringMode.OnOff:
                    str = value ? "On" : "Off"; break;
                case BooleanStringMode.YesNo:
                    str = value ? "Yes" : "No"; break;
                default: throw new ArgumentNullException("mode");
            }
            return capitalize ? str : str.ToLower();
        }

        #endregion bool
    }
}