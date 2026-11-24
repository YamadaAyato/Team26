using System;
using System.IO;
using System.Linq;
using FilePath = System.IO.Path;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     一つのグループの読込時点、下書き、保存先を保持する。
    /// </summary>
    [Serializable]
    internal sealed class AudioGroupDocument
    {
        internal const string ROOT = "Assets/Personal/Yamamoto/AudioTools";
        internal const string DATA = ROOT + "/Data/";

        public string Path;
        public string AssetId;
        public string DiskHash;
        public bool New;
        public string PendingName;
        public bool DeletePending;
        public AudioGroupDraft Baseline;
        public AudioGroupDraft Draft;

        /// <summary> 明示保存が必要か取得する。 </summary>
        internal bool Dirty => New || DeletePending || !string.IsNullOrEmpty(PendingName) || JsonUtility.ToJson(Baseline) != JsonUtility.ToJson(Draft);

        /// <summary>
        ///     既存の専用Assetから比較元と独立した下書きを読み込む。
        /// </summary>
        internal static AudioGroupDocument Load(string path)
        {
            var group = AssetDatabase.LoadAssetAtPath<AudioGroup>(path);
            if (group == null) throw new InvalidDataException("保存済みグループを読み取れません。");
            return new AudioGroupDocument
            {
                Path = path, AssetId = AssetDatabase.AssetPathToGUID(path), DiskHash = Fingerprint(path),
                Baseline = AudioGroupDraft.Read(group), Draft = AudioGroupDraft.Read(group)
            };
        }

        /// <summary>
        ///     一件単位で競合を検査し、保存が検証できた場合のみ新しい基準を返す。
        /// </summary>
        internal AudioGroupDocument Save()
        {
            ValidatePath(Path);
            string destination = TargetPath;
            ValidatePath(destination);
            if (!string.IsNullOrEmpty(PendingName) && NameIssue(PendingName) != "")
                throw new IOException(NameIssue(PendingName));

            AudioGroup group = New ? null : GetUnchangedAsset();
            if (DeletePending || (!New && Draft.Category != Baseline.Category))
            {
                string issue = AudioGroupReferences.BlockReason(this);
                if (issue != "") throw new IOException(issue);
                if (!New) group = GetUnchangedAsset();
            }

            if (DeletePending)
            {
                if (New || AssetDatabase.LoadAllAssetsAtPath(Path).Any(x => x != null && x != group))
                    throw new IOException("このファイルには削除対象以外のデータがあります。");
                if (!AssetDatabase.MoveAssetToTrash(Path)) throw new IOException("グループをゴミ箱へ移動できませんでした。");
                return null;
            }

            string error = Draft.Validate();
            if (error != "") throw new InvalidDataException(error);
            bool moving = !New && destination != Path;
            if ((New || moving) && Exists(destination)) throw new IOException("同じ名前のAssetが既にあります。");

            // 改名と設定変更は一件として確定する。失敗時は作業開始時の内容へ戻す。
            byte[] original = New ? null : File.ReadAllBytes(Path);
            string workingPath = Path;
            bool wrote = false;
            try
            {
                if (moving)
                {
                    Move(Path, destination);
                    workingPath = destination;
                }

                if (New)
                {
                    group = ScriptableObject.CreateInstance<AudioGroup>();
                    Draft.Apply(group);
                    AssetDatabase.CreateAsset(group, destination);
                    wrote = true;
                }
                else if (JsonUtility.ToJson(Draft) != JsonUtility.ToJson(Baseline))
                {
                    wrote = true;
                    Draft.Apply(group);
                    EditorUtility.SetDirty(group);
                    AssetDatabase.SaveAssetIfDirty(group);
                }

                if (!File.Exists(destination) || EditorUtility.IsDirty(group))
                    throw new IOException("設定ファイルへの書き込みを確認できませんでした。");
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                AudioGroupDocument saved = Load(destination);
                if ((!New && saved.AssetId != AssetId) || JsonUtility.ToJson(saved.Baseline) != JsonUtility.ToJson(Draft))
                    throw new IOException("保存後の読み戻しに失敗しました。");
                return saved;
            }
            catch
            {
                if (New && wrote && AssetDatabase.GetAssetPath(group) == destination)
                    AssetDatabase.MoveAssetToTrash(destination);
                if (!New && (wrote || workingPath != Path) && AssetDatabase.AssetPathToGUID(workingPath) == AssetId)
                {
                    if (workingPath != Path) Move(workingPath, Path);
                    File.WriteAllBytes(Path, original);
                    AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }

                throw;
            }
        }

        /// <summary> 保存時に使用するパスを取得する。保存前の選択は元のパスを使う。 </summary>
        internal string TargetPath => string.IsNullOrEmpty(PendingName) ? Path :
            Path.Substring(0, Path.LastIndexOf('/') + 1) + PendingName + ".asset";

        /// <summary>
        ///     Assetとメタファイルの衝突を確認する。
        /// </summary>
        internal static bool Exists(string path) => File.Exists(path) || Directory.Exists(path) ||
            File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null;

        /// <summary>
        ///     別フォルダー・Windows予約名・大小文字の衝突へ書き込まない。
        /// </summary>
        internal static string NameIssue(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "グループ名を入力してください。";
            if (name != name.Trim() || name.StartsWith(".") || name.EndsWith(".") ||
                name.IndexOfAny(FilePath.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\') ||
                System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return "ファイル名に使用できない文字・名前です。";
            return "";
        }

        /// <summary>
        ///     保存先が専用Data内にあり、リンクを経由しないことを確認する。
        /// </summary>
        private static void ValidatePath(string path)
        {
            string full = FilePath.GetFullPath(path);
            string root = FilePath.GetFullPath(DATA).TrimEnd(FilePath.DirectorySeparatorChar) + FilePath.DirectorySeparatorChar;
            if (!path.StartsWith(DATA, StringComparison.Ordinal) || path.Split('/').Any(x => x == ".." || x == ".") ||
                !full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || FilePath.GetExtension(full) != ".asset")
                throw new IOException("専用Data外へは保存できません。");
            for (DirectoryInfo dir = new FileInfo(full).Directory; dir != null; dir = dir.Parent)
            {
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("リンクされた保存先は使用できません。");
            }

            foreach (string file in new[] { full, full + ".meta" })
            {
                if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("リンクされたファイルは変更できません。");
            }
        }

        /// <summary>
        ///     削除・改名・内容更新に共通する競合を、書き込みより先に検出する。
        /// </summary>
        private AudioGroup GetUnchangedAsset()
        {
            AudioGroup group = AssetDatabase.LoadMainAssetAtPath(Path) as AudioGroup;
            if (group == null || AssetDatabase.AssetPathToGUID(Path) != AssetId || AssetDatabase.GUIDToAssetPath(AssetId) != Path || Fingerprint(Path) != DiskHash ||
                EditorUtility.IsDirty(group) || JsonUtility.ToJson(AudioGroupDraft.Read(group)) != JsonUtility.ToJson(Baseline))
                throw new IOException("読込後に別の変更がありました。下書きは保持しています。");
            if ((File.GetAttributes(Path) & FileAttributes.ReadOnly) != 0 || !AssetDatabase.IsOpenForEdit(group))
                throw new IOException("保存先が書き込み可能ではありません。");
            if ((DeletePending || TargetPath != Path) && (!File.Exists(Path + ".meta") ||
                !File.ReadLines(Path + ".meta").Contains("guid: " + AssetId) ||
                (File.GetAttributes(Path + ".meta") & FileAttributes.ReadOnly) != 0 || !AssetDatabase.IsOpenForEdit(Path + ".meta")))
                throw new IOException("メタファイルを書き込めません。");
            return group;
        }

        /// <summary>
        ///     メタファイルと一緒にUnityのAPIで改名する。
        /// </summary>
        private static void Move(string from, string to)
        {
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }

        /// <summary>
        ///     タイムスタンプだけでは検出できない外部編集を検査する。
        /// </summary>
        private static string Fingerprint(string path)
        {
            using var hash = SHA256.Create();
            using var input = File.OpenRead(path);
            return Convert.ToBase64String(hash.ComputeHash(input));
        }
    }
}
