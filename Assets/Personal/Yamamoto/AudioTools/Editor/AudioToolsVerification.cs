using System;
using System.Collections;
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
    ///     専用Sceneと自作の小音量信号だけで、保存・選曲・出力とScene遷移を確認する。
    /// </summary>
    internal static partial class AudioToolsVerification
    {
        private const string KEY = "Yamamoto.AudioTools.Verification.";
        private const string PREFIX = "[AudioToolsVerification] ";
        private static IEnumerator _steps;
        private static double _resumeAt;
        private static readonly float[] Samples = new float[4096];

        /// <summary>
        ///     新規の一時グループを準備して専用Sceneの試験を開始する。
        /// </summary>
        [MenuItem("Tools/Yamamoto/音声ツールの検証")]
        internal static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(KEY + "run", false)) return;
            if (EditorWindow.HasOpenInstances<AudioToolsWindow>() && EditorWindow.GetWindow<AudioToolsWindow>().IsCreating)
            {
                Debug.Log(PREFIX + "グループ作成を完了またはキャンセルしてから検証してください。");
                return;
            }

            string path = AudioGroupDocument.DATA + "Verification-" + Guid.NewGuid().ToString("N") + ".asset";
            SessionState.SetString(KEY + "path", path);
            SessionState.SetBool(KEY + "wasOpen", EditorWindow.HasOpenInstances<AudioToolsWindow>());
            SessionState.SetString(KEY + "prior", SessionState.GetString(AudioToolsWindow.SESSION, ""));
            try
            {
                AudioToolsWindow.Open();
                EditorWindow.GetWindow<AudioToolsWindow>().RestoreSession("");
                VerifyCreation();
                VerifyGroupEditing();
                AudioGroupDocument saved = VerifyStorage(path);
                SessionState.SetString(KEY + "guid", saved.AssetId);
                string sePath = AudioGroupDocument.DATA + "Verification-" + Guid.NewGuid().ToString("N") + ".asset";
                AudioGroupDocument se = new AudioGroupDocument
                {
                    Path = sePath, New = true, Baseline = new AudioGroupDraft(), Draft = MakeTestDraft(AudioGroup.Kind.Se)
                }.Save();
                SessionState.SetString(KEY + "sePath", sePath);
                SessionState.SetString(KEY + "seGuid", se.AssetId);
                AudioToolsWindow.OpenGroup(path);
                AudioToolsWindow window = EditorWindow.GetWindow<AudioToolsWindow>();
                window.SelectedDocument.Draft.Items[0].Gain = 0.5f;
                window.Changed(window.SelectedDocument);
                Check(AudioGroupDocument.Load(path).Baseline.Items[0].Gain == 2f, "draft isolated before Play");
                SessionState.SetBool(KEY + "run", true);
                window.Close();
                EditorApplication.delayCall += ResumeAfterClose;
            }
            catch (Exception ex)
            {
                Debug.LogError(PREFIX + ex);
                Cleanup();
            }
        }

        /// <summary>
        ///     初回登録とキャンセルがディスク・既存下書きから独立していることを確認する。
        /// </summary>
        private static void VerifyCreation()
        {
            AudioToolsWindow.Open();
            var window = EditorWindow.GetWindow<AudioToolsWindow>();
            string before = SessionState.GetString(AudioToolsWindow.SESSION, "");
            string name = "Verification-" + Guid.NewGuid().ToString("N");
            string path = AudioGroupDocument.DATA + name + ".asset";
            try
            {
                window.BeginCreation();
                Check(window.SuggestCreationName("Assets/Audio/Stage1_Normal") == "Stage1_Normal" &&
                    window.SuggestCreationName("Assets/Audio/Footsteps") == "Footsteps", "folder name suggested and follows folder");
                window.EditCreationName("Custom");
                Check(window.SuggestCreationName("Assets/Audio/Reset") == "Custom", "manual creation name protected");
                window.CancelCreation();
                AudioGroupDraft staging = window.BeginCreation(AudioGroup.Kind.Se, false);
                AudioCandidatePanel.Add(staging, TestTone("Tone220"));
                window.CancelCreation();
                Check(SessionState.GetString(AudioToolsWindow.SESSION, "") == before && !File.Exists(path),
                    "cancel leaves original documents and assets untouched");
                staging = window.BeginCreation(AudioGroup.Kind.Se, false);
                Check(!window.FinishCreation(name), "empty first registration stays in form");
                AudioCandidatePanel.Add(staging, TestTone("Tone220"));
                Check(window.FinishCreation(name) && window.SelectedDocument.New && !File.Exists(path) &&
                    window.SelectedDocument.Draft.Items.Count == 1 && window.Channel(AudioGroup.Kind.Se).Path == path &&
                    !window.SelectedDocument.Baseline.Items.Any(), "create selects registered SE as unsaved draft without a comparison baseline");
            }
            finally
            {
                window.CancelCreation();
                window.RestoreSession(before);
            }
        }

        /// <summary>
        ///     ウィンドウ再表示後にも比較元と未保存値が独立して残ることを確認する。
        /// </summary>
        private static void ResumeAfterClose()
        {
            try
            {
                AudioToolsWindow.OpenGroup(SessionState.GetString(KEY + "path", ""));
                AudioGroupDocument doc = EditorWindow.GetWindow<AudioToolsWindow>().SelectedDocument;
                Check(doc.Draft.Items[0].Gain == 0.5f && doc.Baseline.Items[0].Gain == 2f, "draft survives window close and reopen");
                AudioToolsWindow.BeginPreview();
            }
            catch (Exception ex)
            {
                Debug.LogError(PREFIX + ex);
                Cleanup();
            }
        }

        /// <summary>
        ///     保存を一件ずつ検証し、失敗したグループや元音源を上書きしないことを確認する。
        /// </summary>
        private static AudioGroupDocument VerifyStorage(string path)
        {
            VerifyFixtures();
            AudioClip firstTone = TestTone("Tone220");
            var draft = new AudioGroupDraft();
            string folder = AudioGroupDocument.ROOT + "/Preview";
            List<AudioClip> found = AudioCandidatePanel.Discover(draft, folder);
            Check(found.Count == 2 && draft.Items.Count == 0, "search is nondestructive");
            AudioCandidatePanel.Add(draft, firstTone);
            AudioCandidatePanel.Add(draft, firstTone);
            draft.Items[0].Gain = 0.45f;
            draft.Mode = AudioGroup.Order.Sequential;
            List<AudioClip> more = AudioCandidatePanel.Discover(draft, folder);
            Check(more.Count == 1 && draft.Items[0].Gain == 0.45f && draft.Items.Count == 1, "rescan preserves gain, order and registration");
            var panel = new AudioCandidatePanel();
            string originalFixed = draft.FixedId;
            panel.SelectFolder(draft, folder);
            Check(panel.FoundCount == 1 && draft.Items.Count == 1, "folder selection immediately lists missing clip without registering");
            panel.AddChecked(draft);
            panel.Scan(draft);
            Check(panel.FoundCount == 0 && draft.Items.Count == 2 && draft.FixedId == originalFixed &&
                draft.Items[0].Gain == 0.45f && draft.Items[0].Id == originalFixed, "checked import and rescan preserve gain, order and fixed clip");
            var doc = new AudioGroupDocument { Path = path, New = true, Baseline = new AudioGroupDraft(), Draft = draft };
            AudioGroupDocument saved = doc.Save();
            SessionState.SetString(KEY + "guid", saved.AssetId);
            AudioGroupDocument stale = AudioGroupDocument.Load(path);
            foreach (float gain in new[] { 0f, 0.5f, 1f, 10f, 2f })
            {
                saved.Draft.Items[0].Gain = gain;
                saved = saved.Save();
                Check(saved.Baseline.Items[0].Gain == gain && saved.Draft.Mode == AudioGroup.Order.Sequential, "round trip " + gain);
            }

            foreach (float gain in new[] { 0f, 0.5f, 1f, 2f, 10f })
            {
                saved.Draft.Category = AudioGroup.Kind.Se;
                saved.Draft.Items[0].Gain = gain;
                saved = saved.Save();
                Check(saved.Baseline.Category == AudioGroup.Kind.Se && saved.Baseline.Items[0].Gain == gain, "SE round trip " + gain);
            }

            saved.Draft.Category = AudioGroup.Kind.Bgm;
            saved.Draft.Items[0].Gain = 2f;
            saved = saved.Save();
            stale.Draft.Items[0].Gain = 3f;
            bool refused = false;
            try { stale.Save(); } catch (IOException) { refused = true; }
            Check(refused && stale.Draft.Items[0].Gain == 3f && AudioGroupDocument.Load(path).Baseline.Items[0].Gain == 2f, "conflict preserves both sides");
            saved.Draft.Items[0].Gain = 11f;
            Check(saved.Draft.Validate() != "", "gain out of range rejected");
            saved.Draft.Items[0].Gain = 2f;
            string first = saved.Draft.Items[0].Id;
            saved.Draft.Items[0].Id = "";
            Check(saved.Draft.Validate() != "", "missing clip rejected");
            saved.Draft.Items[0].Id = first;
            string outside = saved.Path;
            saved.Path = "Assets/NotAnAudioToolsAsset.asset";
            refused = false;
            try { saved.Save(); } catch (IOException) { refused = true; }
            saved.Path = outside;
            Check(refused, "out of scope save refused");
            return saved;
        }

        /// <summary>
        ///     Play Modeのドメイン再読み込み後に進行を再開する。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Attach()
        {
            EditorApplication.playModeStateChanged += HandlePlayState;
        }

        /// <summary>
        ///     専用Sceneへの入場と、編集状態へ戻った後の下書きを確認する。
        /// </summary>
        private static void HandlePlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(KEY + "run", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _steps = SessionState.GetBool(KEY + "secondPlay", false) ? VerifyNextPlay() : VerifyPlayback();
                _resumeAt = 0;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!SessionState.GetBool(KEY + "pass", false)) { Cleanup(); return; }
                try
                {
                    AudioToolsWindow window = EditorWindow.GetWindow<AudioToolsWindow>();
                    window.SelectGroup(SessionState.GetString(KEY + "path", ""));
                    Check(window.SelectedDocument.Draft.Items[0].Gain == 0.5f && window.SelectedDocument.Baseline.Items[0].Gain == 0.72f,
                        "new saved baseline and unsaved draft survive Play exit");
                    if (!SessionState.GetBool(KEY + "secondPlay", false))
                    {
                        SessionState.SetBool(KEY + "secondPlay", true);
                        SessionState.SetBool(KEY + "pass", false);
                        EditorApplication.delayCall += AudioToolsWindow.BeginPreview;
                        return;
                    }

                    Debug.Log(PREFIX + "PASS all");
                }
                catch (Exception ex) { Debug.LogError(PREFIX + ex); }

                Cleanup();
            }
        }

        /// <summary>
        ///     Editorをブロックせずに、音声バッファの安定と次のフレームを待つ。
        /// </summary>
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _resumeAt) return;
            try
            {
                if (_steps.MoveNext())
                {
                    _resumeAt = EditorApplication.timeSinceStartup + Convert.ToDouble(_steps.Current);
                    EditorApplication.QueuePlayerLoopUpdate();
                    return;
                }

                SessionState.SetBool(KEY + "pass", true);
            }
            catch (Exception ex) { Debug.LogError(PREFIX + ex); }
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }

        /// <summary>
        ///     保存値からの開始、切り替え、SE制限、実出力、Sceneをまたぐ動作を検証する。
        /// </summary>
        private static IEnumerator VerifyPlayback()
        {
            yield return 0.35;
            GroupAudioPlayer player = AudioToolsTransport.Player();
            Check(player != null, "dedicated scene loaded");
            AudioGroup explore = MakeTestGroup(AudioGroup.Kind.Bgm);
            AudioGroup chase = MakeTestGroup(AudioGroup.Kind.Bgm, secondTone: true);
            AudioGroup foot = MakeTestGroup(AudioGroup.Kind.Se);
            AudioGroup enemy = MakeTestGroup(AudioGroup.Kind.Se, gain: 0.4f);
            AudioGroup test = AssetDatabase.LoadAssetAtPath<AudioGroup>(SessionState.GetString(KEY + "path", ""));
            Check(test != null && test.GetGain(0) == 2f && !player.PlayBgm(null), "next launch reads saved setting, null safe");
            player.SetMonitorLevel(1f);
            AudioPreviewExample example = UnityEngine.Object.FindFirstObjectByType<AudioPreviewExample>();
            using (var sample = new SerializedObject(example))
            {
                CheckSampleGroup(sample, "_inhaleSe", "Tone220", AudioGroup.Kind.Se);
                CheckSampleGroup(sample, "_footstepSe", "Tone220", AudioGroup.Kind.Se);
                CheckSampleGroup(sample, "_exploreBgm", "Tone220", AudioGroup.Kind.Bgm);
                CheckSampleGroup(sample, "_chaseBgm", "Tone440", AudioGroup.Kind.Bgm);
            }
            AudioListener.GetOutputData(Samples, 0);
            example.PlayInhale();
            yield return 0.15;
            Check(player.PlayingEffects == 1 && Sources().Single().clip == TestTone("Tone220") && Sources().Single().isPlaying,
                "manual: serialized SE assignment plays one Tone220");
            example.StopAudio();
            example.PlayExplore();
            yield return 0.45;
            Check(player.CurrentMusic == TestTone("Tone220") && Sources().Single().isPlaying, "manual: saved exploration Tone220");
            CheckListenerTone(220);
            example.SwitchToChase();
            yield return 0.4;
            CheckListenerTone(440);
            example.PlayExplore();
            example.FadeToChase();
            yield return 2.4;
            CheckListenerTone(440);
            example.PlayExplore();
            example.CrossfadeToChase();
            yield return 0.25;
            Check(player.IsCrossfading && player.OutgoingMusic == TestTone("Tone220") && player.CurrentMusic == TestTone("Tone440") &&
                Sources().Count(source => source.isPlaying) == 2, "manual: saved Tone220 and Tone440 overlap");
            yield return 2.1;
            Check(player.OutgoingMusic == null && Sources().Single().clip == TestTone("Tone440") && Sources().Single().isPlaying,
                "manual: saved chase Tone440 plays alone");
            CheckListenerTone(440);
            example.StopAudio();
            using (var sample = new SerializedObject(example))
            {
                // Play中のSceneインスタンスだけに検証用の参照を割り当てる。
                Check(sample.FindProperty("_audioPlayer").objectReferenceValue == player, "dedicated Inspector player assignment");
                sample.FindProperty("_exploreBgm").objectReferenceValue = explore;
                sample.FindProperty("_chaseBgm").objectReferenceValue = chase;
                sample.FindProperty("_footstepSe").objectReferenceValue = foot;
                sample.ApplyModifiedPropertiesWithoutUndo();
            }

            example.PlayExplore();
            yield return 0.18;
            AudioSource music = Sources().Single();
            int position = music.timeSamples;
            example.PlayExplore();
            Check(music.isPlaying && music.timeSamples >= position && Sources().Length == 1, "same BGM keeps position");
            example.PlayFootstep();
            Check(Sources().Length == 2, "Inspector BGM plus SE");
            example.StopAudio();

            var direct = player.gameObject.AddComponent<AudioSource>();
            direct.playOnAwake = false;
            direct.clip = explore.GetClip(0);
            direct.loop = true;
            AudioListener.GetOutputData(Samples, 0);
            direct.Play();
            yield return 0.32;
            float reference = Rms();
            for (int attempt = 0; attempt < 12 && reference <= 0.00001f; attempt++)
            {
                yield return 0.1;
                reference = Rms();
            }
            direct.Stop();
            UnityEngine.Object.Destroy(direct);
            Check(reference > 0.00001f, $"direct listener RMS {reference:G6}");
            foreach (AudioGroup group in new[] { explore, foot })
            {
                foreach (float gain in new[] { 0f, 0.5f, 1f, 2f, 10f })
                {
                    player.StopBgm();
                    player.StopSe();
                    Check(player.Audition(group, 0, true), "matrix start " + group.Category);
                    player.SetAuditionGain(group, group.GetClip(0), gain);
                    yield return 0.33;
                    float ratio = Rms() / reference;
                    Check(Mathf.Abs(ratio - gain) <= Mathf.Max(0.015f, 0.045f * gain),
                        $"{group.Category} gain {gain:G4} measured {ratio:G5}");
                }
            }

            player.StopSe();
            player.StopBgm();
            var doc = new AudioGroupDocument
            {
                Path = AssetDatabase.GetAssetPath(test), Baseline = AudioGroupDraft.Read(explore), Draft = AudioGroupDraft.Read(explore)
            };
            doc.Draft.Items[0].Gain = 0.4f;
            var transport = new AudioToolsTransport();
            AudioGroup copy = transport.Refresh(doc, true, player);
            player.Audition(copy, 0, true);
            yield return 0.18;
            music = Sources().Single();
            position = music.timeSamples;
            transport.Refresh(doc, false, player);
            doc.Draft.Items[0].Gain = 0.7f;
            transport.Refresh(doc, false, player);
            Check(music.timeSamples >= position && Mathf.Approximately(music.volume, 0.1f), "A unchanged while editing B, no rewind");
            transport.Refresh(doc, true, player);
            Check(Mathf.Approximately(music.volume, 0.07f), "B applied live");
            transport.Clear();
            Check(player.PlaySe(foot) && player.PlaySe(enemy) && Sources().Any(x => Mathf.Approximately(x.volume, 0.04f)), "same clip independent gain");
            player.StopSe();

            AudioGroupDraft config = AudioGroupDraft.Read(foot);
            config.Limit = 2;
            config.Interval = 0.10f;
            config.Mode = AudioGroup.Order.Sequential;
            var effect = ScriptableObject.CreateInstance<AudioGroup>();
            config.Apply(effect);
            Check(player.PlaySe(effect) && !player.PlaySe(effect), "minimum interval rejects");
            yield return 0.18;
            Check(player.PlaySe(effect) && Sources().Select(x => x.clip).Distinct().Count() == 2, "rejected request did not advance sequence");
            yield return 0.16;
            Check(!player.PlaySe(effect), "voice cap rejects");
            player.StopSe(effect);
            config.Mode = AudioGroup.Order.Random;
            config.Interval = 0f;
            config.Apply(effect);
            yield return 0.15;
            Check(player.PlaySe(effect) && player.PlaySe(effect) && Sources().Select(x => x.clip).Distinct().Count() == 2, "random avoids immediate repeat");
            player.StopSe();
            UnityEngine.Object.Destroy(effect);
            var empty = ScriptableObject.CreateInstance<AudioGroup>();
            Check(!player.PlayBgm(empty), "empty group safe");
            UnityEngine.Object.Destroy(empty);
            Check(player.PlayBgm(test) && Mathf.Approximately(Sources().Single().volume, 0.2f), "saved group playback at 200%");
            yield return 2.25;
            Check(Sources().Any(x => x.clip == test.GetClip(1)), "continuous BGM advances to next candidate");
            yield return 1.95;
            Check(Sources().Any(x => x.clip == test.GetClip(0)), "sequence wraps to first candidate");

            player.SwitchBgm(explore);
            Check(Sources().Length == 1 && Sources()[0].clip == explore.FixedClip, "instant switch");
            player.FadeSwitchBgm(chase, 1f);
            yield return 0.2;
            Check(Sources().Length == 1 && Sources()[0].clip == explore.FixedClip, "sequential first half only old");
            yield return 0.55;
            Check(Sources().Length == 1 && Sources()[0].clip == chase.FixedClip, "sequential second half only new");
            yield return 0.4;
            Check(Mathf.Approximately(Sources().Single().volume, chase.GetGain(0) * 0.1f), "fade ends at saved gain");
            player.CrossfadeBgm(explore, 0.8f);
            yield return 0.22;
            Check(Sources().Length == 2 && Sources().All(x => x.volume > 0f), "overlapping crossfade");
            player.CrossfadeBgm(chase, 0.4f);
            player.StopBgm();
            yield return 0.15;
            Check(Sources().Length == 0, "interrupt and stop remove both voices");

            player.PlayBgm(explore);
            music = Sources().Single();
            player.PlaySe(foot);
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(AudioToolsWindow.NEXT_SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return 0.05;
            yield return 0.2;
            GroupAudioPlayer next = AudioToolsTransport.Player();
            Check(next != null && player == null && music != null && music.isPlaying && Sources().Length == 1 &&
                UnityEngine.Object.FindObjectsByType<GroupPlayback>(FindObjectsSortMode.None).Length == 1, "BGM survives, SE ends, one engine");
            next.PlayBgm(explore);
            Check(Sources().Single() == music, "new scene facade uses playing BGM");
            next.StopBgm();
            next.PlaySe(foot);
            yield return 2.35;
            Check(UnityEngine.Object.FindFirstObjectByType<GroupPlayback>().GetComponentsInChildren<AudioSource>().Length == 0, "ended voices released");
            UnityEngine.Object.Destroy(explore);
            UnityEngine.Object.Destroy(chase);
            UnityEngine.Object.Destroy(foot);
            UnityEngine.Object.Destroy(enemy);
            IEnumerator live = VerifyLiveSave(next);
            while (live.MoveNext()) { yield return live.Current; }
        }

        /// <summary>
        ///     操作画面と同じ保存処理で、連続再生・競合・部分成功・比較元を照合する。
        /// </summary>
        private static IEnumerator VerifyLiveSave(GroupAudioPlayer player)
        {
            AudioToolsWindow window = EditorWindow.GetWindow<AudioToolsWindow>();
            string musicPath = SessionState.GetString(KEY + "path", "");
            string effectPath = SessionState.GetString(KEY + "sePath", "");
            window.SelectGroup(musicPath);
            AudioGroupDocument bgm = window.SelectedDocument;
            window.SetComparison(false, player);
            window.Play(bgm, player, 0);
            yield return 0.15;
            AudioSource source = Sources().Single();
            window.SelectGroup(effectPath);
            AudioGroupDocument se = window.SelectedDocument;
            window.Play(se, player, 0);
            AudioSource effect = Sources().Single(x => x != source);
            AudioToolsWindow.Selection musicSelection = window.Channel(AudioGroup.Kind.Bgm);
            AudioToolsWindow.Selection effectSelection = window.Channel(AudioGroup.Kind.Se);
            musicSelection.SelectGroup(musicPath);
            musicSelection.ClipId = bgm.Draft.Items[0].Id;
            // 一覧で選べる別のSEを下書きとして用意し、本編用グループに依存せず切替を試す。
            AudioGroupDraft alternateEffect = window.BeginCreation(AudioGroup.Kind.Se, false);
            AudioCandidatePanel.Add(alternateEffect, TestTone("Tone440"));
            Check(window.FinishCreation("Verification-" + Guid.NewGuid().ToString("N")) &&
                effectSelection.Path == window.SelectedDocument.Path && effectSelection.Path != effectPath,
                "select a separate temporary SE draft");
            effectSelection.SelectGroup(effectPath);
            window.SelectTab(AudioToolsWindow.Page.Transition);
            window.SelectTab(AudioToolsWindow.Page.Library);
            window.SelectTab(AudioToolsWindow.Page.Audition);
            Check(musicSelection.Path == musicPath && musicSelection.ClipId == bgm.Draft.Items[0].Id &&
                effectSelection.Path == effectPath && bgm.Dirty && source.isPlaying && effect.isPlaying,
                "SE selection and three tabs preserve BGM selection, active sources and draft");
            bgm.DeletePending = true;
            Check(window.SaveReviewed(new[] { bgm }) == 0 && bgm.DeletePending && source.isPlaying &&
                File.Exists(musicPath), "playing group deletion refused without stopping BGM");
            bgm.DeletePending = false;
            bgm.Draft.Category = AudioGroup.Kind.Se;
            Check(window.SaveReviewed(new[] { bgm }) == 0 && bgm.Baseline.Category == AudioGroup.Kind.Bgm &&
                bgm.Draft.Category == AudioGroup.Kind.Se && source.isPlaying, "playing group kind change refused with draft intact");
            bgm.Draft.Category = AudioGroup.Kind.Bgm;
            float volumeAtBaseline = source.volume;
            float seGain = se.Draft.Items[0].Gain;
            window.AdjustGain(bgm, 0, 80f, player);
            window.AdjustGain(bgm, 0, bgm.Draft.Items[0].Gain * 100f + 10f, player);
            Check(Mathf.Approximately(bgm.Draft.Items[0].Gain, 0.9f) && Mathf.Approximately(seGain, se.Draft.Items[0].Gain),
                "editing music returns to draft and preserves independent effect gain");
            window.AdjustGain(bgm, 0, 80f - 10f, player);
            Check(Mathf.Approximately(bgm.Draft.Items[0].Gain, 0.7f), "80 minus ten points produces 70 percent");
            window.AdjustGain(bgm, 0, 995f + 10f, player);
            Check(Mathf.Approximately(bgm.Draft.Items[0].Gain, 10f), "995 plus ten points clamps at 1000 percent");
            window.AdjustGain(bgm, 0, 5f - 10f, player);
            Check(bgm.Draft.Items[0].Gain == 0 && window.HasComparableGain(), "5 minus ten points clamps at zero");
            window.SetComparison(false, player);
            int sampleBeforeEdit = source.timeSamples;
            window.AdjustGain(bgm, 0, 100f, player);
            Check(Mathf.Approximately(source.volume, volumeAtBaseline * 0.5f) && source.isPlaying && source.timeSamples >= sampleBeforeEdit,
                "adjustment during comparison restores live draft without rewinding");
            window.SetComparison(false, player);
            float previousVolume = source.volume;
            int position = source.timeSamples;
            int effectPosition = effect.timeSamples;
            bgm.Draft.Items[0].Gain = 0.65f;
            window.Changed(bgm);
            Check(window.SaveReviewed(new[] { bgm }) == 1 && source.isPlaying && effect.isPlaying &&
                source.timeSamples >= position && effect.timeSamples >= effectPosition && Sources().Contains(source),
                "individual save while BGM and SE keep their sources and positions");
            Check(Mathf.Approximately(source.volume, previousVolume * 0.65f / 2f), "A updates live after successful save");

            window.SelectGroup(musicPath);
            bgm = window.SelectedDocument;
            bgm.Draft.Items[0].Gain = 0.70f;
            se.Draft.Items[0].Gain = 0.62f;
            Check(window.SaveReviewed(new[] { bgm, se }) == 2 && source.isPlaying && effect.isPlaying,
                "batch saved both groups in Play without stopping voices");
            window.SelectGroup(effectPath);
            se = window.SelectedDocument;
            AudioGroupDocument external = AudioGroupDocument.Load(effectPath);
            external.Draft.Items[0].Gain = 0.64f;
            external.Save();
            se.Draft.Items[0].Gain = 0.81f;
            window.SelectGroup(musicPath);
            bgm = window.SelectedDocument;
            bgm.Draft.Items[0].Gain = 0.72f;
            Check(window.SaveReviewed(new[] { se, bgm }) == 1 && se.Baseline.Items[0].Gain == 0.62f &&
                se.Draft.Items[0].Gain == 0.81f && se.Dirty && AudioGroupDocument.Load(effectPath).Draft.Items[0].Gain == 0.64f,
                "partial save preserves failed A/B, external value and successful group");
            bgm = window.SelectedDocument;
            Check(bgm.Baseline.Items[0].Gain == 0.72f && source.isPlaying, "successful A updated after partial save");
            bgm.Draft.Items[0].Gain = 0.5f;
            window.Changed(bgm);
        }

        /// <summary>
        ///     保存済み設定を新しいPlayで再生部品から直接読み出す。
        /// </summary>
        private static IEnumerator VerifyNextPlay()
        {
            yield return 0.2;
            GroupAudioPlayer player = AudioToolsTransport.Player();
            string path = SessionState.GetString(KEY + "path", "");
            AudioGroup saved = AssetDatabase.LoadAssetAtPath<AudioGroup>(path);
            player.SetMonitorLevel(1f);
            Check(saved.Mode == AudioGroup.Order.Sequential && player.PlayBgm(saved) &&
                Mathf.Approximately(Sources().Single().volume, 0.072f), "next Play uses Play-saved 72 percent, not the 50 percent draft");
            player.StopBgm();
        }

        /// <summary> 試験で再生する保存設定が指定の小音量確認音だけを指すことを確認する。 </summary>
        private static void CheckSampleGroup(SerializedObject sample, string field, string tone, AudioGroup.Kind category)
        {
            var group = sample.FindProperty(field).objectReferenceValue as AudioGroup;
            AudioClip clip = TestTone(tone);
            Check(group != null && group.Category == category && group.Mode == AudioGroup.Order.Fixed &&
                group.Count == 1 && group.FixedClip == clip && group.GetClip(0) == clip,
                "manual: dedicated sample assignment " + field);
        }

        /// <summary> 出力波形がゼロを上向きに横切る周期から、専用確認音の周波数を測る。 </summary>
        private static void CheckListenerTone(int expected)
        {
            AudioListener.GetOutputData(Samples, 0);
            int first = -1;
            int last = 0;
            int crossings = 0;
            for (int i = 1; i < Samples.Length; i++)
            {
                if (Samples[i - 1] > 0f || Samples[i] <= 0f) continue;
                if (first < 0) first = i;
                last = i;
                crossings++;
            }

            float hz = crossings < 2 ? 0f : (crossings - 1f) * AudioSettings.outputSampleRate / (last - first);
            Check(Mathf.Abs(hz - expected) <= 3f, $"manual: listener {hz:F1} Hz (expected {expected})");
        }

        /// <summary>
        ///     検証音のListener出力バッファから実効値を計算する。
        /// </summary>
        private static float Rms()
        {
            AudioListener.GetOutputData(Samples, 0);
            double sum = 0;
            foreach (float value in Samples) { sum += value * value; }
            return (float)Math.Sqrt(sum / Samples.Length);
        }

        /// <summary>
        ///     チームが設定を変更した場合に、未知の素材を自動試験で増幅しないようにする。
        /// </summary>
        private static void VerifyFixtures()
        {
            foreach (string name in new[] { "Tone220", "Tone440" })
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioGroupDocument.ROOT + "/Preview/" + name + ".wav");
                if (clip == null || clip.channels != 1 || clip.samples != 96000) throw new InvalidDataException("確認音の形式が変更されています。");
                float[] pcm = new float[clip.samples];
                if (!clip.GetData(pcm, 0) || pcm.Any(v => !float.IsFinite(v) || Mathf.Abs(v) > 0.0021f))
                    throw new InvalidDataException("検証用の小音量信号を確認できません。");
            }

        }

        /// <summary>
        ///     付属の低振幅確認音だけを指定して取得する。
        /// </summary>
        private static AudioClip TestTone(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(AudioGroupDocument.ROOT + "/Preview/" + name + ".wav");

        /// <summary>
        ///     利用者のグループAssetから独立した検証設定を生成する。
        /// </summary>
        private static AudioGroupDraft MakeTestDraft(AudioGroup.Kind kind, bool secondTone = false, float gain = 1f)
        {
            var draft = new AudioGroupDraft { Category = kind };
            AudioCandidatePanel.Add(draft, TestTone(secondTone ? "Tone440" : "Tone220"));
            AudioCandidatePanel.Add(draft, TestTone(secondTone ? "Tone220" : "Tone440"));
            draft.Items[0].Gain = gain;
            return draft;
        }

        /// <summary>
        ///     Sceneへ渡す一時グループをメモリ内だけに作る。
        /// </summary>
        private static AudioGroup MakeTestGroup(AudioGroup.Kind kind, bool secondTone = false, float gain = 1f)
        {
            var group = ScriptableObject.CreateInstance<AudioGroup>();
            MakeTestDraft(kind, secondTone, gain).Apply(group);
            return group;
        }

        /// <summary>
        ///     専用の出力系で実際に発音しているSourceを列挙する。
        /// </summary>
        private static AudioSource[] Sources() => UnityEngine.Object.FindFirstObjectByType<GroupPlayback>().GetComponentsInChildren<AudioSource>()
            .Where(x => x.isPlaying).ToArray();

        /// <summary>
        ///     成否と測定値をEditorログへ記録する。
        /// </summary>
        private static void Check(bool success, string detail)
        {
            if (!success) throw new InvalidOperationException(detail);
            Debug.Log(PREFIX + detail);
        }

        /// <summary>
        ///     自分が作ったGUIDの検証Assetだけを除去し、操作画面の状態を復元する。
        /// </summary>
        private static void Cleanup()
        {
            string path = SessionState.GetString(KEY + "path", "");
            string id = SessionState.GetString(KEY + "guid", "");
            if (path.StartsWith(AudioGroupDocument.DATA + "Verification-", StringComparison.Ordinal) &&
                id != "" && AssetDatabase.AssetPathToGUID(path) == id) AssetDatabase.DeleteAsset(path);
            string sePath = SessionState.GetString(KEY + "sePath", "");
            string seId = SessionState.GetString(KEY + "seGuid", "");
            if (sePath.StartsWith(AudioGroupDocument.DATA + "Verification-", StringComparison.Ordinal) &&
                seId != "" && AssetDatabase.AssetPathToGUID(sePath) == seId) AssetDatabase.DeleteAsset(sePath);
            SessionState.EraseString(KEY + "sePath");
            SessionState.EraseString(KEY + "seGuid");
            SessionState.EraseBool(KEY + "secondPlay");
            string prior = SessionState.GetString(KEY + "prior", "");
            if (EditorWindow.HasOpenInstances<AudioToolsWindow>())
            {
                AudioToolsWindow window = EditorWindow.GetWindow<AudioToolsWindow>();
                window.RestoreSession(prior);
                if (!SessionState.GetBool(KEY + "wasOpen", false)) window.Close();
            }

            SessionState.SetString(AudioToolsWindow.SESSION, prior);
            SessionState.EraseBool(KEY + "wasOpen");
            SessionState.EraseBool(KEY + "run");
            SessionState.EraseBool(KEY + "pass");
            SessionState.EraseString(KEY + "guid");
            SessionState.EraseString(KEY + "path");
            SessionState.EraseString(KEY + "prior");
        }
    }
}
