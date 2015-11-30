using SDL2;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Chip8
{
    public partial class MainWindow : Window
    {
        SDL.SDL_Rect _drawableRegion;
        IntPtr _sdlRendererPointer;
        IntPtr _sdlSurfacePointer;
        SDL.SDL_Surface _sdlSurface;
        IntPtr _sdlTexturePointer;

        Engine _engine;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void drawScreen(byte[] screen)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                for (int screenY = 0; screenY < 32; screenY++)
                {
                    for (int screenX = 0; screenX < 64; screenX++)
                    {
                        if (screen[(screenY * 64) + screenX] == 1)
                        {
                            SDL.SDL_Rect drawRect = new SDL.SDL_Rect()
                            {
                                x = screenX,
                                y = screenY,
                                w = 1,
                                h = 1
                            };
                            SDL.SDL_FillRect(_sdlSurfacePointer, ref drawRect, SDL.SDL_MapRGB(_sdlSurface.format, 0xFF, 0xFF, 0xFF));
                        }
                    }
                }

                SDL.SDL_UpdateTexture(_sdlTexturePointer, IntPtr.Zero, _sdlSurface.pixels, _sdlSurface.pitch);

                SDL.SDL_RenderClear(_sdlRendererPointer);
                SDL.SDL_RenderCopy(_sdlRendererPointer, _sdlTexturePointer, IntPtr.Zero, ref _drawableRegion);
                SDL.SDL_RenderPresent(_sdlRendererPointer);
            }));
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (_engine == null)
            {
                _drawableRegion = new SDL.SDL_Rect()
                {
                    x = (int)drawableSurface.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0)).X,
                    y = (int)drawableSurface.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0)).Y,
                    w = (int)drawableSurface.Width,
                    h = (int)drawableSurface.Height
                };
                SDL.SDL_Rect drawableSize = new SDL.SDL_Rect() { w = _drawableRegion.w, h = _drawableRegion.h };

                SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING);

                //IntPtr sdlWindowPointer = SDL.SDL_CreateWindow("My Game Window", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 256, 128, SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL);
                IntPtr sdlWindowPointer = SDL.SDL_CreateWindowFrom(new WindowInteropHelper(Application.Current.MainWindow).Handle);
                _sdlRendererPointer = SDL.SDL_CreateRenderer(sdlWindowPointer, -1, 0);
                SDL.SDL_SetRenderDrawColor(_sdlRendererPointer, 0xFF, 0xFF, 0xFF, 0xFF);

                _sdlSurfacePointer = SDL.SDL_CreateRGBSurface(0, drawableSize.w, drawableSize.h, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000);
                _sdlSurface = (SDL.SDL_Surface)Marshal.PtrToStructure(_sdlSurfacePointer, typeof(SDL.SDL_Surface));

                _sdlTexturePointer = SDL.SDL_CreateTexture(_sdlRendererPointer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, drawableSize.w, drawableSize.h);

                SDL.SDL_FillRect(_sdlSurfacePointer, ref drawableSize, SDL.SDL_MapRGB(_sdlSurface.format, 0x00, 0x00, 0x00));

                SDL.SDL_UpdateTexture(_sdlTexturePointer, IntPtr.Zero, _sdlSurface.pixels, _sdlSurface.pitch);

                SDL.SDL_RenderClear(_sdlRendererPointer);
                SDL.SDL_RenderCopy(_sdlRendererPointer, _sdlTexturePointer, IntPtr.Zero, ref _drawableRegion);
                SDL.SDL_RenderPresent(_sdlRendererPointer);

                _engine = new Engine();
                _engine.Initialize();
                _engine.NewDrawing = drawScreen;
                _engine.Start();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _engine.Stop();
        }
    }
}
