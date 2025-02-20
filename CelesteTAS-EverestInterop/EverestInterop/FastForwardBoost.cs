﻿using System;
using System.Collections.Generic;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.Helpers;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using TAS.Module;
using TAS.Utils;

namespace TAS.EverestInterop {
    public static class FastForwardBoost {
        private static bool SkipUpdate => Manager.UltraFastForwarding;
        private static readonly List<ILHook> IlHooks = new();

        [Initialize]
        private static void Initialize() {
            if (FakeAssembly.GetFakeEntryAssembly().GetType("Celeste.Mod.IsaGrabBag.DreamSpinnerBorder")?.GetMethodInfo("Update") is
                { } updateMethod) {
                IlHooks.Add(new ILHook(updateMethod, SkipUpdateMethod));
            }
        }

        [Load]
        private static void Load() {
            On.Monocle.Tracker.Initialize += TrackerOnInitialize;
            On.Celeste.BackdropRenderer.Update += BackdropRendererOnUpdate;
            On.Celeste.SoundEmitter.Update += SoundEmitterOnUpdate;
            IL.Celeste.ReflectionTentacles.Update += SkipUpdateMethod;
            IL.Monocle.ParticleSystem.Update += SkipUpdateMethod;
            IL.Celeste.Decal.Update += SkipUpdateMethod;
            IL.Celeste.FloatingDebris.Update += SkipUpdateMethod;
            IL.Celeste.AnimatedTiles.Update += SkipUpdateMethod;
            IL.Celeste.Water.Surface.Update += SkipUpdateMethod;
            IL.Celeste.LightningRenderer.Update += SkipUpdateMethod;
            IL.Celeste.DustGraphic.Update += SkipUpdateMethod;
            IL.Celeste.LavaRect.Update += SkipUpdateMethod;
            IL.Celeste.CliffsideWindFlag.Update += SkipUpdateMethod;
            IL.Celeste.CrystalStaticSpinner.UpdateHue += SkipUpdateMethod;
            IL.Celeste.SeekerBarrierRenderer.Update += SkipUpdateMethod;
            IL.Celeste.SeekerBarrier.Update += SeekerBarrierOnUpdate;
        }

        [Unload]
        private static void Unload() {
            On.Monocle.Tracker.Initialize -= TrackerOnInitialize;
            On.Celeste.BackdropRenderer.Update -= BackdropRendererOnUpdate;
            On.Celeste.SoundEmitter.Update -= SoundEmitterOnUpdate;
            IL.Celeste.ReflectionTentacles.Update -= SkipUpdateMethod;
            IL.Monocle.ParticleSystem.Update -= SkipUpdateMethod;
            IL.Celeste.Decal.Update -= SkipUpdateMethod;
            IL.Celeste.FloatingDebris.Update -= SkipUpdateMethod;
            IL.Celeste.AnimatedTiles.Update -= SkipUpdateMethod;
            IL.Celeste.Water.Surface.Update -= SkipUpdateMethod;
            IL.Celeste.LightningRenderer.Update -= SkipUpdateMethod;
            IL.Celeste.DustGraphic.Update -= SkipUpdateMethod;
            IL.Celeste.LavaRect.Update -= SkipUpdateMethod;
            IL.Celeste.CliffsideWindFlag.Update -= SkipUpdateMethod;
            IL.Celeste.CrystalStaticSpinner.UpdateHue -= SkipUpdateMethod;
            IL.Celeste.SeekerBarrierRenderer.Update -= SkipUpdateMethod;
            IL.Celeste.SeekerBarrier.Update -= SeekerBarrierOnUpdate;
            IlHooks.ForEach(hook => hook.Dispose());
            IlHooks.Clear();
        }

        private static void TrackerOnInitialize(On.Monocle.Tracker.orig_Initialize orig) {
            orig();
            AddTypeToTracker(typeof(PlayerSeeker));
            AddTypeToTracker(typeof(LockBlock));
            AddTypeToTracker(typeof(KeyboardConfigUI), typeof(ModuleSettingsKeyboardConfigUI));
            AddTypeToTracker(typeof(ButtonConfigUI), typeof(ModuleSettingsButtonConfigUI));
        }

        private static void AddTypeToTracker(Type type, params Type[] subTypes) {
            if (!Tracker.StoredEntityTypes.Contains(type)) {
                Tracker.StoredEntityTypes.Add(type);
            }

            if (!Tracker.TrackedEntityTypes.ContainsKey(type)) {
                Tracker.TrackedEntityTypes[type] = new List<Type> {type};
            } else if (!Tracker.TrackedEntityTypes[type].Contains(type)) {
                Tracker.TrackedEntityTypes[type].Add(type);
            }

            foreach (Type subType in subTypes) {
                if (!Tracker.TrackedEntityTypes.ContainsKey(subType)) {
                    Tracker.TrackedEntityTypes[subType] = new List<Type> {type};
                } else if (!Tracker.TrackedEntityTypes[subType].Contains(type)) {
                    Tracker.TrackedEntityTypes[subType].Add(type);
                }
            }
        }

        private static void BackdropRendererOnUpdate(On.Celeste.BackdropRenderer.orig_Update orig, BackdropRenderer self, Scene scene) {
            if (SkipUpdate && Engine.FrameCounter % 1000 > 0) {
                return;
            }

            orig(self, scene);
        }

        private static void SoundEmitterOnUpdate(On.Celeste.SoundEmitter.orig_Update orig, SoundEmitter self) {
            if (SkipUpdate) {
                self.RemoveSelf();
            } else {
                orig(self);
            }
        }

        private static void SkipUpdateMethod(ILContext il) {
            ILCursor ilCursor = new(il);
            Instruction start = ilCursor.Next;
            ilCursor.Emit(OpCodes.Call, typeof(Manager).GetProperty(nameof(Manager.UltraFastForwarding)).GetMethod);
            ilCursor.Emit(OpCodes.Brfalse, start).Emit(OpCodes.Ret);
        }

        private static void SeekerBarrierOnUpdate(ILContext il) {
            ILCursor cursor = new(il);

            if (!cursor.TryGotoNext(MoveType.AfterLabel,
                    instr => instr.MatchLdarg(0),
                    instr => instr.MatchLdfld<SeekerBarrier>("speeds"),
                    instr => instr.MatchLdlen())
               ) {
                return;
            }

            ILLabel target = cursor.DefineLabel();
            cursor.EmitDelegate<Func<bool>>(() => SkipUpdate);
            cursor.Emit(OpCodes.Brtrue, target);

            if (!cursor.TryGotoNext(instr => instr.MatchLdarg(0),
                    instr => instr.MatchCall<Solid>("Update"))
               ) {
                return;
            }

            cursor.MarkLabel(target);
        }
    }
}