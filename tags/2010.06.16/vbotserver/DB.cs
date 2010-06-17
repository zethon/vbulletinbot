using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SQLite;

namespace vbotserver
{
    public sealed class DB
    {
        private SQLiteConnection _connection = null;
        public SQLiteConnection Connection
        {
            get { return _connection; }
        }

        static readonly DB _instance = new DB();
        public static DB Instance
        {
            get { return _instance; }
        }

        private DB()
        {
            _connection = new SQLiteConnection(@"Data Source=vbot.db;Version=3;New=false;Compress=true;");
            _connection.Open();
        }

        public List<Dictionary<string,string>> QueryRead(string strQuery)
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }

            List<Dictionary<string, string>> retval = new List<Dictionary<string, string>>();
            DataSet ds = new DataSet();
            DataTable dt = new DataTable();
            
            SQLiteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = strQuery;
            SQLiteDataAdapter db = new SQLiteDataAdapter(strQuery, _connection);
            ds.Reset();
            db.Fill(ds);
            dt = ds.Tables[0];

            foreach (DataRow row in dt.Rows)
            {
                Dictionary<string, string> newRow = new Dictionary<string, string>();

                foreach (DataColumn col in dt.Columns)
                {
                    newRow.Add(col.ColumnName, row[col.ColumnName].ToString());
                }

                retval.Add(newRow);
            }

            return retval;
        }

        public Dictionary<string, string> QueryFirst(string strQuery)
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }

            Dictionary<string, string> retval = null;
            List<Dictionary<string, string>> temp = QueryRead(strQuery);

            if (temp.Count > 0)
                retval = temp[0];

            return retval;
        }

        public int QueryWrite(string strQuery, bool bNoThrow)
        {
            int retval = 0;

            try
            {
                SQLiteCommand cmd = _connection.CreateCommand();
                cmd.CommandText = strQuery;

                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }

                retval = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                if (!bNoThrow)
                {
                    throw ex;
                }
            }

            return retval;
        }

        public int QueryWrite(string strQuery)
        {
            return QueryWrite(strQuery,false);
        }

        public int LastInsertID()
        {
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }

            int iRetval = 0;
            SQLiteCommand IDCmd = new SQLiteCommand("SELECT last_insert_rowid()", _connection);
            object o = IDCmd.ExecuteScalar();

            if (o != null)
            {
                int.TryParse(o.ToString(),out iRetval);
            }

            return iRetval;
        }
    }
}
