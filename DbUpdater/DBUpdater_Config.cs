using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;

namespace DBUpdate
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal class DBUpdaterConfig
    {
        private DBUpdaterConfig()
        {
            ConfigFilePath = AppDomain.CurrentDomain.BaseDirectory;
            ScriptsPath = AppDomain.CurrentDomain.BaseDirectory;
            Macros = new Dictionary<string, string>();
        }

        public bool IsBatchMode { get; private set; }
        public string SqlServer { get; private set; }
        public string SqlDataBase { get; private set; }
        public string SqlLogin { get; private set; }
        public string SqlPassword { get; private set; }
        public bool SqlTrustedConnection { get; private set; }
        public string ConfigFilePath { get; private set; }
        public string ScriptsPath { get; private set; }

        public string ScriptsPathCRE
        {
            get { return Path.Combine(ScriptsPath, "CRE"); }
        }

        public string ScriptsPathQRY
        {
            get { return Path.Combine(ScriptsPath, "QRY"); }
        }

        public string ConnectionString { get; private set; }
        public Dictionary<string, string> Macros { get; private set; }

        public void Validate()
        {
            if (string.IsNullOrEmpty(ConnectionString))
                throw new ConfigurationErrorsException("DB connection string is not set");

            if (!Directory.Exists(ScriptsPath))
                throw new ConfigurationErrorsException(string.Format("Directory {0} don't exists", ScriptsPath));

            if (string.IsNullOrEmpty(SqlServer))
            {
                throw new ConfigurationErrorsException("SQL server is not set");
            }
            if (string.IsNullOrEmpty(SqlDataBase))
            {
                throw new ConfigurationErrorsException("Database is not set");
            }
            if (!SqlTrustedConnection &&
                (string.IsNullOrEmpty(SqlLogin) || string.IsNullOrEmpty(SqlLogin)))
            {
                throw new ConfigurationErrorsException(
                    "Then SQL-authentication mode is not trusted, used login and password should be set");
            }

            //if (!Directory.Exists(this.ScriptsPathCRE) && !Directory.Exists(this.ScriptsPathQRY))
            //    throw new ConfigurationErrorsException(string.Format("No CRE/QRY directories found in {0}", this.ScriptsPath));

            //if (!File.Exists(Path.Combine(this.ScriptsPathCRE, "install.lst")))
            //    throw new ConfigurationErrorsException(string.Format("File {0} don't exists in {1}", "install.lst", this.ScriptsPathCRE));
        }

        public static DBUpdaterConfig GetConfig(string configFileName, string[] args)
        {
            var config = new DBUpdaterConfig();

            //bool hasServerArg = false;
            //bool hasDataBaseArg = false;
            //bool hasAuthArg = false;
            //bool hasLoginArg = false;
            //bool hasPasswordArg = false;

            if (args != null && args.Length > 0)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (IsArgument(args[i], "C") && i < args.Length - 1)
                    {
                        var newPath = args[i + 1];
                        if (!string.IsNullOrEmpty(newPath))
                        {
                            newPath = newPath.Trim('\"', '\'', '\\');
                            if (!File.Exists(newPath))
                                throw new Exception(string.Format("Configuration file {0} don't exists.", newPath));

                            config.ConfigFilePath = Path.GetDirectoryName(newPath);
                            configFileName = Path.GetFileName(newPath);
                        }
                        else
                            throw new ConfigurationErrorsException(
                                "If /C argument is passed, path to configuration file should be set after it");
                    }
                    if (IsArgument(args[i], "B"))
                    {
                        config.IsBatchMode = true;
                    }
                }
            }
            if (File.Exists(Path.Combine(config.ConfigFilePath, configFileName)))
            {
                var document = new XmlDocument();
                document.Load(Path.Combine(config.ConfigFilePath, configFileName));

                var nodesMacro = document.GetElementsByTagName("macro");
                if (nodesMacro.Count > 0)
                {
                    foreach (XmlNode nodeMacro in nodesMacro)
                    {
                        if (nodeMacro.Attributes != null) config.Macros.Add(nodeMacro.Attributes["name"].Value, nodeMacro.Attributes["value"].Value);
                    }
                }

                var nodesConnectionStrings = document.GetElementsByTagName("connectionString");
                if (nodesConnectionStrings.Count == 1)
                {
                    var xmlAttributeCollection = nodesConnectionStrings[0].Attributes;
                    if (xmlAttributeCollection != null) config.ConnectionString = xmlAttributeCollection["value"].Value;
                }

                var nodesScriptsPath = document.GetElementsByTagName("scriptsPath");
                if (nodesScriptsPath != null && nodesScriptsPath.Count == 1)
                {
                    var xmlAttributeCollection = nodesScriptsPath[0].Attributes;
                    if (xmlAttributeCollection != null) config.ScriptsPath = xmlAttributeCollection["value"].Value;
                }
            }
            else
            {
                Common.Log(string.Format("Configuration file {0} don't exists.",
                    Path.Combine(config.ConfigFilePath, configFileName)));
                if (args != null && (config.IsBatchMode && args.Length == 1))
                {
                    throw new Exception("Can't process in Batch Mode without configuration file and arguments");
                }
            }

            var hasAuthType = false;
            var csb = !string.IsNullOrEmpty(config.ConnectionString)
                ? new SqlConnectionStringBuilder(config.ConnectionString)
                : new SqlConnectionStringBuilder();
            if (csb.ConnectionString.IndexOf("Data Source", StringComparison.InvariantCultureIgnoreCase) != -1 &&
                !string.IsNullOrEmpty(csb.DataSource))
            {
                config.SqlServer = csb.DataSource;
            }
            if (csb.ConnectionString.IndexOf("Initial Catalog", StringComparison.InvariantCultureIgnoreCase) != -1 &&
                !string.IsNullOrEmpty(csb.InitialCatalog))
            {
                config.SqlDataBase = csb.InitialCatalog;
            }
            if (csb.ConnectionString.IndexOf("User ID", StringComparison.InvariantCultureIgnoreCase) != -1 &&
                !string.IsNullOrEmpty(csb.UserID))
            {
                config.SqlLogin = csb.UserID;
            }
            if (csb.ConnectionString.IndexOf("Password", StringComparison.InvariantCultureIgnoreCase) != -1 &&
                !string.IsNullOrEmpty(csb.Password))
            {
                config.SqlPassword = csb.Password;
            }
            if (csb.ConnectionString.IndexOf("Integrated Security", StringComparison.InvariantCultureIgnoreCase) != -1)
            {
                config.SqlTrustedConnection = csb.IntegratedSecurity;
                hasAuthType = true;
            }

            if (args != null && args.Length > 0)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (IsArgument(args[i], "?"))
                    {
                        Common.Log(
                            @"Help:
/B - batch mode
/S – SQL Server name
/D - Database name
/U – SQL login name
/P – SQL password
/E – trusted connection
/M - macros (one ore more pairs macro=""value"" separated by ';')
/C - path to config file
/R - scripts root catalog
For example: 
DBUpdate.exe /B /S (local) /D filuetSQL /U login /P password /E /M macro1=""value1"";macro2=""value2"" /C ""C:\Filuet\DBUpdate\DBUpdate.cfg"" /R ""C:\Filuet\Scripts""
");
                        Environment.Exit(0);
                    }
                    else if (IsArgument(args[i], "S") && i < args.Length - 1)
                    {
                        config.SqlServer = args[i + 1];
                    }
                    else if (IsArgument(args[i], "D") && i < args.Length - 1)
                    {
                        config.SqlDataBase = args[i + 1];
                    }
                    else if (IsArgument(args[i], "E"))
                    {
                        hasAuthType = true;
                        config.SqlTrustedConnection = true;
                    }
                    else if (IsArgument(args[i], "U") && i < args.Length - 1)
                    {
                        config.SqlLogin = args[i + 1];
                    }
                    else if (IsArgument(args[i], "P") && i < args.Length - 1)
                    {
                        config.SqlPassword = args[i + 1];
                    }
                    else if (IsArgument(args[i], "C") && i < args.Length - 1)
                    {
                        config.ConfigFilePath = args[i + 1];
                    }
                    else if (IsArgument(args[i], "R") && i < args.Length - 1)
                    {
                        config.ScriptsPath = args[i + 1];
                        if (!string.IsNullOrEmpty(config.ScriptsPath))
                        {
                            config.ScriptsPath = config.ScriptsPath.Trim('\"', '\'', '\\');
                        }
                        if (!string.IsNullOrEmpty(config.ScriptsPath) &&
                            config.ScriptsPath.Length == 2 &&
                            config.ScriptsPath[1] == ':')
                        {
                            config.ScriptsPath += "\\";
                        }
                    }
                    else if (IsArgument(args[i], "M") && i < args.Length - 1)
                    {
                        var macros = args[i + 1];
                        config.Macros.Clear();
                        if (!string.IsNullOrEmpty(macros))
                        {
                            var macrosArray = macros.Split(';');
                            if (macrosArray != null && macrosArray.Length > 0)
                            {
                                foreach (var macrosItem in macrosArray)
                                {
                                    if (macrosItem.Contains("="))
                                    {
                                        var name = macrosItem.Split('=')[0];
                                        var value =
                                            macrosItem.Split('=')[1].Trim('\"', '\'');
                                        config.Macros.Add(name, value);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!config.IsBatchMode)
            {
                Common.Log("-------------------------------------", Common.ShowDate.None);
                Common.Log("Enter configuration data", Common.ShowDate.None);
                if (string.IsNullOrEmpty(config.SqlServer))
                {
                    config.SqlServer = GetValue("SQL server", null);
                }
                if (string.IsNullOrEmpty(config.SqlDataBase))
                {
                    config.SqlDataBase = GetValue("Database", null);
                }
                if (!hasAuthType)
                {
                    config.SqlTrustedConnection = GetValue("Trusted/Sql", new List<string> {"t", "s"}).
                        Equals("t", StringComparison.InvariantCultureIgnoreCase);
                }
                if (!config.SqlTrustedConnection)
                {
                    if (string.IsNullOrEmpty(config.SqlLogin))
                        config.SqlLogin = GetValue("SQL login", null);
                    if (string.IsNullOrEmpty(config.SqlPassword))
                        config.SqlPassword = GetValue("Password", null);
                }
                Common.Log("-------------------------------------", Common.ShowDate.None);
                Common.Log("", Common.ShowDate.None);
            }

            csb.DataSource = config.SqlServer;
            csb.InitialCatalog = config.SqlDataBase;
            if (!string.IsNullOrEmpty(config.SqlLogin))
                csb.UserID = config.SqlLogin;
            if (!string.IsNullOrEmpty(config.SqlPassword))
                csb.Password = config.SqlPassword;
            csb.IntegratedSecurity = config.SqlTrustedConnection;
            if (csb.ConnectionString.IndexOf("Pooling", StringComparison.InvariantCultureIgnoreCase) == -1)
                csb.Pooling = false;
            if (csb.ConnectionString.IndexOf("Persist Security Info", StringComparison.InvariantCultureIgnoreCase) == -1)
                csb.PersistSecurityInfo = false;

            config.ConnectionString = csb.ConnectionString;

            return config;
        }

        public static bool IsArgument(string arg, string checkArg)
        {
            return
                arg.Equals("/" + checkArg, StringComparison.InvariantCultureIgnoreCase) ||
                arg.Equals("-" + checkArg, StringComparison.InvariantCultureIgnoreCase);
        }

        public static string GetValue(string promt, List<string> availableValues)
        {
            var availableValuesString =
                availableValues != null && availableValues.Count > 0
                    ? string.Join("|", availableValues.ToArray())
                    : null;
            Common.Log(
                promt +
                (!string.IsNullOrEmpty(availableValuesString) ? string.Format(" ({0})", availableValuesString) : "") +
                ": ", Common.ShowDate.None, "");
            var value = Console.ReadLine();
            //Common.Log(value);
            File.AppendAllText(Common.LogFileName, value + Environment.NewLine);

            if (availableValues != null && availableValues.Count > 0)
            {
                foreach (var availableValue in availableValues)
                    if (availableValue.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                        return value;

                return GetValue(promt, availableValues);
            }
            return value;
        }
    }
}