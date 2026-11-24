using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     BGMとSEの独立した選択・音量調整と切替試験を表示する。
    /// </summary>
    internal sealed partial class AudioToolsWindow
    {
        /// <summary>
        ///     共通の比較スイッチと、幅に合わせた二つの操作欄を描画する。
        /// </summary>
        private void DrawAudition(GroupAudioPlayer player)
        {
            bool wide = position.width >= 720f;
            if (wide) EditorGUILayout.BeginHorizontal();
            DrawChannel(AudioGroup.Kind.Bgm, player);
            DrawChannel(AudioGroup.Kind.Se, player);
            if (wide) EditorGUILayout.EndHorizontal();
            DrawComparison(player);
        }

        /// <summary>
        ///     保存済み音源に音量差があるときだけ両チャンネルの比較を提示する。
        /// </summary>
        private void DrawComparison(GroupAudioPlayer player)
        {
            bool changed = HasComparableGain();
            if (!changed && _state.ListenB) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(_state.ListenB ? "変更前後を聴き比べる（BGM・SE共通）" :
                    "変更前の音量で試聴中（BGM・SE共通）", EditorStyles.boldLabel);
                if (GUILayout.Button(_state.ListenB ? "変更前の音量で聴く" : "調整中の音量に戻す", GUILayout.Height(25)))
                    SetComparison(!_state.ListenB, player);
            }
        }

        /// <summary>
        ///     新規音源を保存済みの比較対象に含めない。
        /// </summary>
        internal bool HasComparableGain() => _state.Documents.Any(doc => !doc.DeletePending && doc.Draft.Items.Any(item =>
            doc.Baseline.Items.Exists(saved => saved.Id == item.Id && !Mathf.Approximately(saved.Gain, item.Gain))));

        /// <summary>
        ///     選択中の比較音量を現在の発音に反映する。
        /// </summary>
        internal void SetComparison(bool listenB, GroupAudioPlayer player)
        {
            _state.ListenB = listenB;
            if (player != null)
            {
                foreach (AudioGroupDocument doc in _state.Documents.Where(x => !x.DeletePending)) _transport.Refresh(doc, listenB, player);
            }

            Remember();
        }

        /// <summary> 指定した用途の試聴・検索状態を取得する。 </summary>
        internal Selection Channel(AudioGroup.Kind kind) => kind == AudioGroup.Kind.Bgm ? _state.Bgm : _state.Se;

        /// <summary>
        ///     一方のチャンネルの操作で他方の選択・再生に影響しないようにする。
        /// </summary>
        private void DrawChannel(AudioGroup.Kind kind, GroupAudioPlayer player)
        {
            bool bgm = kind == AudioGroup.Kind.Bgm;
            string label = bgm ? "BGM" : "SE";
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label(label, EditorStyles.boldLabel);
                EditorGUIUtility.labelWidth = 92f;
                Selection selection = Channel(kind);
                string path = selection.Path;
                AudioGroupDocument doc = PickGroup(label + "グループ", kind, ref path);
                selection.SelectGroup(path);
                if (doc == null)
                {
                    if (GUILayout.Button(label + "グループを作成")) BeginCreation(kind);
                    return;
                }

                if (doc.Draft.Items.Count == 0)
                {
                    if (GUILayout.Button("最初の音源を追加")) { SelectGroup(doc.Path); SelectTab(Page.Library); }
                    return;
                }

                string[] names = AudioCandidatePanel.Labels(doc.Draft);
                if (names.Length > 5 || !string.IsNullOrEmpty(selection.Search))
                    selection.Search = EditorGUILayout.TextField("音源を検索", selection.Search ?? "");
                int[] matches = Enumerable.Range(0, names.Length)
                    .Where(i => names[i].IndexOf(selection.Search ?? "", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                int selected = doc.Draft.Items.FindIndex(x => x.Id == selection.ClipId);
                if (matches.Length > 0)
                {
                    int visible = Mathf.Max(0, Array.IndexOf(matches, selected));
                    visible = EditorGUILayout.Popup(bgm ? "試聴する曲" : "試聴する音源", visible, matches.Select(i => names[i]).ToArray());
                    selected = matches[visible];
                    selection.ClipId = doc.Draft.Items[selected].Id;
                }
                else
                {
                    selected = -1;
                    GUILayout.Label("一致する音源がありません。", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(3);
                DrawChannelPlayback(doc, selected, bgm, player);
                if (selected >= 0) DrawGain(doc, selected, player);
                selection.TestOrder = EditorGUILayout.Foldout(selection.TestOrder, "グループの選曲方式を試す", true);
                if (selection.TestOrder)
                {
                    using (new EditorGUI.DisabledScope(player == null))
                    {
                        string mode = AudioGroupDraft.OrderName(doc.Draft.Mode);
                        if (GUILayout.Button(mode + "で再生")) Play(doc, player);
                    }
                }
            }
        }

        /// <summary>
        ///     候補を明示して再生し、停止範囲を用途ごとに限定する。
        /// </summary>
        private void DrawChannelPlayback(AudioGroupDocument doc, int index, bool bgm, GroupAudioPlayer player)
        {
            bool playing = bgm ? player.CurrentMusic != null : player.PlayingEffects > 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(index < 0))
                {
                    string label = bgm && playing ? "選んだ曲を再生" : bgm ? "BGM再生" : "SE再生";
                    if (GUILayout.Button(label, GUILayout.Height(28))) Play(doc, player, index);
                }

                if (playing && GUILayout.Button(bgm ? "BGM停止" : "このグループを停止", GUILayout.Height(28)))
                {
                    if (bgm) player.StopBgm();
                    else player.StopSe(_transport.Refresh(doc, _state.ListenB, player));
                }
            }

            if (bgm) _state.Loop = EditorGUILayout.ToggleLeft("継続再生", _state.Loop);
            else
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                if (playing && GUI.Button(row, "すべてのSEを停止", EditorStyles.miniButton)) player.StopSe();
            }
        }

        /// <summary>
        ///     比較元を示し、選んだ音源の下書きゲインだけを編集する。
        /// </summary>
        private void DrawGain(AudioGroupDocument doc, int index, GroupAudioPlayer player)
        {
            AudioGroupDraft.Item item = doc.Draft.Items[index];
            AudioGroupDraft.Item saved = doc.Baseline.Items.Find(x => x.Id == item.Id);
            float percent = item.Gain * 100f;
            EditorGUILayout.Space(6);
            GUILayout.Label("個別音量", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                float slider = GUILayout.HorizontalSlider(percent, 0f, 1000f);
                if (EditorGUI.EndChangeCheck()) percent = slider;
                percent = EditorGUILayout.FloatField(percent, GUILayout.Width(58));
                GUILayout.Label("%", GUILayout.Width(18));
            }

            bool edited = EditorGUI.EndChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("↓10%")) { percent -= 10f; edited = true; }
                if (GUILayout.Button("↑10%")) { percent += 10f; edited = true; }
                if (GUILayout.Button("100%に戻す")) { percent = 100f; edited = true; }
            }

            if (edited) AdjustGain(doc, index, percent, player);
            if (saved == null) GUILayout.Label("新規音源・保存済み音量なし", EditorStyles.miniLabel);
            else if (!Mathf.Approximately(saved.Gain, item.Gain))
                GUILayout.Label($"保存済み {saved.Gain * 100f:G5}%　→　調整中 {item.Gain * 100f:G5}%", EditorStyles.miniLabel);
            if (item.Gain > 1f) GUILayout.Label("増幅中です。音割れに注意してください。", EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>
        ///     すべての入力を同じゲインに反映し、比較中でも調整音へ戻す。
        /// </summary>
        internal void AdjustGain(AudioGroupDocument doc, int index, float percent, GroupAudioPlayer player)
        {
            if (!float.IsFinite(percent)) return;
            doc.Draft.Items[index].Gain = Mathf.Clamp(percent, 0f, 1000f) / 100f;
            if (!_state.ListenB) SetComparison(true, player);
            Changed(doc);
        }

        /// <summary>
        ///     切替元と切替先を独立して選び、実際の発音状態を表示する。
        /// </summary>
        private void DrawTransitions(GroupAudioPlayer player)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("切り替え元", EditorStyles.boldLabel);
                AudioGroupDocument source = PickGroup("元のBGMグループ", AudioGroup.Kind.Bgm, ref _state.SourcePath);
                if (source == null || source.Draft.Items.Count == 0)
                {
                    if (GUILayout.Button("登録済みグループを編集")) SelectTab(Page.Library);
                }
                else
                {
                    if (GUILayout.Button("切り替え元を再生")) Play(source, player);
                }
            }

            bool playing = player.CurrentMusic != null;
            if (!playing) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("切り替え先", EditorStyles.boldLabel);
                AudioGroupDocument target = PickGroup("先のBGMグループ", AudioGroup.Kind.Bgm, ref _state.DestinationPath, _state.SourcePath);

                if (target == null || target.Draft.Items.Count == 0)
                    GUILayout.Label("音源が登録された切り替え先を選んでください。", EditorStyles.miniLabel);
                else
                {
                    _state.Seconds = Mathf.Max(0f, EditorGUILayout.FloatField("切替全体の時間（秒）", _state.Seconds));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("即時切替")) Play(target, player);
                        if (GUILayout.Button("フェードアウト → イン")) Play(target, player, transition: 1);
                        if (GUILayout.Button("クロスフェード")) Play(target, player, transition: 2);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(player == null || player.CurrentMusic == null))
            {
                if (GUILayout.Button("BGM停止")) player.StopBgm();
            }
        }

        /// <summary>
        ///     選択した候補またはグループの方式で再生する。
        /// </summary>
        internal void Play(AudioGroupDocument doc, GroupAudioPlayer player, int index = -1, int transition = 0)
        {
            AudioGroup group = _transport.Refresh(doc, _state.ListenB, player);
            player.SetMonitorLevel(_state.Monitor);
            bool accepted = index >= 0 ? player.Audition(group, index, _state.Loop) : transition switch
            {
                1 => player.FadeSwitchBgm(group, _state.Seconds, _state.Loop),
                2 => player.CrossfadeBgm(group, _state.Seconds, _state.Loop),
                _ => group.Category == AudioGroup.Kind.Bgm ? player.PlayBgm(group, _state.Loop) : player.PlaySe(group)
            };
            _message = accepted ? "" : "再生できません。音源の参照・採用設定・SEの再生制限を確認してください。";
        }

        /// <summary>
        ///     指定した用途の保存済みグループと新規下書きから選ぶ。
        /// </summary>
        private AudioGroupDocument PickGroup(string label, AudioGroup.Kind kind, ref string path, string other = null)
        {
            string[] paths = _paths.Concat(_state.Documents.Where(x => x.Dirty).Select(x => x.Path)).Distinct()
                .Where(x => _state.Documents.Find(d => d.Path == x)?.DeletePending != true)
                .Where(x => (_state.Documents.Find(d => d.Path == x)?.Draft.Category ??
                    AssetDatabase.LoadAssetAtPath<AudioGroup>(x)?.Category) == kind).ToArray();
            if (paths.Length == 0) return null;
            int index = Array.IndexOf(paths, path);
            if (index < 0)
            {
                index = Array.FindIndex(paths, x => x != other && GetDocument(x)?.Draft.Items.Count > 0);
                if (index < 0) index = Mathf.Max(0, Array.FindIndex(paths, x => x != other));
            }
            index = EditorGUILayout.Popup(label, index, paths.Select(GroupName).ToArray());
            path = paths[index];
            return GetDocument(path);
        }

        /// <summary>
        ///     選択欄と独立して現在の演奏・遷移を示す。
        /// </summary>
        private static string MusicStatus(GroupAudioPlayer player)
        {
            if (player == null || player.CurrentMusic == null) return "BGM停止中";
            if (player.OutgoingMusic == null) return "再生中　" + player.CurrentMusic.name;
            string phase = player.IsCrossfading ? "クロスフェード中" : "フェード切替中";
            return $"{phase}　{player.OutgoingMusic.name} → {player.CurrentMusic.name}";
        }
    }
}
