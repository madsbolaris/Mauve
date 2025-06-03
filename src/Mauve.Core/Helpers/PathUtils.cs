using System;
using System.IO;

namespace Mauve.Core.Helpers
{
    public static class PathUtils
    {
        public static string ExpandHome(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            if (path.StartsWith("~"))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, path.TrimStart('~', '/', '\\'));
            }

            return path;
        }
    }
}
