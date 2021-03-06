﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Oracle.ManagedDataAccess.Client;

namespace OptimaJet.Workflow.Oracle
{
    public class ColumnInfo
    {
        public string Name;
        public OracleDbType Type = OracleDbType.NVarchar2;
        public bool IsKey = false;
        public int Size = 256;
    }
    public class DbObject<T> where T : DbObject<T>, new()
    {
        public DbObject()
        {

        }

        public string db_TableName;
        public List<ColumnInfo> db_Columns = new List<ColumnInfo>();

        public virtual object GetValue(string key)
        {
            return null;
        }

        public virtual void SetValue(string key, object value)
        {
            
        }
      
        #region Command Insert/Update/Delete/Commit
        public virtual int Insert(OracleConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = string.Format("INSERT INTO {0} ({1}) VALUES ({2})",
                    db_TableName, 
                    String.Join(",", db_Columns.Select(c=>c.Name.ToUpper())),
                    String.Join(",", db_Columns.Select(c=> ":" + c.Name)));

                command.Parameters.AddRange(
                    db_Columns.Select(c => new OracleParameter(c.Name, c.Type, GetValue(c.Name), ParameterDirection.Input)).ToArray());
                command.BindByName = true;
                command.CommandType = CommandType.Text;
                int cnt = command.ExecuteNonQuery();
                return cnt;
            }
        }

        public int Update(OracleConnection connection)
        {
            string command = string.Format(@"UPDATE {0} SET {1} WHERE {2}",
                    db_TableName,
                    String.Join(",", db_Columns.Where(c => !c.IsKey).Select(c => c.Name.ToUpper() + " = :" + c.Name)),
                    String.Join(" AND ", db_Columns.Where(c => c.IsKey).Select(c => c.Name.ToUpper() + " = :" + c.Name )));

            var parameters = db_Columns.Select(c =>
                new OracleParameter(c.Name, c.Type, GetValue(c.Name), ParameterDirection.Input)).ToArray();

            return ExecuteCommand(connection, command, parameters);
            
        }

        public static T SelectByKey(OracleConnection connection, object id)
        {
            var t = new T();

            var key = t.db_Columns.Where(c => c.IsKey).FirstOrDefault();
            if(key == null)
            {
                throw new Exception(string.Format("Key for table {0} isn't defined.", t.db_TableName));
            }

            string selectText = string.Format("SELECT * FROM {0} WHERE {1} = :p_id", t.db_TableName, key.Name.ToUpper());
            var parameters = new OracleParameter[]{
                new OracleParameter("p_id", key.Type, ConvertToDBCompatibilityType(id), ParameterDirection.Input)};

            return Select(connection, selectText, parameters).FirstOrDefault();
        }

        public static int Delete(OracleConnection connection, object id)
        {
            var t = new T();
            var key = t.db_Columns.Where(c => c.IsKey).FirstOrDefault();
            if (key == null)
                throw new Exception(string.Format("Key for table {0} isn't defined.", t.db_TableName));

            return ExecuteCommand(connection,
                string.Format("DELETE FROM {0} WHERE {1} = :p_id", t.db_TableName, key.Name.ToUpper()),
                new OracleParameter[]{
                    new OracleParameter("p_id", key.Type, ConvertToDBCompatibilityType(id), ParameterDirection.Input)});
        }

        public static int ExecuteCommand(OracleConnection connection, string commandText, params OracleParameter[] parameters)
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                command.CommandType = CommandType.Text;
                command.BindByName = true;
                command.Parameters.AddRange(parameters);
                var cnt = command.ExecuteNonQuery();
                return cnt;
            }
        }

        public static T[] Select(OracleConnection connection, string commandText, params OracleParameter[] parameters)
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                command.CommandType = CommandType.Text;
                command.BindByName = true;
                command.Parameters.AddRange(parameters);

                DataTable dt = new DataTable();
                using (var oda = new OracleDataAdapter(command))
                {
                    oda.Fill(dt);
                }

                var res = new List<T>();

                foreach (DataRow row in dt.Rows)
                {
                    T item = new T();
                    foreach (var c in item.db_Columns)
                        item.SetValue(c.Name, row[c.Name.ToUpper()]);
                    res.Add(item);
                }

                return res.ToArray();
            }
        }

        public static void Commit(OracleConnection connection)
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = string.Format("COMMIT");
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
            }
        }
        #endregion

        public static object ConvertToDBCompatibilityType(object obj)
        {
            if (obj is Guid)
                return ((Guid)obj).ToByteArray();
            return obj;
        }
    }
}
