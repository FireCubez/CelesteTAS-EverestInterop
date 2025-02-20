using System;
using Celeste;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using TAS.Module;

namespace TAS.EverestInterop {
    public static class HideGameplay {
        private static CelesteTasModuleSettings Settings => CelesteTasModule.Settings;

        [Load]
        private static void Load() {
            IL.Celeste.GameplayRenderer.Render += GameplayRenderer_Render;
        }

        [Unload]
        private static void Unload() {
            IL.Celeste.GameplayRenderer.Render -= GameplayRenderer_Render;
        }

        private static void GameplayRenderer_Render(ILContext il) {
            ILCursor c;

            // Mark the instr after RenderExcept.
            ILLabel lblAfterEntities = il.DefineLabel();
            c = new ILCursor(il).Goto(0);
            c.GotoNext(
                i => i.MatchCallvirt(typeof(EntityList), "RenderExcept")
            );
            c.Index++;
            c.MarkLabel(lblAfterEntities);

            // Branch after calling Begin.
            c = new ILCursor(il).Goto(0);
            // GotoNext skips c.Next
            if (!c.Next.MatchCall(typeof(GameplayRenderer), "Begin")) {
                c.GotoNext(i => i.MatchCall(typeof(GameplayRenderer), "Begin"));
            }

            c.Index++;
            c.EmitDelegate<Func<bool>>(() => !Settings.ShowGameplay);
            // c.Emit(OpCodes.Call, typeof(CelesteTASModule).GetMethod("get_Settings"));
            // c.Emit(OpCodes.Callvirt, typeof(CelesteTASModuleSettings).GetMethod("get_HideGameplay"));
            c.Emit(OpCodes.Brtrue, lblAfterEntities);
        }
    }
}