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
    class Program : Game
    {
        static void Main(string[] args)
        {
            using (Program p = new Program())
            {
                p.Run();
            }
        }

        private GraphicsDeviceManager graphics;
        Program()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;
            graphics.IsFullScreen = false;
        }

        private FrameCounter _frameCounter;
        private SpriteBatch spritebatch;
        private KeyboardState keyboardState;

        private int screen_width;
        private int screen_height;

        private Line playerPos;
        private Vector2 playerRect_prev;
        private float playerX;
        private float playerX_prev;
        private int position;
        private int drawStartPos_prev;
        private int speed;
        private int drawStartPos;

        private float buildingSideClearance;

        private const float playercurveforce = .35f;

        private BasicEffect basicEffect;
        private AlphaTestEffect alphatestEffect;
        private ProjectiveInterpolationEffect projinterpEffect;

        private VertexPositionTextureColorPI[] verts_Road;
        private VertexPositionColor[] verts_Rumble;
        private VertexPositionColor[] verts_Grass;
        private int primitiveCount;
        private int primitiveCount_Grass;

        private VertexPositionTextureColorPI[] verts_Building;
        private VertexPositionColor[] verts_Building_Top;
        private VertexPositionTextureColorPI[] verts_Building_SideWall;
        private int primitiveCount_Building;

        private VertexPositionTextureColorPI[] verts_SideRail;
        private int primitiveCount_SideRail;

        private short[] quadindices;

        Texture2D pixel;

        protected override void Initialize()
        {
            base.Initialize();

            _frameCounter = new FrameCounter(Content);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            screen_width = GraphicsDevice.Viewport.Width;
            screen_height = GraphicsDevice.Viewport.Height;

            MapLayout.InitMap(Content);
            Player.ResetControls();

            playerX = 1;
            position = 0;
            speed = 0;
            drawStartPos = 0;
            drawStartPos_prev = 0;

            verts_Road = new VertexPositionTextureColorPI[RenderConsts.drawLength * 4 * 2];
            verts_Rumble = new VertexPositionColor[RenderConsts.drawLength * 4 * 2];
            verts_Grass = new VertexPositionColor[RenderConsts.drawLength * 4];

            int maxBuildingLength = 55;
            verts_Building = new VertexPositionTextureColorPI[maxBuildingLength * 4];
            verts_Building_Top = new VertexPositionColor[maxBuildingLength * 4];
            verts_Building_SideWall = new VertexPositionTextureColorPI[4];

            int maxSideWallLength = MapLayout.totalLines;
            verts_SideRail = new VertexPositionTextureColorPI[maxSideWallLength * 4 * 2];

            quadindices = new short[Math.Max(verts_Road.Length, Math.Max(verts_Building.Length, verts_SideRail.Length))];
            int i = 0;
            for (int j = 0; j < quadindices.Length; j += 6)
            {
                quadindices[j] = (short)(i + 2); if (j + 1 < quadindices.Length) quadindices[j + 1] = (short)i; if (j + 2 < quadindices.Length) quadindices[j + 2] = (short)(i + 3);
                if (j + 3 < quadindices.Length) quadindices[j + 3] = (short)(i + 3); if (j + 4 < quadindices.Length) quadindices[j + 4] = (short)i; if (j + 5 < quadindices.Length) quadindices[j + 5] = (short)(i + 1);
                i += 4;
            }

            alphatestEffect = new AlphaTestEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0f, -1f),
            };

            Matrix viewMat = Matrix.CreateLookAt(Vector3.Forward, Vector3.Zero, Vector3.Transform(Vector3.Down, Matrix.CreateRotationZ(0f)));
            Matrix projMat = Matrix.CreateScale(1, -1, 1) * Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1f);

            basicEffect = new BasicEffect(this.GraphicsDevice)
            {
                VertexColorEnabled = true,
                TextureEnabled = true,
                World = Matrix.Identity,
                View = viewMat,
                Projection = projMat,
            };

            projinterpEffect = new ProjectiveInterpolationEffect(this.GraphicsDevice)
            {
                World = Matrix.Identity,
                View = viewMat,
                Projection = projMat,
            };
            projinterpEffect.CurrentTechnique = projinterpEffect.Techniques["ProjectiveInterpolation"];

            Player.sprite = Content.Load<Texture2D>("playersprite");
            playerPos = new Line() { clip = screen_height };
            playerPos.SetSpriteTexture(Player.sprite, 0, Color.White);

            pixel = Content.Load<Texture2D>("pixel");

            int camH = (int)(MapLayout.road_Right[0].center.Y + RenderConsts.cameraHeight);
            playerPos.center.X = playerX * RenderConsts.roadWidth;
            playerPos.center.Y = MathHelper.Lerp(MapLayout.road_Right[RenderConsts.playerSegmentOffset].center.Y, MapLayout.road_Right[RenderConsts.playerSegmentOffset+1].center.Y, (position % RenderConsts.segmentLength) / RenderConsts.segmentLength);
            playerPos.center.Z = position + RenderConsts.segmentLength * RenderConsts.playerSegmentOffset;
            playerPos.ProjectToScreen(new Vector3(playerX * RenderConsts.roadWidth, camH, position), screen_width, screen_height);
            buildingSideClearance = RenderConsts.rumbleWidthMultiplier - playerPos.GetSpriteWidth_X() / RenderConsts.roadWidth;
        }

        protected override void LoadContent()
        {
            spritebatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            keyboardState = Keyboard.GetState();
            if (Player.currentSteer < 0) // Going Left
            {
                if (keyboardState.IsKeyDown(Player.c_steer_Left)) Player.currentSteer -= Player.steer;
                else Player.currentSteer = GoToZero(Player.currentSteer, (keyboardState.IsKeyDown(Player.c_steer_Right) && Player.currentSteer > -Player.steerBrakeCutoff) ? Player.steerBrakeCutoffMultiplier : Player.steerBrakeMultiplier, Player.steerCutoff);
            }
            else if (Player.currentSteer > 0) // Going Right
            {
                if (keyboardState.IsKeyDown(Player.c_steer_Right)) Player.currentSteer += Player.steer;
                else Player.currentSteer = GoToZero(Player.currentSteer, (keyboardState.IsKeyDown(Player.c_steer_Left) && Player.currentSteer < Player.steerBrakeCutoff) ? Player.steerBrakeCutoffMultiplier : Player.steerBrakeMultiplier, Player.steerCutoff);
            }
            else
            {
                if (keyboardState.IsKeyDown(Player.c_steer_Left) && keyboardState.IsKeyDown(Player.c_steer_Right)) { }
                else if (keyboardState.IsKeyDown(Player.c_steer_Right)) Player.currentSteer += Player.steer;
                else if (keyboardState.IsKeyDown(Player.c_steer_Left)) Player.currentSteer -= Player.steer;
            }
            Player.currentSteer = MathHelper.Clamp(Player.currentSteer, -Player.maxSteer, Player.maxSteer);
            playerX += Player.currentSteer;
            if (keyboardState.IsKeyDown(Player.c_accel)) speed += Player.accel;
            if (keyboardState.IsKeyDown(Player.c_brake)) speed -= Player.brakes;
            if (!keyboardState.IsKeyDown(Player.c_accel) && !keyboardState.IsKeyDown(Player.c_brake)) speed -= Player.decel;
            speed = (int)MathHelper.Clamp(speed, 0, Player.maxSpeed);

            position += speed;
            position = position % (MapLayout.totalLines * RenderConsts.segmentLength);

            float speedPercent = (float)speed / Player.maxSpeed;
            int startPos = position / RenderConsts.segmentLength;
            Line playerSegment = MapLayout.road_Right[(startPos) % MapLayout.totalLines];
            playerX -= (dt * 2f * speedPercent * speedPercent * playerSegment.curve * playercurveforce);
            for (int i = 0; i < MapLayout.background_offsetX.Length; i++)
            {
                MapLayout.background_offsetX[i] -= MapLayout.background_speedX[i] * playerSegment.curve * speedPercent;
                MapLayout.background_offsetY[i] = -MapLayout.background_speedY[i] * playerSegment.center.Y;
            }

            drawStartPos = position / RenderConsts.segmentLength;

            // COLLISION SEGMENTS TO CHECK
            int startpos = (drawStartPos_prev + RenderConsts.playerSegmentOffset) % MapLayout.totalLines;
            int endpos = (drawStartPos + RenderConsts.playerSegmentOffset) % MapLayout.totalLines;
            if (endpos < startpos) endpos = MapLayout.totalLines - 1;

            // CHECK OBJECT COLLISION
            CheckObjectOverlap(startpos, endpos);

            // CHECK BUILDING START COLLISION
            CheckBuildingOverlap(startpos, endpos);

            // CHECK SIDERAIL COLLISION
            CheckSideRailOverlap(startpos, endpos);
            

            //Debugger.Log(0, null, "X:" + playerX + "\n");

            drawStartPos_prev = drawStartPos;
            playerRect_prev = playerPos.GetSpriteMinMax_X(screen_width);
            playerX_prev = playerX;
            base.Update(gameTime);
        }

        private void CheckObjectOverlap(int startpos, int endpos)
        {
            int playerDrawPos = (drawStartPos + RenderConsts.playerSegmentOffset) % MapLayout.totalLines;
            Vector2 playerrect = playerPos.GetSpriteMinMax_X(screen_width);

            int segskipnum = endpos - startpos;
            if (segskipnum < 2)
            {
                Line currLine_left = MapLayout.road_Left[playerDrawPos];
                Line currLine_right = MapLayout.road_Right[playerDrawPos];

                if (currLine_left.isCollidable && CheckOverlapX(playerrect, currLine_left.GetSpriteMinMax_X(screen_width)))
                {
                    speed /= 2;
                    position = drawStartPos * RenderConsts.segmentLength;
                    playerX += .05f * ((currLine_left.GetSpriteCenter_X(screen_width) <= playerPos.GetSpriteCenter_X(screen_width)) ? 1f : -1f);
                }
                else if (currLine_right.isCollidable && CheckOverlapX(playerrect, currLine_right.GetSpriteMinMax_X(screen_width)))
                {
                    speed /= 2;
                    position = drawStartPos * RenderConsts.segmentLength;
                    playerX += .05f * ((currLine_right.GetSpriteCenter_X(screen_width) <= playerPos.GetSpriteCenter_X(screen_width)) ? 1f : -1f);
                }
            }
            else
            {
                for (int i = startpos + 1; i <= endpos; i++)
                {
                    float lerpamt = (float)(i - startpos) / segskipnum;
                    Vector2 playerrect_interp = new Vector2(MathHelper.Lerp(playerRect_prev.X, playerrect.X, lerpamt), MathHelper.Lerp(playerRect_prev.Y, playerrect.Y, lerpamt));

                    Line currLine_left = MapLayout.road_Left[i];
                    Line currLine_right = MapLayout.road_Right[i];

                    if (currLine_left.isCollidable && CheckOverlapX(playerrect_interp, currLine_left.GetSpriteMinMax_X(screen_width)))
                    {
                        speed /= 2;
                        position = drawStartPos * RenderConsts.segmentLength;
                        playerX += .05f * ((currLine_left.GetSpriteCenter_X(screen_width) <= playerPos.GetSpriteCenter_X(screen_width)) ? 1f : -1f);
                    }
                    else if (currLine_right.isCollidable && CheckOverlapX(playerrect_interp, currLine_right.GetSpriteMinMax_X(screen_width)))
                    {
                        speed /= 2;
                        position = drawStartPos * RenderConsts.segmentLength;
                        playerX += .05f * ((currLine_right.GetSpriteCenter_X(screen_width) <= playerPos.GetSpriteCenter_X(screen_width)) ? 1f : -1f);
                    }
                }
            }
        }

        private void CheckBuildingOverlap(int startpos, int endpos)
        {
            int playerDrawPos = (drawStartPos + RenderConsts.playerSegmentOffset) % MapLayout.totalLines;
            Vector2 playerrect = playerPos.GetSpriteMinMax_X(screen_width);

            BuildingInfo buildingCollided = MapLayout.buildings[0];
            int collisionPos = -1;

            int segskipnum = endpos - startpos;
            if (segskipnum < 2)
            {
                for (int i = 0; i < MapLayout.buildings.Count; i++)
                {
                    if (MapLayout.buildings[i].start == playerDrawPos)
                    {
                        Line with = (MapLayout.buildings[i].isRoadRight) ? MapLayout.road_Right[playerDrawPos] : MapLayout.road_Left[playerDrawPos];
                        Vector2 b;
                        if (MapLayout.buildings[i].isRight) b = new Vector2(with.screen.X + with.width * RenderConsts.rumbleWidthMultiplier, with.screen.X + with.width * RenderConsts.buildingWidthMultiplier);
                        else b = new Vector2(with.screen.X - with.width * RenderConsts.buildingWidthMultiplier, with.screen.X - with.width * RenderConsts.rumbleWidthMultiplier);
                        if (CheckOverlapX(playerrect, b)) { buildingCollided = MapLayout.buildings[i]; collisionPos = drawStartPos; }
                    }
                }
            }
            else
            {
                for (int i = startpos + 1; i <= endpos; i++)
                {
                    float lerpamt = (float)(i - startpos) / segskipnum;
                    Vector2 playerrect_interp = new Vector2(MathHelper.Lerp(playerRect_prev.X, playerrect.X, lerpamt), MathHelper.Lerp(playerRect_prev.Y, playerrect.Y, lerpamt));
                    for (int j = 0; j < MapLayout.buildings.Count; j++)
                    {
                        if (MapLayout.buildings[j].start == i)
                        {
                            Line with = (MapLayout.buildings[j].isRoadRight) ? MapLayout.road_Right[i] : MapLayout.road_Left[i];
                            Vector2 b;
                            if (MapLayout.buildings[j].isRight) b = new Vector2(with.screen.X + with.width * RenderConsts.rumbleWidthMultiplier, with.screen.X + with.width * RenderConsts.buildingWidthMultiplier);
                            else b = new Vector2(with.screen.X - with.width * RenderConsts.buildingWidthMultiplier, with.screen.X - with.width * RenderConsts.rumbleWidthMultiplier);
                            if (CheckOverlapX(playerrect_interp, b)) { buildingCollided = MapLayout.buildings[j]; collisionPos = (i - RenderConsts.playerSegmentOffset); }

                        }
                    }
                }
            }

            // IF DID COLLIDE:
            if (collisionPos >= 0)
            {
                speed = 0;
                drawStartPos = collisionPos;
                playerDrawPos = (drawStartPos + RenderConsts.playerSegmentOffset) % MapLayout.totalLines;
                position = drawStartPos * RenderConsts.segmentLength;
                playerX += .05f * (buildingCollided.isRight ? -1f : 1f);
            }

            // BUILDING FRONT-WALL X-CLAMP
            for (int i = 0; i < MapLayout.buildings.Count; i++)
            {
                if (MapLayout.buildings[i].start <= playerDrawPos && MapLayout.buildings[i].end >= playerDrawPos)
                {
                    BuildingInfo bld = MapLayout.buildings[i];
                    if (bld.isRoadRight)
                    {
                        if (bld.isRight) { float max = (MapLayout.road_Right[playerDrawPos].center.X / RenderConsts.roadWidth) + buildingSideClearance; if (playerX > max) playerX = max; }
                        else { float min = (MapLayout.road_Right[playerDrawPos].center.X / RenderConsts.roadWidth) - buildingSideClearance; if (playerX < min) playerX = min; }
                    }
                    else
                    {
                        if (bld.isRight) { float max = (MapLayout.road_Left[playerDrawPos].center.X / RenderConsts.roadWidth) + buildingSideClearance; if (playerX > max) playerX = max; }
                        else { float min = (MapLayout.road_Left[playerDrawPos].center.X / RenderConsts.roadWidth) - buildingSideClearance; if (playerX < min) playerX = min; }
                    }
                }
            }

        }

        private void CheckSideRailOverlap(int startpos, int endpos)
        {
            int playerDrawPos = (drawStartPos + RenderConsts.playerSegmentOffset) % MapLayout.totalLines;

            int segskipnum = endpos - startpos;
            if (segskipnum < 2)
            {
                for (int i = 0; i < MapLayout.siderails.Count; i++)
                {
                    if (MapLayout.siderails[i].start < playerDrawPos && ((MapLayout.siderails[i].start + MapLayout.siderails[i].width_segments) >= playerDrawPos))
                    {
                        float siderailX = (MapLayout.siderails[i].isRight ? MapLayout.siderails[i].widthPosMultiplier : -MapLayout.siderails[i].widthPosMultiplier)
                            + (MapLayout.siderails[i].isRoadRight ? MapLayout.road_Right[playerDrawPos].center.X / RenderConsts.roadWidth : MapLayout.road_Left[playerDrawPos].center.X / RenderConsts.roadWidth);

                        float playerwidth = playerPos.GetSpriteWidth_X() / RenderConsts.roadWidth;
                        float siderailX_max = siderailX + playerwidth;
                        float siderailX_min = siderailX - playerwidth;

                        if (playerX_prev <= siderailX_min && playerX >= siderailX_min) playerX = siderailX_min;
                        else if (playerX_prev >= siderailX_max && playerX <= siderailX_max) playerX = siderailX_max;
                        else if (playerX > siderailX_min && playerX < siderailX_max)
                        {
                            if (playerX >= siderailX) playerX = siderailX_max;
                            else playerX = siderailX_min;
                        }
                    }
                }
            }
            else
            {
                float playerx_interp_prev = playerX_prev;
                for (int i = startpos + 1; i <= endpos; i++)
                {
                    float lerpamt = (float)(i - startpos) / segskipnum;
                    float playerx_interp = MathHelper.Lerp(playerX_prev, playerX, lerpamt);
                    for (int j = 0; j < MapLayout.siderails.Count; j++)
                    {
                        if (MapLayout.siderails[j].start < i && ((MapLayout.siderails[j].start + MapLayout.siderails[j].width_segments) >= i))
                        {
                            float siderailX = (MapLayout.siderails[j].isRight ? MapLayout.siderails[j].widthPosMultiplier : -MapLayout.siderails[j].widthPosMultiplier)
                                + (MapLayout.siderails[j].isRoadRight ? MapLayout.road_Right[i].center.X / RenderConsts.roadWidth : MapLayout.road_Left[i].center.X / RenderConsts.roadWidth);

                            float playerwidth = playerPos.GetSpriteWidth_X() / RenderConsts.roadWidth;
                            float siderailX_max = siderailX + playerwidth;
                            float siderailX_min = siderailX - playerwidth;

                            if (playerx_interp_prev <= siderailX_min && playerx_interp >= siderailX_min) playerX = siderailX_min;
                            else if (playerx_interp_prev >= siderailX_max && playerx_interp <= siderailX_max) playerX = siderailX_max;
                            else if (playerx_interp > siderailX_min && playerx_interp < siderailX_max)
                            {
                                if (playerx_interp >= siderailX) playerX = siderailX_max;
                                else playerX = siderailX_min;
                            }
                        }
                    }
                    playerx_interp_prev = playerx_interp;
                }
            }
        }
        
        #region DRAW

        protected override void Draw(GameTime gameTime)
        {
            // SPRITEBATCH: 0 IS DRAWN TOP ; 1 IS DRAWN BELOW
            // PRIMITIVES: -1 IS DRAWN TOP ; 0 IS DRAWN BELOW

            GraphicsDevice.Clear(Color.CornflowerBlue);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            spritebatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.Default, RasterizerState.CullCounterClockwise, alphatestEffect);
            Draw_Background();
            spritebatch.End();
            GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
            Draw_RoadSegments();
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            spritebatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise, alphatestEffect);
            Draw_Objects();
            Draw_Player();
            _frameCounter.Update(dt, spritebatch);
            spritebatch.End();

            base.Draw(gameTime);
        }

        private void Draw_Background()
        {
            for (int i = 0; i < MapLayout.background_texture.Length; i++) spritebatch.Draw(
                MapLayout.background_texture[i],
                new Vector2(-2000, 0),
                new Rectangle((int)MapLayout.background_offsetX[i], MapLayout.background_YStartPos[i] + (int)MapLayout.background_offsetY[i], 5000, 420),
                Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None,
                1f);
        }

        private void Draw_RoadSegments()
        {
            primitiveCount = 0;

            // LEFT ROAD
            int camH = (int)(MapLayout.road_Left[drawStartPos].center.Y + RenderConsts.cameraHeight);

            int maxy = screen_height;
            float x = 0;
            float dx = -MapLayout.road_Left[drawStartPos % MapLayout.totalLines].curve * ((position % RenderConsts.segmentLength) / RenderConsts.segmentLength);

            for (int n = drawStartPos; n < drawStartPos + RenderConsts.drawLength; n++)
            {
                Line newLine = MapLayout.road_Left[n % MapLayout.totalLines];
                float linecamZ = position - (n >= MapLayout.totalLines ? MapLayout.totalLines * RenderConsts.segmentLength : 0);
                newLine.ProjectToScreen(new Vector3(playerX * RenderConsts.roadWidth - x, camH, linecamZ), screen_width, screen_height);

                x += dx;
                dx += newLine.curve;

                newLine.clip = maxy;
                if (((newLine.center.Z - linecamZ) <= RenderConsts.cameraDepth) || (newLine.screen.Y >= maxy)) continue;
                maxy = (int)newLine.screen.Y;

                Line prevLine = MapLayout.road_Left[(n - 1) % MapLayout.totalLines];

                Color rumble = ((n / RenderConsts.rumbleLength) % 2) == 1 ? new Color(255, 255, 255) : new Color(0, 0, 0);
                int i = primitiveCount * 4;
                verts_Rumble[i + 0] = new VertexPositionColor(new Vector3(newLine.screen.X - newLine.width * RenderConsts.rumbleWidthMultiplier, newLine.screen.Y, 0f), rumble);
                verts_Rumble[i + 1] = new VertexPositionColor(new Vector3(newLine.screen.X + newLine.width * RenderConsts.rumbleWidthMultiplier, newLine.screen.Y, 0f), rumble);
                verts_Rumble[i + 2] = new VertexPositionColor(new Vector3(prevLine.screen.X - prevLine.width * RenderConsts.rumbleWidthMultiplier, prevLine.screen.Y, 0f), rumble);
                verts_Rumble[i + 3] = new VertexPositionColor(new Vector3(prevLine.screen.X + prevLine.width * RenderConsts.rumbleWidthMultiplier, prevLine.screen.Y, 0f), rumble);

                i = primitiveCount * 4;
                AddRoadTextureVertices(prevLine, newLine, i, n);

                primitiveCount++;
            }

            // RIGHT ROAD
            camH = (int)(MapLayout.road_Right[drawStartPos].center.Y + RenderConsts.cameraHeight);

            maxy = screen_height;
            x = 0;
            dx = -MapLayout.road_Right[drawStartPos % MapLayout.totalLines].curve * ((position % RenderConsts.segmentLength) / RenderConsts.segmentLength);

            primitiveCount_Grass = 0;

            for (int n = drawStartPos; n < drawStartPos + RenderConsts.drawLength; n++)
            {
                Line newLine = MapLayout.road_Right[n % MapLayout.totalLines];
                float linecamZ = position - (n >= MapLayout.totalLines ? MapLayout.totalLines * RenderConsts.segmentLength : 0);
                newLine.ProjectToScreen(new Vector3(playerX * RenderConsts.roadWidth - x, camH, linecamZ), screen_width, screen_height);
                x += dx;
                dx += newLine.curve;

                newLine.clip = maxy;
                if (((newLine.center.Z - linecamZ) <= RenderConsts.cameraDepth) || (newLine.screen.Y >= maxy)) continue;
                maxy = (int)newLine.screen.Y;

                Color grass = ((n / RenderConsts.rumbleLength) % 2) == 1 ? new Color(16, 200, 16) : new Color(0, 154, 0);
                Color rumble = ((n / RenderConsts.rumbleLength) % 2) == 1 ? new Color(255, 255, 255) : new Color(0, 0, 0);

                Line prevLine = MapLayout.road_Right[(n - 1) % MapLayout.totalLines];

                int i = primitiveCount_Grass * 4;
                verts_Grass[i + 0] = new VertexPositionColor(new Vector3(0, newLine.screen.Y, 0f), grass);
                verts_Grass[i + 1] = new VertexPositionColor(new Vector3(screen_width, newLine.screen.Y, 0f), grass);
                verts_Grass[i + 2] = new VertexPositionColor(new Vector3(0, prevLine.screen.Y, 0f), grass);
                verts_Grass[i + 3] = new VertexPositionColor(new Vector3(screen_width, prevLine.screen.Y, 0f), grass);
                primitiveCount_Grass++;

                i = primitiveCount * 4;
                verts_Rumble[i + 0] = new VertexPositionColor(new Vector3(newLine.screen.X - newLine.width * RenderConsts.rumbleWidthMultiplier, newLine.screen.Y, 0f), rumble);
                verts_Rumble[i + 1] = new VertexPositionColor(new Vector3(newLine.screen.X + newLine.width * RenderConsts.rumbleWidthMultiplier, newLine.screen.Y, 0f), rumble);
                verts_Rumble[i + 2] = new VertexPositionColor(new Vector3(prevLine.screen.X - prevLine.width * RenderConsts.rumbleWidthMultiplier, prevLine.screen.Y, 0f), rumble);
                verts_Rumble[i + 3] = new VertexPositionColor(new Vector3(prevLine.screen.X + prevLine.width * RenderConsts.rumbleWidthMultiplier, prevLine.screen.Y, 0f), rumble);


                i = primitiveCount * 4;
                AddRoadTextureVertices(prevLine, newLine, i, n);

                primitiveCount++;
            }

            DrawBuildings();
            DrawSideRails();
            DrawGround(MapLayout.roadTex);
        }

        private void AddRoadTextureVertices(Line prevLine, Line newLine, int i, int texPos)
        {
            Vector3
                pos_TL = new Vector3(newLine.screen.X - newLine.width, newLine.screen.Y, 0f),
                pos_BR = new Vector3(prevLine.screen.X + prevLine.width, prevLine.screen.Y, 0f),
                pos_TR = new Vector3(newLine.screen.X + newLine.width, newLine.screen.Y, 0f),
                pos_BL = new Vector3(prevLine.screen.X - prevLine.width, prevLine.screen.Y, 0f);

            float roadTex_scale = ((float)RenderConsts.segmentLength / MapLayout.roadTex.Height) * ((float)MapLayout.roadTex.Width / RenderConsts.roadWidth);
            Vector3
                tex_TL = new Vector3(0, (texPos + 1) * roadTex_scale, 1),
                tex_BR = new Vector3(1, texPos * roadTex_scale, 1),
                tex_TR = new Vector3(1, (texPos + 1) * roadTex_scale, 1),
                tex_BL = new Vector3(0, texPos * roadTex_scale, 1);


            CalculateProjectiveInterpolationTexture(pos_TR, pos_BR, pos_TL, pos_BL, ref tex_TR, ref tex_BR, ref tex_TL, ref tex_BL);


            verts_Road[i + 0] = new VertexPositionTextureColorPI(pos_TL, tex_TL, Color.White);
            verts_Road[i + 1] = new VertexPositionTextureColorPI(pos_TR, tex_TR, Color.White);
            verts_Road[i + 2] = new VertexPositionTextureColorPI(pos_BL, tex_BL, Color.White);
            verts_Road[i + 3] = new VertexPositionTextureColorPI(pos_BR, tex_BR, Color.White);
        }


        private void Draw_Objects()
        {
            for (int n = drawStartPos + RenderConsts.drawLength; n > drawStartPos; n--)
            {
                MapLayout.road_Right[n % MapLayout.totalLines].DrawSprite(spritebatch, screen_width);
                MapLayout.road_Left[n % MapLayout.totalLines].DrawSprite(spritebatch, screen_width);
            }
        }

        private void Draw_Player()
        {
            int camH = (int)(MapLayout.road_Right[drawStartPos].center.Y + RenderConsts.cameraHeight);
            int startlinepos = (drawStartPos + RenderConsts.playerSegmentOffset) % MapLayout.totalLines;
            int nextlinepos = (startlinepos + 1) % MapLayout.totalLines;

            playerPos.center.X = playerX * RenderConsts.roadWidth;
            playerPos.center.Y = MathHelper.Lerp(MapLayout.road_Right[startlinepos].center.Y, MapLayout.road_Right[nextlinepos].center.Y, (position % RenderConsts.segmentLength) / RenderConsts.segmentLength);
            playerPos.center.Z = position + RenderConsts.segmentLength * RenderConsts.playerSegmentOffset;

            playerPos.ProjectToScreen(new Vector3(playerX * RenderConsts.roadWidth, camH, position), screen_width, screen_height);
            playerPos.DrawSprite(spritebatch, screen_width);
        }

        /* OLD DRAW QUADS
        private void DrawQuad(Color color, Vector2 l1, float l1_width, Vector2 l2, float l2_width, float depth)
        {
            if (l1.Y > l2.Y)
            {
                vert_col[2] = new VertexPositionColor(new Vector3(l1.X - l1_width, l1.Y, depth - 1f), color);   //topleft
                vert_col[3] = new VertexPositionColor(new Vector3(l1.X + l1_width, l1.Y, depth - 1f), color);   //topright
                vert_col[0] = new VertexPositionColor(new Vector3(l2.X - l2_width, l2.Y, depth - 1f), color);   //botleft
                vert_col[1] = new VertexPositionColor(new Vector3(l2.X + l2_width, l2.Y, depth - 1f), color);   //botright
            }
            else
            {
                vert_col[2] = new VertexPositionColor(new Vector3(l2.X - l2_width, l2.Y, depth - 1f), color);   //topleft
                vert_col[3] = new VertexPositionColor(new Vector3(l2.X + l2_width, l2.Y, depth - 1f), color);   //topright
                vert_col[0] = new VertexPositionColor(new Vector3(l1.X - l1_width, l1.Y, depth - 1f), color);   //botleft
                vert_col[1] = new VertexPositionColor(new Vector3(l1.X + l1_width, l1.Y, depth - 1f), color);   //botright
            }

            basicEffect.VertexColorEnabled = true;
            basicEffect.TextureEnabled = false;

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.TriangleStrip, vert_col, 0, 2);
            }
        }
        private void DrawQuad(Texture2D tex, Vector2 l1, float l1_width, Vector2 l2, float l2_width, float depth)
        {
            if (l1.Y > l2.Y)
            {
                vert_tex[2] = new VertexPositionTexture(new Vector3(l1.X - l1_width, l1.Y, depth - 1f), new Vector2(0, 1));   //topleft
                vert_tex[3] = new VertexPositionTexture(new Vector3(l1.X + l1_width, l1.Y, depth - 1f), new Vector2(1, 1));   //topright
                vert_tex[0] = new VertexPositionTexture(new Vector3(l2.X - l2_width, l2.Y, depth - 1f), new Vector2(0, 0));   //botleft
                vert_tex[1] = new VertexPositionTexture(new Vector3(l2.X + l2_width, l2.Y, depth - 1f), new Vector2(1, 0));   //botright
            }
            else
            {
                vert_tex[2] = new VertexPositionTexture(new Vector3(l2.X - l2_width, l2.Y, depth - 1f), new Vector2(0, 1));   //topleft
                vert_tex[3] = new VertexPositionTexture(new Vector3(l2.X + l2_width, l2.Y, depth - 1f), new Vector2(1, 1));   //topright
                vert_tex[0] = new VertexPositionTexture(new Vector3(l1.X - l1_width, l1.Y, depth - 1f), new Vector2(0, 0));   //botleft
                vert_tex[1] = new VertexPositionTexture(new Vector3(l1.X + l1_width, l1.Y, depth - 1f), new Vector2(1, 0));   //botright
            }

            basicEffect.VertexColorEnabled = false;
            basicEffect.TextureEnabled = true;
            basicEffect.Texture = tex;

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives<VertexPositionTexture>(PrimitiveType.TriangleStrip, vert_tex, 0, 2);
            }
        }
        */

        private void DrawGround(Texture2D roadTex)
        {
            basicEffect.VertexColorEnabled = true;
            basicEffect.TextureEnabled = false;
            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleList, verts_Grass, 0, primitiveCount_Grass * 4, quadindices, 0, primitiveCount_Grass * 2);
                GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleList, verts_Rumble, 0, primitiveCount * 4, quadindices, 0, primitiveCount * 2);
            }

            projinterpEffect.Texture = roadTex;
            foreach (EffectPass pass in projinterpEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionTextureColorPI>(PrimitiveType.TriangleList, verts_Road, 0, primitiveCount * 4, quadindices, 0, primitiveCount * 2);
            }
        }

        private void DrawBuildings()
        {
            for (int i = 0; i < MapLayout.buildings.Count; i++) DrawBuilding(MapLayout.buildings[i]);
        }

        private void DrawBuilding(BuildingInfo buildinginfo)
        {
            if (drawStartPos >= buildinginfo.end) return;
            if (drawStartPos + RenderConsts.drawLength < buildinginfo.start) return;

            float h = buildinginfo.heightMultiplier * (buildinginfo.end - buildinginfo.start) * RenderConsts.segmentLength * buildinginfo.texture.Height / buildinginfo.texture.Width;
            float texFrac = 1f / (buildinginfo.end - buildinginfo.start);
            primitiveCount_Building = 0;

            for (int n = (drawStartPos >= buildinginfo.start) ? drawStartPos + 1 : buildinginfo.start; n < Math.Min(buildinginfo.end, drawStartPos + RenderConsts.drawLength - 1); n++)
            {
                Line bot, top;
                if (buildinginfo.isRoadRight)
                {
                    bot = MapLayout.road_Right[n % MapLayout.totalLines];
                    top = MapLayout.road_Right[(n + 1) % MapLayout.totalLines];
                }
                else
                {
                    bot = MapLayout.road_Left[n % MapLayout.totalLines];
                    top = MapLayout.road_Left[(n + 1) % MapLayout.totalLines];
                }

                float depth = (MathHelper.Clamp(top.center.Z / (MapLayout.totalLines * RenderConsts.segmentLength), 0f, 1f)) - 1f;
                Vector3 pos_TR, pos_BR, pos_TL, pos_BL,
                        tex_TR, tex_BR, tex_TL, tex_BL;

                float Y_top = top.ProjectWorldToScreenY(h, screen_height);
                float Y_bot = bot.ProjectWorldToScreenY(h, screen_height);
                float Ymin_top = Math.Min(top.screen.Y, top.clip);
                float Ymin_bot = Math.Min(bot.screen.Y, bot.clip);

                if (buildinginfo.isRight)
                {
                    pos_TR = new Vector3(bot.screen.X + bot.width * RenderConsts.rumbleWidthMultiplier, Y_bot, depth);
                    pos_BR = new Vector3(bot.screen.X + bot.width * RenderConsts.rumbleWidthMultiplier, Ymin_bot, depth);
                    pos_TL = new Vector3(top.screen.X + top.width * RenderConsts.rumbleWidthMultiplier + ((n == buildinginfo.end - 1) ? 10f : 0f), Y_top, depth);
                    pos_BL = new Vector3(top.screen.X + top.width * RenderConsts.rumbleWidthMultiplier + ((n == buildinginfo.end - 1) ? 10f : 0f), Ymin_top, depth);

                    tex_TL = new Vector3(texFrac * (buildinginfo.end - (n + 1)), 1, 1);
                    tex_BR = new Vector3(texFrac * (buildinginfo.end - n), bot.screen.Y <= bot.clip ? 0 : ((bot.screen.Y - bot.clip) / (bot.screen.Y - pos_TR.Y)), 1);
                    tex_TR = new Vector3(texFrac * (buildinginfo.end - n), 1, 1);
                    tex_BL = new Vector3(texFrac * (buildinginfo.end - (n + 1)), top.screen.Y <= top.clip ? 0 : ((top.screen.Y - top.clip) / (top.screen.Y - pos_TL.Y)), 1);
                }
                else
                {
                    pos_TL = new Vector3(bot.screen.X - bot.width * RenderConsts.rumbleWidthMultiplier, Y_bot, depth);
                    pos_BL = new Vector3(bot.screen.X - bot.width * RenderConsts.rumbleWidthMultiplier, Ymin_bot, depth);
                    pos_TR = new Vector3(top.screen.X - top.width * RenderConsts.rumbleWidthMultiplier - ((n == buildinginfo.end - 1) ? 10f : 0f), Y_top, depth);
                    pos_BR = new Vector3(top.screen.X - top.width * RenderConsts.rumbleWidthMultiplier - ((n == buildinginfo.end - 1) ? 10f : 0f), Ymin_top, depth);

                    tex_TR = new Vector3(texFrac * (buildinginfo.end - (n + 1)), 1, 1);
                    tex_BL = new Vector3(texFrac * (buildinginfo.end - n), bot.screen.Y <= bot.clip ? 0 : ((bot.screen.Y - bot.clip) / (bot.screen.Y - pos_TL.Y)), 1);
                    tex_TL = new Vector3(texFrac * (buildinginfo.end - n), 1, 1);
                    tex_BR = new Vector3(texFrac * (buildinginfo.end - (n + 1)), top.screen.Y <= top.clip ? 0 : ((top.screen.Y - top.clip) / (top.screen.Y - pos_TR.Y)), 1);
                }

                if (pos_TR.Y >= pos_BR.Y || pos_TL.Y >= pos_BL.Y) continue;

                CalculateProjectiveInterpolationTexture(pos_TR, pos_BR, pos_TL, pos_BL, ref tex_TR, ref tex_BR, ref tex_TL, ref tex_BL);

                int i = primitiveCount_Building * 4;
                verts_Building[i + 0] = new VertexPositionTextureColorPI(pos_TL, tex_TL, Color.White);
                verts_Building[i + 1] = new VertexPositionTextureColorPI(pos_TR, tex_TR, Color.White);
                verts_Building[i + 2] = new VertexPositionTextureColorPI(pos_BL, tex_BL, Color.White);
                verts_Building[i + 3] = new VertexPositionTextureColorPI(pos_BR, tex_BR, Color.White);

                if (buildinginfo.isRight)
                {
                    float postop = top.screen.X + top.width * RenderConsts.buildingWidthMultiplier + ((n == buildinginfo.end - 1) ? 10f : 0f);
                    float posbot = bot.screen.X + bot.width * RenderConsts.buildingWidthMultiplier;
                    verts_Building_Top[i + 0] = new VertexPositionColor(pos_TL, Color.Black);
                    verts_Building_Top[i + 1] = new VertexPositionColor(new Vector3(postop, pos_TL.Y, depth), Color.Black);
                    verts_Building_Top[i + 2] = new VertexPositionColor(pos_TR, Color.Black);
                    verts_Building_Top[i + 3] = new VertexPositionColor(new Vector3(posbot, pos_TR.Y, depth), Color.Black);
                }
                else
                {
                    float postop = top.screen.X - top.width * RenderConsts.buildingWidthMultiplier - ((n == buildinginfo.end - 1) ? 10f : 0f);
                    float posbot = bot.screen.X - bot.width * RenderConsts.buildingWidthMultiplier;
                    verts_Building_Top[i + 0] = new VertexPositionColor(new Vector3(postop, pos_TR.Y, depth), Color.Black);
                    verts_Building_Top[i + 1] = new VertexPositionColor(pos_TR, Color.Black);
                    verts_Building_Top[i + 2] = new VertexPositionColor(new Vector3(posbot, pos_TL.Y, depth), Color.Black);
                    verts_Building_Top[i + 3] = new VertexPositionColor(pos_TL, Color.Black);
                }

                if (n == buildinginfo.start) DrawBuilding_SideWall(bot, h, buildinginfo.isRight, buildinginfo.texture);

                primitiveCount_Building++;
            }

            if (primitiveCount_Building > 0)
            {
                basicEffect.VertexColorEnabled = true;
                basicEffect.TextureEnabled = false;
                foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleList, verts_Building_Top, 0, primitiveCount_Building * 4, quadindices, 0, primitiveCount_Building * 2);
                }

                projinterpEffect.Texture = buildinginfo.texture;
                foreach (EffectPass pass in projinterpEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionTextureColorPI>(PrimitiveType.TriangleList, verts_Building, 0, primitiveCount_Building * 4, quadindices, 0, primitiveCount_Building * 2);
                }
            }
        }

        private void DrawBuilding_SideWall(Line line, float h, bool isRight, Texture2D tex)
        {
            float depth = (MathHelper.Clamp(line.center.Z / (MapLayout.totalLines * RenderConsts.segmentLength), 0f, 1f)) - 1f;

            Vector3 pos_TR, pos_BR, pos_TL, pos_BL,
                    tex_TR, tex_BR, tex_TL, tex_BL;

            float top = line.ProjectWorldToScreenY(h, screen_height);
            float bot = Math.Min(line.screen.Y, line.clip);

            float w = Math.Abs(line.screen.Y - top) * tex.Width / tex.Height;

            if (isRight)
            {
                float pos = line.screen.X + line.width * RenderConsts.rumbleWidthMultiplier;
                float posend = line.screen.X + line.width * RenderConsts.buildingWidthMultiplier;
                pos_TR = new Vector3(posend, top, depth);
                pos_BR = new Vector3(posend, bot, depth);
                pos_TL = new Vector3(pos, top, depth);
                pos_BL = new Vector3(pos, bot, depth);

                float bot_tex = line.screen.Y <= line.clip ? 0 : ((line.screen.Y - line.clip) / (line.screen.Y - pos_TR.Y));
                float texFrac = Math.Abs((pos_TR.X - pos_TL.X) / w);
                tex_TL = new Vector3(0, 1, 1);
                tex_BR = new Vector3(texFrac, bot_tex, 1);
                tex_TR = new Vector3(texFrac, 1, 1);
                tex_BL = new Vector3(0, bot_tex, 1);
            }
            else
            {
                float pos = line.screen.X - line.width * RenderConsts.rumbleWidthMultiplier;
                float posend = line.screen.X - line.width * RenderConsts.buildingWidthMultiplier;
                pos_TL = new Vector3(posend, top, depth);
                pos_BL = new Vector3(posend, bot, depth);
                pos_TR = new Vector3(pos, top, depth);
                pos_BR = new Vector3(pos, bot, depth);

                float bot_tex = line.screen.Y <= line.clip ? 0 : ((line.screen.Y - line.clip) / (line.screen.Y - pos_TR.Y));
                float texFrac = Math.Abs((pos_TR.X - pos_TL.X) / w);
                tex_TR = new Vector3(0, 1, 1);
                tex_BL = new Vector3(texFrac, bot_tex, 1);
                tex_TL = new Vector3(texFrac, 1, 1);
                tex_BR = new Vector3(0, bot_tex, 1);
            }

            if (pos_TR.Y >= pos_BR.Y || pos_TL.Y >= pos_BL.Y) return;

            CalculateProjectiveInterpolationTexture(pos_TR, pos_BR, pos_TL, pos_BL, ref tex_TR, ref tex_BR, ref tex_TL, ref tex_BL);

            Color dark = new Color(Vector3.One * .13f);
            verts_Building_SideWall[0] = new VertexPositionTextureColorPI(pos_TL, tex_TL, dark);
            verts_Building_SideWall[1] = new VertexPositionTextureColorPI(pos_TR, tex_TR, dark);
            verts_Building_SideWall[2] = new VertexPositionTextureColorPI(pos_BL, tex_BL, dark);
            verts_Building_SideWall[3] = new VertexPositionTextureColorPI(pos_BR, tex_BR, dark);

            projinterpEffect.Texture = tex;
            foreach (EffectPass pass in projinterpEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionTextureColorPI>(PrimitiveType.TriangleList, verts_Building_SideWall, 0, 4, quadindices, 0, 2);
            }

        }

        private void DrawSideRails()
        {
            for (int i = 0; i < MapLayout.siderails.Count; i++) DrawSideRail(MapLayout.siderails[i]);
        }

        private void DrawSideRail(SideRailInfo siderailinfo)
        {
            if (siderailinfo.width_segments < 1) return;
            int end = siderailinfo.start + siderailinfo.width_segments;

            if (drawStartPos >= end) return;
            if (drawStartPos + RenderConsts.drawLength < siderailinfo.start) return;

            float h = siderailinfo.texture.Height * siderailinfo.heightMultiplier;

            float texFrac = (float)siderailinfo.drawNum / siderailinfo.width_segments;
            primitiveCount_SideRail = 0;

            for (int n = (drawStartPos >= siderailinfo.start) ? drawStartPos + 1 : siderailinfo.start; n < Math.Min(end, drawStartPos + RenderConsts.drawLength - 1); n++)
            {
                Line bot, top;
                if (siderailinfo.isRoadRight)
                {
                    bot = MapLayout.road_Right[n % MapLayout.totalLines];
                    top = MapLayout.road_Right[(n + 1) % MapLayout.totalLines];
                }
                else
                {
                    bot = MapLayout.road_Left[n % MapLayout.totalLines];
                    top = MapLayout.road_Left[(n + 1) % MapLayout.totalLines];
                }

                float depth = (MathHelper.Clamp(top.center.Z / (MapLayout.totalLines * RenderConsts.segmentLength), 0f, 1f)) - 1f;
                Vector3 pos_TR, pos_BR, pos_TL, pos_BL,
                        tex_TR, tex_BR, tex_TL, tex_BL;

                float Y_top = top.ProjectWorldToScreenY(h, screen_height);
                float Y_bot = bot.ProjectWorldToScreenY(h, screen_height);
                float Ymin_top = Math.Min(top.screen.Y, top.clip);
                float Ymin_bot = Math.Min(bot.screen.Y, bot.clip);

                if (siderailinfo.isRight)
                {
                    pos_TR = new Vector3(bot.screen.X + bot.width * siderailinfo.widthPosMultiplier, Y_bot, depth);
                    pos_BR = new Vector3(bot.screen.X + bot.width * siderailinfo.widthPosMultiplier, Ymin_bot, depth);
                    pos_TL = new Vector3(top.screen.X + top.width * siderailinfo.widthPosMultiplier + ((n == end - 1) ? 1f : 0f), Y_top, depth);
                    pos_BL = new Vector3(top.screen.X + top.width * siderailinfo.widthPosMultiplier + ((n == end - 1) ? 1f : 0f), Ymin_top, depth);

                    tex_TL = new Vector3(texFrac * (end - (n + 1)), 1, 1);
                    tex_BR = new Vector3(texFrac * (end - n), bot.screen.Y <= bot.clip ? 0 : ((bot.screen.Y - bot.clip) / (bot.screen.Y - pos_TR.Y)), 1);
                    tex_TR = new Vector3(texFrac * (end - n), 1, 1);
                    tex_BL = new Vector3(texFrac * (end - (n + 1)), top.screen.Y <= top.clip ? 0 : ((top.screen.Y - top.clip) / (top.screen.Y - pos_TL.Y)), 1);
                }
                else
                {
                    pos_TL = new Vector3(bot.screen.X - bot.width * siderailinfo.widthPosMultiplier, Y_bot, depth);
                    pos_BL = new Vector3(bot.screen.X - bot.width * siderailinfo.widthPosMultiplier, Ymin_bot, depth);
                    pos_TR = new Vector3(top.screen.X - top.width * siderailinfo.widthPosMultiplier - ((n == end - 1) ? 1f : 0f), Y_top, depth);
                    pos_BR = new Vector3(top.screen.X - top.width * siderailinfo.widthPosMultiplier - ((n == end - 1) ? 1f : 0f), Ymin_top, depth);

                    tex_TR = new Vector3(texFrac * (end - (n + 1)), 1, 1);
                    tex_BL = new Vector3(texFrac * (end - n), bot.screen.Y <= bot.clip ? 0 : ((bot.screen.Y - bot.clip) / (bot.screen.Y - pos_TL.Y)), 1);
                    tex_TL = new Vector3(texFrac * (end - n), 1, 1);
                    tex_BR = new Vector3(texFrac * (end - (n + 1)), top.screen.Y <= top.clip ? 0 : ((top.screen.Y - top.clip) / (top.screen.Y - pos_TR.Y)), 1);
                }

                if (pos_TR.Y >= pos_BR.Y || pos_TL.Y >= pos_BL.Y) continue;

                CalculateProjectiveInterpolationTexture(pos_TR, pos_BR, pos_TL, pos_BL, ref tex_TR, ref tex_BR, ref tex_TL, ref tex_BL);

                int i = primitiveCount_SideRail * 4;
                verts_SideRail[i + 0] = new VertexPositionTextureColorPI(pos_TL, tex_TL, Color.White);
                verts_SideRail[i + 1] = new VertexPositionTextureColorPI(pos_TR, tex_TR, Color.White);
                verts_SideRail[i + 2] = new VertexPositionTextureColorPI(pos_BL, tex_BL, Color.White);
                verts_SideRail[i + 3] = new VertexPositionTextureColorPI(pos_BR, tex_BR, Color.White);

                primitiveCount_SideRail++;
            }

            // DOUBLE SIDED
            for (int n = (drawStartPos >= siderailinfo.start) ? drawStartPos + 1 : siderailinfo.start; n < Math.Min(end, drawStartPos + RenderConsts.drawLength - 1); n++)
            {
                Line bot, top;
                if (siderailinfo.isRoadRight)
                {
                    bot = MapLayout.road_Right[n % MapLayout.totalLines];
                    top = MapLayout.road_Right[(n + 1) % MapLayout.totalLines];
                }
                else
                {
                    bot = MapLayout.road_Left[n % MapLayout.totalLines];
                    top = MapLayout.road_Left[(n + 1) % MapLayout.totalLines];
                }

                float depth = (MathHelper.Clamp(top.center.Z / (MapLayout.totalLines * RenderConsts.segmentLength), 0f, 1f)) - 1f;
                Vector3 pos_TR, pos_BR, pos_TL, pos_BL,
                        tex_TR, tex_BR, tex_TL, tex_BL;

                float Y_top = top.ProjectWorldToScreenY(h, screen_height);
                float Y_bot = bot.ProjectWorldToScreenY(h, screen_height);
                float Ymin_top = Math.Min(top.screen.Y, top.clip);
                float Ymin_bot = Math.Min(bot.screen.Y, bot.clip);

                if (siderailinfo.isRight)
                {
                    pos_TR = new Vector3(bot.screen.X + bot.width * siderailinfo.widthPosMultiplier, Y_bot, depth);
                    pos_BR = new Vector3(bot.screen.X + bot.width * siderailinfo.widthPosMultiplier, Ymin_bot, depth);
                    pos_TL = new Vector3(top.screen.X + top.width * siderailinfo.widthPosMultiplier + ((n == end - 1) ? 1f : 0f), Y_top, depth);
                    pos_BL = new Vector3(top.screen.X + top.width * siderailinfo.widthPosMultiplier + ((n == end - 1) ? 1f : 0f), Ymin_top, depth);

                    tex_TL = new Vector3(texFrac * (end - (n + 1)), 1, 1);
                    tex_BR = new Vector3(texFrac * (end - n), bot.screen.Y <= bot.clip ? 0 : ((bot.screen.Y - bot.clip) / (bot.screen.Y - pos_TR.Y)), 1);
                    tex_TR = new Vector3(texFrac * (end - n), 1, 1);
                    tex_BL = new Vector3(texFrac * (end - (n + 1)), top.screen.Y <= top.clip ? 0 : ((top.screen.Y - top.clip) / (top.screen.Y - pos_TL.Y)), 1);
                }
                else
                {
                    pos_TL = new Vector3(bot.screen.X - bot.width * siderailinfo.widthPosMultiplier, Y_bot, depth);
                    pos_BL = new Vector3(bot.screen.X - bot.width * siderailinfo.widthPosMultiplier, Ymin_bot, depth);
                    pos_TR = new Vector3(top.screen.X - top.width * siderailinfo.widthPosMultiplier - ((n == end - 1) ? 1f : 0f), Y_top, depth);
                    pos_BR = new Vector3(top.screen.X - top.width * siderailinfo.widthPosMultiplier - ((n == end - 1) ? 1f : 0f), Ymin_top, depth);

                    tex_TR = new Vector3(texFrac * (end - (n + 1)), 1, 1);
                    tex_BL = new Vector3(texFrac * (end - n), bot.screen.Y <= bot.clip ? 0 : ((bot.screen.Y - bot.clip) / (bot.screen.Y - pos_TL.Y)), 1);
                    tex_TL = new Vector3(texFrac * (end - n), 1, 1);
                    tex_BR = new Vector3(texFrac * (end - (n + 1)), top.screen.Y <= top.clip ? 0 : ((top.screen.Y - top.clip) / (top.screen.Y - pos_TR.Y)), 1);
                }

                if (pos_TR.Y >= pos_BR.Y || pos_TL.Y >= pos_BL.Y) continue;

                CalculateProjectiveInterpolationTexture(pos_TR, pos_BR, pos_TL, pos_BL, ref tex_TR, ref tex_BR, ref tex_TL, ref tex_BL);

                int i = primitiveCount_SideRail * 4;
                verts_SideRail[i + 2] = new VertexPositionTextureColorPI(pos_TL, tex_TL, Color.White);
                verts_SideRail[i + 3] = new VertexPositionTextureColorPI(pos_TR, tex_TR, Color.White);
                verts_SideRail[i + 0] = new VertexPositionTextureColorPI(pos_BL, tex_BL, Color.White);
                verts_SideRail[i + 1] = new VertexPositionTextureColorPI(pos_BR, tex_BR, Color.White);

                primitiveCount_SideRail++;
            }

            if (primitiveCount_SideRail > 0)
            {
                projinterpEffect.Texture = siderailinfo.texture;
                foreach (EffectPass pass in projinterpEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionTextureColorPI>(PrimitiveType.TriangleList, verts_SideRail, 0, primitiveCount_SideRail * 4, quadindices, 0, primitiveCount_SideRail * 2);
                }
            }
        }

        #endregion

        #region Helper Funcs

        private float GoToZero(float value, float rate, float cutoff)
        {
            if (value == 0) return 0;

            if (value < 0)
            {
                value *= rate;
                if (value > -cutoff) return 0;
                return value;
            }
            else
            {
                value *= rate;
                if (value < cutoff) return 0;
                return value;
            }
        }

        private void CalculateProjectiveInterpolationTexture(Vector3 pos_TopRight, Vector3 pos_BotRight, Vector3 pos_TopLeft, Vector3 pos_BotLeft, ref Vector3 tex_TopRight, ref Vector3 tex_BotRight, ref Vector3 tex_TopLeft, ref Vector3 tex_BotLeft)
        {
            Single divisor = (pos_BotLeft.Y - pos_TopRight.Y) * (pos_BotRight.X - pos_TopLeft.X) - (pos_BotLeft.X - pos_TopRight.X) * (pos_BotRight.Y - pos_TopLeft.Y);
            Single ua = ((pos_BotLeft.X - pos_TopRight.X) * (pos_TopLeft.Y - pos_TopRight.Y) - (pos_BotLeft.Y - pos_TopRight.Y) * (pos_TopLeft.X - pos_TopRight.X)) / divisor;
            Single ub = ((pos_BotRight.X - pos_TopLeft.X) * (pos_TopLeft.Y - pos_TopRight.Y) - (pos_BotRight.Y - pos_TopLeft.Y) * (pos_TopLeft.X - pos_TopRight.X)) / divisor;

            // calculates the intersection point
            Single centerX = pos_TopLeft.X + ua * (pos_BotRight.X - pos_TopLeft.X);
            Single centerY = pos_TopLeft.Y + ub * (pos_BotRight.Y - pos_TopLeft.Y);
            Vector3 center = new Vector3(centerX, centerY, 0.5f);

            // determines distances to center for all vertexes
            Single d1 = (pos_TopLeft - center).Length();
            Single d2 = (pos_BotRight - center).Length();
            Single d3 = (pos_TopRight - center).Length();
            Single d4 = (pos_BotLeft - center).Length();

            // calculates quotients used as w component in uvw texture mapping
            tex_TopLeft *= Single.IsNaN(d2) || d2 == 0.0f ? 1.0f : (d1 + d2) / d2;
            tex_BotRight *= Single.IsNaN(d1) || d1 == 0.0f ? 1.0f : (d2 + d1) / d1;
            tex_TopRight *= Single.IsNaN(d4) || d4 == 0.0f ? 1.0f : (d3 + d4) / d4;
            tex_BotLeft *= Single.IsNaN(d3) || d3 == 0.0f ? 1.0f : (d4 + d3) / d3;
        }

        private bool CheckOverlapX(Vector2 a, Vector2 b)
        {
            return !((a.Y < b.X) || (a.X > b.Y));
        }

        #endregion

    }
}