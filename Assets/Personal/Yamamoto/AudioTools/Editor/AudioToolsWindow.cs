using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     BGM・SEのグループを登録し、保存済み音量と下書きを比較する。
    /// </summary>
    internal sealed partial class AudioToolsWindow : EditorWindow
    {
        internal const string MENU = "Tools/Yamamoto/BGM・SE 調整";
        internal const string SCENE = AudioGroupDocument.ROOT + "/Scenes/AudioPreview.unity";
        internal const string NEXT_SCENE = AudioGroupDocument.ROOT + "/Scenes/AudioPreviewNext.unity";
        internal const string SESSION = "Yamamoto.AudioTools.Storage.1";
        private const string RESTORE = "Yamamoto.AudioTools.RestoreScene";

        /// <summary> 選択グループの下書きを取得する。 </summary>
        internal AudioGroupDocument SelectedDocument => _state.Documents.Find(x => x.Path == _state.Path);

        private State _state;
        private string[] _paths = Array.Empty<string>();
        private readonly AudioToolsTransport _transport = new AudioToolsTransport();
        private AudioCandidatePanel _candidates;
        private Vector2 _scroll;
        private string _search = "";
        private int _categoryFilter;
        private string _message = "";

        /// <summary>
        ///     初期表示で音源の登録・管理・試聴の入口を開く。
        /// </summary>
        [MenuItem(MENU)]
        internal static void Open()
        {
            bool existing = HasOpenInstances<AudioToolsWindow>();
            AudioToolsWindow window = GetWindow<AudioToolsWindow>("BGM・SE 調整");
            if (existing) return;
            window._state.Tab = Page.Audition;
            window.SetComparison(true, AudioToolsTransport.Player());
            if (window.docked || window.position.width >= 720f) return;

            // 初めて開く小さなフローティング窓でも、両方の操作欄を同時に見せる。
            Rect desktop = EditorGUIUtility.GetMainWindowPosition();
            float width = Mathf.Min(820f, desktop.width);
            float height = Mathf.Min(490f, desktop.height);
            window.position = new Rect(desktop.center.x - width * 0.5f, desktop.center.y - height * 0.5f, width, height);
        }

        /// <summary>
        ///     Inspectorで指定したグループの登録内容を開く。
        /// </summary>
        internal static void OpenGroup(string path)
        {
            Open();
            AudioToolsWindow window = GetWindow<AudioToolsWindow>();
            window._state.Tab = Page.Library;
            window._categoryFilter = 0;
            window._search = "";
            window.SelectGroup(path);
        }

        private void OnEnable()
        {
            minSize = new Vector2(420f, 390f);
            _candidates = new AudioCandidatePanel();
            if (_isCreating)
            {
                _creation ??= new AudioGroupDraft();
                _folderCreation ??= new AudioGroupDraft();
                _creationName ??= "";
                _setupPanel = new AudioCandidatePanel();
                _setupPanel.RestoreChecks(_folderCreation, _checkedCreationClips);
            }
            RestoreSession(SessionState.GetString(SESSION, ""));
            RefreshPaths();
            EditorApplication.playModeStateChanged += HandlePlayState;
        }

        /// <summary>
        ///     旧版の下書きを含むセッションを復元する。
        /// </summary>
        internal void RestoreSession(string retained)
        {
            _action = GroupAction.None;
            _state = string.IsNullOrEmpty(retained) ? new State() : JsonUtility.FromJson<State>(retained);
            _message = "";
            _saveResult = "";
            _saveFailures = "";
            _drawer = Drawer.Closed;
            _state.Documents ??= new List<AudioGroupDocument>();
            _state.Bgm ??= new Selection();
            _state.Se ??= new Selection();
            _candidates?.ResetSelection();
            Remember();
        }

        private void OnDisable()
        {
            if (_isCreating && _setupPanel != null) _checkedCreationClips = _setupPanel.CheckedIds();
            EditorApplication.playModeStateChanged -= HandlePlayState;
            Remember();
            _transport.Clear();
        }

        private void OnProjectChange()
        {
            RefreshPaths();
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            GroupAudioPlayer player = AudioToolsTransport.Player();
            EditorGUIUtility.labelWidth = position.width < 540f ? 100f : 135f;
            bool preview = OwnsPreview();
            bool foreignPlay = EditorApplication.isPlaying && !preview;
            if (preview) DrawHeader(player);
            bool review = _drawer != Drawer.Closed;
            bool creating = _isCreating;
            if (preview && !creating && !review)
            {
                SelectTab((Page)GUILayout.Toolbar((int)_state.Tab, new[] { "試聴・音量調整", "登録済みグループを編集", "BGM切替試験" }, GUILayout.Height(28)));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(10, 10, 9, 8) }))
            {
                if (!string.IsNullOrEmpty(_saveFailures)) EditorGUILayout.HelpBox(_saveFailures, MessageType.Warning);
                if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
                if (foreignPlay) EditorGUILayout.HelpBox("通常のPlayを終了すると、登録・試聴・保存ができます。", MessageType.Info);
                else if (creating) DrawCreation();
                else if (review) DrawSaveDetails();
                else if (_state.Tab == Page.Library) DrawManagement();
                else if (!preview) DrawStart();
                else if (_state.Tab == Page.Transition) DrawTransitions(player);
                else DrawAudition(player);
            }

            EditorGUILayout.EndScrollView();
            DrawFooter(preview, foreignPlay);
        }

        /// <summary>
        ///     管理対象の選択と登録設定を描画する。
        /// </summary>
        private void DrawManagement()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("登録済みグループを編集", TitleStyle());
                GUILayout.FlexibleSpace();
                if (!OwnsPreview() && GUILayout.Button("開始画面へ", GUILayout.Width(90))) SelectTab(Page.Audition);
                if (GUILayout.Button("新規グループ作成", GUILayout.Width(134))) BeginCreation();
            }

            if (!DrawLibrary() || SelectedDocument == null) return;
            AudioGroupDocument doc = SelectedDocument;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"{GroupName(doc.Path)}　｜　{(doc.Draft.Category == AudioGroup.Kind.Bgm ? "BGM" : "SE")}", EditorStyles.boldLabel);
                if (DrawGroupActions(doc)) return;
                EditorGUILayout.Space(5);
                if (_candidates.Draw(doc)) Changed(doc);
                if (GUILayout.Button(doc.New ? "この下書きを破棄…" : "保存値を再読込…", EditorStyles.miniButton)) OpenReview(Drawer.Reload);
            }
        }

        /// <summary>
        ///     グループ名と登録音源名から管理対象を検索する。
        /// </summary>
        private bool DrawLibrary()
        {
            _categoryFilter = GUILayout.Toolbar(_categoryFilter, new[] { "すべて", "BGM", "SE" });
            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField("グループ・候補の検索", _search);
            EditorGUILayout.EndHorizontal();

            IEnumerable<string> paths = _paths.Concat(_state.Documents.Where(x => x.Dirty).Select(x => x.Path)).Distinct();
            var filtered = new List<string>();
            var labels = new List<string>();
            foreach (string path in paths)
            {
                AudioGroupDocument draft = _state.Documents.Find(x => x.Path == path);
                AudioGroup group = AssetDatabase.LoadAssetAtPath<AudioGroup>(path);
                bool match = Match(GroupName(path));
                if (!match && draft != null) match = draft.Draft.Items.Any(x => Match(AudioGroupDraft.NameOf(x.Id)));
                if (!match && group != null)
                {
                    for (int i = 0; i < group.Count; i++)
                    {
                        if (group.GetClip(i) != null && Match(group.GetClip(i).name)) match = true;
                    }
                }

                if (!match || (group == null && draft == null)) continue;
                AudioGroup.Kind category = draft?.Draft.Category ?? group.Category;
                if (_categoryFilter != 0 && (int)category + 1 != _categoryFilter) continue;
                filtered.Add(path);
                string missing = group == null && draft?.New == false ? "（保存先不明）" : "";
                labels.Add($"[{(category == AudioGroup.Kind.Bgm ? "BGM" : "SE")}] {GroupName(path)}{(draft?.DeletePending == true ? "（削除予定）" : "")}{missing}{(draft?.Dirty == true ? " ＊" : "")}");
            }

            if (filtered.Count == 0)
            {
                EditorGUILayout.HelpBox("該当グループがありません。", MessageType.Info);
                return false;
            }

            int index = filtered.IndexOf(_state.Path);
            string[] options = new[] { "グループを選択" }.Concat(labels).ToArray();
            int next = EditorGUILayout.Popup("グループ一覧", index + 1, options) - 1;
            if (next != index)
            {
                if (next >= 0) SelectGroup(filtered[next]);
                else { _state.Path = null; Remember(); }
            }

            return next >= 0;
        }

        /// <summary>
        ///     再生後に元の開始Scene設定を復元する。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void InstallRestore() => EditorApplication.playModeStateChanged += RestoreScene;

        /// <summary>
        ///     ツールが開始Sceneを一時指定した場合だけ元に戻す。
        /// </summary>
        private static void RestoreScene(PlayModeStateChange change)
        {
            string old = SessionState.GetString(RESTORE, "?");
            if (change != PlayModeStateChange.EnteredEditMode || old == "?") return;
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(old) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(old);
            SessionState.EraseString(RESTORE);
        }

        /// <summary>
        ///     編集中のSceneを閉じず、専用Sceneを開始Sceneとして試聴する。
        /// </summary>
        internal static void BeginPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SceneAsset preview = AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE);
            if (preview == null) { Debug.LogError($"[{nameof(AudioToolsWindow)}] 専用Sceneが見つかりません。"); return; }
            SessionState.SetString(RESTORE, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = preview;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        ///     下書きを保護し、終了時に試聴用のメモリを解放する。
        /// </summary>
        private void HandlePlayState(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) _transport.Clear();
            Remember();
            Repaint();
        }

        /// <summary>
        ///     管理欄だけの選択を変更する。
        /// </summary>
        internal void SelectGroup(string path)
        {
            if (GetDocument(path) == null) return;
            _action = GroupAction.None;
            _state.Path = path;
            _candidates.ResetSelection();
            Remember();
        }

        /// <summary>
        ///     パネル間で同じグループの下書きを共有する。
        /// </summary>
        private AudioGroupDocument GetDocument(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(AudioGroupDocument.DATA, StringComparison.Ordinal)) return null;
            AudioGroupDocument doc = _state.Documents.Find(x => x.Path == path);
            if (doc != null) return doc;
            try
            {
                doc = AudioGroupDocument.Load(path);
                _state.Documents.Add(doc);
                return doc;
            }
            catch (Exception ex)
            {
                _message = ex.Message;
                return null;
            }
        }

        /// <summary>
        ///     一覧用の短いグループ名を返す。
        /// </summary>
        private string GroupName(string path) => Path.GetFileNameWithoutExtension(_state.Documents.Find(x => x.Path == path)?.TargetPath ?? path);

        /// <summary>
        ///     下書きの保持と、演奏中の基準ゲイン更新を行う。
        /// </summary>
        internal void Changed(AudioGroupDocument doc)
        {
            _saveResult = "";
            _state.LastEditedPath = doc.Path;
            Remember();
            GroupAudioPlayer player = AudioToolsTransport.Player();
            if (player != null) _transport.Refresh(doc, _state.ListenB, player);
        }

        /// <summary>
        ///     Unityのセッションに下書きと選択状態を保持する。
        /// </summary>
        private void Remember()
        {
            if (_state != null) SessionState.SetString(SESSION, JsonUtility.ToJson(_state));
        }

        /// <summary>
        ///     専用の保存フォルダーだけを検索する。
        /// </summary>
        private void RefreshPaths() => _paths = AssetDatabase.FindAssets("t:AudioGroup", new[] { AudioGroupDocument.DATA.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        /// <summary>
        ///     大文字・小文字によらず検索語を照合する。
        /// </summary>
        private bool Match(string value) => value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        ///     表示だけを切り替え、選択・発音・下書きを保持する。
        /// </summary>
        internal void SelectTab(Page next)
        {
            if (_state.Tab == next) return;
            _action = GroupAction.None;
            _state.Tab = next;
            _scroll = Vector2.zero;
            Remember();
        }

        internal enum Page { Audition, Library, Transition }

        /// <summary>
        ///     グループと試聴対象を安定した参照で保持する。
        /// </summary>
        [Serializable]
        internal sealed class Selection
        {
            public string Path;
            public string ClipId;
            public string Search = "";
            public bool TestOrder;

            /// <summary> 新しいグループに合わせて音源検索と曲の選択だけを戻す。 </summary>
            internal void SelectGroup(string path)
            {
                if (Path == path) return;
                Path = path;
                Search = "";
                ClipId = null;
            }
        }

        /// <summary>
        ///     ウィンドウを閉じてもUnity終了までは保持する調整状態。
        /// </summary>
        [Serializable]
        private sealed class State
        {
            public List<AudioGroupDocument> Documents = new List<AudioGroupDocument>();
            public Page Tab;
            public Selection Bgm = new Selection();
            public Selection Se = new Selection();
            public string SourcePath;
            public string DestinationPath;
            public float Seconds = 2f;
            public bool Loop = true;
            public string Path;
            public string LastEditedPath;
            public bool ListenB = true;
            public float Monitor = 0.1f;
        }
    }
}
