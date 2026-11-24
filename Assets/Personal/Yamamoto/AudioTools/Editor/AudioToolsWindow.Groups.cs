using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yamamoto.AudioTools.Editor
{
    /// <summary> 編集中のグループの構成変更を、確認して下書きへ反映する。 </summary>
    internal sealed partial class AudioToolsWindow
    {
        internal enum GroupAction { None, Rename, Kind, Copy, Delete }

        private GroupAction _action;
        private string _actionName = "";
        private string[] _references = Array.Empty<string>();
        private string _referenceError = "";

        /// <summary> 使用可能な名前と、保存前の他のグループが使用する名前を確認する。 </summary>
        private string NameProblem(string name, AudioGroupDocument owner = null)
        {
            string issue = AudioGroupDocument.NameIssue(name);
            if (issue != "") return issue;
            string folder = owner == null ? AudioGroupDocument.DATA : owner.Path.Substring(0, owner.Path.LastIndexOf('/') + 1);
            string path = folder + name + ".asset";
            if (owner?.Path != path && string.Equals(owner?.Path, path, StringComparison.OrdinalIgnoreCase))
                return "大文字・小文字だけを変えるには、一度別名で保存してください。";
            if (_paths.Any(p => p != owner?.Path && string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase)) ||
                _state.Documents.Any(x => x != owner && string.Equals(Path.GetFileNameWithoutExtension(x.TargetPath), name, StringComparison.OrdinalIgnoreCase)) ||
                (owner?.Path != path && AudioGroupDocument.Exists(path)) ||
                _state.Documents.Any(x => x != owner && (string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.TargetPath, path, StringComparison.OrdinalIgnoreCase)))) return "同名のグループまたはAssetがあります。別の名前を指定してください。";
            return "";
        }

        /// <summary> 操作に必要な名前または参照元だけを準備する。 </summary>
        internal void BeginGroupAction(AudioGroupDocument doc, GroupAction action)
        {
            _action = action;
            _actionName = GroupName(doc.Path);
            _references = Array.Empty<string>();
            _referenceError = "";
            if (action == GroupAction.Copy)
            {
                string basis = _actionName + "_Copy";
                if (AudioGroupDocument.NameIssue(basis) != "") basis = "Group_Copy";
                _actionName = basis;
                for (int i = 2; NameProblem(_actionName) != ""; i++) _actionName = basis + i;
            }

            if (action != GroupAction.Delete && action != GroupAction.Kind) return;
            try { _references = AudioGroupReferences.Find(doc); }
            catch (Exception ex) { _referenceError = "参照を確認できません。 " + ex.Message; }
        }

        /// <summary> 名前・種別・複製・削除を選択したときだけ入力欄を表示する。 </summary>
        private bool DrawGroupActions(AudioGroupDocument doc)
        {
            if (doc.DeletePending)
            {
                EditorGUILayout.HelpBox("削除予定です。保存すると、このグループ設定をゴミ箱へ移します。", MessageType.Warning);
                if (GUILayout.Button("削除予定を取り消す")) { doc.DeletePending = false; Remember(); }
                return true;
            }

            if (_action == GroupAction.None)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("名前変更")) BeginGroupAction(doc, GroupAction.Rename);
                    if (GUILayout.Button("種別変更")) BeginGroupAction(doc, GroupAction.Kind);
                    if (GUILayout.Button("複製")) BeginGroupAction(doc, GroupAction.Copy);
                    if (!doc.New && GUILayout.Button("削除…")) BeginGroupAction(doc, GroupAction.Delete);
                }

                return false;
            }

            string issue = "";
            string confirm;
            switch (_action)
            {
                case GroupAction.Rename:
                    _actionName = EditorGUILayout.TextField("新しいグループ名", _actionName);
                    issue = NameProblem(_actionName, doc);
                    if (_actionName == GroupName(doc.Path)) issue = "新しい名前を入力してください。";
                    GUILayout.Label("保存時に設定Assetも改名し、Inspectorの参照を維持します。", EditorStyles.wordWrappedMiniLabel);
                    confirm = "名前を下書きに反映";
                    break;
                case GroupAction.Copy:
                    _actionName = EditorGUILayout.TextField("複製先のグループ名", _actionName);
                    issue = NameProblem(_actionName);
                    GUILayout.Label("現在の設定を新しい下書きにコピーします。音源ファイルは共用します。", EditorStyles.wordWrappedLabel);
                    confirm = "複製を作成";
                    break;
                default:
                    bool delete = _action == GroupAction.Delete;
                    string kind = doc.Draft.Category == AudioGroup.Kind.Bgm ? "BGM → SE" : "SE → BGM";
                    EditorGUILayout.HelpBox(delete ? $"「{GroupName(doc.Path)}」を削除しますか？ 音源ファイルとフォルダーは残ります。" :
                        $"種類を {kind} に変更します。変更前の種類を指定する再生処理では使用できなくなります。", MessageType.Warning);
                    GUILayout.Label(AudioGroupReferences.SCOPE, EditorStyles.wordWrappedMiniLabel);
                    if (_referenceError == "") GUILayout.Label(_references.Length == 0 ? "確認できた参照元：0件" :
                        "参照元\n" + string.Join("\n", _references), EditorStyles.wordWrappedLabel);
                    issue = _referenceError == "" ? AudioGroupReferences.Issue(delete, _references) : _referenceError;
                    confirm = delete ? "削除を予約" : "種別を下書きに反映";
                    break;
            }

            if (issue != "") GUILayout.Label(issue, EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("キャンセル")) _action = GroupAction.None;
                using (new EditorGUI.DisabledScope(issue != ""))
                {
                    if (GUILayout.Button(confirm)) ApplyGroupAction(doc, _actionName);
                }
            }

            return true;
        }

        /// <summary> Assetへの変更を行わず、選択した操作の結果をセッションに保持する。 </summary>
        internal bool ApplyGroupAction(AudioGroupDocument doc, string name)
        {
            if (doc.DeletePending) return false;
            switch (_action)
            {
                case GroupAction.Rename:
                    if (NameProblem(name, doc) != "") return false;
                    doc.PendingName = name == Path.GetFileNameWithoutExtension(doc.Path) ? null : name;
                    break;
                case GroupAction.Copy:
                    if (NameProblem(name) != "") return false;
                    var draft = JsonUtility.FromJson<AudioGroupDraft>(JsonUtility.ToJson(doc.Draft));
                    doc = new AudioGroupDocument { Path = AudioGroupDocument.DATA + name + ".asset", New = true,
                        Draft = draft, Baseline = new AudioGroupDraft { Category = draft.Category } };
                    _state.Documents.Add(doc);
                    break;
                case GroupAction.Kind:
                case GroupAction.Delete:
                    if (_referenceError != "" || AudioGroupReferences.Issue(_action == GroupAction.Delete, _references) != "") return false;
                    if (_action == GroupAction.Delete)
                    {
                        if (doc.New) return false;
                        doc.DeletePending = true;
                    }
                    else doc.Draft.Category = doc.Draft.Category == AudioGroup.Kind.Bgm ? AudioGroup.Kind.Se : AudioGroup.Kind.Bgm;
                    break;
                default: return false;
            }

            _search = "";
            _categoryFilter = 0;
            _saveResult = "";
            _state.LastEditedPath = doc.Path;
            SelectGroup(doc.Path);
            return true;
        }

        /// <summary> 改名・削除前のパスで保持していた選択と試聴コピーを更新する。 </summary>
        private void ChangePath(string before, string after)
        {
            if (before == after) return;
            if (_state.Path == before) _state.Path = after;
            if (_state.LastEditedPath == before) _state.LastEditedPath = after;
            if (_state.Bgm.Path == before) _state.Bgm.Path = after;
            if (_state.Se.Path == before) _state.Se.Path = after;
            if (_state.SourcePath == before) _state.SourcePath = after;
            if (_state.DestinationPath == before) _state.DestinationPath = after;
            if (_saveTarget == before) _saveTarget = after;
            _transport.ChangePath(before, after);
        }
    }
}
