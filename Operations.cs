using System;
using System.Collections.Generic;

namespace Chip8
{
    internal class Operations
    {
        private Core _core;

        private Dictionary<byte, Action<uint>> _opcodeMap;
        private Dictionary<byte, Action<uint>> _opcodeMap00EX;
        private Dictionary<byte, Action<uint>> _opcodeMap8XXX;
        private Dictionary<byte, Action<uint>> _opcodeMapEXXX;
        private Dictionary<byte, Action<uint>> _opcodeMapFXXX;

        internal Operations(Core core)
        {
            _core = core;

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
        }

        internal void ProcessOpcode(uint opcode)
        {
            byte opcodeMSN = (byte)((opcode & 0xF000) >> 12);
            _opcodeMap[opcodeMSN](opcode);
        }

        #region Mapping

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
            Array.Clear(_core.Screen, 0, _core.Screen.Length);
        }

        // 00EE
        private void returnSubroutine(uint opcode)
        {
            _core.SP--;
            _core.PC = _core.Stack[_core.SP];
        }

        #endregion

        // 1NNN
        private void jump(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _core.PC = address;
        }

        // 2NNN
        private void callSubroutine(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _core.Stack[_core.SP] = _core.PC;
            _core.SP++;
            _core.PC = address;
        }

        // 3XNN
        private void jumpIfEqualTo(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            if (_core.V[register] == value)
                _core.PC += Core.OPCODE_SIZE;
        }

        // 4XNN
        private void jumpIfNotEqualTo(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            if (_core.V[register] != value)
                _core.PC += Core.OPCODE_SIZE;
        }

        // 5XY0
        private void jumpIfEqual(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            if (_core.V[registerX] == _core.V[registerY])
                _core.PC += Core.OPCODE_SIZE; ;
        }

        // 6XNN
        private void load(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            _core.V[register] = value;
        }

        // 7XNN
        private void appendValue(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);
            _core.V[register] += value;
        }

        #region 8XXX Operations

        // 8XY0
        private void copy(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            _core.V[registerX] = _core.V[registerY];
        }

        // 8XY1
        private void or(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            _core.V[registerX] = (byte)(_core.V[registerX] | _core.V[registerY]);
        }

        // 8XY2
        private void and(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            _core.V[registerX] = (byte)(_core.V[registerX] & _core.V[registerY]);
        }

        // 8XY3
        private void xor(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            _core.V[registerX] = (byte)(_core.V[registerX] ^ _core.V[registerY]);
        }

        // 8XY4
        private void add(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);

            int result = _core.V[registerX] + _core.V[registerY];
            _core.V[0xF] = result > byte.MaxValue ? (byte)1 : (byte)0;
            _core.V[registerX] = (byte)result;
        }

        // 8XY5
        private void subtract(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);

            _core.V[0xF] = _core.V[registerX] > _core.V[registerY] ? (byte)1 : (byte)0;
            _core.V[registerX] = (byte)(_core.V[registerX] - _core.V[registerY]);
        }

        // 8XY6
        private void shiftRight(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            _core.V[0xF] = (byte)(_core.V[registerX] & 0x01);
            _core.V[registerX] = (byte)(_core.V[registerX] >> 1);
        }

        // 8XY7
        private void subtractReverse(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);

            _core.V[0xF] = _core.V[registerY] > _core.V[registerX] ? (byte)1 : (byte)0;
            _core.V[registerX] = (byte)(_core.V[registerY] - _core.V[registerX]);
        }

        // 8XYE
        private void shiftLeft(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            _core.V[0xF] = (byte)((_core.V[registerX] & 0x80) >> 7);
            _core.V[registerX] = (byte)(_core.V[registerX] << 1);
        }

        #endregion

        // 9XY0
        private void jumpIfNotEqual(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            byte registerY = (byte)((opcode & 0x00F0) >> 4);
            if (_core.V[registerX] != _core.V[registerY])
                _core.PC += Core.OPCODE_SIZE;
        }

        // ANNN
        private void loadIndex(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _core.I = address;
        }

        // BNNN
        private void jumpWithOffset(uint opcode)
        {
            uint address = opcode & 0x0FFF;
            _core.PC = address + _core.V[0x0];
        }

        // CXNN
        private void random(uint opcode)
        {
            byte register = (byte)((opcode & 0x0F00) >> 8);
            byte value = (byte)(opcode & 0x00FF);

            Random random = new Random();
            byte randomValue = (byte)random.Next(256);

            _core.V[register] = (byte)(randomValue & value);
        }

        // DXYN
        private void drawSprite(uint opcode)
        {
            byte xRegister = (byte)((opcode & 0x0F00) >> 8);
            byte yRegister = (byte)((opcode & 0x00F0) >> 4);
            byte numberOfSpriteLines = (byte)(opcode & 0x000F);

            byte xLocation = (byte)(_core.V[xRegister] & 0x3F);
            byte yLocation = (byte)(_core.V[yRegister] & 0x1F);

            byte[] sprite = new byte[numberOfSpriteLines];
            Buffer.BlockCopy(_core.Memory, (int)_core.I, sprite, 0, numberOfSpriteLines);

            bool collision = false;

            for (int spriteLineIndex = 0; spriteLineIndex < numberOfSpriteLines; spriteLineIndex++)
            {
                byte pixelMask = 0x80;

                while (pixelMask != 0x00)
                {
                    if ((sprite[spriteLineIndex] & pixelMask) != 0)
                    {
                        if (_core.Screen[(yLocation * Core.SCREEN_WIDTH) + xLocation] == 0)
                            _core.Screen[(yLocation * Core.SCREEN_WIDTH) + xLocation] = 1;
                        else
                        {
                            _core.Screen[(yLocation * Core.SCREEN_WIDTH) + xLocation] = 0;
                            collision = true;
                        }
                    }

                    pixelMask = (byte)(pixelMask >> 1);
                    xLocation++;

                    if (xLocation >= Core.SCREEN_WIDTH)
                        break;
                }

                xLocation = _core.V[xRegister];
                yLocation++;

                if (yLocation >= Core.SCREEN_HEIGHT)
                    break;
            }

            _core.V[0xF] = collision ? (byte)1 : (byte)0;
        }

        #region EXXX Operations

        // EX9E
        private void jumpIfKeyPressed(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            byte keypress = _core.GetKeypress();

            if (_core.V[registerX] == keypress)
                _core.PC += Core.OPCODE_SIZE;
        }

        // EXA1
        private void jumpIfKeyNotPressed(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            byte keypress = _core.GetKeypress();

            if (_core.V[registerX] != keypress)
                _core.PC += Core.OPCODE_SIZE;
        }

        #endregion

        #region FXXX Operations

        // FX07
        private void readDelayTimer(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            _core.V[registerX] = _core.DelayTimer;
        }

        // FX0A
        private void waitForKeypress(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            byte keypress = 0xFF;

            while ((keypress == 0xFF) && _core.Running)
                keypress = _core.GetKeypress();

            _core.V[registerX] = keypress;
        }

        // FX15
        private void loadDelayTimer(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            _core.DelayTimer = _core.V[registerX];
        }

        // FX18
        private void loadSoundTimer(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            _core.SoundTimer = _core.V[registerX];

            if (_core.SoundTimer > 0)
                _core.PlayTone();
        }

        // FX1E
        private void addToIndex(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            _core.I += _core.V[registerX];
        }

        // FX29
        private void addressFontCharacter(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);
            _core.I = (byte)(_core.V[registerX] * Font.FONT_CHARACTER_SIZE);
        }

        // FX33
        private void storeBCD(uint opcode)
        {
            byte registerX = (byte)((opcode & 0x0F00) >> 8);

            _core.Memory[_core.I] = (byte)((_core.V[registerX] / 100) % 10);
            _core.Memory[_core.I + 1] = (byte)((_core.V[registerX] / 10) % 10);
            _core.Memory[_core.I + 2] = (byte)(_core.V[registerX] % 10);
        }

        // FX55
        private void dumpRegisters(uint opcode)
        {
            byte endRegister = (byte)((opcode & 0x0F00) >> 8);

            for (int register = 0; register <= endRegister; register++)
                _core.Memory[_core.I + register] = _core.V[register];
        }

        // FX65
        private void fillRegisters(uint opcode)
        {
            byte endRegister = (byte)((opcode & 0x0F00) >> 8);

            for (int register = 0; register <= endRegister; register++)
                _core.V[register] = _core.Memory[_core.I + register];
        }

        #endregion

        #endregion
    }
}
