﻿using HaruhiChokuretsuLib.Overlay;
using Keystone;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HaruhiChokuretsuCLI
{
    // This just steals a bunch of code from Wiinject (https://github.com/jonko0493/Wiinject)
    public class AssembleOverlayCodeCommand : Command
    {
        private string _sourceDirectory, _overlayDirectory, _outputPatch;

        public AssembleOverlayCodeCommand() : base("assemble-overlay-code")
        {
            Options = new()
            {
                "Assembles overlay code into the overlay replacement XML file.",
                "Usage: HaruhiChokuretsuCLI assemble-overlay-code -s [SOURCE_DIRECTORY] -l [OVERLAY_DIRECTORY] -o [OUTPUT_PATCH]",
                "",
                { "s|source=", "The directory where your source ASM files live", s => _sourceDirectory = s },
                { "l|overlays=", "The directory containing unpatched overlays", l => _overlayDirectory = l },
                { "o|output=", "The location where the final overlay.xml patch will be output", o => _outputPatch = o },
            };
        }

        public override int Invoke(IEnumerable<string> arguments)
        {
            Options.Parse(arguments);

            string[] asmFiles = Directory.GetFiles(_sourceDirectory, "*.s", SearchOption.AllDirectories);
            Regex funcRegex = new(@"a(?<mode>repl|hook|append)_(?<address>[A-F\d]{8}):");

            List<OverlayPatch> patches = new();
            foreach (string asmFile in asmFiles)
            {
                CommandSet.Out.WriteLine($"Generating overlay patch for file {asmFile}...");

                OverlayPatch patch = new() { Name = Path.GetFileNameWithoutExtension(asmFile) };
                uint currentAppendLocation = (uint)File.ReadAllBytes($"{Path.Combine(_overlayDirectory, patch.Name)}.bin").Length + OverlayPatch.START_LOCATION + 4; // +4 to leave room for overlay end reference

                string[] assemblyRoutines = funcRegex.Split(File.ReadAllText(asmFile));
                for (int i = 1; i < assemblyRoutines.Length; i += 3)
                {
                    if (assemblyRoutines[i] == "append")
                    {
                        List<string> locationVariables = new();
                        foreach (string appendedVariable in assemblyRoutines[i + 2].Replace("\r\n", "\n").Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(appendedVariable))
                            {
                                if (appendedVariable.Contains('['))
                                {
                                    locationVariables.Add(appendedVariable);
                                }
                                else
                                {
                                    patch.AppendedVariables.Add(new(appendedVariable, currentAppendLocation));
                                    currentAppendLocation += (uint)patch.AppendedVariables.Last().Value.Length;
                                }
                            }
                        }
                        foreach (string locationVariable in locationVariables)
                        {
                            patch.AppendedVariables.Add(new(locationVariable, currentAppendLocation, patch.AppendedVariables));
                            currentAppendLocation += (uint)patch.AppendedVariables.Last().Value.Length;
                        }
                    }
                }
                for (int i = 1; i < assemblyRoutines.Length; i += 3)
                {
                    if (assemblyRoutines[i] != "append")
                    {
                        patch.Routines.Add(new(assemblyRoutines[i], uint.Parse(assemblyRoutines[i + 1], System.Globalization.NumberStyles.HexNumber), assemblyRoutines[i + 2], patch.AppendedVariables));
                    }
                }

                foreach (Routine routine in patch.Routines)
                {
                    if (routine.RoutineMode == Routine.Mode.HOOK)
                    {
                        routine.SetBranchInstruction(currentAppendLocation);
                        currentAppendLocation += (uint)routine.Data.Length;
                    }
                }

                patches.Add(patch);
            }

            OverlayPatchDocument patchDocument = new();
            patchDocument.Overlays = new OverlayXml[patches.Count];

            for (int i = 0; i < patchDocument.Overlays.Length; i++)
            {
                patchDocument.Overlays[i] = new();
                patchDocument.Overlays[i].Name = patches[i].Name;
                patchDocument.Overlays[i].Start = OverlayPatch.START_LOCATION;
                patchDocument.Overlays[i].Patches = new OverlayPatchXml[patches[i].Routines.Count];

                for (int j = 0; j < patchDocument.Overlays[i].Patches.Length; j++)
                {
                    patchDocument.Overlays[i].Patches[j] = new();
                    patchDocument.Overlays[i].Patches[j].Location = patches[i].Routines[j].InsertionPoint;
                    if (patches[i].Routines[j].RoutineMode == Routine.Mode.HOOK)
                    {
                        patchDocument.Overlays[i].Patches[j].Value = patches[i].Routines[j].BranchInstruction;
                    }
                    else
                    {
                        patchDocument.Overlays[i].Patches[j].Value = patches[i].Routines[j].Data;
                    }
                }

                List<byte> appendFunction = new();
                appendFunction.AddRange(new byte[4]); // leave blank space for overlay end reference
                foreach (AppendedVariable variable in patches[i].AppendedVariables)
                {
                    appendFunction.AddRange(variable.Value);
                }
                foreach (Routine routine in patches[i].Routines)
                {
                    if (routine.RoutineMode == Routine.Mode.HOOK)
                    {
                        appendFunction.AddRange(routine.Data);
                    }
                }

                patchDocument.Overlays[i].AppendFunction = appendFunction.ToArray();
            }

            XmlSerializer serializer = new(typeof(OverlayPatchDocument));
            using FileStream fileStream = new(_outputPatch, FileMode.Create);
            serializer.Serialize(fileStream, patchDocument);

            return 0;
        }
    }

    public class Assembler
    {
        public static byte[] Assemble(string asm)
        {
            Engine keystone = new(Architecture.ARM, Mode.LITTLE_ENDIAN);
            keystone.ThrowOnError = true;
            EncodedData data = keystone.Assemble(asm, 0);

            return data.Buffer;
        }
    }

    public class OverlayPatch
    {
        public const int START_LOCATION = 0x020C7660;
        public string Name { get; set; }
        public List<Routine> Routines { get; set; } = new();
        public List<AppendedVariable> AppendedVariables { get; set; } = new();
    }

    public class AppendedVariable
    {
        private static Regex NameRegex = new(@"(?<name>\w+):");

        public string Name { get; set; }
        public uint Location { get; set; }
        public byte[] Value { get; set; }

        public AppendedVariable(string asm, uint location)
        {
            Name = NameRegex.Match(asm).Groups["name"].Value;
            Location = location;
            Value = Assembler.Assemble(asm);
        }

        // This method is only called when a variable declaration includes [] indicating it needs to be assigned the location of a different variable
        public AppendedVariable(string asm, uint location, List<AppendedVariable> priorVariables)
        {
            Name = NameRegex.Match(asm).Groups["name"].Value;
            Location = location;

            Regex variableRegex = new(@"\[(?<variableName>\w+)\]");
            AppendedVariable variable = priorVariables.First(v => v.Name == variableRegex.Match(asm).Groups["variableName"].Value);
            asm = variableRegex.Replace(asm, $"0x{variable.Location:X8}");

            Value = Assembler.Assemble(asm);
        }
    }

    public class Routine
    {
        public Mode RoutineMode { get; set; }
        public string Assembly { get; private set; }
        public uint InsertionPoint { get; private set; }
        public byte[] Data { get; private set; }
        public byte[] BranchInstruction { get; private set; }

        public Routine(string mode, uint insertionPoint, string assembly, List<AppendedVariable> appendedVariables)
        {
            RoutineMode = (Mode)Enum.Parse(typeof(Mode), mode.ToUpper());
            Assembly = assembly;
            InsertionPoint = insertionPoint;
            foreach (AppendedVariable appendedVariable in appendedVariables)
            {
                if (RoutineMode == Mode.REPL)
                {
                    Assembly = Regex.Replace(Assembly, $@"={appendedVariable.Name}(?=\s)", $"[pc, #0x{appendedVariable.Location - insertionPoint - 8:X3}]"); // this assumes all replacements will be exactly one instruction
                }
                else
                {
                    StringBuilder sb = new();
                    sb.AppendLine($"{appendedVariable.Name}Loc: .word 0x{appendedVariable.Location:X8}");
                    foreach (string line in Assembly.Replace("\r\n", "\n").Split("\n"))
                    {
                        string regex = $@"={appendedVariable.Name}(?=\s)";
                        if (Regex.IsMatch(line, regex))
                        {
                            // very weak methodology here, assumes you aren't dealing with the link register already
                            sb.AppendLine("push {lr}");
                            sb.AppendLine($"ldr lr, ={appendedVariable.Name}Loc");
                            sb.AppendLine(Regex.Replace(line, regex, $"[lr]"));
                            sb.AppendLine("pop {lr}");
                        }
                    }
                }
            }
            Data = Assembler.Assemble(Assembly);
        }

        public void SetBranchInstruction(uint branchTo)
        {
            int relativeBranch = (int)(branchTo - InsertionPoint);
            string instruction = $"bl 0x{(long)relativeBranch:X8}";
            BranchInstruction = Assembler.Assemble(instruction);
        }

        public enum Mode
        {
            HOOK,
            REPL
        }
    }
}
