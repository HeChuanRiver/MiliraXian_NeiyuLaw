using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Vfx
{
    [StaticConstructorOnStartup]
    public static class MX_RenderStatics
    {
        public static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();
    }
}
