using OpenTK;
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
using System.Windows.Media;

namespace Chip8
{
    public partial class MainWindow : Window
    {
        private const int SCREEN_REFRESH_FREQUENCY = 25;

        private const int TONE_SAMPLE_RATE = 44100;
        private const int TONE_FREQUENCY = 365;
        private const double TONE_PERIOD = .20;

        private Core _core;
        private GLControl _openGLControl;
        private System.Timers.Timer _videoRefreshTimer;
        private int _openALToneSourceID;
        private Dictionary<Key, byte> _keyValueMap;

        private string _runningROMFilename;

        public MainWindow()
        {
            InitializeComponent();

            _openGLControl = new GLControl();
            _openGLControl.Width = (int)openGLControlHost.Width;
            _openGLControl.Height = (int)openGLControlHost.Height;
            openGLControlHost.Child = _openGLControl;
            _openGLControl.Paint += _openGLControl_Paint;

            initializeCore();
            initializeVideo();
            initializeAudio();
            initializeInput();
        }

        #region Window Event Handlers

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            startCore();
            startVideo();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            cleanupVideo();
            cleanupCore();
        }

        #endregion

        #region Menu Event Handlers

        private void actionsMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            _core.Pause();
        }

        private void actionsMenuItem_SubmenuClosed(object sender, RoutedEventArgs e)
        {
            _core.Unpause();
        }

        private void openROMMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Chip-8 ROMs (*.ch8)|*.ch8|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _runningROMFilename = openFileDialog.FileName;
                _core.Reset(_runningROMFilename);
            }
        }

        private void resetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _core.Reset(_runningROMFilename);
        }

        private void speedMenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.MenuItem clickedMenuItem = (System.Windows.Controls.MenuItem)sender;
            System.Windows.Controls.MenuItem mainMenuItem = (System.Windows.Controls.MenuItem)(clickedMenuItem).Parent;

            foreach (var child in LogicalTreeHelper.GetChildren(mainMenuItem))
            {
                if ((child is System.Windows.Controls.MenuItem) && ((System.Windows.Controls.MenuItem)child).IsCheckable)
                    ((System.Windows.Controls.MenuItem)child).IsChecked = false;
            }

            clickedMenuItem.IsChecked = true;

            _core.CycleFrequency = Convert.ToInt32(((string)clickedMenuItem.Header).Split(' ')[0].TrimStart('_'));
        }

        private void saveStateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _core.SaveState();
        }

        private void loadStateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _core.LoadState();
        }

        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region OpenGL Control Event Handlers

        public void _openGLControl_Paint(object sender, PaintEventArgs e)
        {
            refreshVideo();
        }

        #endregion

        #region Video Refresh Timer Handlers

        private void invalidateOpenGLControl(object sender, ElapsedEventArgs e)
        {
            _openGLControl.Invalidate();
        }

        #endregion

        #region Core Routines

        private void initializeCore()
        {
            _core = new Core();
            _core.GetKeypress = getKeypress;
            _core.PlayTone = playTone;
            _core.StopTone = stopTone;
        }

        private void startCore()
        {
            _runningROMFilename = "logo.ch8";
            _core.Reset(_runningROMFilename);
        }

        private void cleanupCore()
        {
            _core.Stop();
        }

        #endregion

        #region Video Routines

        private void initializeVideo()
        {
            _videoRefreshTimer = new System.Timers.Timer(Global.MILLISECONDS_PER_SECOND / SCREEN_REFRESH_FREQUENCY);
            _videoRefreshTimer.Elapsed += invalidateOpenGLControl;
        }

        private void startVideo()
        {
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            GL.Ortho(0, Core.SCREEN_WIDTH, Core.SCREEN_HEIGHT, 0, -1, 1);
            GL.Viewport(0, 0, _openGLControl.Width, _openGLControl.Height);

            _videoRefreshTimer.Start();
        }

        private void refreshVideo()
        {
            GL.ClearColor(System.Drawing.Color.Black);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            GL.Color3(System.Drawing.Color.White);

            GL.Begin(PrimitiveType.Quads);

            byte[] screen = _core.Screen;
            for (int screenY = 0; screenY < Core.SCREEN_HEIGHT; screenY++)
            {
                for (int screenX = 0; screenX < Core.SCREEN_WIDTH; screenX++)
                {
                    if (screen[(screenY * Core.SCREEN_WIDTH) + screenX] == 1)
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

        private void cleanupVideo()
        {
            _videoRefreshTimer.Stop();
        }

        #endregion

        #region Audio Routines

        private void initializeAudio()
        {
            short[] tone = new short[(int)(TONE_PERIOD * TONE_SAMPLE_RATE)];
            for (int index = 0; index < tone.Length; index++)
                tone[index] = (short)(short.MaxValue * Math.Sin(((2 * Math.PI * TONE_FREQUENCY) / TONE_SAMPLE_RATE) * index));

            AudioContext audioContext = new AudioContext();
            int openALBufferID = AL.GenBuffer();
            _openALToneSourceID = AL.GenSource();
            AL.BufferData(openALBufferID, ALFormat.Mono16, tone, (tone.Length * 2), (int)TONE_SAMPLE_RATE);
            AL.Source(_openALToneSourceID, ALSourcei.Buffer, openALBufferID);
            AL.Source(_openALToneSourceID, ALSourceb.Looping, true);
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

        #endregion

        #region Input Routines

        private void initializeInput()
        {
            _keyValueMap = new Dictionary<Key, byte>()
            {
                { Key.Number0, 0x0 }, { Key.Number1, 0x1 }, { Key.Number2, 0x2 }, { Key.Number3, 0x3 },
                { Key.Number4, 0x4 }, { Key.Number5, 0x5 }, { Key.Number6, 0x6 }, { Key.Number7, 0x7 },
                { Key.Number8, 0x8 }, { Key.Number9, 0x9 }, { Key.A, 0xA }, { Key.B, 0xB },
                { Key.C, 0xC }, { Key.D, 0xD }, { Key.E, 0xE }, { Key.F, 0xF }
            };
        }

        private byte getKeypress()
        {
            KeyboardState keyboardState = Keyboard.GetState();

            foreach (Key key in _keyValueMap.Keys)
                if (keyboardState.IsKeyDown(key))
                    return (_keyValueMap[key]);

            return (0xFF);
        }

        #endregion
    }
}
