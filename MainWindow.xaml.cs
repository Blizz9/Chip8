using SDL2;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Chip8
{
    public partial class MainWindow : Window
    {
        Engine _engine;

        public MainWindow()
        {
            InitializeComponent();

            _engine = new Engine();
            _engine.Initialize();
            _engine.Start();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            IntPtr mainWindowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;

            SDL.SDL_Rect drawableRegion = new SDL.SDL_Rect()
            {
                x = (int)drawableSurface.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0)).X,
                y = (int)drawableSurface.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0)).Y,
                w = (int)drawableSurface.Width,
                h = (int)drawableSurface.Height
            };
            SDL.SDL_Rect drawableSize = new SDL.SDL_Rect() { w = drawableRegion.w, h = drawableRegion.h };

            SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING);

            //IntPtr sdlWindowPointer = SDL.SDL_CreateWindow("My Game Window", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 256, 128, SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL);
            IntPtr sdlWindowPointer = SDL.SDL_CreateWindowFrom(mainWindowHandle);

            IntPtr sdlRendererPointer = SDL.SDL_CreateRenderer(sdlWindowPointer, -1, 0);
            SDL.SDL_SetRenderDrawColor(sdlRendererPointer, 0xFF, 0xFF, 0xFF, 0xFF);

            IntPtr sdlSurfacePointer = SDL.SDL_CreateRGBSurface(0, drawableSize.w, drawableSize.h, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000);
            SDL.SDL_Surface sdlSurface = (SDL.SDL_Surface)Marshal.PtrToStructure(sdlSurfacePointer, typeof(SDL.SDL_Surface));

            IntPtr sdlTexturePointer = SDL.SDL_CreateTexture(sdlRendererPointer, SDL.SDL_PIXELFORMAT_ARGB8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, drawableSize.w, drawableSize.h);

            SDL.SDL_FillRect(sdlSurfacePointer, ref drawableSize, SDL.SDL_MapRGB(sdlSurface.format, 0xFF, 0x00, 0x00));

            SDL.SDL_UpdateTexture(sdlTexturePointer, IntPtr.Zero, sdlSurface.pixels, sdlSurface.pitch);

            SDL.SDL_RenderClear(sdlRendererPointer);
            SDL.SDL_RenderCopy(sdlRendererPointer, sdlTexturePointer, IntPtr.Zero, ref drawableRegion);
            SDL.SDL_RenderPresent(sdlRendererPointer);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _engine.Stop();
        }
    }
}
