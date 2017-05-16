using AzureStorage;
using AzureStorage.Table.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchIndexer.v2
{
    /// <summary>
    /// Disclaimer: Neither space nor speed optimized.
    /// </summary>
    public class Search
    {
        public Parser parser;
        public String DocumentRaw { get; set; }
        public Dictionary<string, Posting> DocumentParsed;
        private String key;

        public String IndexTableName { get; set; }

        public StorageAzureTable IndexStorage { get; set; }
        public AzureBlobStorage DocumentsStorage { get; set; }

        public Search(StorageAzureTable indexStorage, AzureBlobStorage documentStorage, string indexTableName)
        {
            parser = new Parser();
            this.IndexStorage = indexStorage;
            this.DocumentsStorage = documentStorage;
            this.IndexTableName = indexTableName;
        }

        public void Clear()
        {
            if (DocumentParsed != null)
                DocumentParsed.Clear();
            parser.Clear();
            DocumentRaw = null;
        }

        public void LoadDocument(String blobKey)
        {
            Clear();
            key = blobKey;
            DocumentRaw = DocumentsStorage.GetBlobContentAsString(blobKey);
            if (DocumentRaw == null)
                throw new ArgumentException("No blob with the name: \"" + blobKey + "\" found.");
            DocumentParsed = IndexDocument(DocumentRaw);
        }

        public void LoadDocument(String blobKey, String content)
        {
            Clear();
            key = blobKey;
            DocumentRaw = content;
            if (DocumentRaw == null)
                throw new ArgumentException("No blob with the name: \"" + blobKey + "\" found.");
            DocumentParsed = IndexDocument(DocumentRaw);
        }

        public void LoadDocument(FileInfo path)
        {
            Clear();
            key = path.Name;
            DocumentRaw = File.ReadAllText(path.FullName);
            DocumentParsed = IndexDocument(DocumentRaw);
        }

        private Dictionary<string, Posting> IndexDocument(String htmlDocument)
        {
            return parser.ParseHtml(htmlDocument);
        }

        public void UploadIndex()
        {
            List<SearchIndexModel> newRows = new List<SearchIndexModel>();
            //SearchIndexModel[] rows = IndexStorage.RetriveBatch<SearchIndexModel>(IndexTableName, DocumentParsed.Keys.ToList());
            SearchIndexModel[] rows = IndexStorage.RetriveSearch(IndexTableName, DocumentParsed.Keys.ToList());

            if(rows.Length != DocumentParsed.Keys.Count)
            {
                ;
            }

            foreach (SearchIndexModel row in rows)
            {
                if (row != null)
                {
                    Posting p = DocumentParsed[row.RowKey];
                    String newPosting = key + p.ToString();
                    row.matches = UpdatePosting(row.matches, newPosting, key);
                    newRows.Add(row);
                    DocumentParsed.Remove(row.RowKey);
                }
            }

            foreach (var word in DocumentParsed)
            {
                String newPosting = key + word.Value.ToString();
                newRows.Add(new SearchIndexModel() { RowKey = word.Key, matches = newPosting, PartitionKey = IndexTableName });
            }
            IndexStorage.Upsert(IndexTableName, newRows);
        }

        public void AddDocumentToIndex(String blobKey)
        {
            LoadDocument(blobKey);
            UploadIndex();
        }

        public void RemoveDocumentFromIndex(String blobKey)
        {
            LoadDocument(blobKey);
            //SearchIndexModel[] rows = IndexStorage.RetriveBatch<SearchIndexModel>(IndexTableName, DocumentParsed.Keys.ToList());
            SearchIndexModel[] rows = IndexStorage.RetriveSearch(IndexTableName, DocumentParsed.Keys.ToList());


            foreach (SearchIndexModel row in rows)
            {
                if (row != null)
                {
                    try
                    {
                        String postingToBeRemoved = key + DocumentParsed[row.RowKey].ToString();
                        row.matches = row.matches.Replace(postingToBeRemoved, "");
                    }
                    catch{}
                }
            }
            IndexStorage.Upsert(IndexTableName, rows.Where(x => x != null).ToList());
        }

        public static string UpdatePosting(string rowContent, string newPosting, string key)
        {
            int oldPostingBegin = rowContent.IndexOf(key + "[");
            if (oldPostingBegin < 0)
                return rowContent + newPosting;

            int oldPostingEnd = rowContent.IndexOf("]", oldPostingBegin) + 1;
            String newRowContent = rowContent.Substring(0, oldPostingBegin);
            newRowContent += newPosting + rowContent.Substring(oldPostingEnd);

            return newRowContent;
        }

        public List<KeyValuePair<string, Result>> DoSearch(string phrase, bool useStemming = false)
        {
            Dictionary<String, Result> results = new Dictionary<string, Result>();

            Parser p = new Parser();
            p.ParsePhrase(phrase, 0, 0);
            List<String> words = p.Words.Keys.ToList();

            if (words.Count == 0)
                return results.ToList<KeyValuePair<string, Result>>();

            //SearchIndexModel[] rows = IndexStorage.RetriveBatch<SearchIndexModel>(IndexTableName, words);
            SearchIndexModel[] rows = IndexStorage.RetriveSearch(IndexTableName, words);


            foreach (SearchIndexModel row in rows)
            {
                if(row != null)
                    ParseRowContent(row.matches, results, row.RowKey, words.Count);
            }

            List<KeyValuePair<string, Result>> res = results.ToList<KeyValuePair<string, Result>>();
            res.Sort(new SortBySearchHitComparer(true));
            return res;
        }

        // docA[3,4,5,99,2,3]docB[23,4,5]
        private static void ParseRowContent(string posting, Dictionary<String, Result> results, string word, int words)
        {
            string[] documents = posting.Split(new string[] { "]" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string document in documents)
            {
                if (document.Length > 0)
                {
                    String doc = document.Substring(0, document.IndexOf('['));
                    String[] positions = document.Replace(doc + "[", "").Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    List<Tuple<int, int, int>> locations = new List<Tuple<int, int, int>>();

                    if (results.ContainsKey(doc))
                    {
                        for (int i = 0; i < positions.Length; i++)
                        {
                            locations.Add(new Tuple<int, int, int>(Convert.ToInt32(positions[i++]), Convert.ToInt32(positions[i++]), Convert.ToInt32(positions[i])));
                        }
                        results[doc].wordHits.Add(word, locations);
                    }
                    else
                    {
                        Result r = new Result()
                        {
                            Doc = doc,
                            wordHits = new Dictionary<string, List<Tuple<int, int, int>>>()
                        };

                        for (int i = 0; i < positions.Length; i++)
                        {
                            locations.Add(new Tuple<int, int, int>(Convert.ToInt32(positions[i++]), Convert.ToInt32(positions[i++]), Convert.ToInt32(positions[i])));
                        }
                        results.Add(doc, r);
                        results[doc].wordHits.Add(word, locations);

                    }
                }
            }
        }

    }

    //public class SearchIndexModel
    //{
    //    public string RowKey { get; set; }
    //    public string matches { get; set; }
    //}

    public class Result
    {
        public String Doc { get; set; }
        public Dictionary<String, List<Tuple<int, int, int>>> wordHits { get; set; }
    }

    public class SortBySearchHitComparer : IComparer<KeyValuePair<String, Result>>
    {
        private bool descending;

        public SortBySearchHitComparer(bool descending = false)
        {
            this.descending = descending;
        }

        public int Compare(KeyValuePair<String, Result> a, KeyValuePair<String, Result> b)
        {
            int aWords = (a.Value.wordHits == null) ? 0 : a.Value.wordHits.Count;
            int bWords = (a.Value.wordHits == null) ? 0 : b.Value.wordHits.Count;

            if (aWords != bWords)
            {
                if (descending)
                    return bWords.CompareTo(aWords);
                else
                    return aWords.CompareTo(bWords);
            }

            aWords = 0;
            bWords = 0;
            foreach (var item in a.Value.wordHits)
            {
                aWords += item.Value.Count;
            }

            foreach (var item in b.Value.wordHits)
            {
                bWords += item.Value.Count;
            }

            if (descending)
                return bWords.CompareTo(aWords);
            else
                return aWords.CompareTo(bWords);
        }
    }
}
