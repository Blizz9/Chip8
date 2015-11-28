using System;
using System.Collections.Generic;
using System.IO;

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

        private byte[] _memory;
        private byte[] _v; // registers
        private uint _i; // index register
        private uint _pc; // program counter
        private uint[] _stack;
        private byte _sp; // stack pointer
        private byte[] _screen;
        private byte _delayTimer;
        private byte _soundTimer;

        private Dictionary<byte, Action<uint>> _opcodeMap;

        internal Engine()
        {
            _memory = new byte[MEMORY_SIZE];
            _v = new byte[REGISTER_COUNT];
            _stack = new uint[STACK_SIZE];
            _screen = new byte[(SCREEN_WIDTH / BITS_IN_BYTE) * (SCREEN_HEIGHT / BITS_IN_BYTE)];

            byte[] rom = File.ReadAllBytes("game.ch8");
            Buffer.BlockCopy(rom, 0, _memory, (int)MEMORY_ROM_OFFSET, rom.Length);

            _i = 0;
            _pc = MEMORY_ROM_OFFSET;
            _sp = 0;
            _delayTimer = 0;
            _soundTimer = 0;

            _opcodeMap = new Dictionary<byte, Action<uint>>();
            _opcodeMap.Add(0x6, loadRegister);
            _opcodeMap.Add(0xA, loadIRegister);
        }

        internal void Clock()
        {
            uint opcode = (uint)(_memory[_pc] << 8) | _memory[_pc + 1];

            byte opcodeMSN = (byte)((opcode & 0xF000) >> 12);
            _opcodeMap[opcodeMSN](opcode);

            _pc += 2;
        }

        #region Operations

        // 6XNN
        private void loadRegister(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            _v[register] = value;
        }

        // ANNN
        private void loadIRegister(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _i = address;
        }

        #endregion
    }
}
