using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan
{
    [StaticConstructorOnStartup]
    internal static class MingyuanWhiteFlameVfx
    {
        private const string LandingSigilDefName = "MX_Mingyuan_Mote_QuestLandingSigil";
        private const string WhiteAshDefName = "MX_Mingyuan_Mote_QuestWhiteAsh";
        private const string WavePortalDefName = "MX_Mingyuan_Mote_QuestWavePortal";
        private const string ReformationDefName = "MX_Mingyuan_Mote_QuestReformation";
        private const string PulseRingDefName = "MX_Mingyuan_Mote_QuestPulseRing";
        private const string CollapseDefName = "MX_Mingyuan_Mote_QuestCollapse";
        private const string LifeBurnBurstDefName = "MX_Mingyuan_Mote_LifeBurnBurst";
        private const string CombustionFlashDefName = "MX_Mingyuan_Mote_InstantCombustionFlash";
        private const string TransferLineDefName = "MX_Mingyuan_Fleck_LifeBurnTransferLine";

        private static readonly Material CoreMaterial = MaterialPool.MatFrom(
            "MiliraXianMingyuan/Effect/LifeBurnMark",
            ShaderDatabase.MoteGlow,
            new Color(1f, 0.72f, 0.34f, 0.90f));

        private static readonly Material HaloMaterial = MaterialPool.MatFrom(
            "MiliraXianMingyuan/Effect/AshesPulse",
            ShaderDatabase.MoteGlow,
            new Color(0.96f, 0.93f, 0.78f, 0.62f));

        private static readonly Material InnerMaterial = MaterialPool.MatFrom(
            "MiliraXianMingyuan/Effect/BurningPillarCore",
            ShaderDatabase.MoteGlow,
            new Color(1f, 0.50f, 0.22f, 0.88f));

        private static readonly Material IncomingMaterial = MaterialPool.MatFrom(
            "MiliraXianMingyuan/Effect/LifeBurnBurst",
            ShaderDatabase.MoteGlow,
            new Color(1f, 0.88f, 0.61f, 0.78f));

        public static void PlayOmen(Map map, IntVec3 center, bool intense)
        {
            if (!Valid(map, center))
            {
                return;
            }

            int ashCount = intense ? 18 : 8;
            for (int index = 0; index < ashCount; index++)
            {
                Vector3 origin = center.ToVector3Shifted();
                origin += new Vector3(Rand.Range(-8f, 8f), 0f, Rand.Range(-8f, 8f));
                SpawnAsh(map, origin, Rand.Range(0f, 360f), Rand.Range(0.12f, 0.36f), Rand.Range(0.35f, 0.75f));
            }

            FleckMaker.ThrowHeatGlow(center, map, intense ? 2.1f : 1.2f);
            if (!intense)
            {
                return;
            }

            map.weatherManager?.eventHandler?.AddEvent(new WeatherEvent_LightningFlash(map));
            SoundDefOf.Thunder_OnMap?.PlayOneShot(new TargetInfo(center, map));
            Shake(map, 0.18f, 150);
        }

        public static void PlayAcceptance(Map map, IntVec3 cell)
        {
            if (!Valid(map, cell))
            {
                return;
            }

            CameraJumper.TryJump(cell, map, CameraJumper.MovementMode.Cut);
            map.weatherManager?.eventHandler?.AddEvent(new WeatherEvent_LightningFlash(map));
            SpawnStatic(LandingSigilDefName, map, cell.ToVector3Shifted(), 1f, Rand.Range(0f, 360f));
            SpawnStatic(PulseRingDefName, map, cell.ToVector3Shifted(), 0.9f, Rand.Range(0f, 360f));
            BurstAsh(map, cell.ToVector3Shifted(), 20, 0.25f, 0.85f);
            SoundDefOf.PsychicPulseGlobal?.PlayOneShotOnCamera(map);
            PlaySound("Bombardment_PreImpact", map, cell);
            Shake(map, 0.24f, 210);
        }

        public static void DrawIncoming(Vector3 drawPos, float angle, int ageTicks)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector3 trailDirection = new Vector3(-Mathf.Sin(radians), 0f, -Mathf.Cos(radians));
            float pulse = 0.86f + Mathf.Sin(ageTicks * 0.22f) * 0.12f;
            for (int index = 0; index < 5; index++)
            {
                Vector3 position = drawPos + trailDirection * (0.48f + index * 0.56f);
                position.y = AltitudeLayer.MoteOverhead.AltitudeFor() + index * 0.001f;
                float size = (3.2f - index * 0.43f) * pulse;
                DrawPlane(IncomingMaterial, position, size, ageTicks * 3.6f + index * 27f);
            }
        }

        public static void TickIncoming(Skyfaller skyfaller)
        {
            if (skyfaller?.Map == null || skyfaller.ageTicks % 4 != 0)
            {
                return;
            }

            Vector3 drawPos = skyfaller.DrawPos;
            IntVec3 cell = drawPos.ToIntVec3();
            if (!cell.InBounds(skyfaller.Map))
            {
                return;
            }

            FleckMaker.ThrowFireGlow(drawPos, skyfaller.Map, Rand.Range(0.65f, 1.25f));
            FleckMaker.ThrowDustPuffThick(drawPos, skyfaller.Map, Rand.Range(0.35f, 0.75f), Color.white);
            if (skyfaller.ageTicks % 12 == 0)
            {
                SpawnAsh(skyfaller.Map, drawPos, Rand.Range(0f, 360f), Rand.Range(0.15f, 0.35f), Rand.Range(0.25f, 0.55f));
            }
        }

        public static void PlayImpact(Map map, IntVec3 cell)
        {
            if (!Valid(map, cell))
            {
                return;
            }

            Vector3 center = cell.ToVector3Shifted();
            map.weatherManager?.eventHandler?.AddEvent(new WeatherEvent_LightningFlash(map));
            SpawnStatic(CombustionFlashDefName, map, center, 1.45f, Rand.Range(0f, 360f));
            SpawnStatic(LifeBurnBurstDefName, map, center, 1.45f, Rand.Range(0f, 360f));
            SpawnStatic(PulseRingDefName, map, center, 1.25f, Rand.Range(0f, 360f));
            BurstAsh(map, center, 34, 0.35f, 1.25f);
            for (int index = 0; index < 12; index++)
            {
                Vector3 dustPos = center + Gen.RandomHorizontalVector(Rand.Range(0.8f, 4.8f));
                FleckMaker.ThrowDustPuffThick(dustPos, map, Rand.Range(0.9f, 1.8f), Color.white);
            }

            PlaySound("Explosion_Flame", map, cell);
            Shake(map, 1.7f, 90);
        }

        public static void DrawMarker(Vector3 drawPos, int tick, float intensity)
        {
            intensity = Mathf.Clamp(intensity, 0.65f, 2f);
            float slowPulse = 0.94f + Mathf.Sin(tick * 0.045f) * 0.09f;
            float fastPulse = 0.92f + Mathf.Sin(tick * 0.12f) * 0.08f;
            Vector3 position = drawPos;
            position.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();

            DrawPlane(HaloMaterial, position, 4.25f * slowPulse * Mathf.Lerp(0.92f, 1.12f, intensity - 0.65f), tick * 0.36f);
            position.y += 0.002f;
            DrawPlane(CoreMaterial, position, 3.05f * fastPulse, -tick * 0.62f);
            position.y += 0.002f;
            DrawPlane(InnerMaterial, position, 2.05f * slowPulse, tick * 0.93f);
        }

        public static void TickMarker(Thing marker, float intensity)
        {
            if (marker?.Map == null || !marker.Spawned)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (tick % 18 == marker.thingIDNumber % 18)
            {
                FleckMaker.ThrowFireGlow(marker.DrawPos, marker.Map, Rand.Range(0.9f, 1.6f) * intensity);
            }

            if (tick % 45 == marker.thingIDNumber % 45)
            {
                Vector3 origin = marker.DrawPos + Gen.RandomHorizontalVector(Rand.Range(0.2f, 1.2f));
                SpawnAsh(marker.Map, origin, Rand.Range(0f, 360f), Rand.Range(0.08f, 0.24f), Rand.Range(0.25f, 0.55f));
            }

            if (tick % 150 == marker.thingIDNumber % 150)
            {
                SpawnStatic(PulseRingDefName, marker.Map, marker.DrawPos, Rand.Range(0.55f, 0.82f) * intensity, Rand.Range(0f, 360f));
            }
        }

        public static void PlayWaveWarning(Thing marker, int waveIndex)
        {
            if (marker?.Map == null || !marker.Spawned)
            {
                return;
            }

            Map map = marker.Map;
            Vector3 center = marker.DrawPos;
            SpawnStatic(WavePortalDefName, map, center, 0.75f + waveIndex * 0.16f, Rand.Range(0f, 360f));
            FleckDef lineDef = DefDatabase<FleckDef>.GetNamedSilentFail(TransferLineDefName);
            if (lineDef != null)
            {
                int rays = 6 + waveIndex * 2;
                for (int index = 0; index < rays; index++)
                {
                    float angle = 360f * index / rays + Rand.Range(-10f, 10f);
                    Vector3 target = center + VectorFromAngle(angle) * Rand.Range(3.2f, 6.4f);
                    FleckMaker.ConnectingLine(center, target, lineDef, map, 0.08f + waveIndex * 0.018f);
                }
            }

            SoundDefOf.MechanoidsWakeUp?.PlayOneShot(new TargetInfo(marker.Position, map));
            Shake(map, 0.18f + waveIndex * 0.08f, 120);
        }

        public static void PlayWaveArrival(List<Pawn> pawns, int waveIndex)
        {
            if (pawns == null || pawns.Count == 0)
            {
                return;
            }

            Pawn focus = null;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                if (pawn?.Spawned != true || pawn.Map == null)
                {
                    continue;
                }

                focus = focus ?? pawn;
                Vector3 position = pawn.DrawPos;
                SpawnStatic(WavePortalDefName, pawn.Map, position, waveIndex == 2 ? 0.9f : 0.62f, Rand.Range(0f, 360f));
                FleckMaker.ThrowDustPuffThick(position, pawn.Map, Rand.Range(1.1f, 1.8f), new Color(0.62f, 0.88f, 1f));
            }

            if (focus == null)
            {
                return;
            }

            CameraJumper.TryJump(focus.Position, focus.Map, waveIndex == 2 ? CameraJumper.MovementMode.Cut : CameraJumper.MovementMode.Pan);
            SoundDefOf.MechanoidsWakeUp?.PlayOneShot(new TargetInfo(focus.Position, focus.Map));
            Shake(focus.Map, waveIndex == 2 ? 1.15f : 0.55f, 90);
        }

        public static void PlayReformationPhase(Thing marker, int phase)
        {
            if (marker?.Map == null || !marker.Spawned)
            {
                return;
            }

            Map map = marker.Map;
            IntVec3 cell = marker.Position;
            Vector3 center = marker.DrawPos;
            switch (phase)
            {
                case 0:
                    CameraJumper.TryJump(cell, map, CameraJumper.MovementMode.Cut);
                    SpawnStatic(ReformationDefName, map, center, 0.78f, Rand.Range(0f, 360f));
                    SpawnStatic(PulseRingDefName, map, center, 1.05f, Rand.Range(0f, 360f));
                    SoundDefOf.PsychicPulseGlobal?.PlayOneShotOnCamera(map);
                    Shake(map, 0.32f, 240);
                    break;
                case 1:
                    BurstAsh(map, center, 30, 0.18f, 0.72f);
                    SpawnStatic(ReformationDefName, map, center, 1.05f, Rand.Range(0f, 360f));
                    break;
                case 2:
                    map.weatherManager?.eventHandler?.AddEvent(new WeatherEvent_LightningFlash(map));
                    SpawnStatic(CombustionFlashDefName, map, center, 1.2f, Rand.Range(0f, 360f));
                    SpawnStatic(PulseRingDefName, map, center, 1.45f, Rand.Range(0f, 360f));
                    PlaySound("EnergyShield_Reset", map, cell);
                    Shake(map, 0.85f, 120);
                    break;
                default:
                    SpawnStatic(ReformationDefName, map, center, 1.35f, Rand.Range(0f, 360f));
                    BurstAsh(map, center, 42, 0.3f, 1.1f);
                    break;
            }
        }

        public static void PlayManifestation(Pawn pawn)
        {
            if (pawn?.Spawned != true || pawn.Map == null)
            {
                return;
            }

            CameraJumper.TryJumpAndSelect(pawn, CameraJumper.MovementMode.Cut);
            SpawnStatic(CombustionFlashDefName, pawn.Map, pawn.DrawPos, 1.25f, Rand.Range(0f, 360f));
            SpawnStatic(ReformationDefName, pawn.Map, pawn.DrawPos, 1.1f, Rand.Range(0f, 360f));
            BurstAsh(pawn.Map, pawn.DrawPos, 38, 0.28f, 0.95f);
            PlaySound("Explosion_Vaporize", pawn.Map, pawn.Position);
            Shake(pawn.Map, 1.3f, 110);
        }

        public static void PlayWelcome(Pawn pawn)
        {
            if (pawn?.Spawned != true || pawn.Map == null)
            {
                return;
            }

            SpawnStatic(PulseRingDefName, pawn.Map, pawn.DrawPos, 0.72f, Rand.Range(0f, 360f));
            SpawnStatic(LifeBurnBurstDefName, pawn.Map, pawn.DrawPos, 0.85f, Rand.Range(0f, 360f));
            BurstAsh(pawn.Map, pawn.DrawPos, 18, 0.15f, 0.55f);
            PlaySound("EnergyShield_Reset", pawn.Map, pawn.Position);
        }

        public static void PlayDeparture(Pawn pawn)
        {
            if (pawn?.Spawned != true || pawn.Map == null)
            {
                return;
            }

            CameraJumper.TryJump(pawn.Position, pawn.Map, CameraJumper.MovementMode.Pan);
            SpawnStatic(CollapseDefName, pawn.Map, pawn.DrawPos, 1f, Rand.Range(0f, 360f));
            SpawnStatic(PulseRingDefName, pawn.Map, pawn.DrawPos, 0.8f, Rand.Range(0f, 360f));
            BurstAsh(pawn.Map, pawn.DrawPos, 26, 0.22f, 0.8f);
            PlaySound("PsychicShockLanceCast", pawn.Map, pawn.Position);
        }

        public static void PlayFailure(Map map, IntVec3 cell)
        {
            if (!Valid(map, cell))
            {
                return;
            }

            map.weatherManager?.eventHandler?.AddEvent(new WeatherEvent_LightningFlash(map));
            SpawnStatic(CollapseDefName, map, cell.ToVector3Shifted(), 1.35f, Rand.Range(0f, 360f));
            BurstAsh(map, cell.ToVector3Shifted(), 32, 0.28f, 1f);
            PlaySound("Explosion_Vaporize", map, cell);
            Shake(map, 1.1f, 100);
        }

        private static void BurstAsh(Map map, Vector3 center, int count, float minSpeed, float maxSpeed)
        {
            for (int index = 0; index < count; index++)
            {
                SpawnAsh(map, center, Rand.Range(0f, 360f), Rand.Range(minSpeed, maxSpeed), Rand.Range(0.28f, 0.72f));
            }
        }

        private static void SpawnAsh(Map map, Vector3 position, float angle, float speed, float scale)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(WhiteAshDefName);
            MoteThrown mote = def == null ? null : ThingMaker.MakeThing(def) as MoteThrown;
            IntVec3 cell = position.ToIntVec3();
            if (mote == null || map == null || !cell.InBounds(map))
            {
                mote?.Destroy(DestroyMode.Vanish);
                return;
            }

            mote.Scale = Mathf.Max(0.1f, scale);
            mote.exactRotation = Rand.Range(0f, 360f);
            mote.rotationRate = Rand.Range(-120f, 120f);
            GenSpawn.Spawn(mote, cell, map);
            mote.exactPosition = position;
            mote.SetVelocity(angle, speed);
        }

        private static Mote SpawnStatic(string defName, Map map, Vector3 position, float scale, float rotation)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            IntVec3 cell = position.ToIntVec3();
            if (def == null || map == null || !cell.InBounds(map))
            {
                return null;
            }

            Mote mote = MoteMaker.MakeStaticMote(position, map, def, Mathf.Max(0.1f, scale), false, rotation);
            if (mote != null)
            {
                mote.exactPosition = position;
                mote.exactRotation = rotation;
                mote.rotationRate = Rand.Range(-22f, 22f);
            }

            return mote;
        }

        private static void DrawPlane(Material material, Vector3 position, float size, float rotation)
        {
            if (material == null || size <= 0.01f)
            {
                return;
            }

            Matrix4x4 matrix = Matrix4x4.TRS(
                position,
                Quaternion.AngleAxis(rotation, Vector3.up),
                new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }

        private static void PlaySound(string defName, Map map, IntVec3 cell)
        {
            DefDatabase<SoundDef>.GetNamedSilentFail(defName)?.PlayOneShot(new TargetInfo(cell, map));
        }

        private static void Shake(Map map, float intensity, int ticks)
        {
            if (map == Find.CurrentMap && Find.CameraDriver?.shaker != null)
            {
                Find.CameraDriver.shaker.DoShake(intensity, ticks);
            }
        }

        private static bool Valid(Map map, IntVec3 cell)
        {
            return map != null && cell.IsValid && cell.InBounds(map);
        }

        private static Vector3 VectorFromAngle(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }

    public class Skyfaller_MingyuanQuestRebirthFlame : Skyfaller
    {
        protected override void Tick()
        {
            base.Tick();
            if (!Destroyed)
            {
                MingyuanWhiteFlameVfx.TickIncoming(this);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            MingyuanWhiteFlameVfx.DrawIncoming(DrawPos, angle, ageTicks);
        }

        protected override void Impact()
        {
            Map map = Map;
            IntVec3 cell = Position;
            MingyuanWhiteFlameVfx.PlayImpact(map, cell);
            base.Impact();
        }
    }
}
