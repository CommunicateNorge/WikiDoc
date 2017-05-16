using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Wiki.Utilities
{
    /// <summary>
    /// This class does stuff!
    /// </summary>
    public static class CustomHelperExtensions
    {
        /// <summary>
        /// Determines whether this instance can edit the specified identifier.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        public static Boolean CanEdit(this IPrincipal me, string id)
        {
            if (WikiBlob.IsOldVersion(id))
                return false;

            if (WikiBlob.IsManualPage(id, false) || WikiBlob.IsTemplate(id, false) || WikiBlob.IsApi(id, false) || WikiBlob.IsTFS(id, false) || WikiBlob.IsSQL(id, false))
                return true;
            
            return false;
        }

        public static string ToHtmlTable<T>(this List<T> listOfClassObjects, string headers)
        {
            var ret = string.Empty;

            return listOfClassObjects == null || !listOfClassObjects.Any()
                ? ret
                : "<table>" +
                  ToColumnHeaders(headers) +
                  listOfClassObjects.Aggregate(ret, (current, t) => current + t.ToHtmlTableRow()) +
                  "</table>";
        }

        public static string ToHtmlTable<T>(this List<T> listOfClassObjects)
        {
            var ret = string.Empty;

            return listOfClassObjects == null || !listOfClassObjects.Any()
                ? ret
                : "<table>" +
                  listOfClassObjects.First().GetType().GetProperties().Select(p => p.Name).ToList().ToColumnHeaders() +
                  listOfClassObjects.Aggregate(ret, (current, t) => current + t.ToHtmlTableRow()) +
                  "</table>";
        }

        public static string ToColumnHeaders<T>(this List<T> listOfProperties)
        {
            var ret = string.Empty;

            return listOfProperties == null || !listOfProperties.Any()
                ? ret
                : "<tr>" +
                  listOfProperties.Aggregate(ret,
                      (current, propValue) =>
                          current +
                          ("<th>" +
                           (Convert.ToString(propValue) + "</th>"))) +
                  "</tr>";
        }

        private static string ToColumnHeaders(string headers)
        {
            string[] headersSplit = headers.SplitSimple(",");
            StringBuilder b = new StringBuilder();
            b.Append("<tr>");
            foreach (var item in headersSplit)
            {
                b.Append("<th>" + (item.Length <= 100 ? item : item.Substring(0, 100) + "..." + "</th>"));
            }

            b.Append("</tr>");
            return b.ToString();
        }

        public static string ToHtmlTableRow<T>(this T classObject)
        {
            var ret = string.Empty;

            return classObject == null
                ? ret
                : "<tr>" +
                  classObject.GetType()
                      .GetProperties()
                      .Aggregate(ret,
                          (current, prop) =>
                              current + ("<td>" +
                                         (Convert.ToString(prop.GetValue(classObject, null)).Length <= 100
                                             ? Convert.ToString(prop.GetValue(classObject, null))
                                             : Convert.ToString(prop.GetValue(classObject, null)).Substring(0, 100) +
                                               "...") +
                                         "</td>")) + "</tr>";
        }

        /// <summary>
        /// Simple string split
        /// </summary>
        /// <param name="seperator">The seperator.</param>
        /// <param name="splitOption">The split option.</param>
        /// <returns></returns>
        public static String[] SplitSimple(this String me, String seperator, StringSplitOptions splitOption = StringSplitOptions.RemoveEmptyEntries)
        {
            if (me == null) return null;
            return me.Split(new String[] { seperator }, splitOption);
        }

        /// <summary>
        /// Gets element at key, or null/default value.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public static TValue GetAt<TKey, TValue>(this Dictionary<TKey, TValue> me, TKey key)
        {
            if (me.ContainsKey(key))
                return me[key];
            return default(TValue);
        }

        public static TValue GetAt<TKey, TValue>(this Dictionary<TKey, TValue> me, TKey key, TValue defaultVal)
        {
            if (me.ContainsKey(key))
                return me[key];
            return defaultVal;
        }

        /// <summary>
        /// Adds all elements in a dictionary to another one.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="other">The dict to add</param>
        /// <returns></returns>
        public static Dictionary<TKey, TValue> AddAll<TKey, TValue>(this Dictionary<TKey, TValue> me, Dictionary<TKey, TValue> other)
        {
            foreach (var item in other)
            {
                me.Add(item.Key, item.Value);
            }
            return me;
        }

        public static IEnumerable<T> Each<T>(this IEnumerable<T> items, Func<T, T> action)
        {
            foreach (var item in items)
                yield return action(item);
        }

        public static IEnumerable<T> Each<T>(this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items)
                action(item);
            return items;
        }

        /// <summary>
        /// Prefixes a string.
        /// </summary>
        /// <param name="prefix">The prefix.</param>
        /// <param name="length">The length.</param>
        /// <returns></returns>
        public static string PrefixString(this String me, string prefix, int length)
        {
            while (me.Length < length)
            {
                me = prefix + me;
            }
            return me;
        }

        public static string Safe(this String me)
        {
            return Regex.Replace(me, "[^A-Za-z0-9_]", "");
        }

        public static string FirstLetterUpperCase(this string me)
        {
            return me?.ElementAt(0).ToString().ToUpperInvariant();
        }

        public static StringBuilder AppendUl(this StringBuilder me, IEnumerable<String> listItems, String ulClass = null, String liClass = null)
        {
            HtmlGenerator.CreateUl(listItems, ref me, ulClass, liClass);
            return me;
        }

        public static StringBuilder AppendHtmlTag(this StringBuilder me, String tag, String content, String tagClass = null, String tagID = null)
        {
            me.AppendLine(HtmlGenerator.CreateTag(tag, content, tagClass, true, tagID)); 
            return me;
        }

        /// <summary>
        /// Encodes the string as a JavaScript string.
        /// </summary>
        /// <param name="me">The string</param>
        /// <returns>JavaScript string</returns>
        public static string JSEncode(this string me)
        {
            return HttpUtility.JavaScriptStringEncode(me);
        }

    }
}