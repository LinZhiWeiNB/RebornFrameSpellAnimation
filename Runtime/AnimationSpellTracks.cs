using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebornFrame.Spell.Animation
{
    /// <summary>Unity Animator 轨道种类，作者 0、本地编号 5。</summary>
    public static class AnimationSpellTracks
    {
        /// <summary>注册给技能包的轨道种类。</summary>
        public static readonly SpellPresentationTrackId TypeId = SpellPresentationTrackId.Combine(0, 5);

        private static readonly Dictionary<string, AnimationClip> Clips = new();
        private static readonly Dictionary<string, GameObject> Prefabs = new();
        private static readonly Dictionary<ulong, Animator> Animators = new();

        /// <summary>把单位绑定到 Animator。</summary>
        /// <param name="stableId">单位稳定 ID。</param>
        /// <param name="animator">要播放状态的 Animator。null 表示解除绑定。</param>
        public static void Bind(ulong stableId, Animator animator)
        {
            if (stableId == 0) throw new ArgumentException("稳定 ID 不能为 0。", nameof(stableId));

            if (animator == null) Animators.Remove(stableId);
            else Animators[stableId] = animator;
        }

        /// <summary>
        ///     预先登记动画片段。预备时优先使用这里的片段，不再从 Resources 加载。
        /// </summary>
        /// <param name="key">与轨道参数里的 clip_key 相同的键。</param>
        /// <param name="clip">动画片段。</param>
        public static void RegisterClip(string key, AnimationClip clip)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("动画键不能为空。", nameof(key));

            if (clip == null) throw new ArgumentNullException(nameof(clip));

            Clips[key] = clip;
        }

        /// <summary>
        ///     收下数据包解开后的预制体。
        ///     数据包不会把 AssetBundle 的字节交到这里。
        /// </summary>
        /// <param name="key">预制体键。</param>
        /// <param name="prefab">预制体。</param>
        public static void AcceptPrefab(string key, GameObject prefab)
        {
            if (string.IsNullOrEmpty(key) || prefab == null) return;

            Prefabs[key] = prefab;
        }

        /// <summary>取出已经收下的动画预制体。</summary>
        public static bool TryGetPrefab(string key, out GameObject prefab)
        {
            if (string.IsNullOrEmpty(key))
            {
                prefab = null;
                return false;
            }

            return Prefabs.TryGetValue(key, out prefab);
        }

        /// <summary>向技能包注册 Animation 轨道。重复调用没有效果。</summary>
        public static void EnsureRegistered()
        {
            if (SpellPresentation.IsRegistered(TypeId)) return;

            SpellPresentation.Register(TypeId, new Factory());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnLoad()
        {
            EnsureRegistered();
        }

        private sealed class Factory : ISpellPresentationTrackFactory
        {
            public void Prepare(ReadOnlySpan<byte> parameters)
            {
                var config = AnimationSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (string.IsNullOrEmpty(config.StateName))
                    throw new ArgumentException("Animation 轨道没有状态名。");

                if (string.IsNullOrEmpty(config.ClipKey) || Clips.ContainsKey(config.ClipKey)) return;

                var clip = Resources.Load<AnimationClip>(config.ClipKey);
                if (clip == null) throw new ArgumentException($"动画片段 {config.ClipKey} 不存在。");

                Clips[config.ClipKey] = clip;
            }

            public ISpellPresentationTrack Create(ReadOnlySpan<byte> parameters, in SpellPresentationContext context)
            {
                var config = AnimationSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (!Animators.TryGetValue(context.OwnerStableId, out var animator))
                    throw new InvalidOperationException($"单位 {context.OwnerStableId} 没有绑定 Animator。");

                return new Track(animator, config.StateName, config.Layer, config.StopState);
            }
        }

        private sealed class Track : ISpellPresentationTrack
        {
            private readonly Animator _animator;
            private readonly int _layer;
            private readonly string _stateName;
            private readonly string _stopState;

            public Track(Animator animator, string stateName, int layer, string stopState)
            {
                _animator = animator;
                _stateName = stateName;
                _layer = layer;
                _stopState = stopState ?? string.Empty;
            }

            public void Play()
            {
                if (_animator == null) return;

                _animator.Play(_stateName, _layer, 0f);
            }

            public void Stop(SpellEndReason reason)
            {
                if (_animator == null || _stopState.Length == 0) return;

                _animator.Play(_stopState, _layer, 0f);
            }

            public void Show(float time, bool play)
            {
                if (_animator == null) return;

                var seconds = Math.Max(0f, time);
                _animator.speed = 1f;
                _animator.Play(_stateName, _layer, 0f);
                if (seconds > 0f) _animator.Update(seconds);
                _animator.speed = play ? 1f : 0f;
            }
        }
    }
}
