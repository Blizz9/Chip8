using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Chip8
{
    internal class Engine
    {
        private const uint MEMORY_SIZE = 0x1000;
        private const byte REGISTER_COUNT = 0x10;
        private const byte STACK_SIZE = 0x10;
        private const byte SCREEN_WIDTH = 64;
        private const byte SCREEN_HEIGHT = 32;
        private const byte BITS_IN_BYTE = 8;
        private const uint MEMORY_ROM_OFFSET = 0x200;
        private const byte OPCODE_SIZE = 2;

        private byte[] _memory;
        private byte[] _v; // registers
        private uint _i; // index registers
        private uint _pc; // program counter
        private uint[] _stack;
        private byte _sp; // stack pointer
        private byte[] _screen;
        private byte _delayTimer;
        private byte _soundTimer;

        private Dictionary<byte, Action<uint>> _opcodeMap;
        private Dictionary<byte, Action<uint>> _opcodeMap8XXX;

        private object _sync = new object();
        private bool _running;
        private bool _paused;

        internal Engine()
        {
            _memory = new byte[MEMORY_SIZE];
            _v = new byte[REGISTER_COUNT];
            _stack = new uint[STACK_SIZE];
            _screen = new byte[(SCREEN_WIDTH / BITS_IN_BYTE) * (SCREEN_HEIGHT / BITS_IN_BYTE)];

            _opcodeMap = new Dictionary<byte, Action<uint>>();
            _opcodeMap.Add(0x1, jump);
            _opcodeMap.Add(0x3, jumpIfEqual);
            _opcodeMap.Add(0x6, loadRegister);
            _opcodeMap.Add(0x7, addValueToRegister);
            _opcodeMap.Add(0x8, handle8XXXOperations);
            _opcodeMap.Add(0xA, loadIRegister);
            _opcodeMap.Add(0xD, drawSprite);

            _opcodeMap8XXX = new Dictionary<byte, Action<uint>>();
            _opcodeMap8XXX.Add(0x2, andRegisters);

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

        #endregion

        #region Accessible Methods

        internal void Initialize()
        {
            Array.Clear(_memory, 0, _memory.Length);
            Array.Clear(_v, 0, _v.Length);
            Array.Clear(_stack, 0, _stack.Length);
            Array.Clear(_screen, 0, _screen.Length);

            byte[] rom = File.ReadAllBytes("game.ch8");
            Buffer.BlockCopy(rom, 0, _memory, (int)MEMORY_ROM_OFFSET, rom.Length);

            _i = 0;
            _pc = MEMORY_ROM_OFFSET;
            _sp = 0;
            _delayTimer = 0;
            _soundTimer = 0;
        }

        internal void Start()
        {
            running = true;

            Thread engineThread = new Thread(loop);
            engineThread.Start();
        }

        internal void Stop()
        {
            running = false;
        }

        #endregion

        #region Main Loop Methods

        private void loop()
        {
            while (running)
            {
                if (paused)
                    Thread.Sleep(10);
                else
                    clock();
            }
        }

        private void clock()
        {
            uint opcode = (uint)(_memory[_pc] << 8) | _memory[_pc + 1];
            _pc += OPCODE_SIZE;

            byte opcodeMSN = (byte)((opcode & 0xF000) >> 12);

            if (!_opcodeMap.ContainsKey(opcodeMSN))
                Debug.WriteLine("Not Implemented");
            else
                _opcodeMap[opcodeMSN](opcode);
        }

        #endregion

        #region Operations Mapping

        private void handle8XXXOperations(uint opcode)
        {
            byte opcodeLSN = (byte)(opcode & 0x000F);

            if (!_opcodeMap8XXX.ContainsKey(opcodeLSN))
                Debug.WriteLine("Not Implemented");
            else
                _opcodeMap8XXX[opcodeLSN](opcode);
        }

        #endregion

        #region Operations

        // 1NNN
        private void jump(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _pc = address;
        }

        // 3XNN
        private void jumpIfEqual(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            if (_v[register] == value)
                _pc += OPCODE_SIZE;
        }

        // 6XNN
        private void loadRegister(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            _v[register] = value;
        }

        // 7XNN
        private void addValueToRegister(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            _v[register] += value;
        }

        // ANNN
        private void loadIRegister(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _i = address;
        }

        // DXYN
        private void drawSprite(uint opcode)
        {
            // need to implement
        }

        #region 8XXX Operations

        // 8XY2
        private void andRegisters(uint opcode)
        {

        }

        #endregion

        #endregion
    }
}
