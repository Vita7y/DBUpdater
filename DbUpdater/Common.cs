using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DBUpdate
{
    public class Common
    {
        public enum ShowDate
        {
            None,
            Console,
            File,
            Both
        }

        private const string DateTimeFormatString = "[yyyy.MM.dd HH:mm:ss]";
        private static readonly object Locker = new object();

        public static readonly string LogFileName = Application.StartupPath + @"\" +
                                                    string.Format("DBUpdate_{0}.log",
                                                        DateTime.Now.ToString("yy-MM-dd-HHmmssffff"));

        public static string GetExceptionDetails(Exception ex)
        {
            var exceptionDetails = string.Empty;
            if (ex != null)
            {
                exceptionDetails = ex.GetType() + ": " + ex.Message;
                if (ex.InnerException != null)
                {
                    exceptionDetails += Environment.NewLine + GetExceptionDetails(ex.InnerException);
                }
            }
            return exceptionDetails;
        }

        public static void Log(string s, ShowDate showDate = ShowDate.Both, string endLine = "\r\n")
        {
            try
            {
                lock (Locker)
                {
                    var dateString = DateTime.Now.ToString(DateTimeFormatString) + "\t";
                    Console.WriteLine((showDate == ShowDate.Console || showDate == ShowDate.Both ? dateString : "") + s);
                    File.AppendAllText(LogFileName,
                        (showDate == ShowDate.File || showDate == ShowDate.Both ? dateString : "") + s + endLine);
                }
            }
                // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
        }

        public static void Log(Exception ex)
        {
            Log("Application error: " + Environment.NewLine + GetExceptionDetails(ex));
        }

        public static List<string> CorrectPath(List<string> files, string path)
        {
            if (files != null && files.Count > 0)
                for (var i = 0; i < files.Count; i++)
                    if (!files[i].Contains('\\'))
                        files[i] = Path.Combine(path, files[i]);
            return files;
        }
    }
}