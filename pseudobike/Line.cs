using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace pseudobike
{
    public class Line
    {
        public Vector3 center;
        public Vector2 screen;
        public float width { get; private set; }
        public float curve;

        private Texture2D sprite;
        private Color spriteColor;
        private float pos_x;
        private bool haveToDraw;
        private float scale;
        public float clip;
        private float depth;

        public bool hasBuildingStart;

        public bool isCollidable;

        private const int spriteScale = 266;

        public Line()
        {
            center = Vector3.Zero;
            screen = Vector2.Zero;
            width = 0;
            curve = 0;

            scale = 0;
            pos_x = 0;
            clip = 0;

            haveToDraw = false;
            isCollidable = false;
        }

        public Line(Vector3 playerpos, float screen_height)
        {
            center = playerpos;
            screen = Vector2.Zero;
            width = 0;
            curve = 0;

            scale = 0;
            pos_x = 0;
            clip = screen_height;

            haveToDraw = false;
            isCollidable = false;
        }

        public void SetSpriteTexture(Texture2D tex, float Position_X, Color color, bool enableCollision = true)
        {
            sprite = tex;
            pos_x = Position_X;
            spriteColor = color;
            haveToDraw = true;
            depth = MathHelper.Clamp(center.Z / (MapLayout.totalLines * RenderConsts.segmentLength), 0f, 1f);
            isCollidable = enableCollision;
        }

        public void ProjectToScreen(Vector3 cam, int screen_width, int screen_height)
        {
            float z = (center.Z - cam.Z);
            scale = (z == 0) ? 0 : RenderConsts.cameraDepth / z;
            screen.X = (float)Math.Round((1 + scale * (center.X - cam.X)) * screen_width / 2);
            screen.Y = (float)Math.Round((1 - scale * (center.Y - cam.Y)) * screen_height / 2);
            width = (float)Math.Round(scale * RenderConsts.roadWidth * screen_width / 2);
        }

        public void DrawSprite(SpriteBatch spritebatch, int screen_width)
        {
            if (!haveToDraw) return;

            int w = sprite.Width;
            int h = sprite.Height;

            float destX = screen.X + scale * pos_x * screen_width / 2;
            float destY = screen.Y + 4;
            float destW = w * width / spriteScale;
            float destH = h * width / spriteScale;

            destX += destW * pos_x;     //offsetX
            destY -= destH;             //offsetY

            float clipH = destY + destH - clip;
            if (clipH < 0) clipH = 0;
            if (clipH >= destH) return;

            int recth = (destH == 0) ? h : (int)(h * (1f - clipH / destH));
            spritebatch.Draw(sprite, new Vector2(destX - destW / 2, destY), new Rectangle(0, 0, w, recth), spriteColor, 0f, Vector2.Zero, new Vector2(destW / w, destH / h), SpriteEffects.None, depth);
        }

        public float ProjectWorldToScreenY(float worldY, int screen_height)
        {
            return screen.Y - (worldY * scale * screen_height / 2);
        }

        public Vector2 GetSpriteMinMax_X(int screen_width)
        {
            float destW = sprite.Width * width / spriteScale;
            float destX = (screen.X + scale * pos_x * screen_width / 2) + (destW * pos_x);
            float halfW = destW / 2;

            return new Vector2(destX - halfW, destX + halfW);
        }

        public float GetSpriteCenter_X(int screen_width)
        {
            return screen.X + scale * pos_x * screen_width / 2 + (sprite.Width * width / spriteScale) * pos_x;
        }

        public float GetSpriteWidth_X()
        {
            return sprite.Width * width / spriteScale;
        }
    }
}