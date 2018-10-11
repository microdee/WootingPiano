using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Commons.Music.Midi;
using static System.Math;

namespace WootingMidi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RawMidiMessage
    {
        public byte Pre;
        public byte Id;
        public byte Param;
        public bool Valid;

        public void Bytes(byte[] buf, int offs)
        {
            buf[offs + 0] = Pre;
            buf[offs + 1] = Id;
            buf[offs + 2] = Param;
        }
        public static RawMidiMessage None { get; } = new RawMidiMessage {Valid = false};
    }

    public static class MidiCc
    {
        public const byte CcPrefix = 0b1011_0000;

        public static RawMidiMessage Generate(int chan, int cc, double val)
        {
            var pre = (byte)(CcPrefix | ((byte)chan & 0x0F));
            var hi = (byte)((byte)cc & 0b01111111);
            var lo = (byte)((byte)(val * 127.0) & 0b01111111);
            return Settings.Last.SendCc ? new RawMidiMessage
            {
                Pre = pre,
                Id = hi,
                Param = lo,
                Valid = true
            } : RawMidiMessage.None;
        }
    }

    public static class MidiChannelAt
    {
        public const byte AtPrefix = 0b1101_0000;

        public static RawMidiMessage Generate(int chan, double val)
        {
            var pre = (byte)(AtPrefix | ((byte)chan & 0x0F));
            var lo = (byte)((byte)(val * 127.0) & 0b01111111);
            return Settings.Last.SendCc ? new RawMidiMessage
            {
                Pre = pre,
                Id = lo,
                Param = lo,
                Valid = true
            } : RawMidiMessage.None;
        }
    }

    public class NoteKey
    {
        public const byte NoteOnPrefix = 0b1001_0000;
        public const byte AftertouchPrefix = 0b1010_0000;
        public const byte NoteOffPrefix = 0b1000_0000;

        public uint NoteId { get; set; }
        public uint Channel { get; set; }

        public double Threshold { get; set; } = 0.1;
        public double Velocity { get; private set; } = 0.0;
        public double Pressure { get; set; }

        public bool NoteOn { get; private set; }
        public bool Aftertouching { get; private set; }
        public bool NoteOff { get; private set; }

        public RawMidiMessage MidiNoteOn()
        {
            var pre = (byte)(NoteOnPrefix | (Channel & 0x0F));
            var hi = (byte)(NoteId & 0b01111111);
            var lo = (byte)((uint)(Velocity * 127.0) & 0b01111111);
            return new RawMidiMessage
            {
                Pre = pre,
                Id = hi,
                Param = lo,
                Valid = true
            };
        }

        public RawMidiMessage MidiAftertouch()
        {
            var pre = (byte)(AftertouchPrefix | (Channel & 0x0F));
            var hi = (byte)(NoteId & 0b01111111);
            var lo = (byte)((uint)(Pressure * 127.0) & 0b01111111);
            return new RawMidiMessage
            {
                Pre = pre,
                Id = hi,
                Param = lo,
                Valid = true
            };
        }

        public RawMidiMessage MidiNoteOff()
        {
            var pre = (byte)(NoteOffPrefix | (Channel & 0x0F));
            var hi = (byte)(NoteId & 0b01111111);
            return new RawMidiMessage
            {
                Pre = pre,
                Id = hi,
                Param = 0,
                Valid = true
            };
        }

        public RawMidiMessage Submit(double currval)
        {
            Velocity = Min(Max(Max(0, currval - Pressure) * 2, Velocity * 0.9), 1);
            Pressure = currval;

            if (NoteOff)
            {
                NoteOff = false;
                return RawMidiMessage.None;
            }
            if (Aftertouching)
            {
                if (currval == 0.0)
                {
                    Aftertouching = false;
                    NoteOff = true;
                    return Settings.Last.SendNote ? MidiNoteOff() : RawMidiMessage.None;
                }
                else
                {
                    return Settings.Last.SendAt ? MidiAftertouch() : RawMidiMessage.None;
                }
            }
            if (NoteOn)
            {
                NoteOn = false;
                Aftertouching = true;
                return Settings.Last.SendAt ? MidiAftertouch() : RawMidiMessage.None;
            }
            if (currval > Threshold)
            {
                NoteOn = true;
                return Settings.Last.SendNote ? MidiNoteOn() : RawMidiMessage.None;
            }
            return RawMidiMessage.None;
        }
    }
}
