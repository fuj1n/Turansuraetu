using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Win32;
using Turansuraetu.Translate;

namespace Turansuraetu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Project _openProject;

        private DataGridCell _contextCell;
        private DataGridRow _contextRow;

        public MainWindow()
        {
            InitializeComponent();

            string lastProject = ConfigFile.Instance.project.lastOpenProjectPath;
            if (!string.IsNullOrWhiteSpace(lastProject) && File.Exists(lastProject))
                OpenProject(lastProject);
        }

        private void Exit_Clicked(object sender, RoutedEventArgs e) => Close();

        private void About_Clicked(object sender, RoutedEventArgs e) =>
            new AboutWindow {Owner = GetWindow(this)}.ShowDialog();

        private void RpgMkTrans_SetExe_Clicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = false,
                ShowReadOnly = false,
                CheckFileExists = true,
                DefaultExt = ".exe",
                Filter = "Executables|rpgmt.exe",
                Title = "Turansuraetu - Set RPGMakerTrans executable"
            };

            if (!(bool) dialog.ShowDialog(this)) return;

            ConfigFile.Instance.rpgMakerTrans.path = dialog.FileName;
            ConfigFile.Instance.Save();
        }

        private async void OpenProject_Clicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = false,
                ShowReadOnly = false,
                CheckFileExists = true,
                Filter = "RPGMakerTrans patch header|RPGMKTRANSPATCH",
                Title = "Turansuraetu - Open Project"
            };

            if (!(bool) dialog.ShowDialog(this))
                return;

            if (_openProject != null)
            {
                MessageBoxResult result = MessageBox.Show("Save open project before quitting?", "Transuraetu",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                switch (result)
                {
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.Yes:
                        Task task = _openProject.Save();
                        await task;
                        if (!task.IsCompletedSuccessfully)
                        {
                            MessageBox.Show("Save failed... Not closing...", "Transuraetu", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return;
                        }

                        break;
                }
            }

            OpenProject(dialog.FileName);
        }

        private async void OpenProject(string projectFile)
        {
            ConfigFile.Instance.project.lastOpenProjectPath = projectFile;
            ConfigFile.Instance.Save();

            _openProject = await Project.Open(this, projectFile);
            OpenSection(_openProject.patchFiles.First().Value);
        }

        private void OpenSection(Project.PatchFile to)
        {
            SectionSwitcher.Items.Clear();
            foreach (Project.PatchFile pf in _openProject.patchFiles.Values)
            {
                MenuItem item = new MenuItem {Header = pf.name};
                item.Click += (sender, args) => OpenSection(pf);
                item.IsChecked = to.name.Equals(pf.name);
                SectionSwitcher.Items.Add(item);
            }

            TranslateData.DataContext = to.pairs;
            // Reset widths so they match RPGMaker
            foreach (DataGridColumn col in TranslateData.Columns)
                col.Width = 295;
        }

        private void SaveProject_Clicked(object sender, RoutedEventArgs e)
        {
            if (_openProject == null)
            {
                MessageBox.Show("No project is open", "Turansuraetu", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _openProject.Save();
        }

        private void OnTypeInTextbox(object sender, KeyEventArgs e)
        {
            if (Key.Return != e.Key || 0 >= (ModifierKeys.Shift & e.KeyboardDevice.Modifiers)) return;
            TextBox tb = (TextBox) sender;
            int caret = tb.CaretIndex;
            tb.Text = tb.Text.Insert(caret, Environment.NewLine);
            tb.CaretIndex = caret + 1;
            e.Handled = true;
        }

        private void Translations_Clear_Clicked(object sender, RoutedEventArgs e)
        {
            if (!(TranslateData.DataContext is Project.TranslationPair[] currentPairs))
            {
                MessageBox.Show(this, "No section is open", "Turansuraetu", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (Project.TranslationPair pair in currentPairs)
            {
                pair.machine = default;
            }

            RefreshTable();
        }

        private void Translate_Batch_Clicked(object sender, RoutedEventArgs e)
        {
            if (!(TranslateData.DataContext is Project.TranslationPair[] currentPairs))
            {
                MessageBox.Show(this, "No section is open", "Turansuraetu", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ProgressDisplay disp = new ProgressDisplay();

            List<ITranslateService> activeServices =
                ITranslateService.TranslationServices.Where(x => x.IsActive(this)).ToList();

            bool overwriteTranslations = DoOverwrite.IsChecked;

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (o, args) =>
            {
                try
                {
                    foreach (ITranslateService service in activeServices)
                    {
                        int i = 1;
                        foreach (Project.TranslationPair pair in currentPairs)
                        {
                            disp.Update($"{service.GetType().Name} - {i}/{currentPairs.Length}",
                                (double) i / currentPairs.Length);

                            try
                            {
                                service.Translate(Translate.Language.Japanese, Translate.Language.English,
                                    pair.Original,
                                    ref pair.machine, overwriteTranslations);
                            }
                            catch (Exception e)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show(
                                        $"An error has occured whilst translating with {service.GetType().Name}: {e.GetType().Name} {e.Message}",
                                        "Turansuraetu", MessageBoxButton.OK, MessageBoxImage.Stop);
                                });
                            }

                            i++;
                        }
                    }
                }
                finally
                {
                    disp.Done();
                }
            };

            worker.RunWorkerCompleted += (o, args) => { RefreshTable(); };

            worker.RunWorkerAsync();
            disp.ShowDialog();
        }

        private void RefreshTable()
        {
            object ob = TranslateData.DataContext;
            TranslateData.DataContext = null;
            TranslateData.DataContext = ob;
        }

        private Process RpgMkTrans_ConstructProcess(string args = "")
        {
            string exe = ConfigFile.Instance.rpgMakerTrans.path;
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardError = true,
                    FileName = exe,
                    UseShellExecute = false,
                    Arguments = args
                }
            };
        }

        private bool RpgMkTrans_Validate()
        {
            string exe = ConfigFile.Instance.rpgMakerTrans.path;
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                MessageBox.Show("RPGMakerTrans executable not set", "Turansuraetu - RPGMakerTrans Integration",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                Process process = RpgMkTrans_ConstructProcess();
                process.Start();

                if (!process.StandardError.ReadToEnd().StartsWith("usage: rpgmt.exe"))
                    throw new Exception();

                return true;
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "RPGMakerTrans executable is not valid, ensure you are using the CLI version of RPGMakerTrans",
                    "Turansuraetu - RPGMakerTrans Integration", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void RpgMkTrans_ApplyPatch_Clicked(object sender, RoutedEventArgs e)
        {
            if (_openProject == null)
            {
                MessageBox.Show("No project is open", "Turansuraetu - RPGMakerTrans Integration", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (!RpgMkTrans_Validate())
                return;

            string dir = Path.GetDirectoryName(_openProject.projectFile);
            string[] split = dir.Replace("\\", "/").Split('/');

            if (split.Length <= 0)
            {
                MessageBox.Show($"Failed to separate path: {dir}", "Turansuraetu - RPGMakerTrans Integration");
                return;
            }

            split[^1] = split[^1].Replace("_patch", "");

            dir = string.Join("/", split);

            Process process = RpgMkTrans_ConstructProcess(dir);
            process.Start();
            string err = process.StandardError.ReadToEnd().Trim();

            if (!string.IsNullOrWhiteSpace(err))
                MessageBox.Show(err, "Turansuraetu - RPGMakerTrans Integration", MessageBoxButton.OK,
                    MessageBoxImage.Error);
        }

        private void RpgMkTrans_CreatePatch_Clicked(object sender, RoutedEventArgs args)
        {
            if (!RpgMkTrans_Validate())
                return;

            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = false,
                ShowReadOnly = false,
                CheckFileExists = true,
                DefaultExt = ".exe",
                Filter = "Executables|*.exe",
                Title = "Turansuraetu - Create Patch for Executable"
            };

            if (!dialog.ShowDialog(this).Value)
                return;

            Process process = RpgMkTrans_ConstructProcess(Path.GetDirectoryName(Path.GetFullPath(dialog.FileName)));
            process.Start();
            string err = process.StandardError.ReadToEnd().Trim();

            if (!string.IsNullOrWhiteSpace(err))
            {
                MessageBox.Show(err, "Turansuraetu - RPGMakerTrans Integration", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            try
            {
                OpenProject(Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(dialog.FileName)).Trim('/', '\\') + "_patch",
                    "RPGMKTRANSPATCH"));
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (_openProject == null)
                return;

            MessageBoxResult result = MessageBox.Show("Save open project before quitting?", "Transuraetu",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    return;
                case MessageBoxResult.Yes:
                    e.Cancel = true;

                    Task task = _openProject.Save();
                    await task;
                    if (!task.IsCompletedSuccessfully)
                    {
                        MessageBox.Show("Save failed... Not closing...", "Transuraetu", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    _openProject = null;
                    Close();
                    break;
            }
        }

        private void TranslateCurrentLine(object sender, RoutedEventArgs e)
        {
            ITranslateService service = null;

            try
            {
                if (_contextRow == null || _contextCell == null)
                    return;

                const Language from = Translate.Language.Japanese;
                const Language to = Translate.Language.English;


                if (!(TranslateData.DataContext is Project.TranslationPair[] pairs))
                    return;
                ref Project.TranslationPair pair = ref pairs[_contextRow.GetIndex()];

                service = _contextCell.Column.Header switch
                {
                    "Google Translate" => ITranslateService.TranslationServices.First(x => x is GoogleTranslateService),
                    "Bing Translate" => ITranslateService.TranslationServices.First(x => x is BingTranslateService),
                    "Transliteration" => ITranslateService.TranslationServices.First(x => x is TransliterateService),
                    _ => null
                };

                if (service == null)
                    return;

                service.Translate(from, to, pair.Original, ref pair.machine, true);
                RefreshTable();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error has occured whilst translating with {service?.GetType().Name}: {ex.GetType().Name} {ex.Message}",
                    "Turansuraetu", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
        }

        private void UpdateGridContextMenu(object sender, ContextMenuEventArgs e)
        {

            DependencyObject dep = DataGridMiscHelpers.FindVisualParentAsDataGridSubComponent(
                (DependencyObject)e.OriginalSource);
            if (dep == null)
            {
                return;
            }

            DataGridMiscHelpers.FindCellAndRow(dep, out DataGridCell cell, out DataGridRow row);
            if (dep is DataGridColumnHeader || dep is DataGridRow)
            {
                e.Handled = true;
                return;
            }

            switch (cell.Column.Header)
            {
                case "Google Translate":
                case "Bing Translate": 
                case "Transliteration":
                    CtxMenuTranslateCurrent.IsEnabled = true;
                    _contextCell = cell;
                    _contextRow = row;
                    break;
                default:
                    CtxMenuTranslateCurrent.IsEnabled = false;
                    break;
            }
        }
    }
}