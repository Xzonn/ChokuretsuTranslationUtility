﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HaruhiChokuretsuLib.Archive.Data
{
    public class PuzzleMap : DataFile
    {
        public int NumSections { get; set; }
        public int EndPointersOffset { get; set; }
        public int HeaderEndPointer { get; set; }
        public List<(int Offset, int ItemCount)> SectionOffsetsAndCounts { get; set; } = new();

        public PuzzleMapSettings Settings { get; set; }

        public override void Initialize(byte[] decompressedData, int offset)
        {
            Offset = offset;
            Data = decompressedData.ToList();

            NumSections = BitConverter.ToInt32(Data.Take(4).ToArray());
            EndPointersOffset = BitConverter.ToInt32(Data.Skip(0x04).Take(4).ToArray());
            HeaderEndPointer = BitConverter.ToInt32(Data.Skip(0x08).Take(4).ToArray());
            for (int i = 0x0C; i < HeaderEndPointer; i += 0x08)
            {
                SectionOffsetsAndCounts.Add((BitConverter.ToInt32(Data.Skip(i).Take(4).ToArray()), BitConverter.ToInt32(Data.Skip(i + 4).Take(4).ToArray())));
            }

            Settings = new(Data.Skip(SectionOffsetsAndCounts[0].Offset).Take(0x48));
        }
    }

    public class PuzzleMapSettings
    {
        public int MapId { get; set; }
        public int BaseTime { get; set; }
        public int NumSingularities { get; set; }
        public int Unknown04 { get; set; }
        public int TargetNumber { get; set; }
        public bool ContinueOnFailure { get; set; }
        public string AccompanyingCharacter { get; set; }
        public string PowerCharacter1 { get; set; }
        public string PowerCharacter2 { get; set; }
        public int SingularityGraphic { get; set; }
        public int SingularityLayout { get; set; }
        public int SingularityAnim1 { get; set; }
        public int SingularityAnim2 { get; set; }
        public int TopicSet { get; set; }
        public int Unknown15 { get; set; }
        public int Unknown16 { get; set; }
        public int Unknown17 { get; set; }
        public int Unknown18 { get; set; }

        public PuzzleMapSettings(IEnumerable<byte> data)
        {
            MapId = BitConverter.ToInt32(data.Take(4).ToArray());
            BaseTime = BitConverter.ToInt32(data.Skip(0x04).Take(4).ToArray());
            NumSingularities = BitConverter.ToInt32(data.Skip(0x08).Take(4).ToArray());
            Unknown04 = BitConverter.ToInt32(data.Skip(0x0C).Take(4).ToArray());
            TargetNumber = BitConverter.ToInt32(data.Skip(0x10).Take(4).ToArray());
            ContinueOnFailure = BitConverter.ToInt32(data.Skip(0x14).Take(4).ToArray()) > 0;
            AccompanyingCharacter = CharacterSwitch(BitConverter.ToInt32(data.Skip(0x18).Take(4).ToArray()));
            PowerCharacter1 = CharacterSwitch(BitConverter.ToInt32(data.Skip(0x1C).Take(4).ToArray()));
            PowerCharacter2 = CharacterSwitch(BitConverter.ToInt32(data.Skip(0x20).Take(4).ToArray()));
            SingularityGraphic = BitConverter.ToInt32(data.Skip(0x24).Take(4).ToArray());
            SingularityLayout = BitConverter.ToInt32(data.Skip(0x28).Take(4).ToArray());
            SingularityAnim1 = BitConverter.ToInt32(data.Skip(0x2C).Take(4).ToArray());
            SingularityAnim2 = BitConverter.ToInt32(data.Skip(0x30).Take(4).ToArray());
            TopicSet = BitConverter.ToInt32(data.Skip(0x34).Take(4).ToArray());
            Unknown15 = BitConverter.ToInt32(data.Skip(0x38).Take(4).ToArray());
            Unknown16 = BitConverter.ToInt32(data.Skip(0x3C).Take(4).ToArray());
            Unknown17 = BitConverter.ToInt32(data.Skip(0x40).Take(4).ToArray());
            Unknown18 = BitConverter.ToInt32(data.Skip(0x44).Take(4).ToArray());
        }

        public string GetMapName(List<byte> qmapData)
        {
            return Encoding.ASCII.GetString(
                qmapData.Skip(BitConverter.ToInt32(qmapData.Skip(0x14 + MapId * 8).Take(4).ToArray()))
                .TakeWhile(b => b != 0).ToArray());
        }

        private static string CharacterSwitch(int characterCode)
        {
            switch (characterCode)
            {
                default:
                    return $"UNKNOWN ({characterCode})";
                case 1:
                    return "KYON";
                case 2:
                    return "HARUHI";
                case 3:
                    return "MIKURU";
                case 4:
                    return "NAGATO";
                case 5:
                    return "KOIZUMI";
                case 22:
                    return "ANY";
            }
        }
    }
}
