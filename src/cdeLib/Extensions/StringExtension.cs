﻿using System;

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

        if (!System.IO.Path.EndsInDirectorySeparator(rootPath))
        {
            rootPath += System.IO.Path.DirectorySeparatorChar;
        }

        if (fullPath.Contains(rootPath))
        {
            return fullPath.Substring(rootPath.Length);
        }

        return null;
    }
}