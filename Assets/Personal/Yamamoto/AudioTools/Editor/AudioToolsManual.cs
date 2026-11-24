using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     ゲーム側のInspectorへの割り当てと、保存済みグループを使うコードを案内する。
    /// </summary>
    internal sealed class AudioToolsManual : EditorWindow
    {
        private AudioGroup _group;
        private AudioGroup[] _groups = Array.Empty<AudioGroup>();
        private string[] _choices = Array.Empty<string>();
        private Vector2 _scroll;
        private bool _showSample;
        private bool _showDetails;

        private const string PREFAB = AudioGroupDocument.ROOT + "/Runtime/GroupAudioPlayer.prefab";
        private const string INHALE = AudioGroupDocument.DATA + "Player_Inhale.asset";

        /// <summary>
        ///     編集セッションから独立した説明画面を、吸い込みSEの手順から開く。
        /// </summary>
        internal static void Open()
        {
            AudioToolsManual window = GetWindow<AudioToolsManual>(true, "トリセツ");
            window.minSize = new Vector2(420f, 395f);
            window._group = AssetDatabase.LoadAssetAtPath<AudioGroup>(INHALE);
            window._scroll = Vector2.zero;
            window._showSample = false;
            window._showDetails = false;
            Rect desktop = EditorGUIUtility.GetMainWindowPosition();
            float width = Mathf.Min(640f, desktop.width);
            float height = Mathf.Min(530f, desktop.height);
            window.position = new Rect(desktop.center.x - width / 2, desktop.center.y - height / 2, width, height);
            window.Focus();
        }

        private void OnEnable() => RefreshGroups();

        private void OnProjectChange()
        {
            RefreshGroups();
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (_showSample) Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(14, 14, 9, 10) }))
            {
                bool bgm = _group != null && _group.Category == AudioGroup.Kind.Bgm;
                bool inhale = !bgm && (_group == null || AssetDatabase.GetAssetPath(_group) == INHALE);
                string slot = bgm ? "BGM" : inhale ? "吸い込みSE" : "効果音";
                string field = bgm ? "_bgm" : inhale ? "_inhaleSe" : "_se";
                GUILayout.Label(bgm ? "保存したBGMを流す" : slot + "を1回鳴らす", EditorStyles.boldLabel);
                DrawPlacement();
                DrawAssignment(slot, field);
                DrawCall(bgm, inhale, field);

                EditorGUILayout.Space(5);
                _showSample = EditorGUILayout.Foldout(_showSample, "専用SceneでSE・BGM切替を試す", true);
                if (_showSample) DrawSample();
                _showDetails = EditorGUILayout.Foldout(_showDetails, "停止・Scene遷移・独自asmdef", true);
                if (_showDetails) DrawDetails();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        ///     保存された設定だけを一覧にする。編集中の下書きには触れない。
        /// </summary>
        private void RefreshGroups()
        {
            _groups = AssetDatabase.FindAssets("t:AudioGroup", new[] { AudioGroupDocument.DATA.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AudioGroup>).Where(group => group != null).ToArray();
            _choices = new[] { "保存済みグループを選択" }.Concat(_groups.Select(group => group.name)).ToArray();
        }

        /// <summary>
        ///     Sceneに置く再生部品のProject上の場所を示す。
        /// </summary>
        private static void DrawPlacement()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("① 再生部品を置く", EditorStyles.boldLabel);
                GUILayout.Label("GroupAudioPlayer.prefab を、音を指示するSceneのHierarchyに1個置きます。", EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("再生用PrefabをProjectで表示")) PingAsset(AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB));
            }
        }

        /// <summary>
        ///     コードを貼る位置と、ドラッグ元からInspectorの欄への対応を示す。
        /// </summary>
        private void DrawAssignment(string slot, string field)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("② Inspectorに割り当てる", EditorStyles.boldLabel);
                int index = Array.IndexOf(_groups, _group) + 1;
                int next = EditorGUILayout.Popup("使うグループ", index, _choices);
                if (next != index)
                {
                    _group = next == 0 ? null : _groups[next - 1];
                    GUIUtility.ExitGUI();
                }

                string declarations = "[Header(\"再生部品\"), SerializeField] private GroupAudioPlayer _audioPlayer;\n" +
                    $"[Header(\"{slot}\"), SerializeField] private AudioGroup {field};";
                GUILayout.Label("自分のスクリプトの先頭に追加（using UnityEngine; も必要）", EditorStyles.miniLabel);
                GUILayout.Label("using Yamamoto.AudioTools;", EditorStyles.label);
                GUILayout.Label("MonoBehaviourクラス内の、メソッドの外に貼り付け", EditorStyles.miniLabel);
                GUILayout.Label(declarations, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("フィールド宣言をコピー")) Copy(declarations);
                GUILayout.Label("コンパイル後、自分のコンポーネントのInspectorへドラッグ", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Hierarchy　 GroupAudioPlayer　→　再生部品 ／ Audio Player", EditorStyles.wordWrappedLabel);
                    GUILayout.Label($"Project　　 {(_group != null ? _group.name + ".asset" : "保存したグループAsset")}　→　{slot} ／ {ObjectNames.NicifyVariableName(field)}", EditorStyles.wordWrappedLabel);
                }

                using (new EditorGUI.DisabledScope(_group == null))
                {
                    if (GUILayout.Button("グループAssetをProjectで表示")) PingAsset(_group);
                }

                if (_group == null) EditorGUILayout.HelpBox("使うグループを選択してください。", MessageType.Info);
                else if (_group.Count == 0) EditorGUILayout.HelpBox("このグループには音源が登録されていません", MessageType.Warning);
            }
        }

        /// <summary>
        ///     選択した種類に合う呼び出しを、フィールド宣言と同じ変数名で渡す。
        /// </summary>
        private void DrawCall(bool bgm, bool inhale, string field)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("③ 音を鳴らす", EditorStyles.boldLabel);
                string when = bgm ? "ステージ開始など" : inhale ? "死体の吸い込みが成立した箇所" : "効果音を鳴らす処理が成立した箇所";
                GUILayout.Label(when + "に貼り付けます。保存した音源・音量・再生方式が使われます。", EditorStyles.wordWrappedLabel);
                string call = bgm ? $"_audioPlayer.PlayBgm({field}, loop: true);" : $"_audioPlayer.PlaySe({field});";
                GUILayout.Label(call);
                if (GUILayout.Button("再生の1行をコピー")) Copy(call);
            }
        }

        /// <summary>
        ///     クリップボードへ表示中のコードをコピーする。
        /// </summary>
        private void Copy(string code)
        {
            EditorGUIUtility.systemCopyBuffer = code;
            ShowNotification(new GUIContent("コピーしました"));
        }

        /// <summary>
        ///     Inspectorの選択を保ちながらProject上でAssetを示す。
        /// </summary>
        private static void PingAsset(UnityEngine.Object asset)
        {
            if (asset == null) return;
            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        ///     専用Sceneに割り当てたサンプルを通常の公開APIで操作する。
        /// </summary>
        private static void DrawSample()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("探索（220 Hz）を鳴らしてから、追跡（440 Hz）への切替を1つ選びます。", EditorStyles.wordWrappedLabel);
                GroupAudioPlayer player = AudioToolsTransport.Player();
                if (player == null || !player.TryGetComponent(out AudioPreviewExample example))
                {
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (GUILayout.Button("専用Sceneで試聴開始")) AudioToolsWindow.BeginPreview();
                    }

                    if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("現在のPlayを終了してから専用Sceneで試せます。", MessageType.Info);
                    return;
                }

                if (GUILayout.Button("確認音でSEを1回鳴らす")) example.PlayInhale();
                if (GUILayout.Button("探索BGMを再生（220 Hz）")) example.PlayExplore();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("即時切替")) example.SwitchToChase();
                    if (GUILayout.Button("順次フェード（2秒）")) example.FadeToChase();
                    if (GUILayout.Button("クロスフェード（2秒）")) example.CrossfadeToChase();
                }

                GUILayout.Label("現在の曲　" + (player.CurrentMusic != null ? player.CurrentMusic.name : "停止中"));
                if (GUILayout.Button("停止")) example.StopAudio();
            }
        }

        /// <summary>
        ///     Sceneの寿命とアセンブリ参照についてゲーム担当者を案内する。
        /// </summary>
        private static void DrawDetails()
        {
            GUILayout.Label("StopBgm() はBGMを即時停止。StopSe(group) は指定グループ、StopSe() は全SEを停止します。", EditorStyles.wordWrappedLabel);
            GUILayout.Label("BGMは次のSceneでも続きます。次のSceneから再生・切替を指示するときは、そのSceneに置いた再生部品を使います。直接PlayするSceneにもPrefabと開始処理、有効なAudioListenerを用意します。", EditorStyles.wordWrappedLabel);
            GUILayout.Label("独自asmdefでは呼び出し側のAssembly Definition Referencesに Yamamoto.AudioTools を追加します。", EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("詳しいAPI例をGuide.mdで開く"))
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(AudioGroupDocument.ROOT + "/Docs/Guide.md"));
            }
        }
    }
}
