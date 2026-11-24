using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary>
    ///     試聴状態を表示し、確認したグループだけを個別に保存する。
    /// </summary>
    internal sealed partial class AudioToolsWindow
    {
        private Drawer _drawer;
        private string _saveTarget;
        private string _saveResult;
        private string _saveFailures;

        /// <summary>
        ///     頻用操作をスクロール領域の外にまとめる。
        /// </summary>
        private void DrawHeader(GroupAudioPlayer player)
        {
            GUILayout.Label("試聴中　｜　" + MusicStatus(player) + $"　｜　SE {player.PlayingEffects}音", EditorStyles.toolbar);
            using (new EditorGUILayout.HorizontalScope())
            {
                var label = new GUIContent("試聴時の全体音量", "試聴中のBGM・SE全体の音量を変更します。保存する個別音量や、本編の音量には影響しません。");
                GUILayout.Label(label, GUILayout.Width(118));
                EditorGUI.BeginChangeCheck();
                float percent = EditorGUILayout.Slider(_state.Monitor * 100f, 0f, 100f);
                GUILayout.Label("%", GUILayout.Width(17));
                if (EditorGUI.EndChangeCheck())
                {
                    _state.Monitor = percent / 100f;
                    if (player != null) player.SetMonitorLevel(_state.Monitor);
                    Remember();
                }
            }

        }

        /// <summary>
        ///     作業内容をスクロールしても保存と試聴の主操作を固定する。
        /// </summary>
        private void DrawFooter(bool preview, bool foreignPlay)
        {
            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (foreignPlay) return;
                if (_isCreating)
                {
                    string issue = CreationIssue(_creationName.Trim());
                    if (issue != "") GUILayout.Label(issue, EditorStyles.miniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("キャンセル", GUILayout.Height(32))) CancelCreation();
                        using (new EditorGUI.DisabledScope(issue != ""))
                        {
                            if (PrimaryButton("グループを作成")) FinishCreation(_creationName);
                        }
                    }

                    return;
                }

                string block = SaveBlockReason();
                if (block != "") GUILayout.Label(block, EditorStyles.miniLabel);
                else if (!string.IsNullOrEmpty(_saveResult)) GUILayout.Label(_saveResult, EditorStyles.boldLabel);
                int dirty = _state.Documents.Count(x => x.Dirty);
                if (dirty > 0 && _drawer == Drawer.Closed)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label($"未保存　{dirty}グループ", EditorStyles.boldLabel);
                        using (new EditorGUI.DisabledScope(block != ""))
                        {
                            if (GUILayout.Button("個別保存…", GUILayout.Height(25))) OpenReview(Drawer.One);
                            if (GUILayout.Button("すべて保存…", GUILayout.Height(25))) OpenReview(Drawer.All);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_drawer != Drawer.Closed)
                    {
                        if (GUILayout.Button("戻る", GUILayout.Height(32))) _drawer = Drawer.Closed;
                        GUILayout.FlexibleSpace();
                        DrawConfirmation(block);
                        return;
                    }

                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(block != "" || (!preview && !HasRegisteredClips())))
                    {
                        if (PrimaryButton(preview ? "試聴を終了" : "専用Sceneで試聴開始", !preview))
                        {
                            if (preview) EditorApplication.ExitPlaymode();
                            else
                            {
                                SelectTab(Page.Audition);
                                _state.ListenB = true;
                                Remember();
                                BeginPreview();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     下部の主要操作を他のボタンから区別する。
        /// </summary>
        private static bool PrimaryButton(string label, bool prominent = false)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.48f, 0.79f, 1f) : new Color(0.73f, 0.88f, 1f);
            GUIStyle style = prominent ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 14 } : GUI.skin.button;
            bool clicked = GUILayout.Button(label, style, GUILayout.MinWidth(prominent ? 280 : 165), GUILayout.Height(prominent ? 48 : 35));
            GUI.backgroundColor = previous;
            return clicked;
        }

        /// <summary>
        ///     保存対象を選び、差分の先頭を表示する。
        /// </summary>
        private void OpenReview(Drawer drawer)
        {
            _drawer = drawer;
            _scroll = Vector2.zero;
            _saveTarget = _state.Tab == Page.Library ? _state.Path : _state.LastEditedPath;
        }

        /// <summary>
        ///     表示中の差分と同じ対象だけを確定する。
        /// </summary>
        private void DrawConfirmation(string block)
        {
            if (block != "") return;
            if (_drawer == Drawer.Reload)
            {
                AudioGroupDocument doc = _state.Documents.Find(x => x.Path == _saveTarget);
                if (doc != null && GUILayout.Button(doc.New ? "この下書きを破棄" : "下書きを破棄して再読込", GUILayout.Height(32))) Reload(doc);
                return;
            }

            AudioGroupDocument[] docs = _state.Documents.Where(x => x.Dirty &&
                (_drawer == Drawer.All || x.Path == _saveTarget)).OrderBy(x => x.Path).ToArray();
            if (docs.Length == 0) return;
            if (PrimaryButton(docs.Length + "グループを保存")) SaveReviewed(docs);
        }

        /// <summary>
        ///     保存可能な編集時・ツールから開始した専用Playを判定する。
        /// </summary>
        private static string SaveBlockReason()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return "Unityの更新が終わると保存できます。";
            if (EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode) return "Play切替中です。";
            if (EditorApplication.isPlaying && !OwnsPreview()) return "通常のPlay中です。保存には専用試聴を使用してください。";
            return "";
        }

        /// <summary>
        ///     本編や手動で開始したPlayを終了・編集の対象に含めない。
        /// </summary>
        private static bool OwnsPreview() => EditorApplication.isPlaying &&
            SessionState.GetString(RESTORE, "?") != "?" && AudioToolsTransport.Player() != null;

        /// <summary>
        ///     保存先と変更内容をその場で確認できるようにする。
        /// </summary>
        private void DrawSaveDetails()
        {
            if (_drawer == Drawer.Closed) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                AudioGroupDocument[] dirty = _state.Documents.Where(x => x.Dirty).OrderBy(x => x.Path).ToArray();
                bool reload = _drawer == Drawer.Reload;
                AudioGroupDocument[] choices = reload ? _state.Documents.OrderBy(x => x.Path).ToArray() : dirty;
                GUILayout.Label(reload ? "下書きの破棄・再読込" : "保存する変更を確認", TitleStyle());

                bool one = _drawer == Drawer.One || reload;
                if (one && choices.Length > 0)
                {
                    int initial = Mathf.Max(0, Array.FindIndex(choices, x => x.Path == _saveTarget));
                    int selected = EditorGUILayout.Popup("対象グループ", initial, choices.Select(x => GroupName(x.Path)).ToArray());
                    _saveTarget = choices[selected].Path;
                    choices = new[] { choices[selected] };
                }

                foreach (AudioGroupDocument doc in choices)
                {
                    GUILayout.Label(GroupName(doc.Path), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(doc.Path, EditorStyles.wordWrappedMiniLabel);
                    if (doc.DeletePending)
                    {
                        EditorGUILayout.HelpBox("削除予定：設定Assetだけをゴミ箱へ移します。登録音源は残ります。", MessageType.Warning);
                        continue;
                    }
                    if (doc.TargetPath != doc.Path)
                        EditorGUILayout.LabelField($"名前　{Path.GetFileNameWithoutExtension(doc.Path)} → {GroupName(doc.Path)}", EditorStyles.wordWrappedLabel);
                    if (doc.New) GUILayout.Label("新規グループ", EditorStyles.miniLabel);
                    foreach (string change in doc.Draft.Changes(doc.Baseline))
                    {
                        EditorGUILayout.LabelField(change, EditorStyles.wordWrappedLabel);
                    }
                }

            }
        }

        /// <summary>
        ///     成否をグループごとに確定し、失敗した下書きと比較元を保持する。
        /// </summary>
        internal int SaveReviewed(IEnumerable<AudioGroupDocument> reviewed)
        {
            string reason = SaveBlockReason();
            if (reason != "") { _message = reason; return 0; }
            var successes = new List<string>();
            var failures = new List<string>();
            GroupAudioPlayer player = AudioToolsTransport.Player();
            foreach (AudioGroupDocument doc in reviewed.ToArray())
            {
                if (!doc.Dirty || !_state.Documents.Contains(doc)) continue;
                AudioGroupDocument saved;
                try
                {
                    saved = doc.Save();
                }
                catch (Exception ex)
                {
                    failures.Add($"{GroupName(doc.Path)}　{ex.Message}");
                    continue;
                }

                successes.Add(GroupName(doc.Path) + (saved == null ? "（削除）" : ""));
                if (saved == null) _state.Documents.Remove(doc);
                else _state.Documents[_state.Documents.IndexOf(doc)] = saved;
                ChangePath(doc.Path, saved?.Path);
                if (player != null && saved != null)
                {
                    try { _transport.Refresh(saved, _state.ListenB, player); }
                    catch (Exception ex) { failures.Add($"{GroupName(doc.Path)} は保存済みです。試聴音量の更新に失敗しました。{ex.Message}"); }
                }
            }

            _action = GroupAction.None;
            _drawer = Drawer.Closed;
            _message = "";
            if (!_state.ListenB && !HasComparableGain()) SetComparison(true, player);
            _saveFailures = string.Join("\n", failures);
            _saveResult = $"{successes.Count}件保存 ／ {_state.Documents.Count(x => x.Dirty)}件未保存";
            if (successes.Count == 1) _saveResult += "　" + successes[0];
            if (successes.Count > 1) _message = "保存済み　" + string.Join("、", successes);
            RefreshPaths();
            Remember();
            return successes.Count;
        }

        /// <summary>
        ///     確認した一件だけを読み戻す。再生中のコピーは維持する。
        /// </summary>
        private void Reload(AudioGroupDocument doc)
        {
            try
            {
                bool discard = doc.New && !File.Exists(doc.Path);
                if (discard)
                {
                    _state.Documents.Remove(doc);
                    if (_state.Path == doc.Path) _state.Path = null;
                    if (_state.Bgm.Path == doc.Path) _state.Bgm.SelectGroup(null);
                    if (_state.Se.Path == doc.Path) _state.Se.SelectGroup(null);
                }
                else
                {
                    AudioGroupDocument saved = AudioGroupDocument.Load(doc.Path);
                    _state.Documents[_state.Documents.IndexOf(doc)] = saved;
                    Changed(saved);
                    if (!_state.ListenB && !HasComparableGain()) SetComparison(true, AudioToolsTransport.Player());
                }

                _drawer = Drawer.Closed;
                _saveFailures = "";
                _saveResult = "";
                _message = GroupName(doc.Path) + (discard ? " の下書きを破棄しました。" : " を再読込しました。");
                _candidates.ResetSelection();
                RefreshPaths();
                Remember();
            }
            catch (Exception ex) { _message = ex.Message; }
        }

        private enum Drawer { Closed, One, All, Reload }
    }
}
