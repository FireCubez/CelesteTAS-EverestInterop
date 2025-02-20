﻿using Celeste;
using MonoMod.Utils;
using TAS.Module;

namespace TAS.EverestInterop {
    public static class DisableAchievements {
        private static CelesteTasModuleSettings Settings => CelesteTasModule.Settings;

        [Load]
        private static void Load() {
            // Optional: Disable achievements, stats and terminal.

            // Before hooking Stats.Increment, check if the method is empty.
            // Hooking empty methods causes issues on Linux versions notably, and Stats.Increment is empty in non-Steam versions of the game.
            using (DynamicMethodDefinition statsDmd = new(typeof(Stats).GetMethod("Increment"))) {
                int instructionCount = statsDmd.Definition.Body.Instructions.Count;
                if (instructionCount > 1) {
                    // the method has more than a lonely "ret", so hook it.
                    On.Celeste.Stats.Increment += Stats_Increment;
                }
            }

            // Before hooking Achievements.Register, check the size of the method.
            // If it is 4 instructions long, hooking it is unnecessary and even causes issues.
            using (DynamicMethodDefinition statsDmd = new(typeof(Achievements).GetMethod("Register"))) {
                int instructionCount = statsDmd.Definition.Body.Instructions.Count;
                if (instructionCount > 4) {
                    On.Celeste.Achievements.Register += Achievements_Register;
                }
            }
        }

        [Unload]
        private static void Unload() {
            On.Celeste.Achievements.Register -= Achievements_Register;
            On.Celeste.Stats.Increment -= Stats_Increment;
        }

        private static void Achievements_Register(On.Celeste.Achievements.orig_Register orig, Achievement achievement) {
            if (Settings.Enabled && Settings.DisableAchievements) {
                return;
            }

            orig(achievement);
        }

        private static void Stats_Increment(On.Celeste.Stats.orig_Increment orig, Stat stat, int increment) {
            if (Settings.Enabled && Settings.DisableAchievements) {
                return;
            }

            orig(stat, increment);
        }
    }
}