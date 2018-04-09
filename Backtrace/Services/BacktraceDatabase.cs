﻿using Backtrace.Base;
using Backtrace.Common;
using Backtrace.Interfaces;
using Backtrace.Model;
using Backtrace.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Backtrace.Tests")]
namespace Backtrace.Services
{
    /// <summary>
    /// Backtrace Database 
    /// </summary>
    internal class BacktraceDatabase<T> : IBacktraceDatabase<T>
    {
        /// <summary>
        /// Determine if BacktraceDatabase is enable and library can store reports
        /// </summary>
        private readonly bool _enable = true;

        /// <summary>
        /// Database settings
        /// </summary>
        public BacktraceDatabaseSettings DatabaseSettings { get; private set; }

        /// <summary>
        /// Create Backtrace database instance
        /// </summary>
        /// <param name="databaseSettings">Backtrace database settings</param>
        public BacktraceDatabase(BacktraceDatabaseSettings databaseSettings)
        {
            if(databaseSettings == null || string.IsNullOrEmpty(databaseSettings.DatabasePath))
            {
                _enable = false;
                return;
            }
            DatabaseSettings = databaseSettings;
            ValidateDatabaseDirectory();
        }

        /// <summary>
        /// Check if used directory database is available 
        /// </summary>
        private void ValidateDatabaseDirectory()
        {
            string databasePath = DatabaseSettings.DatabasePath;
            //there is no database directory
            if (string.IsNullOrEmpty(databasePath))
            {
                return;
            }
           
            ClearDatabase();
        }

        /// <summary>
        /// Delete all existing files and directories in current database directory
        /// </summary>
        private void ClearDatabase()
        {
            var directoryInfo = new DirectoryInfo(_directoryPath);

            IEnumerable<FileInfo> files;
            IEnumerable<DirectoryInfo> directories;
#if !NET35
            files = directoryInfo.EnumerateFiles();
            directories = directoryInfo.EnumerateDirectories();
#else
            files = directoryInfo.GetFiles();
            directories = directoryInfo.GetDirectories();
#endif
            foreach (FileInfo file in files)
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in directories)
            {
                dir.Delete(true);
            }
        }

        /// <summary>
        /// Create new minidump file in database directory path. Minidump file name is a random Guid
        /// </summary>
        /// <param name="backtraceReport">Current report</param>
        /// <param name="miniDumpType">Generated minidump type</param>
        /// <returns>Path to minidump file</returns>
        public string GenerateMiniDump(BacktraceReportBase<T> backtraceReport, MiniDumpType miniDumpType)
        {
            if (!_enable)
            {
                return string.Empty;
            }
            //note that every minidump file generated by app ends with .dmp extension
            //its important information if you want to clear minidump file
            string minidumpDestinationPath = Path.Combine(DatabaseSettings.DatabasePath, $"{Guid.NewGuid()}.dmp");
            MinidumpException minidumpExceptionType = backtraceReport.ExceptionTypeReport
                ? MinidumpException.Present
                : MinidumpException.None;

            bool minidumpSaved = MinidumpHelper.Write(
                filePath: minidumpDestinationPath,
                options: miniDumpType,
                exceptionType: minidumpExceptionType);

            return minidumpSaved
                ? minidumpDestinationPath
                : string.Empty;
        }

        /// <summary>
        /// Clear generated minidumps
        /// </summary>
        /// <param name="pathToMinidump">Path to created minidump</param>
        public void ClearMiniDump(string pathToMinidump)
        {
            //if _enable == false then application wont generate any minidump file
            //note that every minidump file generated by app ends with .dmp extension
            //its important information if you want to clear minidump file
            if (!_enable || string.IsNullOrEmpty(pathToMinidump) || Path.GetExtension(pathToMinidump) != ".dmp")
            {
                return;
            }
            File.Delete(pathToMinidump);
        }

        /// <summary>
        /// Save diagnostic report on hard drive
        /// </summary>
        /// <param name="backtraceReport"></param>
        [Obsolete]
        public bool SaveReport(BacktraceData<T> backtraceReport)
        {
            if (!_enable)
            {
                return true;
            }

            string json = JsonConvert.SerializeObject(backtraceReport);
            byte[] file = Encoding.UTF8.GetBytes(json);
            string filename = $"Backtrace_{backtraceReport.Timestamp}";
            string filePath = Path.Combine(DatabaseSettings.DatabasePath, filename);
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(file, 0, file.Length);
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
    }
}
