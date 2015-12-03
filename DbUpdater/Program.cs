using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DBUpdate
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            var exceptions = new List<Exception>();
            var retCode = 0;
            var isBatchMode = false;
            var openNotepad = false;

            try
            {
                Common.Log("Started");
                Common.Log("Reading config", Common.ShowDate.None);
                if (args != null && args.Length > 0)
                {
                    isBatchMode = args.FirstOrDefault(t => DBUpdaterConfig.IsArgument(t, "B")) != null;
                }

                var config = DBUpdaterConfig.GetConfig("DBUpdate.cfg", args);
                openNotepad = true;
                Common.Log("Current configuration", Common.ShowDate.None);
                Common.Log("IsBatchMode: " + config.IsBatchMode, Common.ShowDate.None);
                Common.Log("ConfigFilePath: " + config.ConfigFilePath, Common.ShowDate.None);
                Common.Log("ScriptsPath: " + config.ScriptsPath, Common.ShowDate.None);
                Common.Log("ScriptsPathCRE: " + config.ScriptsPathCRE, Common.ShowDate.None);
                Common.Log("ScriptsPathQRY: " + config.ScriptsPathQRY, Common.ShowDate.None);
                Common.Log("Macros count: " + config.Macros.Count, Common.ShowDate.None);
                Common.Log("SqlServer: " + config.SqlServer, Common.ShowDate.None);
                Common.Log("SqlDataBase: " + config.SqlDataBase, Common.ShowDate.None);
                Common.Log("SqlLogin: " + config.SqlLogin, Common.ShowDate.None);
                Common.Log("SqlPassword: " + config.SqlPassword, Common.ShowDate.None);
                Common.Log("SqlTrustedConnection:" + config.SqlTrustedConnection, Common.ShowDate.None);
                Common.Log("ConnectionString: " + config.ConnectionString, Common.ShowDate.None);
                config.Validate();

                isBatchMode = config.IsBatchMode;
                if (!isBatchMode)
                {
                    var answer = DBUpdaterConfig.GetValue("Continue?", new List<string> {"Y", "N"});
                    if (answer.Equals("N", StringComparison.InvariantCultureIgnoreCase))
                        Environment.Exit(0);
                }

                Common.Log("-------------------------------------", Common.ShowDate.None);
                var creFiles = new List<string>();
                if (Directory.Exists(config.ScriptsPathCRE))
                {
                    if (!File.Exists(Path.Combine(config.ScriptsPathCRE, "install.lst")))
                    {
                        throw new ConfigurationErrorsException(
                            string.Format("File {0} don't exists in {1}",
                                "install.lst", config.ScriptsPathCRE));
                    }

                    creFiles = new List<string>(File.ReadAllLines(Path.Combine(config.ScriptsPathCRE, "install.lst")));
                    creFiles = creFiles.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                    creFiles = Common.CorrectPath(creFiles, config.ScriptsPathCRE);
                }
                else
                {
                    Common.Log("Directory CRE not found");
                }

                var qryFiles = new List<string>();
                if (Directory.Exists(config.ScriptsPathQRY))
                {
                    qryFiles = File.Exists(Path.Combine(config.ScriptsPathQRY, "install.lst")) ? new List<string>(File.ReadAllLines(Path.Combine(config.ScriptsPathQRY, "install.lst"))) : new List<string>(Directory.GetFiles(config.ScriptsPathQRY));
                    qryFiles = qryFiles.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                    qryFiles = Common.CorrectPath(qryFiles, config.ScriptsPathQRY);
                }
                else
                {
                    Common.Log("Directory QRY not found");
                }

                Common.Log(string.Format("Total {0} scripts to execute in CRE, {1} scripts in QRY", creFiles.Count,
                    qryFiles.Count));

                using (var updater = new DBUpdater(config.ConnectionString))
                {
                    exceptions.AddRange(updater.ProcessScripts(creFiles, "Processing CRE files", config.Macros, true));
                    exceptions.AddRange(updater.ProcessScripts(qryFiles, "Processing QRY files", config.Macros, false));
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                Common.Log(ex);
                retCode = -1;
            }

            Common.Log("-------------------------------------", Common.ShowDate.None);
            Common.Log(string.Format("Done: Errors({0})", exceptions.Count));
            foreach (var exception in exceptions)
            {
                Common.Log(exception.ToString());
            }
            Common.Log("-------------------------------------", Common.ShowDate.None);

            if (!isBatchMode && openNotepad)
            {
                if (DBUpdaterConfig.GetValue("Open log in notepad?", new List<string> {"Y", "N"})
                    .Equals("Y", StringComparison.InvariantCultureIgnoreCase))
                {
                    Process.Start("notepad.exe",
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Common.LogFileName));
                }
            }

            return exceptions.Any() ? -2 : retCode;
        }
    }
}