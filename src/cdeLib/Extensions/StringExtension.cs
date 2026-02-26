using System;

namespace cdeLib.Extensions;

public static class StringExtension
{
    /// <summary>
    /// Assumption that rootPath of form "D:" is equivalent to root of a device not a relative path to start with?
    /// </summary>
    /// <param name="fullPath"></param>
    /// <param name="rootPath"></param>
    /// <returns></returns>
    public static string GetRelativePath(this string fullPath, string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath) || string.IsNullOrEmpty(fullPath))
        {
            return null;
        }

        if (fullPath.Equals(rootPath))
        {
            return string.Empty;
        }

        // Use Span for zero-allocation path manipulation
        var fullPathSpan = fullPath.AsSpan();
        var rootPathSpan = rootPath.AsSpan();

        // Check if we need to add a directory separator
        var rootLength = rootPath.Length;
        if (!System.IO.Path.EndsInDirectorySeparator(rootPath))
        {
            // Check if the fullPath starts with rootPath + separator
            if (fullPathSpan.Length > rootLength &&
                fullPathSpan.StartsWith(rootPathSpan, StringComparison.Ordinal) &&
                (fullPathSpan[rootLength] == '\\' || fullPathSpan[rootLength] == '/'))
            {
                // Skip rootPath and the separator
                return new string(fullPathSpan[(rootLength + 1)..]);
            }

            return null;
        }

        // rootPath already ends with separator
        return fullPathSpan.StartsWith(rootPathSpan, StringComparison.Ordinal)
            ? new string(fullPathSpan[rootLength..])
            : null;
    }
}