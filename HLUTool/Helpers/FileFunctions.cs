// The DataTools are a suite of ArcGIS Pro addins used to extract, sync
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2025-2026 Andy Foy Consulting
//
// This file is part of DataTools suite of programs..
//
// DataTools are free software: you can redistribute it and/or modify
// them under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// DataTools are distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with with program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;

namespace HLU.Helpers
{
    /// <summary>
    /// This class provides basic file and folder functions.
    /// </summary>
    internal static class FileFunctions
    {
        #region Directories

        /// <summary>
        /// Check if a directory exists.
        /// </summary>
        /// <param name="filePath">The path of the directory to check.</param>
        /// <returns><c>true</c> if the directory exists; otherwise, <c>false</c>.</returns>
        public static bool DirExists(string filePath)
        {
            // Check input first.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check if the directory exists.
            DirectoryInfo myDir = new(filePath);
            if (!myDir.Exists)
                return false;

            return true;
        }

        /// <summary>
        /// Get the name of a directory from a full path.
        /// </summary>
        /// <param name="fullPath">The full path of the directory.</param>
        /// <returns>The name of the directory, or <c>null</c> if the path is invalid.</returns>
        public static string GetDirectoryName(string fullPath)
        {
            // Check input first.
            if (string.IsNullOrEmpty(fullPath))
                return null;

            // Get the directory name.
            FileInfo fileInfo = new(fullPath);
            string dirName = fileInfo.DirectoryName;

            return dirName;
        }

        #endregion Directories

        #region Files

        /// <summary>
        /// Check if a file exists from a file path and name.
        /// </summary>
        /// <param name="filePath">The path of the directory containing the file.</param>
        /// <param name="fileName">The name of the file to check.</param>
        /// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
        public static bool FileExists(string filePath, string fileName)
        {
            // Check input first.
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check input first.
            if (string.IsNullOrEmpty(fileName))
                return false;

            // If the directory exists.
            if (DirExists(filePath))
            {
                string strFileName;
                string pathEnd = filePath.Substring(filePath.Length - 1, 1);
                if (pathEnd != @"\")
                {
                    strFileName = filePath + @"\" + fileName;
                }
                else
                {
                    strFileName = filePath + fileName;
                }

                FileInfo fileInfo = new(strFileName);

                if (fileInfo.Exists) return true;
                else return false;
            }
            return false;
        }

        /// <summary>
        /// Check if a file exists from a full path.
        /// </summary>
        /// <param name="fullPath">The full path of the file to check.</param>
        /// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
        public static bool FileExists(string fullPath)
        {
            // Check input first.
            if (string.IsNullOrEmpty(fullPath))
                return false;

            // Check if the file exists.
            FileInfo fileInfo = new(fullPath);
            if (fileInfo.Exists)
                return true;

            return false;
        }

        /// <summary>
        /// Get the name of a file from a full path.
        /// </summary>
        /// <param name="fullPath">The full path of the file.</param>
        /// <returns>The name of the file, or <c>null</c> if the path is invalid.</returns>
        public static string GetFileName(string fullPath)
        {
            // Check input first.
            if (string.IsNullOrEmpty(fullPath))
                return null;

            // Get the file name.
            FileInfo fileInfo = new(fullPath);
            string fileName = fileInfo.Name;

            return fileName;
        }

        /// <summary>
        /// Get a file extension from a full path.
        /// </summary>
        /// <param name="fullPath">The full path of the file.</param>
        /// <returns>The file extension, or <c>null</c> if the path is invalid.</returns>
        public static string GetExtension(string fullPath)
        {
            // Check input first.
            if (string.IsNullOrEmpty(fullPath))
                return null;

            // Get the file extension.
            FileInfo fileInfo = new(fullPath);
            string aExt = fileInfo.Extension;

            return aExt;
        }

        /// <summary>
        /// Get all files in a directory.
        /// </summary>
        /// <param name="filePath">The path of the directory.</param>
        /// <returns>A list of file paths, or <c>null</c> if the path is invalid.</returns>
        public static List<string> GetAllFilesInDirectory(string filePath)
        {
            // Check input first.
            if (string.IsNullOrEmpty(filePath))
                return null;

            List<string> myFileList = [];
            if (DirExists(filePath))
            {
                string[] fileEntries = Directory.GetFiles(filePath);
                foreach (string aFile in fileEntries)
                {
                    myFileList.Add(aFile);
                }
            }

            return myFileList;
        }

        /// <summary>
        /// Get a full file name without the extension.
        /// </summary>
        /// <param name="fullName">The full path of the file.</param>
        /// <returns>The full file name without the extension, or <c>null</c> if the path is invalid.</returns>
        public static string GetFullNameWithoutExtension(string fullName)
        {
            // Check input first.
            if (string.IsNullOrEmpty(fullName))
                return null;

            // Get the directory name.
            string filePath = GetDirectoryName(fullName);

            // Get the file name without the extension.
            string fileName = Path.GetFileNameWithoutExtension(fullName);

            return filePath + @"\" + fileName;
        }

        /// <summary>
        /// Get a full file name without the extension.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <returns>The file name without the extension, or <c>null</c> if the input is invalid.</returns>
        public static string GetFileNameWithoutExtension(string fileName)
        {
            // Check input first.
            if (string.IsNullOrEmpty(fileName))
                return null;

            // Get the file name without the extension.
            fileName = Path.GetFileNameWithoutExtension(fileName);

            return fileName;
        }

        /// <summary>
        /// Delete a file.
        /// </summary>
        /// <param name="fullPath">The full path of the file to delete.</param>
        /// <returns><c>true</c> if the file was deleted successfully; otherwise, <c>false</c>.</returns>
        public static bool DeleteFile(string fullPath)
        {
            // Check input first.
            if (string.IsNullOrEmpty(fullPath))
                return false;

            if (FileExists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else
                return true;
        }

        /// <summary>
        /// Write a new text file with optional headers.
        /// </summary>
        /// <param name="outTable">The full path of the file to create.</param>
        /// <param name="outHeader">The header line to write to the file.</param>
        /// <returns><c>true</c> if the file was created successfully; otherwise, <c>false</c>.</returns>
        public static bool WriteEmptyTextFile(string outTable, string outHeader)
        {
            // Check input first.
            if (string.IsNullOrEmpty(outTable))
                return false;

            try
            {
                // Open output file.
                StreamWriter txtFile = new(outTable, false);

                // Write the headers to the file.
                if (!string.IsNullOrEmpty(outHeader))
                    txtFile.WriteLine(outHeader);

                // Close the file.
                txtFile.Close();

                txtFile.Dispose();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Rename a file.
        /// </summary>
        /// <param name="oldPath">The full path of the file to rename.</param>
        /// <param name="newPath">The new full path of the file.</param>
        /// <returns><c>true</c> if the file was renamed successfully; otherwise, <c>false</c>.</returns>
        public static bool RenameFile(string oldPath, string newPath)
        {
            // Check input first.
            if (string.IsNullOrEmpty(oldPath))
                return false;

            // Check if input exists.
            if (!FileExists(oldPath))
                return true;

            try
            {
                File.Move(oldPath, newPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion Files

        #region Logfile

        /// <summary>
        /// Create a log file.
        /// </summary>
        /// <param name="logFile">The full path of the log file to create.</param>
        /// <returns><c>true</c> if the log file was created successfully; otherwise, <c>false</c>.</returns>
        public static bool CreateLogFile(string logFile)
        {
            // Check input first.
            if (string.IsNullOrEmpty(logFile))
                return false;

            StreamWriter myWriter = new(logFile, false);

            myWriter.WriteLine("Log file started on " + DateTime.Now.ToString());
            myWriter.Close();
            myWriter.Dispose();
            return true;
        }

        /// <summary>
        /// Write to the end of a log file.
        /// </summary>
        /// <param name="logFile">The full path of the log file to write to.</param>
        /// <param name="logLine">The line of text to write to the log file.</param>
        /// <returns><c>true</c> if the line was written successfully; otherwise, <c>false</c>.</returns>
        public static bool WriteLine(string logFile, string logLine)
        {
            // Check input first.
            if (string.IsNullOrEmpty(logFile))
                return false;

            try
            {
                // Add the date and time to the start of the text.
                logLine = DateTime.Now.ToString() + " : " + logLine;

                // Open the log file.
                StreamWriter myWriter = new(logFile, true);

                // Write the line to the end of the log file.
                myWriter.WriteLine(logLine);

                // Close the log file and dispose of the object.
                myWriter.Close();
                myWriter.Dispose();
            }
            catch
            {
                return false;
            }

            return true;
        }

        #endregion Logfile
    }
}