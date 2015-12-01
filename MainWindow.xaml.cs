﻿using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Timers;
using System.Windows;
using System.Windows.Forms;

namespace Chip8
{
    public partial class MainWindow : Window
    {
        private GLControl _openGLControl;

        private Engine _engine;

        private int _openALToneSourceID;

        private System.Timers.Timer _screenRefreshTimer;

        public MainWindow()
        {
            InitializeComponent();

            _openGLControl = new GLControl();
            _openGLControl.Width = (int)openGLControlHost.Width;
            _openGLControl.Height = (int)openGLControlHost.Height;
            openGLControlHost.Child = _openGLControl;
            _openGLControl.Load += _openGLControl_Load;
            _openGLControl.Paint += _openGLControl_Paint;

            short[] tone = new short[(int)(.20 * Engine.TONE_SAMPLE_RATE)];
            for (int index = 0; index < tone.Length; index++)
            {
                tone[index] = (short)(short.MaxValue * Math.Sin(((2 * Math.PI * Engine.TONE_FREQUENCY) / Engine.TONE_SAMPLE_RATE) * index));
            }

            AudioContext audioContext = new AudioContext();
            int openALBufferID = AL.GenBuffer();
            _openALToneSourceID = AL.GenSource();
            AL.BufferData(openALBufferID, ALFormat.Mono16, tone, (tone.Length * 2), (int)Engine.TONE_SAMPLE_RATE);
            AL.Source(_openALToneSourceID, ALSourcei.Buffer, openALBufferID);
            AL.Source(_openALToneSourceID, ALSourceb.Looping, true);
        }

        private void _openGLControl_Load(object sender, EventArgs e)
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            GL.Ortho(0, Engine.SCREEN_WIDTH, Engine.SCREEN_HEIGHT, 0, -1, 1);
            GL.Viewport(0, 0, _openGLControl.Width, _openGLControl.Height);
        }

        public void _openGLControl_Paint(object sender, PaintEventArgs e)
        {
            GL.ClearColor(Color.Black);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            GL.Color3(Color.White);

            GL.Begin(PrimitiveType.Quads);

            byte[] screen = _engine.Screen;
            for (int screenY = 0; screenY < Engine.SCREEN_HEIGHT; screenY++)
            {
                for (int screenX = 0; screenX < Engine.SCREEN_WIDTH; screenX++)
                {
                    if (screen[(screenY * Engine.SCREEN_WIDTH) + screenX] == 1)
                    {
                        GL.Vertex2(screenX, screenY);
                        GL.Vertex2(screenX + 1, screenY);
                        GL.Vertex2(screenX + 1, screenY + 1);
                        GL.Vertex2(screenX, screenY + 1);
                    }
                }
            }

            GL.End();

            _openGLControl.SwapBuffers();
        }

        private byte getKeypress()
        {
            Dictionary<Key, byte> keyValueMap = new Dictionary<Key, byte>()
            {
                { Key.Number0, 0x0 }, { Key.Number1, 0x1 }, { Key.Number2, 0x2 }, { Key.Number3, 0x3 },
                { Key.Number4, 0x4 }, { Key.Number5, 0x5 }, { Key.Number6, 0x6 }, { Key.Number7, 0x7 },
                { Key.Number8, 0x8 }, { Key.Number9, 0x9 }, { Key.A, 0xA }, { Key.B, 0xB },
                { Key.C, 0xC }, { Key.D, 0xD }, { Key.E, 0xE }, { Key.F, 0xF }
            };

            KeyboardState keyboardState = Keyboard.GetState();

            foreach (Key key in keyValueMap.Keys)
                if (keyboardState.IsKeyDown(key))
                    return (keyValueMap[key]);
            
            return (0xFF);
        }

        private void playTone()
        {
            if (AL.GetSourceState(_openALToneSourceID) != ALSourceState.Playing)
                AL.SourcePlay(_openALToneSourceID);
        }

        private void stopTone()
        {
            AL.SourceStop(_openALToneSourceID);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (_engine == null)
            {
                _screenRefreshTimer = new System.Timers.Timer(40);
                _screenRefreshTimer.Elapsed += _screenRefreshTimer_Elapsed;
                _screenRefreshTimer.Start();

                _engine = new Engine();
                _engine.GetKeypress = getKeypress;
                _engine.PlayTone = playTone;
                _engine.StopTone = stopTone;
                _engine.Initialize();
                _engine.Start();
            }
        }

        private void _screenRefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _openGLControl.Invalidate();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _screenRefreshTimer.Stop();
            _engine.Stop();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //if (AL.GetSourceState(_openALToneSourceID) == ALSourceState.Playing)
            //    AL.SourceStop(_openALToneSourceID);
            //else
            //    AL.SourcePlay(_openALToneSourceID);
        }
    }
}
