﻿using HaruhiChokuretsuLib.Archive;
using Mono.Options;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaruhiChokuretsuCLI
{
    public class ExportLayoutCommand : Command
    {
        private string _grp, _layoutName, _outputFile;
        private int _layoutIndex, _layoutStart, _layoutEnd;
        private int[] _indices;
        private string[] _names;
        public ExportLayoutCommand() : base("export-layout", "Exports a layout given a series of texture files")
        {
            Options = new()
            {
                { "g|grp=", "Input grp.bin", g => _grp = g },
                { "l|layout=", "Layout name or index", l =>
                    {
                        if (!int.TryParse(l, out _layoutIndex))
                        {
                            _layoutIndex = -1;
                            _layoutName = l;
                        }
                    } 
                },
                { "i|indices=", "List of comma-delimited file indices to build the layout with", i => _indices = i.Split(',').Select(ind => int.Parse(ind)).ToArray() },
                { "n|names=", "List of comma-delimited file names to build the layout with", n => _names = n.Split(",") },
                { "s|layout-start=", "Layout starting index", s => _layoutStart = int.Parse(s) },
                { "e|layout-end=", "Layout ending index", e => _layoutEnd = int.Parse(e) },
                { "o|output=", "Output PNG file location", o => _outputFile = o },
            };
        }

        public override int Invoke(IEnumerable<string> arguments)
        {
            Options.Parse(arguments);

            ArchiveFile<GraphicsFile> grp = ArchiveFile<GraphicsFile>.FromFile(_grp);

            GraphicsFile layout;
            if (_layoutIndex < 0)
            {
                layout = grp.Files.First(f => f.Name == _layoutName);
            }
            else
            {
                layout = grp.Files.First(f => f.Index == _layoutIndex);
            }

            List<GraphicsFile> layoutTextures;
            if (_indices is null || _indices.Length == 0)
            {
                layoutTextures = _names.Select(n => grp.Files.First(f => f.Name == n)).ToList();
            }
            else
            {
                layoutTextures = _indices.Select(i => grp.Files.First(f => f.Index == i)).ToList();
            }

            if (_layoutEnd == 0)
            {
                _layoutEnd = layout.LayoutEntries.Count;
            }

            (SKBitmap layoutImage, List<LayoutEntry> _) = layout.GetLayout(layoutTextures, _layoutStart, _layoutEnd - _layoutStart, darkMode: false, preprocessedList: true);

            using FileStream layoutStream = new(_outputFile, FileMode.Create);
            layoutImage.Encode(layoutStream, SKEncodedImageFormat.Png, GraphicsFile.PNG_QUALITY);

            return 0;
        }
    }
}