using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Turansuraetu
{
    public class Project
    {
        public readonly string projectFile;
        public readonly Dictionary<string, PatchFile> patchFiles = new Dictionary<string, PatchFile>();

        private Project(string file)
        {
            projectFile = file;
        }

        public static Task<Project> Open(MainWindow window, string file)
        {
            file = Path.GetFullPath(file);

            { // Validate file
                string header = File.ReadAllLines(file).SingleOrDefault() ?? "";
                if (!header.Trim().Equals("> RPGMAKER TRANS PATCH V3"))
                {
                    MessageBox.Show(window, "Cannot open project, header file is invalid.", "Turansuraetu",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }

            ProgressDisplay progress = new ProgressDisplay();
            Task<Project> worker = new Task<Project>(() => Open_Do(file, progress));
            worker.Start();
            progress.ShowDialog();

            return worker;
        }

        private static Project Open_Do(string file, ProgressDisplay progress)
        {
            string machinePath = Path.Combine(Path.GetDirectoryName(file) ?? "", "TransuraetuMachine.json");

            progress.Update("Loading machine translations...", .0);
            Dictionary<string, Dictionary<string, TranslationPair.MachineTranslations>> machineTranslations;
            if (File.Exists(machinePath))
                machineTranslations =
                    JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, TranslationPair.MachineTranslations>>>(
                        File.ReadAllText(machinePath));
            else
                machineTranslations = new Dictionary<string, Dictionary<string, TranslationPair.MachineTranslations>>();

            progress.Update("Listing files...", .0);

            Project project = new Project(file);
            string dir = Path.Combine(Path.GetDirectoryName(file) ?? "", "patch");
            string[] files = Directory.GetFiles(dir);

            int i = 1; // Used purely for progress bar
            foreach (string f in files)
            {
                string fn = Path.GetFileName(f);
                Dictionary<string, TranslationPair.MachineTranslations> machineTrs;
                if (!machineTranslations.TryGetValue(fn, out machineTrs))
                    machineTrs = new Dictionary<string, TranslationPair.MachineTranslations>();

                progress.Update($"Reading file {fn} ({i}/{f.Length})", (double) i / f.Length);
                i++;

                string[] lines = File.ReadAllLines(f);
                if(!(lines.FirstOrDefault() ?? "").StartsWith("> RPGMAKER TRANS PATCH FILE VERSION 3"))
                    continue;

                string header = lines.First();
                lines = lines.Skip(1).ToArray();

                List<TranslationPair> pairs = new List<TranslationPair>();
                string original = "";
                string translation = "";
                List<string> context = new List<string>();
                bool isTranslation = false;
                foreach(string line in lines)
                {
                    if (line.StartsWith('>'))
                    {
                        if (line.StartsWith("> BEGIN STRING"))
                        {
                            isTranslation = false;
                            original = "";
                            translation = "";
                        }
                        else if (line.StartsWith("> CONTEXT:"))
                        {
                            isTranslation = true;
                            context.Add(line);
                        } 
                        else if (line.StartsWith("> END STRING"))
                        {
                            isTranslation = false;
                            if (!string.IsNullOrWhiteSpace(original?.Trim()))
                            {
                                TranslationPair pair = new TranslationPair(original, translation, context.ToArray());
                                machineTrs.TryGetValue(pair.Original, out pair.machine);

                                pairs.Add(pair);
                            }

                            original = "";
                            translation = "";
                            context.Clear();
                        }
                    }
                    else
                    {
                        ref string current = ref original;
                        if (isTranslation)
                            current = ref translation;

                        if (!string.IsNullOrWhiteSpace(current))
                            current += '\n';

                        current += line;
                    }
                }

                if(pairs.Count > 0)
                    project.patchFiles.Add(fn, new PatchFile{name = fn, header = header, pairs = pairs.ToArray()});
            }

            progress.Done();
            return project;
        }

        public Task Save()
        {
            ProgressDisplay progress = new ProgressDisplay();
            Task worker = Task.Factory.StartNew(() => Save_Do(progress));
            progress.ShowDialog();

            return worker;
        }

        private void Save_Do(ProgressDisplay progress)
        {
            string projectFolder = Path.GetDirectoryName(projectFile) ?? "";
            string patchFolder = Path.Combine(projectFolder, "patch");

            Dictionary<string, Dictionary<string, TranslationPair.MachineTranslations>> machineTranslations = new Dictionary<string, Dictionary<string, TranslationPair.MachineTranslations>>();

            int i = 1; // Used for progress bar
            foreach (PatchFile pf in patchFiles.Values)
            {
                Dictionary<string, TranslationPair.MachineTranslations> machineTrs = new Dictionary<string, TranslationPair.MachineTranslations>();

                progress.Update($"Saving file {pf.name} ({i}/{patchFiles.Count})", (double) i / patchFiles.Count);
                i++;

                StringBuilder builder = new StringBuilder();

                builder.Append(pf.header);
                foreach (TranslationPair pair in pf.pairs)
                {
                    builder.AppendLine();
                    builder.AppendLine("> BEGIN STRING");
                    builder.AppendLine(pair.Original);
                    builder.AppendJoin('\n', pair.Context);
                    builder.AppendLine();
                    builder.AppendLine(pair.Translation);
                    builder.AppendLine("> END STRING");

                    // Machine translation save data
                    if(!pair.machine.IsEmpty())
                        machineTrs.Add(pair.Original, pair.machine);
                }

                if(machineTrs.Count > 0)
                    machineTranslations.Add(pf.name, machineTrs);

                progress.Update($"Writing {pf.name} to disk...", 100.0);
                File.WriteAllText(Path.Combine(patchFolder, pf.name), builder.ToString());
            }

            if(machineTranslations.Count > 0)
                File.WriteAllText(Path.Combine(projectFolder, "TransuraetuMachine.json"), JsonConvert.SerializeObject(machineTranslations));
            progress.Done();
        }

        public class PatchFile
        {
            public string name;
            public string header;
            public TranslationPair[] pairs;
        }

        public class TranslationPair
        {
            public string Original { get; }
            public string Translation { get; set; }

            public string[] Context { get; }
            public string ContextPreview { get; }

            [UsedImplicitly] // Used for data binding
            public MachineTranslations Machine => machine;
            public MachineTranslations machine;

            public TranslationPair(string original, string translation, string[] context)
            {
                Original = original;
                Translation = translation;
                Context = context;
                ContextPreview = string.Join('\n', Context.Select(x => x.Substring(x.IndexOf(": ", StringComparison.Ordinal) + 2)));
            }

            public struct MachineTranslations
            {
                public string Google { get; set; }
                public string Bing { get; set; }
                public string Transliteration { get; set; }

                public bool IsEmpty()
                {
                    return string.IsNullOrWhiteSpace(Google) && 
                           string.IsNullOrWhiteSpace(Bing) &&
                           string.IsNullOrWhiteSpace(Transliteration);
                }
            }
        }
    }
}
