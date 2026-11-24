using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Yamamoto.AudioTools
{
    /// <summary>
    ///     Sceneをまたぐ二つのBGM出力と、再生中だけ存在するSE出力を管理する。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class GroupPlayback : MonoBehaviour
    {
        internal static GroupPlayback Current { get; private set; }

        /// <summary> 試聴画面に表示する現在の曲を取得する。 </summary>
        internal AudioClip CurrentClip => _music?.Clip;
        /// <summary> 切り替え中の元の曲を取得する。 </summary>
        internal AudioClip OutgoingClip => _outgoing?.Clip;
        /// <summary> 現在発音中のSE数を取得する。 </summary>
        internal int EffectCount => _effects.Count;
        /// <summary> クロスフェード中かどうかを取得する。 </summary>
        internal bool IsCrossfading => _outgoing != null && _transition == Transition.Cross;

        internal enum Transition { Immediate, Sequential, Cross }

        private readonly List<Voice> _effects = new List<Voice>();
        private readonly Dictionary<AudioGroup, int> _previousEffects = new Dictionary<AudioGroup, int>();
        private readonly Dictionary<AudioGroup, double> _nextEffectTime = new Dictionary<AudioGroup, double>();
        private AudioMixerGroup _output;
        private Voice _music;
        private Voice _outgoing;
        private Transition _transition;
        private float _duration;
        private double _start;
        private float _outgoingWeight;
        private bool _repeat;
        private int _auditionIndex;
        private float _monitor = 1f;

        /// <summary>
        ///     明示的な再生・試聴要求を受けた時点で出力を用意する。
        /// </summary>
        internal static GroupPlayback GetOrCreate(AudioMixerGroup output)
        {
            if (Current != null) return Current;
            GameObject root = new GameObject("AudioTools Playback");
            DontDestroyOnLoad(root);
            Current = root.AddComponent<GroupPlayback>();
            Current._output = output;
            return Current;
        }

        /// <summary>
        ///     次の候補を検証し、現在のフェードを引き継いで切り替える。
        /// </summary>
        internal bool ChangeMusic(AudioGroup group, bool repeat, float duration, Transition transition, int index)
        {
            if (_music != null && (_music.Source.isPlaying || !_music.Started || AudioListener.pause) &&
                _music.Group == group && (index < 0 || _music.Clip == group.GetClip(index)))
            {
                _repeat = repeat;
                _auditionIndex = index;
                ConfigureLoop();
                return true;
            }

            int selected = index >= 0 ? index : group.SelectNext(-1);
            if (selected < 0 || group.GetClip(selected) == null || !float.IsFinite(duration)) return false;
            if (_music != null && _music.Started && (_outgoing == null || _music.Weight >= _outgoing.Weight))
            {
                Release(_outgoing);
                _outgoing = _music;
            }
            else
            {
                Release(_music);
            }

            _music = CreateVoice(group, selected);
            _repeat = repeat;
            _auditionIndex = index;
            _transition = transition;
            _duration = Mathf.Max(0f, duration);
            _start = AudioSettings.dspTime;
            _outgoingWeight = _outgoing?.Weight ?? 0f;
            if (_outgoing == null || transition == Transition.Immediate || _duration <= 0f)
            {
                Release(_outgoing);
                _outgoing = null;
                StartMusic(1f);
            }
            else if (transition == Transition.Cross)
            {
                StartMusic(0f);
            }

            return true;
        }

        /// <summary>
        ///     SEの受付条件を先に判定し、拒否した要求で選曲順を進めない。
        /// </summary>
        internal bool PlayEffect(AudioGroup group, int index)
        {
            CollectEffects();
            double now = AudioSettings.dspTime;
            if (_nextEffectTime.TryGetValue(group, out double next) && now < next) return false;
            int playing = 0;
            foreach (Voice voice in _effects)
            {
                if (voice.Group == group) playing++;
            }

            if (playing >= group.VoiceLimit) return false;
            int previous = _previousEffects.TryGetValue(group, out int last) ? last : -1;
            int selected = index >= 0 ? index : group.SelectNext(previous);
            if (selected < 0 || group.GetClip(selected) == null) return false;
            Voice effect = CreateVoice(group, selected);
            SetWeight(effect, 1f);
            effect.Source.Play();
            _effects.Add(effect);
            if (index < 0) _previousEffects[group] = selected;
            _nextEffectTime[group] = now + group.MinInterval;
            return true;
        }

        /// <summary>
        ///     フェード待機中の音源も含めて停止する。
        /// </summary>
        internal void StopMusic()
        {
            Release(_music);
            Release(_outgoing);
            _music = null;
            _outgoing = null;
        }

        /// <summary>
        ///     対象グループの発音を終了する。
        /// </summary>
        internal void StopEffects(AudioGroup group)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (group != null && _effects[i].Group != group) continue;
                Release(_effects[i]);
                _effects.RemoveAt(i);
            }
        }

        /// <summary>
        ///     音源と用途が両方一致するボイスにだけ変更を適用する。
        /// </summary>
        internal void SetGain(AudioGroup group, AudioClip clip, float gain)
        {
            ApplyGain(_music, group, clip, gain);
            ApplyGain(_outgoing, group, clip, gain);
            foreach (Voice voice in _effects)
            {
                ApplyGain(voice, group, clip, gain);
            }
        }

        /// <summary>
        ///     基準音量に影響しない全体試聴倍率を適用する。
        /// </summary>
        internal void SetMonitor(float level)
        {
            _monitor = Mathf.Clamp01(level);
            if (_music != null) SetWeight(_music, _music.Weight);
            if (_outgoing != null) SetWeight(_outgoing, _outgoing.Weight);
            foreach (Voice voice in _effects)
            {
                SetWeight(voice, voice.Weight);
            }
        }

        private void OnEnable() => SceneManager.activeSceneChanged += HandleSceneChanged;

        private void Update()
        {
            if (AudioListener.pause) return;
            CollectEffects();
            if (_music == null) return;
            if (_outgoing != null) AdvanceTransition();
            if (!_music.Started || _music.Source.isPlaying) return;
            if (!_repeat || _music.Group == null)
            {
                StopMusic();
                return;
            }

            int next = _auditionIndex >= 0 ? _auditionIndex : _music.Group.SelectNext(_music.Index);
            if (next < 0 || next >= _music.Group.Count || _music.Group.GetClip(next) == null)
            {
                StopMusic();
                return;
            }

            _music.Assign(_music.Group, next);
            StartMusic(_music.Weight);
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            StopMusic();
            StopEffects(null);
            if (Current == this) Current = null;
        }

        /// <summary>
        ///     Scene境界でSEと選曲履歴を片付け、BGMの再生位置を保つ。
        /// </summary>
        private void HandleSceneChanged(Scene before, Scene after)
        {
            StopEffects(null);
            _previousEffects.Clear();
            _nextEffectTime.Clear();
        }

        /// <summary>
        ///     DSP時刻から二つの出力に別々のフェード倍率を適用する。
        /// </summary>
        private void AdvanceTransition()
        {
            float t = Mathf.Clamp01((float)((AudioSettings.dspTime - _start) / _duration));
            bool sequential = _transition == Transition.Sequential;
            float oldWeight = sequential ? Mathf.Clamp01(1f - 2f * t) : 1f - t;
            SetWeight(_outgoing, _outgoingWeight * oldWeight);
            if (!sequential || t >= 0.5f)
            {
                if (sequential && _outgoing.Source.isPlaying) _outgoing.Source.Stop();
                float weight = sequential ? 2f * t - 1f : t;
                if (!_music.Started) StartMusic(weight);
                else SetWeight(_music, weight);
            }

            if (t < 1f) return;
            Release(_outgoing);
            _outgoing = null;
        }

        /// <summary>
        ///     固定または候補指定の継続再生にはAudioSourceの単曲ループを使う。
        /// </summary>
        private void ConfigureLoop() =>
            _music.Source.loop = _repeat && (_auditionIndex >= 0 || _music.Group.Mode == AudioGroup.Order.Fixed);

        /// <summary>
        ///     新しい曲を現在の調整倍率から開始する。
        /// </summary>
        private void StartMusic(float weight)
        {
            ConfigureLoop();
            SetWeight(_music, weight);
            _music.Source.Play();
            _music.Started = true;
        }

        /// <summary>
        ///     自動再生しない2D音源を専用の増幅経路に接続する。
        /// </summary>
        private Voice CreateVoice(AudioGroup group, int index)
        {
            GameObject child = new GameObject(group.name);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = _output;
            Voice voice = new Voice { Source = source };
            voice.Assign(group, index);
            return voice;
        }

        /// <summary>
        ///     +20 dBの専用経路に対して10分の1の入力を渡し、個別倍率を復元する。
        /// </summary>
        private void SetWeight(Voice voice, float weight)
        {
            voice.Weight = weight;
            voice.Source.volume = Mathf.Clamp(voice.Gain, 0f, 10f) * 0.1f * weight * _monitor;
        }

        /// <summary>
        ///     比較対象の音だけを現在の再生位置で更新する。
        /// </summary>
        private void ApplyGain(Voice voice, AudioGroup group, AudioClip clip, float gain)
        {
            if (voice == null || voice.Group != group || voice.Clip != clip) return;
            voice.Gain = gain;
            SetWeight(voice, voice.Weight);
        }

        /// <summary>
        ///     再生を終えたSEを発音数から外し、Sourceを解放する。
        /// </summary>
        private void CollectEffects()
        {
            if (AudioListener.pause) return;
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (_effects[i].Source.isPlaying) continue;
                Release(_effects[i]);
                _effects.RemoveAt(i);
            }
        }

        /// <summary>
        ///     発音を即時停止し、所有する子オブジェクトだけを破棄する。
        /// </summary>
        private static void Release(Voice voice)
        {
            if (voice == null || voice.Source == null) return;
            voice.Source.Stop();
            Destroy(voice.Source.gameObject);
        }

        /// <summary>
        ///     再生位置に関係するSourceと、個別ゲイン・フェード倍率を保持する。
        /// </summary>
        private sealed class Voice
        {
            public AudioSource Source;
            public AudioGroup Group;
            public AudioClip Clip;
            public float Gain;
            public float Weight;
            public bool Started;
            public int Index;

            /// <summary>
            ///     設定から今回の音源と基準値を取得する。
            /// </summary>
            public void Assign(AudioGroup group, int index)
            {
                Group = group;
                Index = index;
                Clip = group.GetClip(index);
                Gain = group.GetGain(index);
                Source.clip = Clip;
            }
        }
    }
}
