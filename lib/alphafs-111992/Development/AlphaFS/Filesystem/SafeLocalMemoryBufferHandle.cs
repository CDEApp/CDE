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
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
#if NET35
using System.Security.Permissions;
#endif

namespace Alphaleonis.Win32.Security
{
   /// <summary>IntPtr wrapper which can be used as result of Marshal.AllocHGlobal operation. Calls Marshal.FreeHGlobal when disposed or finalized.</summary>
   internal sealed class SafeLocalMemoryBufferHandle : SafeHandleZeroOrMinusOneIsInvalid
   {
      #region Constructor

      /// <summary>Creates new instance with zero IntPtr.</summary>      
      public SafeLocalMemoryBufferHandle() : base(true)
      {
      }

      #endregion // Constructor

      #region Methods

      #region CopyFrom

      /// <summary>Copies data from a one-dimensional, managed 8-bit unsigned integer array to the unmanaged memory pointer referenced by this instance.</summary>
      /// <param name="source">The one-dimensional array to copy from.</param>
      /// <param name="startIndex">The zero-based index into the array where Copy should start.</param>
      /// <param name="length">The number of array elements to copy.</param>      
      public void CopyFrom(byte[] source, int startIndex, int length)
      {
         Marshal.Copy(source, startIndex, handle, length);
      }

      #endregion // CopyFrom

      #region ToByteArray

      public byte[] ToByteArray(int startIndex, int length)
      {
         if (IsInvalid)
            return null;

         byte[] arr = new byte[length];
         Marshal.Copy(handle, arr, startIndex, length);
         return arr;
      }

      #endregion // ToByteArray

      #region ReleaseHandle

      /// <summary>Called when object is disposed or finalized.</summary>
#if NET35
      [SecurityPermissionAttribute(SecurityAction.LinkDemand, UnmanagedCode = true)]
#endif
      protected override bool ReleaseHandle()
      {
         return NativeMethods.LocalFree(handle) == IntPtr.Zero;
      }

      #endregion // ReleaseHandle

      #endregion // Methods
   }
}