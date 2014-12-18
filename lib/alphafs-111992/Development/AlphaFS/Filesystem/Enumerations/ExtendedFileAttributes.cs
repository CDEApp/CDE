/* Copyright (c) 2008-2014 Peter Palotas, Jeffrey Jangli, Normalex
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
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Alphaleonis.Win32.Filesystem
{
   /// <summary>Specifies how the operating system should open a file.</summary>
   [SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32")]
   [Flags]
   public enum ExtendedFileAttributes : uint
   {
      /// <summary>None of the file attributes specified.</summary>
      None = 0,

      #region FILE_ATTRIBUTE - Attributes applying to any file

      /// <summary>The file is read only. Applications can read the file, but cannot write to or delete it.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.ReadOnly"/>1</remarks>
      ReadOnly = FileAttributes.ReadOnly,

      /// <summary>The file is hidden. Do not include it in an ordinary directory listing.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.Hidden"/>2</remarks>
      Hidden = FileAttributes.Hidden,

      /// <summary>The file is part of or used exclusively by an operating system.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.System"/>4</remarks>
      System = FileAttributes.System,

      /// <summary>The handle that identifies a directory.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.Directory"/>16</remarks>
      Directory = FileAttributes.Directory,

      /// <summary>The file should be archived. Applications use this attribute to mark files for backup or removal.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.Archive"/>32</remarks>
      Archive = FileAttributes.Archive,

      /// <summary>The file should be archived. Applications use this attribute to mark files for backup or removal.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.Device"/>64</remarks>
      Device = FileAttributes.Device,

      /// <summary>The file does not have other attributes set. This attribute is valid only if used alone.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.Normal"/>128</remarks>
      Normal = FileAttributes.Normal,

      /// <summary>The file is being used for temporary storage.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.Temporary"/>256</remarks>
      Temporary = FileAttributes.Temporary,

      /// <summary>A file that is a sparse file.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.SparseFile"/>512</remarks>
      SparseFile = FileAttributes.SparseFile,

      /// <summary>A file or directory that has an associated reparse point, or a file that is a symbolic link.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.ReparsePoint"/>1024</remarks>
      ReparsePoint = FileAttributes.ReparsePoint,

      /// <summary>A file or directory that is compressed. For a file, all of the data in the file is compressed. For a directory, compression is the default for newly created files and subdirectories.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.Compressed"/>2048</remarks>
      Compressed = FileAttributes.Compressed,

      /// <summary>The data of a file is not immediately available. This attribute indicates that file data is physically moved to offline storage. This attribute is used by Remote Storage, the hierarchical storage management software. Applications should not arbitrarily change this attribute.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.Offline"/>4096</remarks>
      Offline = FileAttributes.Offline,

      /// <summary>The file or directory is not to be indexed by the content indexing service.</summary>
      /// <remarks>Equals <see cref="T:FileAttributes.NotContentIndexed"/>8192</remarks>
      NotContentIndexed = FileAttributes.NotContentIndexed,

      /// <summary>The file or directory is encrypted. For a file, this means that all data in the file is encrypted. For a directory, this means that encryption is the default for newly created files and subdirectories.</summary>
      /// <remarks>Equals <see cref="T:FileOptions.Encrypted"/>16384</remarks>
      Encrypted = FileOptions.Encrypted,

      #endregion // FILE_ATTRIBUTE - Attributes applying to any file

      /// <summary>(0x8000) The directory or user data stream is configured with integrity (only supported on ReFS volumes). It is not included in an ordinary directory listing. The integrity setting persists with the file if it's renamed. If a file is copied the destination file will have integrity set if either the source file or destination directory have integrity set.</summary>
      /// <remarks>This flag is not supported until Windows Server 2012.</remarks>
      IntegrityStream = 32768,

      /// <summary>(0x20000) The user data stream not to be read by the background data integrity scanner (AKA scrubber). When set on a directory it only provides inheritance. This flag is only supported on Storage Spaces and ReFS volumes. It is not included in an ordinary directory listing.</summary>
      /// <remarks>This flag is not supported until Windows Server 2012.</remarks>
      NoScrubData = 131072,

      /// <summary>0x80000</summary>
      FirstPipeInstance = 524288,

      /// <summary>(0x100000) The file data is requested, but it should continue to be located in remote storage. It should not be transported back to local storage. This flag is for use by remote storage systems.</summary>
      OpenNoRecall = 1048576,

      /// <summary>(0x200000) Normal reparse point processing will not occur; an attempt to open the reparse point will be made. When a file is opened, a file handle is returned, whether or not the filter that controls the reparse point is operational. See MSDN documentation for more information.</summary>
      OpenReparsePoint = 2097152,

      /// <summary>(0x1000000) Access will occur according to POSIX rules. This includes allowing multiple files with names, differing only in case, for file systems that support that naming. Use care when using this option, because files created with this flag may not be accessible by applications that are written for MS-DOS or 16-bit Windows.</summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Posix")]
      PosixSemantics = 16777216,

      /// <summary>(0x2000000) The file is being opened or created for a backup or restore operation. The system ensures that the calling process overrides file security checks when the process has SE_BACKUP_NAME and SE_RESTORE_NAME privileges. You must set this flag to obtain a handle to a directory. A directory handle can be passed to some functions instead of a file handle.</summary>
      BackupSemantics = 33554432,

      /// <summary>The file is to be deleted immediately after all of its handles are closed, which includes the specified handle and any other open or duplicated handles. If there are existing open handles to a file, the call fails unless they were all opened with the <see cref="T:FileShare.Delete"/> share mode. Subsequent open requests for the file fail, unless the <see cref="T:FileShare.Delete"/> share mode is specified.</summary>
      /// <remarks>Equals <see cref="T:FileOptions.DeleteOnClose"/>67108864</remarks>
      DeleteOnClose = FileOptions.DeleteOnClose,

      /// <summary>Access is intended to be sequential from beginning to end. The system can use this as a hint to optimize file caching.</summary>
      /// <remarks>Equals <see cref="T:FileOptions.SequentialScan"/>134217728</remarks>
      SequentialScan = FileOptions.SequentialScan,

      /// <summary>Access is intended to be random. The system can use this as a hint to optimize file caching.</summary>
      /// <remarks>Equals <see cref="T:FileOptions.RandomAccess"/>268435456</remarks>
      RandomAccess = FileOptions.RandomAccess,

      /// <summary>(0x20000000) There are strict requirements for successfully working with files opened with the <see cref="T:NoBuffering"/> flag, for details see the section on "File Buffering" in the online MSDN documentation.</summary>
      NoBuffering = 536870912,

      /// <summary>The file or device is being opened or created for asynchronous I/O.</summary>
      /// <remarks>Equals <see cref="T:FileOptions.Asynchronous"/>1073741824</remarks>
      Overlapped = FileOptions.Asynchronous,

      /// <summary>(0x80000000) Write operations will not go through any intermediate cache, they will go directly to disk.</summary>
      /// <remarks>Equals .NET <see cref="T:FileOptions.WriteThrough"/>-2147483648</remarks>
      WriteThrough = 2147483648
   }
}