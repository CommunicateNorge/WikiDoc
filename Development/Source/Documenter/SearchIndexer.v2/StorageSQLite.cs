using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SearchIndexer.v2
{
    public class StorageSQLite : IStorage
    {
        public SQLiteDb Db { get; set; }
        private int batchSize = MaxBatchSize;
        public int BatchSize 
        {
            get { return batchSize; }
            set 
            {
                if (value > MaxBatchSize)
                    batchSize = MaxBatchSize;
                else if (value < 1)
                    batchSize = 1;
                else
                    batchSize = value;
            }
        }
        private const int MaxBatchSize = 300;

        public StorageSQLite(String dbPath)
        {
            Db = new SQLiteDb(@"C:\tfs\MartinWL\REMA1000Online\REMA1000\Documentation\Source\Documenter\SearchIndexer.v2\bin\Debug\tpdocindex.sqlite");
        }

        public string GetContentAsString(string groupingEntityName, string blobKey)
        {
            throw new NotImplementedException();
        }


        public T Retrive<T>(string groupingEntityName, string key, string keyName = null)
        {
            T instance = (T)Activator.CreateInstance(typeof(T));
            PropertyInfo[] props = typeof(T).GetProperties();

            keyName = keyName ?? props.First(z => z.Name.Contains("Key")).Name;

            DataColumnCollection columns = null;
            DataRow result = GetValue(groupingEntityName, key, keyName, out columns);

            foreach (var prop in typeof(T).GetProperties())
            {
                MapValue<T>(instance, result, columns, prop);
            }
            return instance;
        }

        public T[] RetriveBatch<T>(string groupingEntityName, IEnumerable<string> keys, string keyName = null)
        {
            if(keys.Count() > BatchSize)
            {
                List<T> res = new List<T>();
                int count = keys.Count();
                int processed = 0;
                while(processed < count)
                {
                    T[] tres = RetriveBatch<T>(groupingEntityName, keys.Skip(processed).Take(Math.Min(BatchSize, count-processed)), keyName);
                    res.AddRange(tres);
                    processed += BatchSize;
                }
                return res.ToArray();
            }


            T[] values = new T[keys.Count()];

            if (keys.Count() == 0)
                return values;

            PropertyInfo[] props = typeof(T).GetProperties();

            keyName = keyName ?? props.First(z => z.Name.Contains("Key")).Name;

            DataColumnCollection columns = null;
            DataRowCollection results = GetValues(groupingEntityName, keys, keyName, out columns);

            int i = 0;
            foreach (DataRow row in results)
            {
                T instance = (T)Activator.CreateInstance(typeof(T));
                foreach (var prop in typeof(T).GetProperties())
                {
                    MapValue<T>(instance, row, columns, prop);
                }
                values[i++] = instance;
            }
            return values;
        }

        private void MapValue<T>(T instance, DataRow result, DataColumnCollection columns, PropertyInfo prop)
        {
            if (columns.IndexOf(prop.Name) >= 0)
            {
                try
                {
                    prop.SetValue(instance, result[prop.Name]);
                }
                catch (ArgumentException ex)
                {
                    if (ex.Message.IndexOf("Int32") >= 0)
                    {
                        prop.SetValue(instance, Convert.ToInt32(result[prop.Name]));
                    }
                    else if (ex.Message.IndexOf("System.Single") >= 0)
                    {
                        prop.SetValue(instance, Convert.ToSingle(result[prop.Name]));
                    }
                }
            }
        }

        private DataRowCollection GetValues(string groupingEntityName, IEnumerable<string> keyValues, string keyName, out DataColumnCollection columns)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("SELECT * FROM ").Append(groupingEntityName).Append(" WHERE ");
            int count = 0;
            List<SQLiteParameter> sparams = new List<SQLiteParameter>();

            foreach (String str in keyValues)
            {
                sparams.Add(new SQLiteParameter("@GetParam" + count, str));
                sb.Append(keyName).Append(" = ").Append("@GetParam" + count);
                if (++count < keyValues.Count())
                    sb.Append(" OR ");
            }
            DataTable table = Db.GetDataTable(sb.ToString(), sparams);
            columns = table.Columns;
            return table.Rows;
        }

        private DataRow GetValue(string groupingEntityName, string keyValue, string keyName, out DataColumnCollection columns)
        {
            List<SQLiteParameter> sparams = new List<SQLiteParameter>();
            sparams.Add(new SQLiteParameter("@GetParam", keyValue));
            sparams.Add(new SQLiteParameter("@GetParamName", keyName));
            DataTable table = Db.GetDataTable(String.Format("select * from {0} where @GetParamName = @GetParam", groupingEntityName), sparams);
            columns = table.Columns;

            if (table.Rows.Count == 0)
                return null;
            return table.Rows[0];
        }

        public bool Upsert<T>(string groupingEntityName, IEnumerable<T> entitiesToUpdateOrInsert)
        {
            if (entitiesToUpdateOrInsert.Count() == 0)
                return true;

            if(entitiesToUpdateOrInsert.Count() > BatchSize)
            {
                int count = entitiesToUpdateOrInsert.Count();
                int processed = 0;
                while(processed < count)
                { 
                    Upsert<T>(groupingEntityName, entitiesToUpdateOrInsert.Skip(processed).Take(Math.Min(BatchSize, count - processed)));
                    processed += BatchSize;
                }
                return true;
            }

            PropertyInfo[] props = typeof(T).GetProperties();
            List<String> values = new List<string>();
            List<SQLiteParameter> sparams = new List<SQLiteParameter>();
            String[] columns = new String[props.Length];

            for (int i = 0; i < props.Length; i++)
            {
                columns[i] = props[i].Name;
            }

            int x = 0;
            foreach (var item in entitiesToUpdateOrInsert)
            {
                x++;
                String[] vals = new string[props.Length];
                for (int i = 0; i < props.Length; i++)
                {
                    object o = props[i].GetValue(item);
                    sparams.Add(new SQLiteParameter("@" + x + "InsertParam" + i, o));
                    vals[i] = "@" + x + "InsertParam" + i;
                }
                values.Add("(" + String.Join(",", vals) + ")");
            }
            return UpsertValues(groupingEntityName, values, columns, sparams) > 0;
        }

        private int UpsertValues(string groupingEntityName, List<string> values, string[] columns, List<SQLiteParameter> sparams)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("insert or replace into ").Append(groupingEntityName).Append(" (").Append(String.Join(",", columns)).Append(")");
            sb.Append(" VALUES ");
            sb.Append(String.Join(",", values));
            return Db.ExecuteNonQuery(sparams, sb.ToString());
        }
    }
}
