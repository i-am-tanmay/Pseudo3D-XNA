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
    public struct BuildingInfo
    {
        public int start;
        public int end;
        public bool isRight;
        public bool isRoadRight;
        public float heightMultiplier;
        public Texture2D texture;
    }

    public class SideRailInfo
    {
        public int start;
        public int drawNum;
        public bool isRight;
        public bool isRoadRight;
        public float widthPosMultiplier;
        public float heightMultiplier;
        public Texture2D texture;

        public int width_segments { get; private set; }

        public SideRailInfo(int start, int drawNum, bool isRight, bool isRoadRight, float widthPosMultiplier, float heightMultiplier, Texture2D texture)
        {
            this.start = start;
            this.drawNum = drawNum;
            this.isRight = isRight;
            this.isRoadRight = isRoadRight;
            this.widthPosMultiplier = widthPosMultiplier;
            this.heightMultiplier = heightMultiplier;
            this.texture = texture;

            float width = texture.Width * heightMultiplier * drawNum;
            width_segments = (int)Math.Floor(width / RenderConsts.segmentLength);
        }
    }

    public static class MapLayout
    {
        private static Vector2 worldLimits;

        public static Line[] road_Right { get; private set; }
        public static Line[] road_Left { get; private set; }
        public static ushort totalLines { get; private set; }
        public static Vector2[] mapLimits { get; private set; }

        public static Texture2D[] background_texture { get; private set; }
        public static ushort[] background_YStartPos { get; private set; }
        public static float[] background_speedX { get; private set; }
        public static float[] background_speedY { get; private set; }
        public static float[] background_offsetX { get; private set; }
        public static float[] background_offsetY { get; private set; }

        public static List<BuildingInfo> buildings { get; private set; }    
        
        public static List<SideRailInfo> siderails { get; private set; }

        public static Texture2D roadTex { get; private set; }
        public static Texture2D buildingTex { get; private set; }
        public static Texture2D sideRailTex { get; private set; }
        public static Texture2D carTex { get; private set; }

        public static void InitMap(ContentManager content)
        {
            worldLimits = new Vector2(-7, 5);

            totalLines = 1600;

            road_Right = new Line[totalLines];
            road_Left = new Line[totalLines];
            for (int i = 0; i < totalLines; i++)
            {
                // ROAD INIT + POS
                road_Right[i] = new Line();
                road_Left[i] = new Line();
                road_Right[i].center.Z = road_Left[i].center.Z = i * RenderConsts.segmentLength;
                road_Left[i].center.X = -RenderConsts.roadWidth * 2;

                // CURVES
                if (i > 300 && i < 700) road_Right[i].curve = road_Left[i].curve = 1.5f;
                if (i > 1100) road_Right[i].curve = road_Left[i].curve = -2.7f;

                // HILLS
                if (i > 750 && i < 1410) road_Right[i].center.Y = road_Left[i].center.Y = EaseInOut(road_Right[i - 1].center.Y, (float)Math.Sin(((float)i - 751f) / 30f) * 3000f, ((float)i - 750f) / 500f);

                // SIDE-OBJECTS
                if (i > 50 && i % 50 == 0)
                {
                    road_Right[i].SetSpriteTexture(content.Load<Texture2D>("playersprite"), 8.2f, Color.White);
                    road_Left[i].SetSpriteTexture(content.Load<Texture2D>("playersprite"), -10f, Color.White);
                }
            }

            // BACKGROUND
            background_texture = new Texture2D[2];
            background_YStartPos = new ushort[2];
            background_speedX = new float[2];
            background_speedY = new float[2];
            background_offsetX = new float[2];
            background_offsetY = new float[2];

            background_texture[0] = content.Load<Texture2D>("cloud");
            background_YStartPos[0] = 3500;
            background_speedX[0] = .5f;
            background_speedY[0] = .0007f;
            background_offsetX[0] = 0f;
            background_offsetY[0] = 0f;

            background_texture[1] = content.Load<Texture2D>("trees");
            background_YStartPos[1] = 1100;
            background_speedX[1] = 1f;
            background_speedY[1] = .0015f;
            background_offsetX[1] = 0f;
            background_offsetY[1] = 0f;

            // TEXTURES
            roadTex = content.Load<Texture2D>("roadtex");
            buildingTex = SaveAsFlippedTexture2D(content.Load<Texture2D>("buildingtex"));
            sideRailTex = SaveAsFlippedTexture2D(content.Load<Texture2D>("siderail"));
            carTex = content.Load<Texture2D>("car");

            // BUILDINGS
            buildings = new List<BuildingInfo>();
            BuildingInfo b;
            for (int i = 0; i < totalLines; i += 70)
            {
                b = new BuildingInfo();
                b.start = i;
                b.end = i + 55;
                b.isRight = true; b.isRoadRight = true;
                b.texture = buildingTex;
                b.heightMultiplier = 1f;
                buildings.Add(b);

                b = new BuildingInfo();
                b.start = i + 15;
                b.end = i + 70;
                b.isRight = false; b.isRoadRight = false;
                b.texture = buildingTex;
                b.heightMultiplier = 1f;
                buildings.Add(b);
            }

            siderails = new List<SideRailInfo>();
            siderails.Add(new SideRailInfo(50, 5, true, true, 1.1f, 1f, sideRailTex));
            siderails.Add(new SideRailInfo(50, 5, false, false, 1.1f, 1f, sideRailTex));
        }

        private static float EaseIn(float from, float to, float value) { return from + (to - from) * (float)Math.Pow(value, 2); }
        private static float EaseOut(float from, float to, float value) { return from + (to - from) * (1 - (float)Math.Pow(1 - value, 2)); }
        private static float EaseInOut(float from, float to, float value) { return from + (to - from) * ((-(float)Math.Cos(value * Math.PI) / 2f) + 0.5f); }

        public static Texture2D SaveAsFlippedTexture2D(Texture2D input)
        {
            Texture2D flipped = new Texture2D(input.GraphicsDevice, input.Width, input.Height);
            Color[] data = new Color[input.Width * input.Height];
            Color[] flipped_data = new Color[data.Length];

            input.GetData<Color>(data);

            for (int x = 0; x < input.Width; x++)
            {
                for (int y = 0; y < input.Height; y++)
                {
                    flipped_data[x + y * input.Width] = data[x + (input.Height - 1 - y) * input.Width];
                }
            }

            flipped.SetData<Color>(flipped_data);

            return flipped;
        }
    }


}
