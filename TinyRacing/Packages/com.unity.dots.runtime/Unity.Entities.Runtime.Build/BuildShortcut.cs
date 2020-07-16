using System.IO;

namespace Unity.Entities.Runtime.Build
{
    internal static class BuildShortcut
    {
        // Convenience method for CI workflow so they can quickly create all files
        // required by the buildprogram to build the samples without using the editor UI
        public static void UpdateAsmDefsJson()
        {
            var bootstrapFolder = Path.GetFullPath("./Library/DotsRuntimeBuild");

            if (!Directory.Exists(bootstrapFolder))
            {
                Directory.CreateDirectory(bootstrapFolder);
            }

            BuildProgramDataFileWriter.WriteAll(bootstrapFolder);
        }
    }
}
