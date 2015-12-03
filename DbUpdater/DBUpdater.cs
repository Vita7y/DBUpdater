using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace DBUpdate
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class DBUpdater : IDisposable
    {
        private SqlCommand _command;
        private SqlConnection _connection;

        public DBUpdater(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
            _connection.Open();
            _command = _connection.CreateCommand();
        }

        public void Dispose()
        {
            if (_command != null)
            {
                _command.Dispose();
                _command = null;
            }
            if (_connection != null)
            {
                if (_connection.State != ConnectionState.Closed)
                {
                    _connection.Close();
                }
                _connection.Dispose();
                _connection = null;
            }
        }

        public void Execute(string fileName, string fileExtension, string commandText, bool isCre)
        {
            var serverConnection = new ServerConnection(_connection);
            var server = new Server(serverConnection);
            server.ConnectionContext.InfoMessage += ConnectionContext_InfoMessage;
            server.ConnectionContext.LockTimeout = 0;
            server.ConnectionContext.StatementTimeout = 0;
            try
            {
                if (isCre)
                {
                    if (fileExtension.Equals(".CRE", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (CheckTableExists(server.ConnectionContext, fileName))
                            throw new Exception(string.Format("Can't execute CRE-script: table {0} already exists", fileName));
                    }
                    if (fileExtension.Equals(".UPD", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!CheckTableExists(server.ConnectionContext, fileName))
                            throw new Exception(string.Format("Can't execute UPD-script: table {0} not exists", fileName));
                    }
                }
                server.ConnectionContext.BeginTransaction();
                server.ConnectionContext.ExecuteNonQuery(commandText);
                server.ConnectionContext.CommitTransaction();
            }
            catch (Exception)
            {
                server.ConnectionContext.RollBackTransaction();
                throw;
            }
            finally
            {
                server.ConnectionContext.InfoMessage -= ConnectionContext_InfoMessage;
            }
        }

        private void ConnectionContext_InfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            Common.Log(e.Message);
        }

        private bool CheckTableExists(string tableName)
        {
            var serverConnection = new ServerConnection(_connection);
            var server = new Server(serverConnection);
            var command = string.Format("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE [TABLE_CATALOG] = '{0}' AND [TABLE_NAME] = '{1}'",
                server.ConnectionContext.DatabaseName, tableName);
            var count = (int) server.ConnectionContext.ExecuteScalar(command);
            return count > 0;
        }

        private bool CheckTableExists(ServerConnection connectionContext, string tableName)
        {
            var command = string.Format("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE [TABLE_CATALOG] = '{0}' AND [TABLE_NAME] = '{1}'",
                connectionContext.DatabaseName, tableName);
            var count = (int) connectionContext.ExecuteScalar(command);
            return count > 0;
        }

        public List<Exception> ProcessScripts(List<string> files, string message, Dictionary<string, string> macros, bool isCre)
        {
            var exceptions = new List<Exception>();
            if (files != null && files.Count > 0)
            {
                Common.Log("-------------------------------------", Common.ShowDate.None);
                Common.Log(message);
                foreach (var file2 in files)
                {
                    try
                    {
                        var execFileName = file2;
                        var fileName = Path.GetFileNameWithoutExtension(file2);
                        if (fileName.StartsWith("--"))
                        {
                            Common.Log("-------------------------------------", Common.ShowDate.None);
                            Common.Log("Skip processing: " + fileName, Common.ShowDate.None);
                            continue;
                        }
                        var fileExtension = Path.GetExtension(file2);

                        Common.Log("-------------------------------------", Common.ShowDate.None);
                        Common.Log("Processing " + fileName, Common.ShowDate.None);

                        if (isCre)
                        {
                            if (!string.IsNullOrEmpty(fileExtension))
                            {
                                execFileName = execFileName.Remove(execFileName.Length - fileExtension.Length, fileExtension.Length);
                            }
                            var tableExists = CheckTableExists(fileName);
                            if (!tableExists)
                            {
                                execFileName = execFileName + ".cre";
                                fileExtension = ".cre";
                            }
                            else
                            {
                                execFileName = execFileName + ".upd";
                                fileExtension = ".upd";
                            }
                            if (!File.Exists(execFileName))
                            {
                                Common.Log(string.Format("Table {0} {1}exists, but file {2} not found", fileName, (tableExists ? "" : "don't "), execFileName), Common.ShowDate.None);
                                continue;
                            }
                        }

                        Common.Log("Execute " + Path.GetFileName(execFileName));

                        var script = File.ReadAllText(execFileName);
                        foreach (var macro in macros)
                        {
                            script = script.Replace("<" + macro.Key + ">", macro.Value);
                        }

                        Execute(fileName, fileExtension, script, isCre);
                        Common.Log("success");
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        Common.Log(ex);
                    }
                }
            }
            return exceptions;
        }
    }
}