using AzureStorage;
using AzureStorage.Table.Models;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models;

namespace SearchIndexer
{
    /// <summary>
    /// Disclaimer: Neither space nor speed optimized.
    /// </summary>
    public class Search
    {
        public Parser parser;
        public String DocumentRaw { get; set; } 
        public Dictionary<string, Posting> DocumentParsed;
        public static CloudTable searchIndex;
        private String key;
        public AzureBlobStorage azureStorage;
        public TableBatchOperation BatchUpsert;
        public TableBatchOperation BatchRetrieve;


        public Search(AzureBlobStorage azureStorage)
        {
            parser = new Parser();
            this.azureStorage = azureStorage;

            // Create the table client.            
            CloudTableClient tableClient = azureStorage.StorageAccount.CreateCloudTableClient();

            string tbl = ConfigurationManager.AppSettings["PrimaryAzureStorageTableIndex"];
            if(String.IsNullOrWhiteSpace(tbl))
                tbl = WikiConfiguration.GetConfiguration().PrimaryAzureStorageTableIndex;

            // Create the table if it doesn't exist.
            searchIndex = tableClient.GetTableReference(tbl);
            searchIndex.CreateIfNotExists();

            BatchUpsert = new TableBatchOperation();
            BatchRetrieve = new TableBatchOperation();
        }

        public static bool RecreateTable(AzureBlobStorage azureStorage)
        {
            string tbl = ConfigurationManager.AppSettings["PrimaryAzureStorageTableIndex"];
            if (String.IsNullOrWhiteSpace(tbl))
                tbl = WikiConfiguration.GetConfiguration().PrimaryAzureStorageTableIndex;

            Console.WriteLine("Recreating table: " + tbl);
            // Create the table client.            
            CloudTableClient tableClient = azureStorage.StorageAccount.CreateCloudTableClient();

            // Create the table if it doesn't exist.
            CloudTable table = tableClient.GetTableReference(tbl);

            table.DeleteIfExists();
            return table.SafeCreateIfNotExists(200.0);
        }

        public void Flush()
        {
            if (BatchUpsert.Count > 0)
            { 
                IList<TableResult> res = searchIndex.ExecuteBatch(BatchUpsert);
                BatchUpsert.Clear();
            }
        }

        public void Clear()
        {
            if(DocumentParsed != null)
                DocumentParsed.Clear();
            parser.Clear();
            BatchUpsert.Clear();
            DocumentRaw = null;
        }

        public void LoadDocument(String blobKey)
        {
            Clear();
            key = blobKey;
            DocumentRaw = azureStorage.GetBlobContentAsString(blobKey);
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
            foreach (var word in DocumentParsed)
            {
                String newPosting = key + word.Value.ToString();
                if (newPosting.Length < 30000)
                {
                    String rowContent = TableGetRowContent(word.Key);
                    if (rowContent == null || (rowContent.Length + newPosting.Length) < 30000)
                    {
                        rowContent = (rowContent == null) ? newPosting : UpdatePosting(rowContent, newPosting, key);
                        TableUpsertPosting(word.Key, rowContent);
                    }
                }
            }
            Flush();
        }

        //private List<SearchIndexModel> RetrieveAllRows()
        //{
        //    List<SearchIndexModel> rows = new List<SearchIndexModel>();

        //    foreach (var word in DocumentParsed)
        //    {
        //        BatchRetrieve.Add(TableOperation.Retrieve(TABLE_STORE_PARTITION_ID, word.Key));

        //        if (BatchRetrieve.Count % 90 == 0)
        //        {
        //            IList<TableResult> list = searchIndex.ExecuteBatch(BatchRetrieve);
        //            foreach (var item in list)
        //            {
        //                rows.Add((SearchIndexModel)item.Result);
        //            }
        //            BatchRetrieve.Clear();
        //        }
        //    }

        //    if(BatchRetrieve.Count > 0)
        //    {
        //        IList<TableResult> list = searchIndex.ExecuteBatch(BatchRetrieve);
        //        foreach (var item in list)
        //        {
        //            rows.Add((SearchIndexModel)item.Result);
        //        }
        //        BatchRetrieve.Clear();
        //    }
        //    return rows;
        //}
        
        public void AddDocumentToIndex(String blobKey)
        {
            LoadDocument(blobKey);
            UploadIndex();
        }

        public void RemoveDocumentFromIndex(String blobKey)
        {
            LoadDocument(blobKey);
            foreach (var word in DocumentParsed)
            {
                String postingToBeRemoved = key + word.Value.ToString();
                String rowContent = TableGetRowContent(word.Key);
                
                if(rowContent != null)
                {
                    RemovePosting(word.Key, rowContent, postingToBeRemoved);
                }
            }
            Flush();
        }

        private void RemovePosting(string wordKey, string rowContent, string postingToBeRemoved)
        {
            rowContent = rowContent.Replace(postingToBeRemoved, "");
            TableUpsertPosting(wordKey, rowContent);
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

        private string TableGetRowContent(string key)
        {
            SearchIndexModel retrievedResult = TableGetRowModel(key);

            return (retrievedResult == null) ? null : retrievedResult.matches;
        }

        private SearchIndexModel TableGetRowModel(string key)
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<SearchIndexModel>(AzureBlobStorage.TABLE_STORE_INDEX_PARTITION_ID, key);
            return (searchIndex.Execute(retrieveOperation)).Result as SearchIndexModel;
        }

        private void TableUpsertPosting(string key, string rowContent)
        {
            SearchIndexModel entityToUpdateOrInsert = new SearchIndexModel()
                { 
                    matches = rowContent, 
                    RowKey = key
                };

            BatchUpsert.Add(TableOperation.InsertOrReplace(entityToUpdateOrInsert));

            if (BatchUpsert.Count >= 90)
            {
                searchIndex.ExecuteBatch(BatchUpsert);
                BatchUpsert.Clear();
            }
        }

        public List<KeyValuePair<string, Result>> DoSearch(string phrase, bool useStemming = false)
        {
            Dictionary<String, Result> results = new Dictionary<string, Result>();

            Parser p = new Parser();
            p.ParsePhrase(phrase, 0, 0);
            List<String> words = p.Words.Keys.ToList();

            if (words.Count == 0)
                return results.ToList<KeyValuePair<string, Result>>();

            foreach (String w in words)
            {
                String posting = TableGetRowContent(w);

                if (posting == null)
                    continue;

                ParseRowContent(posting, results, w, words.Count);
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
                    String[] positions = document.Replace(doc + "[", "").Split(new string[]{","}, StringSplitOptions.RemoveEmptyEntries);

                    List<Tuple<int, int, int>> locations = new List<Tuple<int, int, int>>();

                    if(results.ContainsKey(doc))
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
