﻿using HaruhiChokuretsuLib.Archive.Event;
using HaruhiChokuretsuLib.Archive.Graphics;
using HaruhiChokuretsuLib.Util;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaruhiChokuretsuLib.Archive.Data
{
    /// <summary>
    /// A representation of CHRDATA.S in dat.bin
    /// </summary>
    public class CharacterDataFile : DataFile
    {
        /// <summary>
        /// The list of character sprites contained in the character data file
        /// </summary>
        public List<CharacterSprite> Sprites { get; set; } = [];

        public override void Initialize(byte[] decompressedData, int offset, ILogger log)
        {
            _log = log;

            int numSections = IO.ReadInt(decompressedData, 0);
            if (numSections != 1)
            {
                _log.LogError($"Character data file should only have 1 section; {numSections} specified");
                return;
            }
            int sectionStart = IO.ReadInt(decompressedData, 0x0C);
            int sectionCount = IO.ReadInt(decompressedData, 0x10);

            for (int i = 0; i < sectionCount; i++)
            {
                Sprites.Add(new(decompressedData.Skip(sectionStart + 0x18 * i).Take(0x18)));
            }
        }

        public override string GetSource(Dictionary<string, IncludeEntry[]> includes)
        {
            if (!includes.ContainsKey("GRPBIN"))
            {
                _log.LogError("Includes needs GRPBIN to be present.");
                return null;
            }

            StringBuilder sb = new();

            sb.AppendLine(".include \"GRPBIN.INC\"");
            sb.AppendLine();
            sb.AppendLine($".word 1");
            sb.AppendLine(".word END_POINTERS");
            sb.AppendLine(".word FILE_START");
            sb.AppendLine(".word SPRITE_LIST");
            sb.AppendLine($".word {Sprites.Count}");

            sb.AppendLine();
            sb.AppendLine("FILE_START:");
            sb.AppendLine("SPRITE_LIST:");

            foreach (CharacterSprite sprite in Sprites)
            {
                sb.AppendLine($".short {sprite.Unknown00}");
                sb.AppendLine($".short {(sprite.IsLarge ? 1 : 0)}");
                sb.AppendLine($".short {(short)sprite.Character}");
                sb.AppendLine($".short {(sprite.TextureIndex1 > 0 ? includes["GRPBIN"].First(inc => inc.Value == sprite.TextureIndex1).Name : 0)}");
                sb.AppendLine($".short {(sprite.TextureIndex2 > 0 ? includes["GRPBIN"].First(inc => inc.Value == sprite.TextureIndex2).Name : 0)}");
                sb.AppendLine($".short {(sprite.LayoutIndex > 0 ? includes["GRPBIN"].First(inc => inc.Value == sprite.LayoutIndex).Name : 0)}");
                sb.AppendLine($".short {(sprite.TextureIndex3 > 0 ? includes["GRPBIN"].First(inc => inc.Value == sprite.TextureIndex3).Name : 0)}");
                sb.AppendLine($".short {sprite.Padding}");
                sb.AppendLine($".short {(sprite.EyeTextureIndex > 0 ? includes["GRPBIN"].First(inc => inc.Value == sprite.EyeTextureIndex).Name : 0)}");
                sb.AppendLine($".short {(sprite.MouthTextureIndex > 0 ? includes["GRPBIN"].First(inc => inc.Value == sprite.MouthTextureIndex).Name : 0)}");
                sb.AppendLine($".short {(sprite.EyeAnimationIndex > 0 ? includes["GRPBIN"].First(inc => inc.Value == sprite.EyeAnimationIndex).Name : 0)}");
                sb.AppendLine($".short {(sprite.MouthAnimationIndex > 0 ? includes["GRPBIN"].First(inc => inc.Value == sprite.MouthAnimationIndex).Name : 0)}");
                sb.AppendLine();
            }

            sb.AppendLine("END_POINTERS:");
            sb.AppendLine(".word 0");

            return sb.ToString();
        }
    }

    /// <summary>
    /// A representation of a character sprite as displayed on screen during Chokuretsu's VN sections;
    /// defined in CHRDATA.S
    /// </summary>
    public class CharacterSprite(IEnumerable<byte> data)
    {
        public short Unknown00 { get; set; } = IO.ReadShort(data, 0);
        /// <summary>
        /// Is true if the sprite is large
        /// </summary>
        public bool IsLarge { get; set; } = IO.ReadShort(data, 0x02) == 1;
        /// <summary>
        /// The character depicted in the sprite (defined with the same Speaker value used in scripts)
        /// </summary>
        public Speaker Character { get; set; } = (Speaker)IO.ReadShort(data, 0x04);
        /// <summary>
        /// The grp.bin index of the first texture used in the sprite layout
        /// </summary>
        public short TextureIndex1 { get; set; } = IO.ReadShort(data, 0x06);
        /// <summary>
        /// The grp.bin index of the second texture used in the sprite layout
        /// </summary>
        public short TextureIndex2 { get; set; } = IO.ReadShort(data, 0x08);
        /// <summary>
        /// The grp.bin index of the sprite layout
        /// </summary>
        public short LayoutIndex { get; set; } = IO.ReadShort(data, 0x0A);
        /// <summary>
        /// The grp.bin index of the third texture used in the sprite layout
        /// </summary>
        public short TextureIndex3 { get; set; } = IO.ReadShort(data, 0x0C);
        /// <summary>
        /// Unused
        /// </summary>
        public short Padding { get; set; } = IO.ReadShort(data, 0x0E);
        /// <summary>
        /// The grp.bin index of the eye texture
        /// </summary>
        public short EyeTextureIndex { get; set; } = IO.ReadShort(data, 0x10);
        /// <summary>
        /// The grp.bin index of the mouth texture
        /// </summary>
        public short MouthTextureIndex { get; set; } = IO.ReadShort(data, 0x12);
        /// <summary>
        /// The grp.bin index of the eye animation file
        /// </summary>
        public short EyeAnimationIndex { get; set; } = IO.ReadShort(data, 0x14);
        /// <summary>
        /// The grp.bin index of the mouth animation file
        /// </summary>
        public short MouthAnimationIndex { get; set; } = IO.ReadShort(data, 0x16);

        /// <summary>
        /// Gets the animation of the sprite blinking without lip flap animation
        /// </summary>
        /// <param name="grp">The grp.bin ArchiveFile object</param>
        /// <param name="messageInfoFile">The MessageInfo file from dat.bin</param>
        /// <returns>A list of tuples containing SKBitmap frames and timings for how long those frames are to be displayed</returns>
        public List<(SKBitmap frame, int timing)> GetClosedMouthAnimation(ArchiveFile<GraphicsFile> grp, MessageInfoFile messageInfoFile)
        {
            return GetAnimation(grp, messageInfoFile, false);
        }

        /// <summary>
        /// Gets the animation of the sprite blinking and moving its lips
        /// </summary>
        /// <param name="grp">The grp.bin ArchiveFile object</param>
        /// <param name="messageInfoFile">The MessageInfo file from dat.bin</param>
        /// <returns>A list of tuples containing SKBitmap frames and timings for how long those frames are to be displayed</returns>
        public List<(SKBitmap frame, int timing)> GetLipFlapAnimation(ArchiveFile<GraphicsFile> grp, MessageInfoFile messageInfoFile)
        {
            return GetAnimation(grp, messageInfoFile, true);
        }

        private List<(SKBitmap frame, int timing)> GetAnimation(ArchiveFile<GraphicsFile> grp, MessageInfoFile messageInfoFile, bool lipFlap)
        {
            List<(SKBitmap, int)> frames = [];

            if (Unknown00 == 0)
            {
                return frames;
            }

            List<GraphicsFile> textures = [grp.Files.First(f => f.Index == TextureIndex1), grp.Files.First(f => f.Index == TextureIndex2), grp.Files.First(f => f.Index == TextureIndex3)];
            GraphicsFile layout = grp.Files.First(f => f.Index == LayoutIndex);
            GraphicsFile eyeTexture = grp.Files.First(f => f.Index == EyeTextureIndex);
            GraphicsFile eyeAnimation = grp.Files.First(f => f.Index == EyeAnimationIndex);
            GraphicsFile mouthTexture = grp.Files.First(f => f.Index == MouthTextureIndex);
            GraphicsFile mouthAnimation = grp.Files.First(f => f.Index == MouthAnimationIndex);
            MessageInfo messageInfo = messageInfoFile.MessageInfos[(int)Character];

            (SKBitmap spriteBitmap, _) = layout.GetLayout(textures, 0, layout.LayoutEntries.Count, darkMode: false, preprocessedList: true);
            SKBitmap[] eyeFrames = eyeAnimation.GetAnimationFrames(eyeTexture).Select(f => f.GetImage()).ToArray();
            SKBitmap[] mouthFrames = mouthAnimation.GetAnimationFrames(mouthTexture).Select(f => f.GetImage()).ToArray();

            int e = 0, m = 0;
            int currentEyeTime = ((FrameAnimationEntry)eyeAnimation.AnimationEntries[e]).Time;
            int currentMouthTime = messageInfo.TextTimer * 30;
            for (int f = 0; f < Math.Max(eyeAnimation.AnimationEntries.Sum(a => ((FrameAnimationEntry)a).Time), 
                mouthAnimation.AnimationEntries.Sum(a => ((FrameAnimationEntry)a).Time));)
            {
                SKBitmap frame = new(spriteBitmap.Width, spriteBitmap.Height);
                SKCanvas canvas = new(frame);
                canvas.DrawBitmap(spriteBitmap, new SKPoint(0, 0));
                canvas.DrawBitmap(eyeFrames[e], new SKPoint(eyeAnimation.AnimationX, eyeAnimation.AnimationY));
                if (lipFlap)
                {
                    canvas.DrawBitmap(mouthFrames[m], new SKPoint(mouthAnimation.AnimationX, mouthAnimation.AnimationY));
                }
                else
                {
                    canvas.DrawBitmap(mouthFrames[0], new SKPoint(mouthAnimation.AnimationX, mouthAnimation.AnimationY));
                }
                int time;

                if (currentEyeTime == currentMouthTime)
                {
                    f += currentEyeTime;
                    time = currentEyeTime;
                    e++;
                    m++;
                    if (e >= eyeAnimation.AnimationEntries.Count)
                    {
                        e = 0;
                    }
                    if (m >= mouthAnimation.AnimationEntries.Count)
                    {
                        m = 0;
                    }

                    currentEyeTime = ((FrameAnimationEntry)eyeAnimation.AnimationEntries[e]).Time;
                    currentMouthTime = messageInfo.TextTimer * 30;
                }
                else if (currentEyeTime < currentMouthTime)
                {
                    f += currentEyeTime;
                    time = currentEyeTime;
                    currentMouthTime -= currentEyeTime;
                    e++;
                    if (e >= eyeAnimation.AnimationEntries.Count)
                    {
                        e = 0;
                    }
                    currentEyeTime = ((FrameAnimationEntry)eyeAnimation.AnimationEntries[e]).Time;
                }
                else
                {
                    f += currentMouthTime;
                    time = currentMouthTime;
                    currentEyeTime -= currentMouthTime;
                    m++;
                    if (m >= mouthAnimation.AnimationEntries.Count)
                    {
                        m = 0;
                    }
                    currentMouthTime = messageInfo.TextTimer;
                }

                canvas.Flush();
                frames.Add((frame, time));
            }

            return frames;
        }
    }
}
