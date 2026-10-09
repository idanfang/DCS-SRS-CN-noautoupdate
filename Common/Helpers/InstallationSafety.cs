using System;
using System.IO;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

public static class InstallationSafety
{
    public static bool IsInside(string file, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(file).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidTarget(string target, string payload)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        var path = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        var source = Path.GetFullPath(payload).TrimEnd(Path.DirectorySeparatorChar);
        return !path.Equals(Path.GetPathRoot(path).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            && !path.Equals(source, StringComparison.OrdinalIgnoreCase)
            && !IsInside(source, path) && !IsInside(path, source);
    }
}
