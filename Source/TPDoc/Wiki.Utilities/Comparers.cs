using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Wiki.Utilities
{
    public class SortHtmlNodeByInnerText : IComparer<HtmlNode>
    {
        public int Compare(HtmlNode a, HtmlNode b)
        {
            if (a == b) return 0;
            else
                return a.FirstChild.InnerText.CompareTo(b.FirstChild.InnerText);
        }
    }

    public class SortListKeyvaluePair : IComparer<KeyValuePair<string, string>>
    {
        public int Compare(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
        {
            if (a.Key == b.Key) return 0;
            else
                return a.Key.CompareTo(b.Key);
        }
    }
}