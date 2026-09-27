using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace CharacterUtilities.Editor.Module.CharacterImporter
{
    /// <summary>Editor entry point. Python only converts FBX; Unity owns all asset operations.</summary>
    public static class CharacterAnimationImporter
    {
        [Serializable] private sealed class Mapping { public string target; }
        [Serializable] private sealed class Report { public Mapping[] mapping; }

        public static async Task<AnimationClip> ImportAsync(string input, string output, Avatar avatar,
            string python, string converter, string take = "", string sdk = "")
        {
            ValidateOutput(output, avatar);
            foreach (var path in new[] { input, python, converter })
                if (!File.Exists(path)) throw new FileNotFoundException("Required input or tool was not found", path);
            if (!string.Equals(Path.GetExtension(input), ".fbx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Select an FBX animation file.");
            var work = Path.Combine(Path.GetTempPath(), "character-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var converted = Path.Combine(work, "converted.fbx");
                var log = await RunPython(python, converter, input, converted, sdk);
                var jsonStart = log.IndexOf('{');
                if (jsonStart < 0) throw new InvalidOperationException("Python returned no conversion report.");
                var report = JsonUtility.FromJson<Report>(log.Substring(jsonStart));
                if (report?.mapping == null) throw new InvalidOperationException("Python returned an invalid bone mapping report.");
                var names = report.mapping.Select(m => m.target).Concat(new[] { "Bone_Root" }).ToArray();
                var missing = avatar.humanDescription.human.Select(h => h.boneName).Except(names).ToArray();
                if (missing.Length > 0) throw new InvalidOperationException("Missing Avatar bones: " + string.Join(", ", missing));
                // Await resumes on Unity's editor synchronization context before touching AssetDatabase.
                return ImportConvertedClip(converted, output, avatar, take);
            }
            finally
            {
                // Only this invocation's uniquely allocated temporary directory is removed.
                if (Directory.Exists(work)) Directory.Delete(work, true);
            }
        }

        private static async Task<string> RunPython(string python, string converter, string input, string output, string sdk)
        {
            var arguments = new[] { converter, input, "--output", output, "--no-import" };
            if (!string.IsNullOrEmpty(sdk)) arguments = arguments.Concat(new[] { "--sdk", sdk }).ToArray();
            var start = new ProcessStartInfo
            {
                FileName = Path.GetFullPath(python),
                Arguments = string.Join(" ", arguments.Select(QuoteArgument)),
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(converter)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            using (var process = new Process { StartInfo = start })
            {
                process.Start();
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                var exited = await Task.Run(() => process.WaitForExit(180000));
                if (!exited)
                {
                    process.Kill();
                    throw new TimeoutException("FBX conversion exceeded three minutes.");
                }
                var text = await stdout;
                var errors = await stderr;
                if (process.ExitCode != 0) throw new InvalidOperationException("FBX conversion failed:\n" + errors + "\n" + text);
                return text;
            }
        }

        // Windows command-line quoting; no command shell or string-built shell command is used.
        private static string QuoteArgument(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        private static void ValidateOutput(string output, Avatar avatar)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Import animations outside Play mode.");
            if (avatar == null || !avatar.isHuman || !avatar.isValid || !AssetDatabase.Contains(avatar))
                throw new ArgumentException("Assign a valid saved Humanoid Avatar.");
            var assets = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(output);
            if (!output.StartsWith("Assets/", StringComparison.Ordinal) ||
                !absolute.StartsWith(assets, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(output), ".anim", StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(output).StartsWith("Ani_", StringComparison.Ordinal))
                throw new ArgumentException("Output must be an Ani_*.anim path inside Assets.");
            if (File.Exists(absolute) || File.Exists(absolute + ".meta"))
                throw new IOException("Output already exists; it will not be overwritten: " + output);
        }

        /// <summary>Imports an already converted skeleton FBX and leaves only an independent clip.</summary>
        public static AnimationClip ImportConvertedClip(string converted, string output, Avatar avatar, string take = "")
        {
            ValidateOutput(output, avatar);
            var folder = "Assets/__CharacterImport_" + Guid.NewGuid().ToString("N");
            var temporary = folder + "/Animation.fbx";
            bool created = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                int curveCount;
                float duration;
                try
                {
                    File.Copy(converted, temporary, false);
                    AssetDatabase.ImportAsset(temporary, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (ModelImporter)AssetImporter.GetAtPath(temporary);
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                    importer.importAnimation = true;
                    importer.importBlendShapes = false;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.SaveAndReimport();
                    var assets = AssetDatabase.LoadAllAssetsAtPath(temporary);
                    if (assets.OfType<Mesh>().Any()) throw new InvalidOperationException("Unexpected Mesh in converted animation.");
                    var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
                    var matches = clips.Where(c => string.IsNullOrEmpty(take) || c.name == take).ToArray();
                    if (matches.Length != 1)
                        throw new InvalidOperationException("Specify exactly one Take. Available: " + string.Join(", ", clips.Select(c => c.name)));
                    var source = matches[0];
                    if (!source.humanMotion) throw new InvalidOperationException("Imported clip is not Humanoid.");
                    curveCount = AnimationUtility.GetCurveBindings(source).Length;
                    duration = source.length;
                    var clip = UnityEngine.Object.Instantiate(source);
                    clip.name = Path.GetFileNameWithoutExtension(output);
                    clip.hideFlags = HideFlags.None;
                    AssetDatabase.CreateAsset(clip, output);
                    created = true;
                    AssetDatabase.SaveAssetIfDirty(clip);
                }
                finally
                {
                    if (AssetDatabase.IsValidFolder(folder) && !AssetDatabase.DeleteAsset(folder))
                        throw new IOException("Could not clean temporary import folder: " + folder);
                }
                AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
                var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
                if (saved == null || !saved.humanMotion || Mathf.Abs(saved.length - duration) > .00001f ||
                    AnimationUtility.GetCurveBindings(saved).Length != curveCount ||
                    AssetDatabase.GetDependencies(output, true).Any(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Standalone clip verification failed after FBX removal.");
                return saved;
            }
            catch
            {
                if (created) AssetDatabase.DeleteAsset(output);
                throw;
            }
        }
    }
}
