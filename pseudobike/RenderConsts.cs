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
    public static class RenderConsts
    {
        public const int roadWidth = 2048;
        public const int segmentLength = 128;
        public const int rumbleLength = 3;
        public const int drawLength = 250;
        public const int playerSegmentOffset = 10;

        public const float rumbleWidthMultiplier = 1.2f;
        public const float buildingWidthMultiplier = 4.8f;

        public const float cameraDepth = .84f;
        public const int cameraHeight = 1024;
    }
}