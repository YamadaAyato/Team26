using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     初回登録を一つの画面にまとめ、作成を確定するまで他の下書きから分離する。
    /// </summary>
    internal sealed partial class AudioToolsWindow
    {
        [SerializeField] private bool _isCreating;
        [SerializeField] private string[] _checkedCreationClips;
        private AudioGroupDraft _creation;
        private AudioGroupDraft _folderCreation;
        private AudioCandidatePanel _setupPanel;
        private string _creationName;
        private bool _fromFolder;
        private bool _manualCreationName;

        /// <summary> 作成途中の入力があるときは検証用画面への置換を避ける。 </summary>
        internal bool IsCreating => _isCreating;

        /// <summary>
        ///     初期画面では作業の入口だけを表示する。
        /// </summary>
        private void DrawStart()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("BGM・SE 調整", TitleStyle());
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("トリセツ", "保存した音をゲームから鳴らす手順"), GUILayout.Width(88)))
                {
                    AudioToolsManual.Open();
                }
            }

            GUILayout.Label("BGM・SEを聴きながら音量を調整し、ゲームで使う設定を作ります。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8);
            if (!HasRegisteredClips()) EditorGUILayout.HelpBox("まずグループを作成し、最初の音源を登録してください。", MessageType.Info);
            var caption = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    if (GUILayout.Button("新規グループ作成", GUILayout.Height(36))) BeginCreation();
                    GUILayout.Label("新しく使う音源を登録", caption);
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    if (GUILayout.Button("登録済みグループを編集", GUILayout.Height(36))) SelectTab(Page.Library);
                    GUILayout.Label("設定の変更・削除、音源の追加", caption);
                }
            }

            EditorGUILayout.Space(12);
            GUILayout.Label("基本の使い方", EditorStyles.boldLabel);
            GUILayout.Label("① 音源を登録　→　② 専用Sceneで試聴・調整　→　③ 設定を保存", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);
            GUILayout.Label("BGM／SEグループとは？", EditorStyles.boldLabel);
            GUILayout.Label("探索BGMや足音など、用途ごとに音源をまとめた設定です。ひとつのグループに複数の候補を登録できます。", EditorStyles.wordWrappedLabel);
        }

        /// <summary>
        ///     保存済み設定・現在の下書きに試聴できる候補があるかを調べる。
        /// </summary>
        private bool HasRegisteredClips()
        {
            if (_state.Documents.Any(x => !x.DeletePending && x.Draft.Items.Any(item => AudioGroupDraft.Clip(item.Id) != null))) return true;
            foreach (string path in _paths)
            {
                if (_state.Documents.Any(x => x.Path == path)) continue;
                AudioGroup group = AssetDatabase.LoadAssetAtPath<AudioGroup>(path);
                if (group == null) continue;
                for (int i = 0; i < group.Count; i++)
                {
                    if (group.GetClip(i) != null) return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     入力フォーム用の一時データを準備する。
        /// </summary>
        internal AudioGroupDraft BeginCreation(AudioGroup.Kind kind = AudioGroup.Kind.Bgm, bool fromFolder = true)
        {
            _action = GroupAction.None;
            _manualCreationName = false;
            _isCreating = true;
            _creation = new AudioGroupDraft { Category = kind };
            _folderCreation = new AudioGroupDraft();
            _setupPanel = new AudioCandidatePanel();
            _creationName = "";
            _fromFolder = fromFolder;
            _message = "";
            _scroll = Vector2.zero;
            _drawer = Drawer.Closed;
            return fromFolder ? _folderCreation : _creation;
        }

        /// <summary>
        ///     グループを追加せず、呼び出し元の画面へ戻る。
        /// </summary>
        internal void CancelCreation()
        {
            _isCreating = false;
            _checkedCreationClips = null;
            _creation = null;
            _folderCreation = null;
            _setupPanel = null;
            _message = "";
            _scroll = Vector2.zero;
        }

        /// <summary>
        ///     選んだ登録方法だけを表示する。
        /// </summary>
        private void DrawCreation()
        {
            GUILayout.Label("新規グループ作成", TitleStyle());
            EditorGUILayout.Space(8);
            _creation.Category = (AudioGroup.Kind)GUILayout.Toolbar((int)_creation.Category, new[] { "BGM", "SE" }, GUILayout.Height(24));
            EditorGUILayout.Space(8);
            GUILayout.Label("登録する音源", EditorStyles.boldLabel);
            _fromFolder = GUILayout.Toolbar(_fromFolder ? 0 : 1, new[] { "フォルダーからまとめて登録", "音源を個別に登録" }) == 0;
            EditCreationName(EditorGUILayout.TextField("グループ名", _creationName));
            if (_fromFolder)
            {
                if (_setupPanel.DrawFolder(_folderCreation, true)) SuggestCreationName(AssetDatabase.GUIDToAssetPath(_folderCreation.Folder));
            }
            else
            {
                _setupPanel.DrawIndividual(_creation);
                string[] names = AudioCandidatePanel.Labels(_creation);
                for (int i = 0; i < names.Length; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(names[i]);
                        if (GUILayout.Button("外す", GUILayout.Width(54)))
                        {
                            _creation.Items.RemoveAt(i);
                            _creation.FixedId = _creation.Items.FirstOrDefault()?.Id ?? "";
                            break;
                        }
                    }
                }
            }
        }

        /// <summary> 手動で編集するまでは選択フォルダーの末尾を候補名に使う。 </summary>
        internal string SuggestCreationName(string folder)
        {
            if (!_manualCreationName) _creationName = Path.GetFileName(folder);
            return _creationName;
        }

        /// <summary> 手入力した名前を、その後のフォルダー選択から保護する。 </summary>
        internal void EditCreationName(string name)
        {
            if (name == _creationName) return;
            _manualCreationName = true;
            _creationName = name;
        }

        /// <summary>
        ///     既存Assetや作業中のグループと衝突する名前を受け付けない。
        /// </summary>
        private string CreationIssue(string name)
        {
            string issue = NameProblem(name);
            if (issue != "") return issue;
            if ((_fromFolder ? _setupPanel.CheckedCount : _creation.Items.Count) == 0) return "登録する音源を選んでください。";
            return "";
        }

        /// <summary>
        ///     初回登録を確定し、該当する用途のグループだけを選択する。
        /// </summary>
        internal bool FinishCreation(string name)
        {
            name = name.Trim();
            if (CreationIssue(name) != "") return false;
            AudioGroupDraft draft = _fromFolder ? _folderCreation : _creation;
            if (_fromFolder) _setupPanel.AddChecked(draft);
            if (draft.Items.Count == 0) { _message = "登録する音源を選び直してください。"; return false; }
            draft.Category = _creation.Category;
            string path = AudioGroupDocument.DATA + name + ".asset";
            _state.Documents.Add(new AudioGroupDocument
            {
                Path = path, New = true, Baseline = new AudioGroupDraft { Category = draft.Category }, Draft = draft
            });
            CancelCreation();
            _saveResult = "";
            _search = "";
            _categoryFilter = 0;
            _state.LastEditedPath = path;
            Channel(draft.Category).SelectGroup(path);
            SelectTab(Page.Library);
            SelectGroup(path);
            return true;
        }

        /// <summary>
        ///     表示中のスキンに合わせて画面見出しを作成する。
        /// </summary>
        private static GUIStyle TitleStyle() => new GUIStyle(EditorStyles.boldLabel) { fontSize = 17, fixedHeight = 26 };
    }
}
