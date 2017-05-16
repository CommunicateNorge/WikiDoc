using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Wiki.Models
{
    public class SearchViewModel
    {
        public string pageKey { get; set; }
        public string position { get; set; }
        public string phrase { get; set; }
        public Dictionary<string, int> occurences { get; set; }
        public string lastModiefied { get; set; }

    }



    public class SortSearchViewModel : IComparer<SearchViewModel>
    {
        private bool descending;

        public SortSearchViewModel(bool descending = false)
        {
            this.descending = descending;
        }

        public int Compare(SearchViewModel a, SearchViewModel b)
        {
            int aWords = (a.occurences == null) ? 0 : a.occurences.Count;
            int bWords = (b.occurences == null) ? 0 : b.occurences.Count;

            if (aWords != bWords)
            {
                if (descending)
                    return bWords.CompareTo(aWords);
                else
                    return aWords.CompareTo(bWords);
            }

            aWords = 0;
            bWords = 0;
            foreach (var item in a.occurences)
            {
                aWords += item.Value;
            }

            foreach (var item in b.occurences)
            {
                bWords += item.Value;
            }

            if (descending)
                return bWords.CompareTo(aWords);
            else
                return aWords.CompareTo(bWords);
        }
    }
}