using System;
using System.IO;

namespace Microsoft.Xna.Framework.Graphics
{
    public class ProjectiveInterpolationEffect : Effect, IEffectMatrices
    {
        #region Effect Parameters

        EffectParameter textureParam;
        EffectParameter worldViewProjParam;

        #endregion

        #region Fields

        Matrix world = Matrix.Identity;
        Matrix view = Matrix.Identity;
        Matrix projection = Matrix.Identity;

        Matrix worldView;

        bool dirtyworldviewproj = true;

        #endregion

        #region Public Properties

        public Matrix World
        {
            get { return world; }

            set
            {
                world = value;
                dirtyworldviewproj = true;
            }
        }


        /// <summary>
        /// Gets or sets the view matrix.
        /// </summary>
        public Matrix View
        {
            get { return view; }

            set
            {
                view = value;
                dirtyworldviewproj = true;
            }
        }


        /// <summary>
        /// Gets or sets the projection matrix.
        /// </summary>
        public Matrix Projection
        {
            get { return projection; }

            set
            {
                projection = value;
                dirtyworldviewproj = true;
            }
        }

        /// <summary>
        /// Gets or sets the current texture.
        /// </summary>
        public Texture2D Texture
        {
            get { return textureParam.GetValueTexture2D(); }
            set { textureParam.SetValue(value); }
        }

        #endregion

        #region Methods


        /// <summary>
        /// Creates a new BasicEffect with default parameter settings.
        /// </summary>
        public ProjectiveInterpolationEffect(GraphicsDevice device)
            : base(device, GetEffectFile())
        {
            CacheParameters();
        }

        /// <summary>
        /// Creates a new BasicEffect by cloning parameter settings from an existing instance.
        /// </summary>
        protected ProjectiveInterpolationEffect(ProjectiveInterpolationEffect cloneSource)
            : base(cloneSource)
        {
            CacheParameters();

            world = cloneSource.world;
            view = cloneSource.view;
            projection = cloneSource.projection;
        }

        private void CacheParameters()
        {
            textureParam = Parameters["intexture"];
            worldViewProjParam = Parameters["worldviewprojMatrix"];
        }

        /// <summary>
        /// Creates a clone of the current BasicEffect instance.
        /// </summary>
        public override Effect Clone()
        {
            return new ProjectiveInterpolationEffect(this);
        }

        /// <summary>
        /// Lazily computes derived parameter values immediately before applying the effect.
        /// </summary>
        protected override void OnApply()
        {
            // Recompute the world+view+projection matrix?
            if (dirtyworldviewproj)
            {
                Matrix worldViewProj;

                Matrix.Multiply(ref world, ref view, out worldView);
                Matrix.Multiply(ref worldView, ref projection, out worldViewProj);

                worldViewProjParam.SetValue(worldViewProj);

                dirtyworldviewproj = false;
            }
        }

        #endregion

        private static byte[] GetEffectFile()
        {
            using (MemoryStream data = new MemoryStream())
            {
                using (Stream file = File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"projinterpolation.fxb")))
                {
                    file.CopyTo(data);
                    return data.ToArray();
                }
            }
        }
    }
}
