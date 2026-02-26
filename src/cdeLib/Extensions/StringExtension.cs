using System;

namespace cdeLib;

public static class StringExtension
{
    public static bool IsNullOrEmpty(this string value)
    {
        return String.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Assumption that rootPath of form "D:" is equivalent to root of device not a relative path to start with ?
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
        ReadOnlySpan<char> fullPathSpan = fullPath.AsSpan();
        ReadOnlySpan<char> rootPathSpan = rootPath.AsSpan();

        // Check if we need to add directory separator
        int rootLength = rootPath.Length;
        if (!System.IO.Path.EndsInDirectorySeparator(rootPath))
        {
            // Check if fullPath starts with rootPath + separator
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
        if (fullPathSpan.StartsWith(rootPathSpan, StringComparison.Ordinal))
        {
            return new string(fullPathSpan[rootLength..]);
        }

        return null;
    }
}