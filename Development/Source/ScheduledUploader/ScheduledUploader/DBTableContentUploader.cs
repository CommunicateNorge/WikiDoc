using AzureStorage;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using ScheduledUploader;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wiki.Utilities;
using static ScheduledUploader.DynamicallyNamedDynamicObject;

namespace ScheduledUploader
{
    class DBTableContentUploader
    {
        //private const string columns = "Id,Text,Title,Description,ContractTextElementTypeId,ContractTextElementOrdering";
        //string query = ConfigurationManager.AppSettings["MySetting"]; //$"SELECT {columns} FROM [MDM_CodeAndRule].[dbo].[ContractTextElement]";
        //string connString = "data source=Z52OS2CS7CN1\\A135151;initial catalog=MDM_CodeAndRule;integrated security=True;MultipleActiveResultSets=True;App=EntityFramework;";
        //string connString = "data source=REITANCSDEV001;initial catalog=MDM_CodeAndRule;integrated security=True;MultipleActiveResultSets=True;App=EntityFramework;";


        public List<MyDynObject> GetTableContentsFromDb(string[] columnHeaders, string query, string connString)
        {
            DataSet ds = new DataSet();
            SqlParameter[] parameters = new SqlParameter[] { };
            List<MyDynObject> rows = new List<MyDynObject>();

            using (SqlConnection conn = new SqlConnection(connString))
            {
                SqlCommand cmd = new SqlCommand(query, conn);
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                { 
                    while (reader.Read())
                    {
                        Dictionary<string, object> dict = new Dictionary<string, object>();

                        foreach (var item in columnHeaders)
                        {
                            string trimmed = item.Trim();
                            Type type = reader[trimmed].GetType();
                            if (type.Name == "DBNull")
                                dict.Add(trimmed, "null");
                            else
                            {
                                Object o = reader[trimmed];
                                dict.Add(trimmed, o.ToString());
                            }
                        }

                        rows.Add(DynamicallyNamedDynamicObject.GetDynamicObject(dict));
                    }
                }
                return rows;
            }
        }

        public string GetTableContentsAsHtml(string columns, string query, string connString)
        {
            string[] columnHeaders = columns.SplitSimple(",");
            List<MyDynObject> elems = GetTableContentsFromDb(columnHeaders, query, connString);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(HtmlGenerator.StartTag("table"));

            sb.AppendLine(HtmlGenerator.StartTag("thead"));
            foreach (var item in columnHeaders)
            {
                sb.AppendLine(HtmlGenerator.StartTag("th"));
                sb.AppendLine(item);
                sb.AppendLine(HtmlGenerator.CloseTag("th"));
            }
            sb.AppendLine(HtmlGenerator.CloseTag("thead"));

            foreach (var item in elems)
            {
                sb.AppendLine(HtmlGenerator.StartTag("tr"));

                foreach (var column in columnHeaders)
                {
                    var propertyInfo = item.GetType().GetProperty(column);

                    sb.AppendLine(HtmlGenerator.StartTag("td"));
                    var value = item.GetDynamicMember(item, column.Trim());
                    sb.AppendLine(value.ToString());
                    sb.AppendLine(HtmlGenerator.CloseTag("td"));
                }
                sb.AppendLine(HtmlGenerator.CloseTag("tr"));
            }

            
            sb.AppendLine(HtmlGenerator.CloseTag("table"));
            return sb.ToString();
        }

        public void StartDocumentingAllTables(AzureBlobStorage azureStorage, StringBuilder log)
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Queries.json");
            string[] file = File.ReadAllLines(path);
            string json = String.Join("", file);

            log.AppendLine("Getting setting from file: " + path);

            JsonConfigFile config = JsonConvert.DeserializeObject<JsonConfigFile>(json);

            foreach (var item in config.Queries)
            {
                log.AppendLine("Getting data and uploading table " + item.BlobName);

                string reg = @"select(.*)FROM";
                Match match = Regex.Match(item.Query, reg, RegexOptions.IgnoreCase);

                if(match.Success)
                {
                    string columns = match.Groups[1].Value.Replace("[", "").Replace("]", "").Trim();
                    string fileContent = GetTableContentsAsHtml(columns, item.Query, config.ConnectionStrings.FirstOrDefault(x => x.Name == item.ConnectionName).Connection);
                    azureStorage.SetBlobContentAsString(WikiBlob.Combine("Custom", "Table", item.BlobName), fileContent);
                }

                log.AppendLine("Uploading finished for " + item.BlobName);
            }
        }
    }



    public class Queries
    {
        public string Query { get; set; }
        public string ConnectionName { get; set; }
        public string BlobName { get; set; }    
    }

    public class ConnectionStrings
    {
        public string Name { get; set; }
        public string Connection { get; set; }
    }

    public class JsonConfigFile
    {
        public List<Queries> Queries { get; set; }
        public List<ConnectionStrings> ConnectionStrings { get; set; }
    }
}
