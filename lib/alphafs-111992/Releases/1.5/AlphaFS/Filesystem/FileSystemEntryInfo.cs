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
    public class FileSystemEntryInfo
    {
        [NonSerializedAttribute]
        private NativeMethods.WIN32_FIND_DATA _win32FindData;
        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemEntryInfo"/> class.
        /// </summary>
        /// <param name="findData">The WIN32 find data structure.</param>
        internal FileSystemEntryInfo(NativeMethods.WIN32_FIND_DATA findData)
        {
            _win32FindData = findData;
        }

        /// <summary>
        /// Gets internal WIN32 FIND Data
        /// </summary>
        internal NativeMethods.WIN32_FIND_DATA Win32FindData
        { get { return _win32FindData; } }

        /// <summary>
        /// Gets the attributes.
        /// </summary>
        /// <value>The attributes.</value>
        public FileAttributes Attributes { get { return _win32FindData.dwFileAttributes; } }

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        /// <value>The name of the file.</value>
        public string FileName { get { return _win32FindData.cFileName; } }

        /// <summary>
        /// Gets the size of the file.
        /// </summary>
        /// <value>The size of the file.</value>
        public long FileSize
        {
            get
            {
                return (((long)_win32FindData.nFileSizeHigh) << 32) | (((long)_win32FindData.nFileSizeLow) & 0xFFFFFFFF);
            }
        }

        /// <summary>
        /// Gets the 8.3 version of the filename.
        /// </summary>
        /// <value>the 8.3 version of the filename.</value>
        public string AlternateFileName { get { return _win32FindData.cAlternateFileName; } }

        /// <summary>
        /// Gets the time this entry was created.
        /// </summary>
        /// <value>The time this entry was created.</value>
        public DateTime Created { get { return DateTime.FromFileTime(_win32FindData.ftCreationTime.AsLong()); } }

        /// <summary>
        /// Gets the time this entry was last accessed.
        /// </summary>
        /// <value>The time this entry was last accessed.</value>
        public DateTime LastAccessed { get { return DateTime.FromFileTime(_win32FindData.ftLastAccessTime.AsLong()); } }

        /// <summary>
        /// Gets the time this entry was last modified.
        /// </summary>
        /// <value>The time this entry was last modified.</value>
        public DateTime LastModified { get { return DateTime.FromFileTime(_win32FindData.ftLastWriteTime.AsLong()); } }

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
            get { return IsReparsePoint ? _win32FindData.dwReserved0 : ReparsePointTag.None; }
        }

        /// <summary>
        /// The real full path of the file system object/entry.
        /// </summary>
        public string FullPath
        { get; set; }

        /// <summary>
        /// This property is intended to be used with in the future versions of the library
        /// to store a full path that is relative to a parent symbolic link or junction point.
        /// It will be correctly set by enumerating methods.
        /// <code>
        /// Parent Symbolic Directory Link Pointed
        /// From: C:\Users\Novels\Application Data
        /// To: C:\Users\Novels\AppData\Roaming
        /// so the entryu info for vlc-qt-interface.ini file will have following values
        /// FullPath: C:\Users\Novels\AppData\Roaming\vlc\vlc-qt-interface.ini
        /// VirtualFullPath: C:\Users\Novels\Application Data\vlc\vlc-qt-interface.ini
        /// </code>
        /// </summary>
        public string VirtualFullPath
        { get; set; }
    }
}
