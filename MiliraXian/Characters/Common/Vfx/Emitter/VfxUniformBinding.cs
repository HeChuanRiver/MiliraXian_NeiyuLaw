using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// One uniform written per frame: a constant, a float channel, or a whole vec4 channel.
    /// <see cref="Component"/> selects a slot of a vec4; -1 writes a float uniform.
    ///
    /// It belongs to a pass, not to a renderer: Field and Sprite both read these.
    /// </summary>
    internal struct VfxUniformBinding
    {
        /// <summary>Frame-uniform id for the camera-derived screen-per-cell value.</summary>
        internal const int ScreenPerCell = 1;

        /// <summary>Frame-uniform id for the shader's animation clock, in seconds.</summary>
        internal const int Time = 2;

        internal int Uniform;

        /// <summary>-1 writes a float uniform; 0..3 fills that slot of a vec4.</summary>
        internal int Component;

        /// <summary>A float channel id, or -1.</summary>
        internal int Channel;

        /// <summary>A vec4 channel id, or -1. When set it replaces the whole vector.</summary>
        internal int VecChannel;

        /// <summary>A frame uniform (see <see cref="VfxFrameUniforms"/>), or 0 for none.</summary>
        internal int Frame;

        internal Vector4 Constant;

        internal static VfxUniformBinding Float(int uniform, int channel)
        {
            return new VfxUniformBinding { Uniform = uniform, Component = -1, Channel = channel, VecChannel = -1, Frame = 0 };
        }

        internal static VfxUniformBinding FloatConstant(int uniform, float value)
        {
            return new VfxUniformBinding
            {
                Uniform = uniform,
                Component = -1,
                Channel = -1,
                VecChannel = -1,
                Constant = new Vector4(value, 0f, 0f, 0f),
            };
        }

        internal static VfxUniformBinding Vector(int uniform, Vector4 constant)
        {
            return new VfxUniformBinding
            {
                Uniform = uniform,
                Component = 0,
                Channel = -1,
                VecChannel = -1,
                Constant = constant,
            };
        }

        internal static VfxUniformBinding VectorChannel(int uniform, int component, int channel, Vector4 constant)
        {
            return new VfxUniformBinding
            {
                Uniform = uniform,
                Component = component,
                Channel = channel,
                VecChannel = -1,
                Constant = constant,
            };
        }

        /// <summary>Writes the whole vec4 uniform from a vec4 channel.</summary>
        internal static VfxUniformBinding FromVectorChannel(int uniform, int vecChannel)
        {
            return new VfxUniformBinding
            {
                Uniform = uniform,
                Component = 0,
                Channel = -1,
                VecChannel = vecChannel,
            };
        }

        /// <summary>Writes the whole vec4 uniform from a camera-derived frame value.</summary>
        internal static VfxUniformBinding FromFrame(int uniform, int frame)
        {
            return new VfxUniformBinding
            {
                Uniform = uniform,
                Component = 0,
                Channel = -1,
                VecChannel = -1,
                Frame = frame,
            };
        }

        /// <summary>Writes this binding into the block, reading <paramref name="particleIndex"/>'s channel.</summary>
        internal static void Apply(MaterialPropertyBlock block, VfxEmitter emitter, in VfxUniformBinding binding,
            int particleIndex, VfxFrameUniforms frame)
        {
            if (binding.Frame > 0)
            {
                switch (binding.Frame)
                {
                    case Time:
                        block.SetFloat(binding.Uniform, frame.Time);
                        return;
                    default:
                        block.SetVector(binding.Uniform, frame.ScreenPerCell);
                        return;
                }
            }

            if (binding.VecChannel >= 0)
            {
                block.SetVector(binding.Uniform, emitter.VectorChannel(binding.VecChannel));
                return;
            }

            if (binding.Channel < 0)
            {
                if (binding.Component < 0)
                {
                    block.SetFloat(binding.Uniform, binding.Constant.x);
                }
                else
                {
                    block.SetVector(binding.Uniform, binding.Constant);
                }
                return;
            }

            float value = emitter.Channel(particleIndex, binding.Channel);
            if (binding.Component < 0)
            {
                block.SetFloat(binding.Uniform, value);
                return;
            }

            Vector4 vector = binding.Constant;
            switch (binding.Component)
            {
                case 0: vector.x = value; break;
                case 1: vector.y = value; break;
                case 2: vector.z = value; break;
                default: vector.w = value; break;
            }
            block.SetVector(binding.Uniform, vector);
        }
    }
}
