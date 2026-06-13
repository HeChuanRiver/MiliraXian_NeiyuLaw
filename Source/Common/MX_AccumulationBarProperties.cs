using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class MX_AccumulationBarProperties : DefModExtension
    {
        public string backgroundTexPath;
        public string fillTexPath;
        public ShaderTypeDef backgroundShaderType;
        public ShaderTypeDef fillShaderType;
        public string iconTexPath;
        public ShaderTypeDef iconShaderType;
        public Color iconColor = Color.white;
        public Color backgroundColor = new Color(0.05f, 0.10f, 0.14f, 0.72f);
        public Color fillColor = new Color(0.56f, 0.88f, 1f, 0.86f);
        public Vector2 size = new Vector2(0.92f, 0.12f);
        public float margin = 0.05f;
        public float offsetX;
        public float offsetZ = -0.55f;
        public float altitudeOffset = 0.06f;
        public float stackSpacing = 0.16f;
        public bool stackDownward = true;
        public float iconSize = 0.16f;
        public float iconGap = 0.04f;
        public float iconAltitudeOffset = 0.02f;

        public static readonly MX_AccumulationBarProperties Default = new MX_AccumulationBarProperties();
    }
}
