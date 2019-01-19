using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Helpers
{
    public static class PathHelper
    {
        public static string GetFullPathWithoutExtension(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (dir != path)
            {
                string newPath = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(newPath) && newPath.Length > 0 && newPath.ElementAt(newPath.Length - 1) != Path.DirectorySeparatorChar)
                {
                    newPath += Path.DirectorySeparatorChar;
                }
                newPath += Path.GetFileNameWithoutExtension(path);
                return newPath;
            }
            return path;
        }

    }
}
