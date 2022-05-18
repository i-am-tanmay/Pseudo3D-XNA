using System;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Graphics
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VertexPositionTextureColorPI : IVertexType
    {
        #region Private Properties

        VertexDeclaration IVertexType.VertexDeclaration
        {
            get
            {
                return VertexDeclaration;
            }
        }

        #endregion

        #region Public Variables

        public Vector3 Position;
        public Color Color;
        public Vector3 TextureCoordinate;

        #endregion

        #region Public Static Variables

        public static readonly VertexDeclaration VertexDeclaration;

        #endregion

        #region Static Constructor

        static VertexPositionTextureColorPI()
        {
            VertexDeclaration = new VertexDeclaration(
                new VertexElement[]
                {
                    new VertexElement(
                        0,
                        VertexElementFormat.Vector3,
                        VertexElementUsage.Position,
                        0
                    ),
                    new VertexElement(
                        12,
                        VertexElementFormat.Color,
                        VertexElementUsage.Color,
                        0
                    ),
                    new VertexElement(
                        16,
                        VertexElementFormat.Vector3,
                        VertexElementUsage.TextureCoordinate,
                        0
                    )
                }
            );
        }

        #endregion

        #region Public Constructor

        public VertexPositionTextureColorPI(Vector3 position, Vector3 textureCoordinate, Color color)
        {
            Position = position;
            TextureCoordinate = textureCoordinate;
            Color = color;
        }

        #endregion

        #region Public Static Operators and Override Methods

        public override int GetHashCode()
        {
            // TODO: Fix GetHashCode
            return 0;
        }

        public override string ToString()
        {
            return (
                "{{Position:" + Position.ToString() +
                " TextureCoordinate:" + TextureCoordinate.ToString() +
                " Color:" + Color.ToString() +
                "}}"
            );
        }

        public static bool operator ==(VertexPositionTextureColorPI left, VertexPositionTextureColorPI right)
        {
            return ((left.Position == right.Position) &&
                    (left.TextureCoordinate == right.TextureCoordinate)) &&
                    (left.Color == right.Color);
        }

        public static bool operator !=(VertexPositionTextureColorPI left, VertexPositionTextureColorPI right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            if (obj.GetType() != base.GetType())
            {
                return false;
            }
            return (this == ((VertexPositionTextureColorPI)obj));
        }

        #endregion
    }
}
