using Celeste;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using TAS.Module;
using TAS.Utils;

namespace TAS.EverestInterop.Hitboxes {
    public static class HitboxFinalBoss {
        [Load]
        private static void Load() {
            On.Monocle.Entity.DebugRender += ModHitbox;
            On.Monocle.Hitbox.Render += HitboxOnRender;
        }

        [Unload]
        private static void Unload() {
            On.Monocle.Entity.DebugRender -= ModHitbox;
            On.Monocle.Hitbox.Render -= HitboxOnRender;
        }

        private static void ModHitbox(On.Monocle.Entity.orig_DebugRender orig, Entity self, Camera camera) {
            if (!CelesteTasModule.Settings.ShowHitboxes) {
                orig(self, camera);
                return;
            }

            orig(self, camera);

            if (self is FinalBossBeam finalBossBeam) {
                DynData<FinalBossBeam> dynData = finalBossBeam.GetDynDataInstance();
                if (dynData.Get<float>("chargeTimer") <= 0f && dynData.Get<float>("activeTimer") > 0f) {
                    FinalBoss boss = dynData.Get<FinalBoss>("boss");
                    float angle = dynData.Get<float>("angle");
                    Vector2 vector = boss.BeamOrigin + Calc.AngleToVector(angle, 12f);
                    Vector2 vector2 = boss.BeamOrigin + Calc.AngleToVector(angle, 2000f);
                    Vector2 value = (vector2 - vector).Perpendicular().SafeNormalize(2f);
                    Draw.Line(vector + value, vector2 + value, Color.Goldenrod);
                    Draw.Line(vector - value, vector2 - value, Color.Goldenrod);
                    Draw.Line(vector, vector2, Color.Goldenrod);
                }
            }
        }

        private static void HitboxOnRender(On.Monocle.Hitbox.orig_Render orig, Hitbox self, Camera camera, Color color) {
            if (!CelesteTasModule.Settings.ShowHitboxes) {
                orig(self, camera, color);
                return;
            }

            if (self.Entity is FinalBossShot) {
                color = Color.Goldenrod;
            }

            orig(self, camera, color);
        }
    }
}