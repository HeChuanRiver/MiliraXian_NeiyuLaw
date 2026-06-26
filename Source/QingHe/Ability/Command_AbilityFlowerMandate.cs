using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class Command_AbilityFlowerMandate : Command_Ability
    {
        private static readonly Texture2D TimedMandateBGTex = RecolorAbilityBackground(BGTex, new Color(1f, 0.82f, 0.22f, 1f));
        private static readonly Texture2D TimedMandateBGTexShrunk = RecolorAbilityBackground(BGTexShrunk, new Color(1f, 0.82f, 0.22f, 1f));

        public Command_AbilityFlowerMandate(Ability ability, Pawn pawn) : base(ability, pawn)
        {
        }

        public override Texture2D BGTexture => IsTimedFlowerMandate() ? TimedMandateBGTex : base.BGTexture;

        public override Texture2D BGTextureShrunk => IsTimedFlowerMandate() ? TimedMandateBGTexShrunk : base.BGTextureShrunk;

        private bool IsTimedFlowerMandate()
        {
            HediffComp_FlowerChoices choices = FlowerCourtUtility.GetFlowerChoices(Pawn);
            return choices != null
                && Ability?.def != null
                && choices.SelectedTimedFlowerMandate == Ability.def
                && choices.SelectedFlowerMandate != Ability.def;
        }

        private static Texture2D RecolorAbilityBackground(Texture2D source, Color tint)
        {
            if (source == null)
            {
                return null;
            }

            Texture2D texture = CopyTexture(source);
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                float value = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                pixels[i] = new Color(tint.r * value, tint.g * value, tint.b * value, pixel.a);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CopyTexture(Texture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }
}
