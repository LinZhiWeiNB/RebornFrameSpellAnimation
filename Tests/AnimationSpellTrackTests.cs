using Google.Protobuf;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor.Animations;
using UnityEngine;

namespace RebornFrame.Spell.Animation.Tests
{
    public class AnimationSpellTrackTests
    {
        private GameObject _host;
        private SpellStartup _startup;
        private World _world;

        [TearDown]
        public void TearDown()
        {
            _startup?.Dispose();
            if (_world is { IsCreated: true }) _world.Dispose();
            SpellPresentation.Reset();
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void Play_SwitchesBoundAnimatorState()
        {
            var controller = new AnimatorController();
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Cast");
            controller.layers[0].stateMachine.AddState("Idle");
            _host = new GameObject("actor");
            var animator = _host.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            AnimationSpellTracks.Bind(1, animator);
            AnimationSpellTracks.EnsureRegistered();
            SpellPresentation.SetFrameDuration(0.1f);

            _world = new World(nameof(Play_SwitchesBoundAnimatorState));
            _startup = new SpellStartup();
            var spell = SpellId.Combine(1, 1);
            var data = new SpellModuleData
            {
                Spells =
                {
                    new SpellData
                    {
                        Id = spell.Value,
                        Activation = (int)SpellActivation.Passive,
                        PresentationTracks =
                        {
                            new SpellPresentationTrack
                            {
                                TypeId = AnimationSpellTracks.TypeId.Value,
                                Parameters = new AnimationSpellTrackConfig
                                {
                                    StateName = "Cast",
                                    StopState = "Idle"
                                }.ToByteString()
                            }
                        }
                    }
                }
            };
            _startup.AddModuleData(data);
            _startup.Start(_world);
            _startup.InitializeUnit(_world.EntityManager.CreateEntity(), 1);
            Assert.IsTrue(SpellAccess.TryGrant(_world.EntityManager, spell, 1));
            _startup.Tick(0);
            animator.Update(0f);
            Assert.AreEqual("Cast", CurrentState(animator));

            Assert.IsTrue(SpellAccess.TryRevoke(_world.EntityManager, spell, 1));
            _startup.Tick(1);
            animator.Update(0f);
            Assert.AreEqual("Idle", CurrentState(animator));
        }

        private static string CurrentState(Animator animator)
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsName("Cast")
                ? "Cast"
                : animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")
                    ? "Idle"
                    : string.Empty;
        }
    }
}
