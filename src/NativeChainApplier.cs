using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SoftSway
{
    /// <summary>
    /// Writes values into the game's own breast/hip DynamicBone_Ver02 chains and can put the
    /// game's original values back. Breasts are written through Patterns[0] (the "normal" pattern)
    /// and re-selected with setPtn, hips are written to the live particles, the same way
    /// BreastPhysicsController and SomaDynamics do it.
    /// </summary>
    internal sealed class NativeChainApplier
    {
        private static readonly FieldInfo ParticlesField = typeof(DynamicBone_Ver02)
            .GetField("Particles", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ChaControl _cha;
        private readonly Dictionary<int, Snapshot> _snapshots = new Dictionary<int, Snapshot>();

        public bool BreastApplied { get; private set; }
        public bool ButtApplied { get; private set; }

        public NativeChainApplier(ChaControl cha)
        {
            _cha = cha;
        }

        public static DynamicBone_Ver02 GetBone(ChaControl cha, ChaInfo.DynamicBoneKind kind)
        {
            // Low-poly / not fully loaded characters throw instead of returning null.
            try { return cha == null ? null : cha.getDynamicBoneBust(kind); }
            catch (NullReferenceException) { return null; }
            catch (IndexOutOfRangeException) { return null; }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        public static bool HasChains(ChaControl cha)
        {
            return GetBone(cha, ChaInfo.DynamicBoneKind.BreastL) != null;
        }

        public void ApplyBreast(BoneValues[] values, Vector3 gravity)
        {
            bool any = false;
            any |= WriteBreast(GetBone(_cha, ChaInfo.DynamicBoneKind.BreastL), values, gravity);
            any |= WriteBreast(GetBone(_cha, ChaInfo.DynamicBoneKind.BreastR), values, gravity);
            if (!any) return;
            BreastApplied = true;
            // Commit both sides, then let the game rebuild the chain baseline once.
            _cha.ReSetupDynamicBoneBust();
        }

        public void ApplyButt(BoneValues[] values, Vector3 gravity)
        {
            bool any = false;
            any |= WriteButt(GetBone(_cha, ChaInfo.DynamicBoneKind.HipL), values, gravity);
            any |= WriteButt(GetBone(_cha, ChaInfo.DynamicBoneKind.HipR), values, gravity);
            if (any) ButtApplied = true;
        }

        public void RestoreBreast()
        {
            if (!BreastApplied) return;
            Restore(GetBone(_cha, ChaInfo.DynamicBoneKind.BreastL), true);
            Restore(GetBone(_cha, ChaInfo.DynamicBoneKind.BreastR), true);
            BreastApplied = false;
            _cha.ReSetupDynamicBoneBust();
        }

        public void RestoreButt()
        {
            if (!ButtApplied) return;
            Restore(GetBone(_cha, ChaInfo.DynamicBoneKind.HipL), false);
            Restore(GetBone(_cha, ChaInfo.DynamicBoneKind.HipR), false);
            ButtApplied = false;
        }

        public void RestoreAll()
        {
            RestoreBreast();
            RestoreButt();
            _snapshots.Clear();
        }

        /// <summary>Forget snapshots without restoring (the chains were rebuilt by the game).</summary>
        public void Forget()
        {
            _snapshots.Clear();
            BreastApplied = false;
            ButtApplied = false;
        }

        private void TakeSnapshot(DynamicBone_Ver02 target)
        {
            int id = target.GetInstanceID();
            if (!_snapshots.ContainsKey(id))
                _snapshots[id] = new Snapshot(target, GetParticles(target));
        }

        private void Restore(DynamicBone_Ver02 target, bool reselectPattern)
        {
            if (target == null) return;
            Snapshot snap;
            if (_snapshots.TryGetValue(target.GetInstanceID(), out snap))
                snap.Restore(target, GetParticles(target), reselectPattern);
        }

        private bool WriteBreast(DynamicBone_Ver02 target, BoneValues[] values, Vector3 gravity)
        {
            if (target == null || target.Patterns == null || target.Patterns.Count == 0) return false;
            TakeSnapshot(target);

            target.setGravity(0, gravity, true);
            DynamicBone_Ver02.BonePtn ptn = target.Patterns[0];
            for (int i = 0; i < ptn.Params.Count; i++)
            {
                int idx = PhysicsMapper.BoneIndex(Region.Breast, ptn.Params[i].Name);
                if (idx < 0 || idx >= values.Length) continue;
                BoneValues v = values[idx];
                DynamicBone_Ver02.BoneParameter p = ptn.Params[i];
                p.IsRotationCalc = v.Rotation;
                p.Damping = v.Damping;
                p.Elasticity = v.Elasticity;
                p.Stiffness = v.Stiffness;
                p.Inert = v.Inert;
            }
            CopyParamsToParticlePtns(ptn);
            target.setPtn(0, true); // pushes the pattern into the live particles
            return true;
        }

        private bool WriteButt(DynamicBone_Ver02 target, BoneValues[] values, Vector3 gravity)
        {
            List<DynamicBone_Ver02.Particle> particles = target == null ? null : GetParticles(target);
            if (particles == null) return false;
            TakeSnapshot(target);

            target.setGravity(0, gravity, true);
            for (int i = 0; i < particles.Count; i++)
            {
                DynamicBone_Ver02.Particle p = particles[i];
                if (p == null || p.refTrans == null) continue;
                int idx = PhysicsMapper.BoneIndex(Region.Butt, p.refTrans.name);
                if (idx < 0 || idx >= values.Length) continue;
                BoneValues v = values[idx];
                p.IsRotationCalc = v.Rotation;
                p.Damping = v.Damping;
                p.Elasticity = v.Elasticity;
                p.Stiffness = v.Stiffness;
                p.Inert = v.Inert;
            }
            return true;
        }

        private static void CopyParamsToParticlePtns(DynamicBone_Ver02.BonePtn ptn)
        {
            for (int i = 0; i < ptn.Params.Count && i < ptn.ParticlePtns.Count; i++)
            {
                DynamicBone_Ver02.BoneParameter s = ptn.Params[i];
                DynamicBone_Ver02.ParticlePtn d = ptn.ParticlePtns[i];
                d.IsRotationCalc = s.IsRotationCalc;
                d.Damping = s.Damping;
                d.Elasticity = s.Elasticity;
                d.Stiffness = s.Stiffness;
                d.Inert = s.Inert;
            }
        }

        private static List<DynamicBone_Ver02.Particle> GetParticles(DynamicBone_Ver02 target)
        {
            return ParticlesField == null ? null : ParticlesField.GetValue(target) as List<DynamicBone_Ver02.Particle>;
        }

        /// <summary>The game's original values for one DynamicBone, captured before the first write.</summary>
        private sealed class Snapshot
        {
            private readonly Vector3 _gravity;
            private readonly List<BoneValues> _params = new List<BoneValues>();
            private readonly List<BoneValues> _particlePtns = new List<BoneValues>();
            private readonly List<BoneValues> _particles = new List<BoneValues>();

            public Snapshot(DynamicBone_Ver02 target, List<DynamicBone_Ver02.Particle> particles)
            {
                _gravity = target.Gravity;
                if (target.Patterns != null && target.Patterns.Count > 0)
                {
                    DynamicBone_Ver02.BonePtn ptn = target.Patterns[0];
                    foreach (DynamicBone_Ver02.BoneParameter p in ptn.Params)
                        _params.Add(p == null ? null : new BoneValues(p.IsRotationCalc, p.Damping, p.Elasticity, p.Stiffness, p.Inert));
                    foreach (DynamicBone_Ver02.ParticlePtn p in ptn.ParticlePtns)
                        _particlePtns.Add(p == null ? null : new BoneValues(p.IsRotationCalc, p.Damping, p.Elasticity, p.Stiffness, p.Inert));
                }
                if (particles != null)
                {
                    foreach (DynamicBone_Ver02.Particle p in particles)
                        _particles.Add(p == null ? null : new BoneValues(p.IsRotationCalc, p.Damping, p.Elasticity, p.Stiffness, p.Inert));
                }
            }

            public void Restore(DynamicBone_Ver02 target, List<DynamicBone_Ver02.Particle> particles, bool reselectPattern)
            {
                target.setGravity(0, _gravity, true);
                if (target.Patterns != null && target.Patterns.Count > 0)
                {
                    DynamicBone_Ver02.BonePtn ptn = target.Patterns[0];
                    for (int i = 0; i < ptn.Params.Count && i < _params.Count; i++)
                    {
                        BoneValues v = _params[i];
                        DynamicBone_Ver02.BoneParameter p = ptn.Params[i];
                        if (v == null || p == null) continue;
                        p.IsRotationCalc = v.Rotation; p.Damping = v.Damping; p.Elasticity = v.Elasticity;
                        p.Stiffness = v.Stiffness; p.Inert = v.Inert;
                    }
                    for (int i = 0; i < ptn.ParticlePtns.Count && i < _particlePtns.Count; i++)
                    {
                        BoneValues v = _particlePtns[i];
                        DynamicBone_Ver02.ParticlePtn p = ptn.ParticlePtns[i];
                        if (v == null || p == null) continue;
                        p.IsRotationCalc = v.Rotation; p.Damping = v.Damping; p.Elasticity = v.Elasticity;
                        p.Stiffness = v.Stiffness; p.Inert = v.Inert;
                    }
                    if (reselectPattern)
                        target.setPtn(0, true);
                }
                if (particles != null)
                {
                    for (int i = 0; i < particles.Count && i < _particles.Count; i++)
                    {
                        BoneValues v = _particles[i];
                        DynamicBone_Ver02.Particle p = particles[i];
                        if (v == null || p == null) continue;
                        p.IsRotationCalc = v.Rotation; p.Damping = v.Damping; p.Elasticity = v.Elasticity;
                        p.Stiffness = v.Stiffness; p.Inert = v.Inert;
                    }
                }
            }
        }
    }
}
