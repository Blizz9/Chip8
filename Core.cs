using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;

namespace Chip8
{
    // TODO: Move uints to ushort
    // TODO: Rename logo
    // TODO: Add key graphic
    // TODO: Add key highlighting based on opcodes

    internal class Core
    {
        internal const byte SCREEN_WIDTH = 64;
        internal const byte SCREEN_HEIGHT = 32;

        private const uint MEMORY_SIZE = 0x1000;
        private const byte REGISTER_COUNT = 0x10;
        private const byte STACK_SIZE = 0x10;
        private const uint MEMORY_ROM_OFFSET = 0x200;

        internal const byte OPCODE_SIZE = 0x2;

        private const int BATCH_FREQUENCY = 50;
        private const int INITIAL_CYCLE_FREQUENCY = 600;
        private const int INTERNAL_TIMER_FREQUENCY = 60;

        private const string SAVE_STATE_FILENAME = "SaveState.ss";

        private byte[] _memory;
        private uint _pc;
        private byte[] _v;
        private uint _i;
        private uint[] _stack;
        private byte _sp;
        private byte _delayTimer;
        private byte _soundTimer;
        private byte[] _screen;

        private Operations _operations;
        private Font _font;

        private Thread _coreThread;
        private System.Timers.Timer _internalTimer;
        private int _cycleFrequency;

        private readonly object _sync = new object();

        private bool _running;
        private bool _paused;

        private Func<byte> _getKeypress;
        private Action _playTone;
        private Action _stopTone;

        internal Core()
        {
            _memory = new byte[MEMORY_SIZE];
            _v = new byte[REGISTER_COUNT];
            _stack = new uint[STACK_SIZE];
            Screen = new byte[SCREEN_WIDTH * SCREEN_HEIGHT];

            _operations = new Operations(this);
            _font = new Font();

            _internalTimer = new System.Timers.Timer(Global.MILLISECONDS_PER_SECOND / INTERNAL_TIMER_FREQUENCY);
            _internalTimer.Elapsed += internalTimerClock;

            _cycleFrequency = INITIAL_CYCLE_FREQUENCY;

            Running = false;
            Paused = false;
        }

        #region Accessible Properties

        internal bool Running
        {
            get { lock (_sync) { return (_running); } }
            set { lock (_sync) { _running = value; } }
        }

        internal bool Paused
        {
            get { lock (_sync) { return (_paused); } }
            set { lock (_sync) { _paused = value; } }
        }

        internal byte[] Memory
        {
            get { return (_memory); }
            set { _memory = value; }
        }

        internal uint PC
        {
            get { return (_pc); }
            set { _pc = value; }
        }

        internal byte[] V
        {
            get { return (_v); }
            set { _v = value; }
        }

        internal uint I
        {
            get { return (_i); }
            set { _i = value; }
        }

        internal uint[] Stack
        {
            get { return (_stack); }
            set { _stack = value; }
        }

        internal byte SP
        {
            get { return (_sp); }
            set { _sp = value; }
        }

        internal byte[] Screen
        {
            get { lock (_sync) { return (_screen); } }
            set { lock (_sync) { _screen = value; } }
        }

        internal byte DelayTimer
        {
            get { lock (_sync) { return (_delayTimer); } }
            set { lock (_sync) { _delayTimer = value; } }
        }

        internal byte SoundTimer
        {
            get { lock (_sync) { return (_soundTimer); } }
            set { lock (_sync) { _soundTimer = value; } }
        }

        internal int CycleFrequency
        {
            get { lock (_sync) { return (_cycleFrequency); } }
            set { lock (_sync) { _cycleFrequency = value; } }
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

        #region Accessible Routines

        internal void Reset(string romFilename)
        {
            if ((_coreThread != null) && _coreThread.IsAlive)
            {
                _internalTimer.Stop();
                Running = false;

                while (_coreThread.IsAlive) ;
            }

            Array.Clear(_memory, 0, _memory.Length);
            Array.Clear(_v, 0, _v.Length);
            Array.Clear(_stack, 0, _stack.Length);
            Array.Clear(Screen, 0, Screen.Length);

            foreach (byte[] fontCharacter in _font.FontCharacters)
                Buffer.BlockCopy(fontCharacter, 0, _memory, (0x0000 + (_font.FontCharacters.IndexOf(fontCharacter) * Font.FONT_CHARACTER_SIZE)), Font.FONT_CHARACTER_SIZE);

            byte[] rom = File.ReadAllBytes(romFilename);
            Buffer.BlockCopy(rom, 0, _memory, (int)MEMORY_ROM_OFFSET, rom.Length);

            _i = 0;
            _pc = MEMORY_ROM_OFFSET;
            _sp = 0;
            DelayTimer = 0;
            SoundTimer = 0;

            Running = true;

            _coreThread = new Thread(loop);
            _coreThread.Start();

            _internalTimer.Start();
        }

        internal void Pause()
        {
            _internalTimer.Stop();
            Paused = true;
        }

        internal void Unpause()
        {
            Paused = false;
            _internalTimer.Start();
        }

        internal void Stop()
        {
            _internalTimer.Stop();
            Running = false;
        }

        internal void SaveState()
        {
            BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());

            binaryWriter.Write(_memory);
            binaryWriter.Write(_pc);
            binaryWriter.Write(_v);
            binaryWriter.Write(_i);
            binaryWriter.Write(_stack.SelectMany(BitConverter.GetBytes).ToArray());
            binaryWriter.Write(_sp);
            binaryWriter.Write(DelayTimer);
            binaryWriter.Write(SoundTimer);
            binaryWriter.Write(Screen);

            byte[] saveState = ((MemoryStream)binaryWriter.BaseStream).GetBuffer();

            FileStream fileStream = new FileStream(SAVE_STATE_FILENAME, FileMode.Create, FileAccess.Write);
            fileStream.Write(saveState, 0, saveState.Length);

            binaryWriter.Flush();
            binaryWriter.Close();

            fileStream.Flush();
            fileStream.Close();
        }

        internal void LoadState()
        {
            FileStream fileStream = new FileStream(SAVE_STATE_FILENAME, FileMode.Open, FileAccess.Read);
            byte[] saveState = new byte[fileStream.Length];
            fileStream.Read(saveState, 0, saveState.Length);
            fileStream.Close();

            BinaryReader binaryReader = new BinaryReader(new MemoryStream(saveState));

            _memory = binaryReader.ReadBytes((int)MEMORY_SIZE);
            _pc = binaryReader.ReadUInt32();
            _v = binaryReader.ReadBytes(REGISTER_COUNT);
            _i = binaryReader.ReadUInt32();
            for (int index = 0; index < STACK_SIZE; index++)
            {
                _stack[index] = BitConverter.ToUInt32(binaryReader.ReadBytes(sizeof(UInt32)), 0);
            }
            _sp = binaryReader.ReadByte();
            DelayTimer = binaryReader.ReadByte();
            SoundTimer = binaryReader.ReadByte();
            Screen = binaryReader.ReadBytes(SCREEN_WIDTH * SCREEN_HEIGHT);

            binaryReader.Close();
        }

        #endregion

        #region Main Loop Routines

        private void loop()
        {
            Stopwatch timingStopwatch = new Stopwatch();
            int cycleCount = 0;

            while (Running)
            {
                if (Paused)
                    Thread.Sleep(1);
                else
                {
                    if (!timingStopwatch.IsRunning)
                    {
                        cycleCount = 0;
                        timingStopwatch.Reset();
                        timingStopwatch.Start();
                    }

                    clock();

                    cycleCount++;

                    if (cycleCount >= (CycleFrequency / BATCH_FREQUENCY))
                    {
                        timingStopwatch.Stop();

                        if (timingStopwatch.ElapsedMilliseconds < (Global.MILLISECONDS_PER_SECOND / BATCH_FREQUENCY))
                            Thread.Sleep((int)((Global.MILLISECONDS_PER_SECOND / BATCH_FREQUENCY) - timingStopwatch.ElapsedMilliseconds));
                    }
                }
            }
        }

        private void clock()
        {
            uint opcode = (uint)(_memory[_pc] << 8) | _memory[_pc + 1];
            _pc += OPCODE_SIZE;

            _operations.ProcessOpcode(opcode);
        }

        private void internalTimerClock(object sender, ElapsedEventArgs e)
        {
            if (DelayTimer > 0)
                DelayTimer--;

            if (SoundTimer > 0)
            {
                SoundTimer--;

                if (SoundTimer == 0)
                    StopTone();
            }
        }

        #endregion
    }
}
