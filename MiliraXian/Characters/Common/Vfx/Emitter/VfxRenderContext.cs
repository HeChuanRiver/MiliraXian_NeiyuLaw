namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// What a renderer is handed each call: its particles (from the emitter), its description (the
    /// pass), and the three services it may use — engine submission, resolved assets, mesh pooling.
    ///
    /// It is a bundle, not a facade: each service is its own type with its own job, so adding a
    /// renderer kind never means growing one god object.
    /// </summary>
    internal readonly struct VfxRenderContext
    {
        internal readonly VfxEngine Engine;
        internal readonly VfxAssets Assets;
        internal readonly VfxMeshPool Meshes;

        internal VfxRenderContext(VfxEngine engine, VfxAssets assets, VfxMeshPool meshes)
        {
            Engine = engine;
            Assets = assets;
            Meshes = meshes;
        }
    }
}
