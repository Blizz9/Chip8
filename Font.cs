using System.Collections.Generic;

namespace Chip8
{
    internal class Font
    {
        internal const byte FONT_CHARACTER_SIZE = 0x5;

        private List<byte[]> _fontCharacters;

        internal Font()
        {
            _fontCharacters = new List<byte[]>();

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x90, // 1001
                0x90, // 1001
                0x90, // 1001
                0xF0  // 1111
            }); // 0

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0x20, // 0010
                0x60, // 0110
                0x20, // 0010
                0x20, // 0010
                0x70  // 0111
            }); // 1

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x10, // 0001
                0xF0, // 1111
                0x80, // 1000
                0xF0  // 1111
            }); // 2

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x10, // 0001
                0xF0, // 1111
                0x10, // 0001
                0xF0  // 1111
            }); // 3

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0x90, // 1001
                0x90, // 1001
                0xF0, // 1111
                0x10, // 0001
                0x10  // 0001
            }); // 4

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0xF0, // 1111
                0x10, // 0001
                0xF0  // 1111
            }); // 5

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0xF0, // 1111
                0x90, // 1001
                0xF0  // 1111
            }); // 6

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x10, // 0001
                0x20, // 0010
                0x40, // 0100
                0x40  // 0100
            }); // 7

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x90, // 1001
                0xF0, // 1111
                0x90, // 1001
                0xF0  // 1111
            }); // 8

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x90, // 1001
                0xF0, // 1111
                0x10, // 0001
                0xF0  // 1111
            }); // 9

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x90, // 1001
                0xF0, // 1111
                0x90, // 1001
                0x90  // 1001
            }); // A

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xE0, // 1110
                0x90, // 1001
                0xE0, // 1110
                0x90, // 1001
                0xE0  // 1110
            }); // B

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0x80, // 1000
                0x80, // 1000
                0xF0  // 1111
            }); // C

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xE0, // 1110
                0x90, // 1001
                0x90, // 1001
                0x90, // 1001
                0xE0  // 1110
            }); // D

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0xF0, // 1111
                0x80, // 1000
                0xF0  // 1111
            }); // E

            _fontCharacters.Add(new byte[FONT_CHARACTER_SIZE]
            {
                0xF0, // 1111
                0x80, // 1000
                0xF0, // 1111
                0x80, // 1000
                0x80  // 1000
            }); // F
        }

        internal List<byte[]> FontCharacters
        {
            get { return (_fontCharacters); }

        }
    }
}
