using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Documenter
{
    public static class CustomExtensions
    {
        public static String DotEncode(this String str)
        {
            return str.Replace(".", "€");
        }

        public static String DotDecode(this String str)
        {
            return str.Replace("€", ".");
        }

        /// <summary>
        /// Extracts a part of a string
        /// </summary>
        /// <param name="content">The string</param>
        /// <param name="prefix">The preceding characters</param>
        /// <param name="suffix">The succeding characters</param>
        /// <returns>A substring bewteen a prefix and a suffix</returns>
        public static string ExtractPart(this string content, string prefix, string suffix)
        {
            int i = content.IndexOf(prefix);
            String name = content.Substring(i + prefix.Length);
            i = name.IndexOf(suffix);
            return name.Substring(0, i);
        }

        public static string Surround(this string me, string prefix, string suffix)
        {
            return prefix + me + suffix;
        }

        public static string BlobKeyCombine(this string me, params string[] parts)
        {
            me = me.TrimStart('£').TrimEnd('£');
            foreach (var item in parts)
            {
                me += "£" + item.TrimEnd('£').TrimStart('£');
            }
            return me;
        }

        public static string CreateWikiLink(this string lnk, string title = null, bool dotEncodeLink = false, string className = null)
        {
            return CreateLink("/Wiki/Page/" + lnk, title, dotEncodeLink, className);
        }

        public static string CreateLink(this string lnk, string title = null, bool dotEncodeLink = false, string className = null)
        {
            if (title == null)
                title = lnk;

            if (dotEncodeLink)
                lnk = lnk.DotEncode();

            if (className != null)
                return "<a href=\"" + lnk + "\" class=\"" + className + "\">" + title + "</a>";
            return "<a href=\"" + lnk + "\">" + title + "</a>";
        }

    }
}
