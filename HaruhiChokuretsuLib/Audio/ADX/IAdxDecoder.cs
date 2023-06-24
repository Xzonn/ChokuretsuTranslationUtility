﻿namespace HaruhiChokuretsuLib.Audio.ADX
{
    public interface IAdxDecoder
    {
        public uint Channels { get; }
        public uint SampleRate { get; }
        public LoopInfo LoopInfo { get; }
        public Sample NextSample();
    }
}