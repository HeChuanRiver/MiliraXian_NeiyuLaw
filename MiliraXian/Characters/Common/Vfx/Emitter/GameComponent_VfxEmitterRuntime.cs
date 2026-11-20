using MiliraXian.Characters.Common.Vfx.Emitter;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Per-game owner of the emitter system's def-level state.
    ///
    /// <see cref="Verse.Game.FillComponents"/> instantiates every <see cref="GameComponent"/> subclass
    /// by reflection, so this needs no Def. It exists for one job: drop the compiled specs when a
    /// game is entered, because a spec is keyed by its <see cref="VfxDef"/> and would otherwise keep
    /// that def — and anything the spec built for it — reachable for the life of the process.
    /// </summary>
    public sealed class GameComponent_VfxEmitterRuntime : GameComponent
    {
        public GameComponent_VfxEmitterRuntime(Game game)
        {
        }

        public override void StartedNewGame()
        {
            VfxDefCompiler.Reset();
        }

        public override void LoadedGame()
        {
            VfxDefCompiler.Reset();
        }
    }
}
