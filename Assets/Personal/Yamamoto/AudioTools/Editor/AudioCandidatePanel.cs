using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     グループの登録方法、選曲設定、登録順を編集する。
    /// </summary>
    internal sealed class AudioCandidatePanel
    {
        /// <summary> フォルダーから見つけた未登録音源の件数を取得する。 </summary>
        internal int FoundCount => _found.Count;

        private List<AudioClip> _found = new List<AudioClip>();
        private readonly HashSet<AudioClip> _checked = new HashSet<AudioClip>();
        private string _scannedFolder;
        private string _registeredSearch = "";
        private int _sourceTab;
        private int _total;
        private bool _editList;
        private bool _addMore;
        private string _folderMessage;

        /// <summary> フォルダーから追加する選択数を取得する。 </summary>
        internal int CheckedCount => _checked.Count;

        /// <summary>
        ///     管理するグループを変えたときに検索表示を初期化する。
        /// </summary>
        internal void ResetSelection()
        {
            _scannedFolder = null;
            _registeredSearch = "";
            _folderMessage = "";
            _addMore = false;
            _editList = false;
            _found.Clear();
            _checked.Clear();
        }

        /// <summary>
        ///     登録や設定の変更だけを下書きへ通知する。
        /// </summary>
        internal bool Draw(AudioGroupDocument doc)
        {
            AudioGroupDraft draft = doc.Draft;
            bool changed = false;
            if (draft.Items.Count > 0)
                _addMore = EditorGUILayout.Foldout(_addMore, "音源を追加", true);
            if (draft.Items.Count == 0 || _addMore)
            {
                if (draft.Items.Count == 0) GUILayout.Label("最初の音源を追加", EditorStyles.boldLabel);
                _sourceTab = GUILayout.Toolbar(_sourceTab, new[] { "フォルダーから", "個別に追加" });
                changed = _sourceTab == 0 ? DrawFolder(draft) : DrawIndividual(draft);
            }

            if (draft.Items.Count == 0) return changed;
            EditorGUILayout.Space(6);
            changed |= DrawRegistered(draft);
            EditorGUILayout.Space(8);
            GUILayout.Label("再生設定", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            draft.Mode = (AudioGroup.Order)EditorGUILayout.Popup("選曲方式", (int)draft.Mode, MODE_NAMES);
            if (draft.Mode == AudioGroup.Order.Fixed && draft.Items.Count > 0)
            {
                string[] options = new[] { "採用する音源を選択" }.Concat(Labels(draft)).ToArray();
                int current = draft.Items.FindIndex(x => x.Id == draft.FixedId) + 1;
                int chosen = EditorGUILayout.Popup("採用する音源", current, options);
                if (chosen != current) draft.FixedId = chosen == 0 ? "" : draft.Items[chosen - 1].Id;
            }

            if (draft.Category == AudioGroup.Kind.Se)
            {
                draft.Limit = EditorGUILayout.IntField("同時再生上限", draft.Limit);
                draft.Interval = EditorGUILayout.FloatField("最短間隔（秒）", draft.Interval);
            }

            return EditorGUI.EndChangeCheck() || changed;
        }

        /// <summary>
        ///     指定されたAudioClipだけを下書きへ追加する。
        /// </summary>
        internal bool DrawIndividual(AudioGroupDraft draft)
        {
            AudioClip clip = (AudioClip)EditorGUILayout.ObjectField("追加する音源", null, typeof(AudioClip), false);
            if (clip == null) return false;
            int count = draft.Items.Count;
            Add(draft, clip);
            _scannedFolder = null;
            return count != draft.Items.Count;
        }

        /// <summary>
        ///     再コンパイルをまたぐフォームのチェックを音源IDで保持する。
        /// </summary>
        internal string[] CheckedIds() => _checked.Select(AudioGroupDraft.Id).ToArray();

        /// <summary>
        ///     現在も候補に残る音源だけを再選択する。
        /// </summary>
        internal void RestoreChecks(AudioGroupDraft draft, string[] ids)
        {
            Scan(draft);
            if (ids != null) _checked.RemoveWhere(clip => !ids.Contains(AudioGroupDraft.Id(clip)));
        }

        private static readonly string[] MODE_NAMES = { "固定", "ランダム", "順番" };

        /// <summary>
        ///     フォルダーを指定した場で未登録の候補を表示する。
        /// </summary>
        internal bool DrawFolder(AudioGroupDraft draft, bool creating = false)
        {
            string path = AssetDatabase.GUIDToAssetPath(draft.Folder);
            DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            bool changed = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                DefaultAsset next = (DefaultAsset)EditorGUILayout.ObjectField("音源フォルダー", folder, typeof(DefaultAsset), false);
                if (folder != next && (next == null || AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(next))))
                {
                    SelectFolder(draft, AssetDatabase.GetAssetPath(next));
                    changed = true;
                }

                if (GUILayout.Button("フォルダーを選ぶ…", GUILayout.Width(116)))
                {
                    string selected = EditorUtility.OpenFolderPanel("音源フォルダーを選択",
                        string.IsNullOrEmpty(path) ? Application.dataPath : Path.GetFullPath(path), "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        string relative = FileUtil.GetProjectRelativePath(selected);
                        if ((relative == "Assets" || relative?.StartsWith("Assets/", StringComparison.Ordinal) == true) &&
                            AssetDatabase.IsValidFolder(relative))
                        {
                            SelectFolder(draft, relative);
                            changed = true;
                        }
                        else _folderMessage = "このプロジェクトのAssets内のフォルダーを選んでください。";
                    }
                }

                if (!string.IsNullOrEmpty(draft.Folder))
                {
                    if (GUILayout.Button(new GUIContent("再検索", "フォルダーに追加した音源を探します。"), GUILayout.Width(62))) _scannedFolder = null;
                }
            }

            if (!string.IsNullOrEmpty(_folderMessage)) EditorGUILayout.HelpBox(_folderMessage, MessageType.Info);
            if (_scannedFolder != draft.Folder) Scan(draft);
            if (string.IsNullOrEmpty(draft.Folder))
            {
                GUILayout.Label("Projectからフォルダーをドロップできます。", EditorStyles.miniLabel);
                return changed;
            }

            if (!AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(draft.Folder)))
            {
                EditorGUILayout.HelpBox("フォルダーが見つかりません。指定し直してください。", MessageType.Warning);
                if (GUILayout.Button("フォルダー指定を解除")) { draft.Folder = ""; return true; }
                return changed;
            }

            if (_found.Count == 0)
            {
                GUILayout.Label(_total == 0 ? "音源が見つかりません。" : "このフォルダーの音源はすべて登録済みです。", EditorStyles.miniLabel);
                return changed;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"未登録の音源　{_found.Count}件", EditorStyles.boldLabel);
                if (GUILayout.Button("全選択", GUILayout.Width(60))) _checked.UnionWith(_found);
                if (GUILayout.Button("選択解除", GUILayout.Width(65))) _checked.Clear();
            }

            string[] names = ClipLabels(_found);
            for (int i = 0; i < _found.Count; i++)
            {
                AudioClip clip = _found[i];
                bool enabled = EditorGUILayout.ToggleLeft(new GUIContent(names[i], AssetDatabase.GetAssetPath(clip)), _checked.Contains(clip));
                if (enabled) _checked.Add(clip);
                else _checked.Remove(clip);
            }

            if (creating) return changed;
            using (new EditorGUI.DisabledScope(_checked.Count == 0))
            {
                if (GUILayout.Button($"選んだ{_checked.Count}件をグループに追加"))
                {
                    AddChecked(draft);
                    return true;
                }
            }

            return changed;
        }

        /// <summary>
        ///     登録済みの並びを表示し、元の音声ファイルに触れずに編集する。
        /// </summary>
        private bool DrawRegistered(AudioGroupDraft draft)
        {
            GUILayout.Label($"登録した音源　{draft.Items.Count}件", EditorStyles.boldLabel);
            if (draft.Items.Count == 0) return false;
            _editList = EditorGUILayout.Foldout(_editList, "登録順・登録解除", true);
            if (draft.Items.Count > 5 || !string.IsNullOrEmpty(_registeredSearch)) _registeredSearch = EditorGUILayout.TextField("音源を検索", _registeredSearch);
            string[] names = Labels(draft);
            for (int i = 0; i < draft.Items.Count; i++)
            {
                if (names[i].IndexOf(_registeredSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                AudioGroupDraft.Item item = draft.Items[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    string mark = draft.Mode == AudioGroup.Order.Fixed && draft.FixedId == item.Id ? "（採用中）" : "";
                    GUILayout.Label($"{i + 1}. {names[i]}{mark}", EditorStyles.wordWrappedLabel);
                    if (!_editList && draft.Mode != AudioGroup.Order.Sequential) continue;
                    int target = i;
                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("上へ", GUILayout.Width(42))) target--;
                    }

                    using (new EditorGUI.DisabledScope(i + 1 == draft.Items.Count))
                    {
                        if (GUILayout.Button("下へ", GUILayout.Width(42))) target++;
                    }

                    if (target != i)
                    {
                        draft.Items.RemoveAt(i);
                        draft.Items.Insert(target, item);
                        return true;
                    }

                    if (_editList && GUILayout.Button(new GUIContent("登録解除", "このグループの登録だけを解除します。"), GUILayout.Width(68)))
                    {
                        draft.Items.RemoveAt(i);
                        if (item.Id == draft.FixedId) draft.FixedId = "";
                        _scannedFolder = null;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     フォルダー変更時に登録せず候補だけを取得する。
        /// </summary>
        internal void SelectFolder(AudioGroupDraft draft, string path)
        {
            _folderMessage = "";
            draft.Folder = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
            Scan(draft);
        }

        /// <summary>
        ///     表示順にチェックした候補を末尾へ追加する。
        /// </summary>
        internal void AddChecked(AudioGroupDraft draft)
        {
            foreach (AudioClip clip in _found.Where(_checked.Contains)) { Add(draft, clip); }
            Scan(draft);
        }

        /// <summary>
        ///     指定フォルダーを再帰検索し、登録前の選択を整える。
        /// </summary>
        internal void Scan(AudioGroupDraft draft)
        {
            _scannedFolder = draft.Folder;
            string folder = AssetDatabase.GUIDToAssetPath(draft.Folder);
            _found.Clear();
            _checked.Clear();
            _total = 0;
            if (!AssetDatabase.IsValidFolder(folder)) return;
            _found = Discover(draft, folder, out _total);
            _checked.UnionWith(_found);
        }

        /// <summary>
        ///     グループの候補を識別できる表示名にする。
        /// </summary>
        internal static string[] Labels(AudioGroupDraft draft) =>
            ClipLabels(draft.Items.Select(x => AudioGroupDraft.Clip(x.Id)).ToList());

        /// <summary>
        ///     同じ音源名が複数あるときは配置場所を添える。
        /// </summary>
        private static string[] ClipLabels(List<AudioClip> clips)
        {
            var duplicates = new HashSet<string>(clips.Where(x => x != null).GroupBy(x => x.name)
                .Where(x => x.Count() > 1).Select(x => x.Key));
            return clips.Select(x => x == null ? "参照切れ" : duplicates.Contains(x.name)
                ? $"{x.name}（{Path.GetDirectoryName(AssetDatabase.GetAssetPath(x))}）" : x.name).ToArray();
        }

        /// <summary>
        ///     音源を登録し、最初の候補だけを初期採用にする。
        /// </summary>
        internal static void Add(AudioGroupDraft draft, AudioClip clip)
        {
            string id = AudioGroupDraft.Id(clip);
            if (id == "" || draft.Items.Exists(x => x.Id == id)) return;
            draft.Items.Add(new AudioGroupDraft.Item { Id = id });
            if (draft.Items.Count == 1) draft.FixedId = id;
        }

        /// <summary>
        ///     登録していない音源だけを列挙する。
        /// </summary>
        internal static List<AudioClip> Discover(AudioGroupDraft draft, string folder) => Discover(draft, folder, out _);

        /// <summary>
        ///     検索結果が空の理由を表示するため全音源数も取得する。
        /// </summary>
        private static List<AudioClip> Discover(AudioGroupDraft draft, string folder, out int total)
        {
            List<AudioClip> clips = AssetDatabase.FindAssets("t:AudioClip", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Distinct().OrderBy(x => x, StringComparer.Ordinal).SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<AudioClip>().Distinct().ToList();
            total = clips.Count;
            var ids = new HashSet<string>(draft.Items.Select(x => x.Id));
            return clips.Where(x => !ids.Contains(AudioGroupDraft.Id(x))).ToList();
        }
    }
}
