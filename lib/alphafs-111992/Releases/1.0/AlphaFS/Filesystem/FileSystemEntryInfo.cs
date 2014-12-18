/* Copyright (c) 2008-2009 Peter Palotas
 *  
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *  
 *  The above copyright notice and this permission notice shall be included in
 *  all copies or substantial portions of the Software.
 *  
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 *  THE SOFTWARE.
 */

using System;
using System.Runtime.InteropServices;

namespace Alphaleonis.Win32.Filesystem
{
    /// <summary>
    /// Represents information about a file system entry. Used together with <see cref="FileSystemEntryEnumerator"/>.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
    public class FileSystemEntryInfo
    {
        private uint dwFileAttributes;
        private FileTime ftCreationTime;
        private FileTime ftLastAccessTime;
        private FileTime ftLastWriteTime;
        private uint nFileSizeHigh;
        private uint nFileSizeLow;
        private uint dwReserved0;
        private uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        private string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        private string cAlternateFileName;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemEntryInfo"/> class.
        /// </summary>
        public FileSystemEntryInfo()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemEntryInfo"/> class.
        /// </summary>
        /// <param name="winFileAttribData">The WIN32_FILE_ATTRIBUTE_DATA.</param>
        internal FileSystemEntryInfo(NativeMethods.WIN32_FILE_ATTRIBUTE_DATA winFileAttribData)
        {
            dwFileAttributes = (uint)winFileAttribData.dwFileAttributes;
            ftCreationTime = winFileAttribData.ftCreationTime;
            ftLastAccessTime = winFileAttribData.ftLastAccessTime;
            ftLastWriteTime = winFileAttribData.ftLastWriteTime;
            nFileSizeHigh = winFileAttribData.nFileSizeHigh;
            nFileSizeLow = winFileAttribData.nFileSizeLow;
            dwReserved0 = dwReserved1 = 0;
            cFileName = string.Empty;
            cAlternateFileName = string.Empty;
        }

        /// <summary>
        /// Gets the attributes.
        /// </summary>
        /// <value>The attributes.</value>
        public FileAttributes Attributes { get { return (FileAttributes)dwFileAttributes; } }

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        /// <value>The name of the file.</value>
        public string FileName { get { return cFileName; } }

        /// <summary>
        /// Gets the size of the file.
        /// </summary>
        /// <value>The size of the file.</value>
        public long FileSize
        {
            get
            {
                return (((long)nFileSizeHigh) << 32) | (((long)nFileSizeLow) & 0xFFFFFFFF);
            }
        }

        /// <summary>
        /// Gets the 8.3 version of the filename.
        /// </summary>
        /// <value>the 8.3 version of the filename.</value>
        public string AlternateFileName { get { return cAlternateFileName; } }

        /// <summary>
        /// Gets the time this entry was created.
        /// </summary>
        /// <value>The time this entry was created.</value>
        public DateTime Created { get { return DateTime.FromFileTime(ftCreationTime.AsLong()); } }

        /// <summary>
        /// Gets the time this entry was last accessed.
        /// </summary>
        /// <value>The time this entry was last accessed.</value>
        public DateTime LastAccessed { get { return DateTime.FromFileTime(ftLastAccessTime.AsLong()); } }

        /// <summary>
        /// Gets the time this entry was last modified.
        /// </summary>
        /// <value>The time this entry was last modified.</value>
        public DateTime LastModified { get { return DateTime.FromFileTime(ftLastWriteTime.AsLong()); } }

        /// <summary>
        /// Gets a value indicating whether this instance represents a directory.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance represents a directory; otherwise, <c>false</c>.
        /// </value>
        public bool IsDirectory { get { return (Attributes & FileAttributes.Directory) == FileAttributes.Directory; } }

        /// <summary>
        /// Gets a value indicating whether this instance is definitely a file.
        /// </summary>
        /// <value><c>true</c> if this instance is file; otherwise, <c>false</c>.</value>
        /// <remarks>Definite file is NOT a directory and NOT a device.</remarks>
        public bool IsFile { get { return (!IsDirectory && !((Attributes & FileAttributes.Device) == FileAttributes.Device)); } }

        /// <summary>
        /// Gets a value indicating whether this instance is a reparse point.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is a reparse point; otherwise, <c>false</c>.
        /// </value>
        public bool IsReparsePoint
        {
            get
            {
                return (Attributes & FileAttributes.ReparsePoint) != 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is a mount point.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is a mount point; otherwise, <c>false</c>.
        /// </value>
        public bool IsMountPoint
        {
            get { return ReparsePointTag == ReparsePointTag.MountPoint; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is a symbolic link.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is a symbolic link; otherwise, <c>false</c>.
        /// </value>
        public bool IsSymbolicLink
        {
            get
            {
                return ReparsePointTag == ReparsePointTag.SymLink;
            }
        }

        /// <summary>
        /// Gets the reparse point tag of this entry.
        /// </summary>
        /// <value>The reparse point tag of this entry.</value>
        public ReparsePointTag ReparsePointTag
        {
            get { return IsReparsePoint ? (ReparsePointTag)dwReserved0 : ReparsePointTag.None; }
        }
    }
}
