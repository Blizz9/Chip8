using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Timers;

namespace Chip8
{
    // TODO: Clean up code
    // TODO: Add save/load state
    // TODO: Add ROM selector and close
    // TODO: Add speed selector
    // TODO: Add reset
    // TODO: Add pause on menu popup
    // TODO: Add key graphic
    // TODO: Add key highlighting based on opcodess

    internal class Engine
    {
        public const byte SCREEN_WIDTH = 64;
        public const byte SCREEN_HEIGHT = 32;

        private const uint MEMORY_SIZE = 0x1000;
        private const byte REGISTER_COUNT = 0x10;
        private const byte STACK_SIZE = 0x10;
        private const uint MEMORY_ROM_OFFSET = 0x200;

        private const byte OPCODE_SIZE = 0x2;
        private const byte FONT_SIZE = 0x5;

        private const byte BATCH_COUNT = 50;
        private const uint CYCLE_HZ = 700;
        private const byte INTERNAL_TIMER_HZ = 60;

        public const uint TONE_SAMPLE_RATE = 44100;
        public const uint TONE_FREQUENCY = 365;

        public const byte BITS_PER_BYTE = 8;
        private const uint MILLISECONDS_PER_SECOND = 1000;

        private byte[] _memory;
        private uint _pc; // program counter
        private byte[] _v; // registers
        private uint _i; // index register
        private uint[] _stack;
        private byte _sp; // stack pointer
        private byte _delayTimer;
        private byte _soundTimer;
        private byte[] _screen;

        private System.Timers.Timer _internalTimer;

        private List<byte[]> _fonts;

        private Dictionary<byte, Action<uint>> _opcodeMap;
        private Dictionary<byte, Action<uint>> _opcodeMap00EX;
        private Dictionary<byte, Action<uint>> _opcodeMap8XXX;
        private Dictionary<byte, Action<uint>> _opcodeMapEXXX;
        private Dictionary<byte, Action<uint>> _opcodeMapFXXX;

        private readonly object _sync = new object();

        private bool _running;
        private bool _paused;

        private Func<byte> _getKeypress;
        private Action _playTone;
        private Action _stopTone;

        internal Engine()
        {
            _memory = new byte[MEMORY_SIZE];
            _v = new byte[REGISTER_COUNT];
            _stack = new uint[STACK_SIZE];
            Screen = new byte[SCREEN_WIDTH * SCREEN_HEIGHT];

            _internalTimer = new System.Timers.Timer(MILLISECONDS_PER_SECOND / INTERNAL_TIMER_HZ);
            _internalTimer.Elapsed += internalTimerClock;

            _opcodeMap = new Dictionary<byte, Action<uint>>();
            _opcodeMap.Add(0x0, map00EXOperations);
            _opcodeMap.Add(0x1, jump);
            _opcodeMap.Add(0x2, callSubroutine);
            _opcodeMap.Add(0x3, jumpIfEqualTo);
            _opcodeMap.Add(0x4, jumpIfNotEqualTo);
            _opcodeMap.Add(0x5, jumpIfEqual);
            _opcodeMap.Add(0x6, load);
            _opcodeMap.Add(0x7, appendValue);
            _opcodeMap.Add(0x8, map8XXXOperations);
            _opcodeMap.Add(0x9, jumpIfNotEqual);
            _opcodeMap.Add(0xA, loadIndex);
            _opcodeMap.Add(0xB, jumpWithOffset);
            _opcodeMap.Add(0xC, random);
            _opcodeMap.Add(0xD, drawSprite);
            _opcodeMap.Add(0xE, mapEXXXOperations);
            _opcodeMap.Add(0xF, mapFXXXOperations);

            _opcodeMap00EX = new Dictionary<byte, Action<uint>>();
            _opcodeMap00EX.Add(0xE0, clearScreen);
            _opcodeMap00EX.Add(0xEE, returnSubroutine);

            _opcodeMap8XXX = new Dictionary<byte, Action<uint>>();
            _opcodeMap8XXX.Add(0x0, copy);
            _opcodeMap8XXX.Add(0x1, or);
            _opcodeMap8XXX.Add(0x2, and);
            _opcodeMap8XXX.Add(0x3, xor);
            _opcodeMap8XXX.Add(0x4, add);
            _opcodeMap8XXX.Add(0x5, subtract);
            _opcodeMap8XXX.Add(0x6, shiftRight);
            _opcodeMap8XXX.Add(0x7, subtractReverse);
            _opcodeMap8XXX.Add(0xE, shiftLeft);

            _opcodeMapEXXX = new Dictionary<byte, Action<uint>>();
            _opcodeMapEXXX.Add(0x9E, jumpIfKeyPressed);
            _opcodeMapEXXX.Add(0xA1, jumpIfKeyNotPressed);

            _opcodeMapFXXX = new Dictionary<byte, Action<uint>>();
            _opcodeMapFXXX.Add(0x07, readDelayTimer);
            _opcodeMapFXXX.Add(0x0A, waitForKeypress);
            _opcodeMapFXXX.Add(0x15, loadDelayTimer);
            _opcodeMapFXXX.Add(0x18, loadSoundTimer);
            _opcodeMapFXXX.Add(0x1E, addToIndex);
            _opcodeMapFXXX.Add(0x29, addressFontCharacter);
            _opcodeMapFXXX.Add(0x33, storeBCD);
            _opcodeMapFXXX.Add(0x55, dumpRegisters);
            _opcodeMapFXXX.Add(0x65, fillRegisters);

            running = false;
            paused = false;
        }

        #region Private Properties

        private bool running
        {
            get { lock (_sync) { return (_running); } }
            set { lock (_sync) { _running = value; } }
        }

        private bool paused
        {
            get { lock (_sync) { return (_paused); } }
            set { lock (_sync) { _paused = value; } }
        }

        private byte delayTimer
        {
            get { lock (_sync) { return (_delayTimer); } }
            set { lock (_sync) { _delayTimer = value; } }
        }

        private byte soundTimer
        {
            get { lock (_sync) { return (_soundTimer); } }
            set { lock (_sync) { _soundTimer = value; } }
        }

        #endregion

        #region Accessible Properties

        internal byte[] Screen
        {
            get { lock (_sync) { return (_screen); } }
            set { lock (_sync) { _screen = value; } }
        }

        #endregion

        #region Accessible Callbacks

        internal Func<byte> GetKeypress
        {
            get { lock (_sync) { return (_getKeypress); } }
            set { lock (_sync) { _getKeypress = value; } }
        }

        internal Action PlayTone
        {
            get { lock (_sync) { return (_playTone); } }
            set { lock (_sync) { _playTone = value; } }
        }

        internal Action StopTone
        {
            get { lock (_sync) { return (_stopTone); } }
            set { lock (_sync) { _stopTone = value; } }
        }

        #endregion

        #region Accessible Methods

        internal void Initialize()
        {
            Array.Clear(_memory, 0, _memory.Length);
            Array.Clear(_v, 0, _v.Length);
            Array.Clear(_stack, 0, _stack.Length);
            Array.Clear(Screen, 0, Screen.Length);

            _fonts = new List<byte[]>();

            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x90, // 1001
                0x90, // 1001
                0x90, // 1001
                0xF0  // 1111
            }); // 0
            _fonts.Add(new byte[FONT_SIZE]
            {
                0x20, // 0010
                0x60, // 0110
                0x20, // 0010
                0x20, // 0010
                0x70  // 0111
            }); // 1
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x10, // 0001
                0xF0, // 1111
                0x80, // 1000
                0xF0  // 1111
            }); // 2
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x10, // 0001
                0xF0, // 1111
                0x10, // 0001
                0xF0  // 1111
            }); // 3
            _fonts.Add(new byte[FONT_SIZE]
            {
                0x90, // 1001
                0x90, // 1001
                0xF0, // 1111
                0x10, // 0001
                0x10  // 0001
            }); // 4
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0xF0, // 1111
                0x10, // 0001
                0xF0  // 1111
            }); // 5
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0xF0, // 1111
                0x90, // 1001
                0xF0  // 1111
            }); // 6
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x10, // 0001
                0x20, // 0010
                0x40, // 0100
                0x40  // 0100
            }); // 7
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x90, // 1001
                0xF0, // 1111
                0x90, // 1001
                0xF0  // 1111
            }); // 8
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x90, // 1001
                0xF0, // 1111
                0x10, // 0001
                0xF0  // 1111
            }); // 9
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x90, // 1001
                0xF0, // 1111
                0x90, // 1001
                0x90  // 1001
            }); // A
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xE0, // 1110
                0x90, // 1001
                0xE0, // 1110
                0x90, // 1001
                0xE0  // 1110
            }); // B
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0x80, // 1000
                0x80, // 1000
                0xF0  // 1111
            }); // C
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xE0, // 1110
                0x90, // 1001
                0x90, // 1001
                0x90, // 1001
                0xE0  // 1110
            }); // D
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0xF0, // 1111
                0x80, // 1000
                0xF0  // 1111
            }); // E
            _fonts.Add(new byte[FONT_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0xF0, // 1111
                0x80, // 1000
                0x80  // 1000
            }); // F

            foreach (byte[] font in _fonts)
                Buffer.BlockCopy(font, 0, _memory, (0x0000 + (_fonts.IndexOf(font) * FONT_SIZE)), FONT_SIZE);

            //byte[] rom = File.ReadAllBytes("ROMs\\Chip8 emulator Logo [Garstyciuks].ch8");
            //byte[] rom = File.ReadAllBytes("ROMs\\Chip8 Picture.ch8");
            //byte[] rom = File.ReadAllBytes("ROMs\\Breakout [Carmelo Cortez, 1979].ch8");
            //byte[] rom = File.ReadAllBytes("ROMs\\15 Puzzle [Roger Ivie].ch8");
            //byte[] rom = File.ReadAllBytes("ROMs\\Cave.ch8");
            //byte[] rom = File.ReadAllBytes("ROMs\\Breakout (Brix hack) [David Winter, 1997].ch8");
            byte[] rom = File.ReadAllBytes("ROMs\\Particle Demo [zeroZshadow, 2008].ch8");
            //byte[] rom = File.ReadAllBytes("ROMs\\Pong 2 (Pong hack) [David Winter, 1997].ch8");
            //byte[] rom = File.ReadAllBytes("ROMs\\Tetris [Fran Dachille, 1991].ch8");

            Buffer.BlockCopy(rom, 0, _memory, (int)MEMORY_ROM_OFFSET, rom.Length);

            _i = 0;
            _pc = MEMORY_ROM_OFFSET;
            _sp = 0;
            delayTimer = 0;
            soundTimer = 0;
        }

        internal void Start()
        {
            running = true;

            _internalTimer.Start();

            Thread engineThread = new Thread(loop);
            engineThread.Start();
        }

        internal void Stop()
        {
            _internalTimer.Stop();
            running = false;
        }

        #endregion

        #region Main Loop Methods

        private void loop()
        {
            Stopwatch timingStopwatch = new Stopwatch();
            int cycleCount = 0;

            while (running)
            {
                if (!paused)
                {
                    if (!timingStopwatch.IsRunning)
                    {
                        cycleCount = 0;
                        timingStopwatch.Reset();
                        timingStopwatch.Start();
                    }

                    clock();

                    cycleCount++;

                    if (cycleCount >= (CYCLE_HZ / BATCH_COUNT))
                    {
                        timingStopwatch.Stop();

                        if (timingStopwatch.ElapsedMilliseconds < (MILLISECONDS_PER_SECOND / BATCH_COUNT))
                            Thread.Sleep((int)((MILLISECONDS_PER_SECOND / BATCH_COUNT) - timingStopwatch.ElapsedMilliseconds));
                    }
                }
            }
        }

        private void clock()
        {
            uint opcode = (uint)(_memory[_pc] << 8) | _memory[_pc + 1];
            _pc += OPCODE_SIZE;

            byte opcodeMSN = (byte)((opcode & 0xF000) >> 12);

            _opcodeMap[opcodeMSN](opcode);
        }

        private void internalTimerClock(object sender, ElapsedEventArgs e)
        {
            if (delayTimer > 0)
                delayTimer--;

            if (soundTimer > 0)
            {
                soundTimer--;

                if (soundTimer == 0)
                    StopTone();
            }
        }

        #endregion

        #region Operations Mapping

        private void map00EXOperations(uint opcode)
        {
            byte opcodeLSB = (byte)(opcode & 0x00FF);
            _opcodeMap00EX[opcodeLSB](opcode);
        }

        private void map8XXXOperations(uint opcode)
        {
            byte opcodeLSN = (byte)(opcode & 0x000F);
            _opcodeMap8XXX[opcodeLSN](opcode);
        }

        private void mapEXXXOperations(uint opcode)
        {
            byte opcodeLSB = (byte)(opcode & 0x00FF);
            _opcodeMapEXXX[opcodeLSB](opcode);
        }

        private void mapFXXXOperations(uint opcode)
        {
            byte opcodeLSB = (byte)(opcode & 0x00FF);
            _opcodeMapFXXX[opcodeLSB](opcode);
        }

        #endregion

        #region Operations

        #region 00EX Operations

        // 00E0
        private void clearScreen(uint opcode)
        {
            Array.Clear(Screen, 0, Screen.Length);
        }

        // 00EE
        private void returnSubroutine(uint opcode)
        {
            _sp--;
            _pc = _stack[_sp];
        }

        #endregion

        // 1NNN
        private void jump(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _pc = address;
        }

        // 2NNN
        private void callSubroutine(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _stack[_sp] = _pc;
            _sp++;
            _pc = address;
        }

        // 3XNN
        private void jumpIfEqualTo(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            if (_v[register] == value)
                _pc += OPCODE_SIZE;
        }

        // 4XNN
        private void jumpIfNotEqualTo(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            if (_v[register] != value)
                _pc += OPCODE_SIZE;
        }

        // 5XY0
        private void jumpIfEqual(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY= (byte)((opcode & 0x00F0) >> 4);
            if (_v[registerX] == _v[registerY])
                _pc += OPCODE_SIZE;
        }

        // 6XNN
        private void load(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            _v[register] = value;
        }

        // 7XNN
        private void appendValue(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            _v[register] += value;
        }

        #region 8XXX Operations

        // 8XY0
        private void copy(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            _v[registerX] = _v[registerY];
        }

        // 8XY1
        private void or(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            _v[registerX] = (byte)(_v[registerX] | _v[registerY]);
        }

        // 8XY2
        private void and(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            _v[registerX] = (byte)(_v[registerX] & _v[registerY]);
        }

        // 8XY3
        private void xor(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            _v[registerX] = (byte)(_v[registerX] ^ _v[registerY]);
        }

        // 8XY4
        private void add(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);

            int result = _v[registerX] + _v[registerY];
            _v[0xF] = result > (int)byte.MaxValue ? (byte)1 : (byte)0;
            _v[registerX] = (byte)result;
        }

        // 8XY5
        private void subtract(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);

            _v[0xF] = _v[registerX] > _v[registerY] ? (byte)1 : (byte)0;
            _v[registerX] = (byte)(_v[registerX] - _v[registerY]);
        }

        // 8XY6
        private void shiftRight(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            _v[0xF] = (byte)(_v[registerX] & 0x01);
            _v[registerX] = (byte)(_v[registerX] >> 1);
        }

        // 8XY7
        private void subtractReverse(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);

            _v[0xF] = _v[registerY] > _v[registerX] ? (byte)1 : (byte)0;
            _v[registerX] = (byte)(_v[registerY] - _v[registerX]);
        }

        // 8XYE
        private void shiftLeft(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            _v[0xF] = (byte)((_v[registerX] & 0x80) >> 7);
            _v[registerX] = (byte)(_v[registerX] << 1);
        }

        #endregion

        // 9XY0
        private void jumpIfNotEqual(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            if (_v[registerX] != _v[registerY])
                _pc += OPCODE_SIZE;
        }

        // ANNN
        private void loadIndex(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _i = address;
        }

        // BNNN
        private void jumpWithOffset(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _pc = address + _v[0x0];
        }

        // CXNN
        private void random(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);

            Random random = new Random();
            byte randomValue = (byte)random.Next(256);

            _v[register] = (byte)(randomValue & value);
        }

        // DXYN
        private void drawSprite(uint opcode)
        {
            byte xRegister = (byte)((opcode & 0x0F00) >> 8);
            byte yRegister = (byte)((opcode & 0x00F0) >> 4);
            byte numberOfSpriteLines = (byte)(opcode & 0x000F);

            byte xLocation = (byte)(_v[xRegister] & 0x3F);
            byte yLocation = (byte)(_v[yRegister] & 0x1F);

            byte[] sprite = new byte[numberOfSpriteLines];
            Buffer.BlockCopy(_memory, (int)_i, sprite, 0, numberOfSpriteLines);

            bool collision = false;

            for (int spriteLineIndex = 0; spriteLineIndex < numberOfSpriteLines; spriteLineIndex++)
            {
                byte pixelMask = 0x80;

                while (pixelMask != 0x00)
                {
                    if ((sprite[spriteLineIndex] & pixelMask) != 0)
                    {
                        if (Screen[(yLocation * SCREEN_WIDTH) + xLocation] == 0)
                            Screen[(yLocation * SCREEN_WIDTH) + xLocation] = 1;
                        else
                        {
                            Screen[(yLocation * SCREEN_WIDTH) + xLocation] = 0;
                            collision = true;
                        }
                    }

                    pixelMask = (byte)(pixelMask >> 1);
                    xLocation++;

                    if (xLocation >= SCREEN_WIDTH)
                        break;
                }

                xLocation = _v[xRegister];
                yLocation++;

                if (yLocation >= SCREEN_HEIGHT)
                    break;
            }

            _v[0xF] = collision ? (byte)1 : (byte)0;
        }

        #region EXXX Operations

        // EX9E
        private void jumpIfKeyPressed(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            byte keypress = GetKeypress();

            if (_v[registerX] == keypress)
                _pc += OPCODE_SIZE;
        }

        // EXA1
        private void jumpIfKeyNotPressed(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            byte keypress = GetKeypress();

            if (_v[registerX] != keypress)
                _pc += OPCODE_SIZE;
        }

        #endregion

        #region FXXX Operations

        // FX07
        private void readDelayTimer(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            _v[registerX] = delayTimer;
        }

        // FX0A
        private void waitForKeypress(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            byte keypress = 0xFF;

            while ((keypress == 0xFF) && running)
                keypress = GetKeypress();

            _v[registerX] = keypress;
        }

        // FX15
        private void loadDelayTimer(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            delayTimer = _v[registerX];
        }

        // FX18
        private void loadSoundTimer(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            soundTimer = _v[registerX];

            if (soundTimer > 0)
                PlayTone();
        }

        // FX1E
        private void addToIndex(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            _i += _v[registerX];
        }

        // FX29
        private void addressFontCharacter(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            _i = (byte)(_v[registerX] * FONT_SIZE);
        }

        // FX33
        private void storeBCD(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            _memory[_i] = (byte)((_v[registerX] / 100) % 10);
            _memory[_i + 1] = (byte)((_v[registerX] / 10) % 10);
            _memory[_i + 2] = (byte)(_v[registerX] % 10);
        }

        // FX55
        private void dumpRegisters(uint opcode)
        {
            byte endRegister = (byte)((opcode & 0x0F00) >> 8);

            for (int register = 0; register <= endRegister; register++)
                 _memory[_i + register] = _v[register];
        }

        // FX65
        private void fillRegisters(uint opcode)
        {
            byte endRegister = (byte)((opcode & 0x0F00) >> 8);

            for (int register = 0; register <= endRegister; register++)
                _v[register] = _memory[_i + register];
        }

        #endregion

        #endregion
    }
}
