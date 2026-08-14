using System.IO;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Where build and verification output goes. Deliberately outside Temp/, which Unity wipes on
    /// shutdown, and outside Assets/, so nothing generated ends up imported as a project asset.
    /// </summary>
    public static class ArtifactPaths
    {
        public static string Root
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(projectRoot, "Artifacts");
            }
        }

        public static string Screenshots => EnsureDirectory(Path.Combine(Root, "screenshots"));

        public static string WebGLBuild => EnsureDirectory(Path.Combine(Root, "WebGL"));

        public static string Screenshot(string fileName) => Path.Combine(Screenshots, fileName);

        private static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
