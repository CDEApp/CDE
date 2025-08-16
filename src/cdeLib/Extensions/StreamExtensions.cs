using System;
using System.IO;

namespace cdeLib.Extensions;

public static class StreamExtensions
{
    public static byte[] ToByteArray(this Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
    
        var originalPosition = stream.Position;
        try 
        {
            stream.Position = 0;
            var length = stream.Length > int.MaxValue ? int.MaxValue : Convert.ToInt32(stream.Length);
            var buffer = new byte[length];
            stream.ReadExactly(buffer, 0, length);
            return buffer;
        }
        finally 
        {
            stream.Position = originalPosition;
        }
    }

}