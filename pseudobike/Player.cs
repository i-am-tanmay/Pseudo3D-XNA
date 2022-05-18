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
    public static class Player
    {
        public static int accel = 1;
        public static int brakes = 3;
        public static int decel = 1;
        public static int maxSpeed = 300;

        public static float steer = .0015f;
        public static float currentSteer = 0f;
        public const float maxSteer = 0.15f;
        public static float steerCutoff = .001f;
        public static float steerBrakeMultiplier = .9f;
        public static float steerBrakeCutoff = .05f;
        public static float steerBrakeCutoffMultiplier = .7f;

        public static Texture2D sprite;

        public static Keys c_steer_Left { get; private set; }
        public static Keys c_steer_Right { get; private set; }
        public static Keys c_accel { get; private set; }
        public static Keys c_brake { get; private set; }

        public static void ResetControls()
        {
            c_steer_Left = Keys.A;
            c_steer_Right = Keys.D;
            c_accel = Keys.W;
            c_brake = Keys.S;
        }
    }
}