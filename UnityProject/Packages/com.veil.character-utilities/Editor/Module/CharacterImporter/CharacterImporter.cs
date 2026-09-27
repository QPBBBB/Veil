using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace CharacterUtilities.Editor.Module.CharacterImporter
{
    /// <summary>Code/Unity CLI entry point. No window, menus, scene changes or playback.</summary>
    public static class CharacterImporter
    {
        private static Task<AnimationClip> operation;
        private static string outputPath;

        public sealed class ImportStatus
        {
            public string state;
            public string asset;
            public string error;
            public float duration;
        }

        /// <summary>Starts import and returns immediately so Unity can continue processing.</summary>
        public static ImportStatus Execute(string source, string clipName,
            string outputFolder = "Assets/Resources/Pawn/Humanoid/Animation",
            string avatarPath = "Assets/Resources/Pawn/Humanoid/Avatar/Humanoid_Avatar.asset",
            string pythonPath = "", string sdkPath = "", string take = "")
        {
            if (operation != null && !operation.IsCompleted)
                throw new InvalidOperationException("An animation import is already running. Query GetStatus before starting another.");
            if (string.IsNullOrWhiteSpace(clipName) || clipName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || clipName.Contains("."))
                throw new ArgumentException("Supply a plain animation name without an extension.");
            var repository = Directory.GetParent(Application.dataPath)?.Parent?.FullName;
            if (string.IsNullOrEmpty(pythonPath)) pythonPath = Path.Combine(repository ?? "", "Tools/Python39/python.exe");
            if (string.IsNullOrEmpty(sdkPath)) sdkPath = Path.Combine(repository ?? "", "Tools/SDK/FBX");
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CharacterImporter).Assembly);
            if (package == null) throw new InvalidOperationException("Could not resolve the Character Utilities package.");
            var converter = Path.Combine(package.resolvedPath, "Editor/Module/CharacterImporter/PythonScripts~/convert.py");
            var name = clipName.StartsWith("Ani_", StringComparison.Ordinal) ? clipName : "Ani_" + clipName;
            outputPath = outputFolder.Replace('\\', '/').TrimEnd('/') + "/" + name + ".anim";
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            operation = CharacterAnimationImporter.ImportAsync(Path.GetFullPath(source), outputPath, avatar, pythonPath, converter, take, sdkPath);
            return GetStatus();
        }

        /// <summary>Poll this from Unity CLI; never synchronously wait on the import Task on the editor thread.</summary>
        public static ImportStatus GetStatus()
        {
            if (operation == null) return new ImportStatus { state = "idle" };
            if (!operation.IsCompleted) return new ImportStatus { state = "running", asset = outputPath };
            if (operation.IsCanceled) return new ImportStatus { state = "failed", error = "Import was cancelled.", asset = outputPath };
            if (operation.IsFaulted) return new ImportStatus { state = "failed", error = operation.Exception.GetBaseException().Message, asset = outputPath };
            return new ImportStatus { state = "completed", asset = outputPath, duration = operation.Result.length };
        }
    }
}
