﻿/* Copyright (c) 2008-2014 Peter Palotas, Jeffrey Jangli, Normalex
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

using System.Diagnostics.CodeAnalysis;

namespace Alphaleonis.Win32.Filesystem
{
   /// <summary>WIN32_STREAM_ID structure - The type of the data contained in the backup stream.  This enum is used by the Filesystem classes BackupFileStream() and AlternateDataStreamInfo().</summary>

   public enum StreamType
   {
      /// <summary>(0) This indicates an error.</summary>
      None = 0,

      /// <summary>(1) BACKUP_DATA - Standard data. This corresponds to the NTFS $DATA stream type on the default (unnamed) data stream.</summary>
      Data = 1,

      /// <summary>(2) BACKUP_EA_DATA - Extended attribute data. This corresponds to the NTFS $EA stream type.</summary>
      ExtendedAttributesData = 2,

      /// <summary>BACKUP_SECURITY_DATA - Security descriptor data.</summary>
      SecurityData = 3,

      /// <summary>(4) BACKUP_ALTERNATE_DATA - Alternative data streams. This corresponds to the NTFS $DATA stream type on a named data stream.</summary>
      AlternateData = 4,

      /// <summary>(5) BACKUP_LINK - Hard link information. This corresponds to the NTFS $FILE_NAME stream type.</summary>
      Link = 5,

      /// <summary>(6) BACKUP_PROPERTY_DATA - Property data.</summary>
      PropertyData = 6,

      /// <summary>(7) BACKUP_OBJECT_ID - Objects identifiers. This corresponds to the NTFS $OBJECT_ID stream type.</summary>
      ObjectId = 7,

      /// <summary>(8) BACKUP_REPARSE_DATA - Reparse points. This corresponds to the NTFS $REPARSE_POINT stream type.</summary>
      ReparseData = 8,

      /// <summary>(9) BACKUP_SPARSE_BLOCK - Sparse file. This corresponds to the NTFS $DATA stream type for a sparse file.</summary>
      SparseBlock = 9,

      /// <summary>(10) BACKUP_TXFS_DATA - Transactional NTFS (TxF) data stream.</summary>
      /// <remarks>Windows Server 2003 and Windows XP:  This value is not supported.</remarks>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Txfs")]
      TxfsData = 10
   }
}