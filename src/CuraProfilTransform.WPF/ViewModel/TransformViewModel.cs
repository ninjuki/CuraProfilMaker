using ConfigurationParser;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CuraProfilTransform.ViewModel
{
    public class TransformViewModel : ViewModelBase
    {
        private string _InputProfilOutput;
        private Parser _BaseProfilParser;
        private Parser _InputProfilParser;
        private string _SectionSeparator = "=";

        private string _OutputProfil;
        private string _BaseProfil;
        private string _InputProfil;
        private string _LastFolderPath;

        private FileSystemWatcher _InputProfilWatcher;

        private RelayCommand _BaseProfilCommand;
        private RelayCommand _OutputProfilCommand;
        private RelayCommand _TransformCommand;
        private RelayCommand _InputProfilCommand;

        #region Commands

        public RelayCommand InputProfilCommand
        {
            get
            {
                if (_InputProfilCommand == null)
                    _InputProfilCommand = new RelayCommand(ExecInputProfilCommand, () => true);
                return _InputProfilCommand;
            }
        }

        private void ExecInputProfilCommand()
        {
            var lResult = OpenProfile(GetUserCuraProfil());
            if (lResult == null) return;

            InputProfil = lResult;
        }


        public RelayCommand BaseProfilCommand
        {
            get
            {
                if (_BaseProfilCommand == null)
                    _BaseProfilCommand = new RelayCommand(ExecBaseProfilCommand, CanBaseProfilCommand);
                return _BaseProfilCommand;
            }
        }

        private bool CanBaseProfilCommand()
        {
            return true;
        }

        private void ExecBaseProfilCommand()
        {
            var lResult = OpenProfile(GetUserCuraProfil());
            if (lResult == null) return;

            BaseProfil = lResult;
        }


        public RelayCommand OutputProfilCommand
        {
            get
            {
                if (_OutputProfilCommand == null)
                    _OutputProfilCommand = new RelayCommand(ExecOutputProfilCommand, CanOutputProfilCommand);
                return _OutputProfilCommand;
            }
        }

        private bool CanOutputProfilCommand()
        {
            return true;
        }

        private void ExecOutputProfilCommand()
        {
            var lResult = OpenProfile(_LastFolderPath ?? GetUserCuraProfil());
            if (lResult == null) return;

            _LastFolderPath = Path.GetDirectoryName(lResult);
            OutputProfil = lResult;
        }


        public RelayCommand TransformCommand
        {
            get
            {
                if (_TransformCommand == null)
                    _TransformCommand = new RelayCommand(ExecTransformCommand, CanTransformCommand);
                return _TransformCommand;
            }
        }

        private bool CanTransformCommand()
        {
            return InputProfil != null && OutputProfil != null && File.Exists(InputProfil) && File.Exists(OutputProfil);
        }

        private async void ExecTransformCommand()
        {
            try
            {
                _InputProfilParser = InputProfil == null ? null : new Parser(_SectionSeparator, InputProfil, true);
                if (_InputProfilParser == null) return;

                _BaseProfilParser = BaseProfil == null ? null : new Parser(_SectionSeparator, BaseProfil, true);
                if (_BaseProfilParser == null) return;

                var parserDatasTo = new Parser(_SectionSeparator, OutputProfil);

                var IgnoredFromData = new HashSet<string>() { "plugin_config", /*"start.gcode", "end.gcode",*/ "start2.gcode", "end2.gcode", "start3.gcode", "end3.gcode", "start4.gcode", "end4.gcode" };
                var ReMap = new Dictionary<string, string>() {
                { "start.gcode", "start5.gcode" },
                { "end.gcode", "end5.gcode" }
            };

                var lBaseProfilParser = _BaseProfilParser ?? _InputProfilParser;

                string offset_input = null;
                string offset_value = "0";
                string printing_surface_height = null;

                //INFO : Recherche dans le profil de base.
                foreach (KeyValuePair<string, Section> section in _InputProfilParser.Sections)
                {
                    foreach (var item in section.Value)
                    {
                        string lSearchKey = item.Key;
                        string lData = item.Value;

                        if (lSearchKey == "offset_value") offset_value = lData;
                        if (lSearchKey == "offset_input") offset_input = lData;
                        if (lSearchKey == "printing_surface_height") printing_surface_height = lData;
                    }
                }


                foreach (KeyValuePair<string, Section> section in lBaseProfilParser.Sections)
                {
                    Section lDatasSection = _InputProfilParser.Sections[section.Key];

                    var lKeySection = section.Key;
                    if (lKeySection.EndsWith("_0"))
                        lKeySection = lKeySection.Replace("_0", "");

                    var lToSection = new Section(lKeySection);
                    parserDatasTo.Sections.Add(lKeySection, lToSection);

                    foreach (var item in section.Value)
                    {
                        string lSearchKey = item.Key;

                        if (ReMap.ContainsKey(item.Key))
                            lSearchKey = ReMap[item.Key];

                        string lData = item.Value;

                        //INFO : If key exist un Data from use else keep data
                        if (!IgnoredFromData.Contains(item.Key) && lDatasSection.ContainsKey(lSearchKey))
                        {
                            var lRawData = lDatasSection[lSearchKey];
                            //if (!string.IsNullOrWhiteSpace(lRawData))

                            if (lRawData.Contains(";{palpeur}"))
                                lRawData = lRawData.Replace(";{palpeur}", "G29");

                            if (lRawData.Contains("{z_offset}"))
                                lRawData = lRawData.Replace("{z_offset}", string.Format("{0} ; OFFSET -{1} + {2}", offset_value, printing_surface_height, offset_input));

                            lData = lRawData;
                        }
                        lToSection.Add(item.Key, lData);
                    }
                }

                parserDatasTo.Save();
                await ShowMessage("Profil enregistré", MessageDialogStyle.Affirmative);
            }
            catch (Exception ex)
            {
                await ShowMessage(ex.Message, MessageDialogStyle.Affirmative);
            }

        }

        public async Task<MessageDialogResult> ShowMessage(string message, MessageDialogStyle dialogStyle)
        {
            var metroWindow = (Application.Current.MainWindow as MetroWindow);
            metroWindow.MetroDialogOptions.ColorScheme = MetroDialogColorScheme.Accented;

            return await metroWindow.ShowMessageAsync("Cura Profil", message, dialogStyle, metroWindow.MetroDialogOptions);
        }

        #endregion

        #region Properties View
        private bool _DragPossible;

        public bool DragPossible
        {
            get { return _DragPossible; }
            set
            {
                _DragPossible = value;
                RaisePropertyChanged(nameof(DragPossible));
            }
        }

        public string InputProfil
        {
            get { return _InputProfil; }
            set
            {
                _InputProfil = value;
                RaisePropertyChanged(nameof(InputProfil));
                TransformCommand.RaiseCanExecuteChanged();

                OnInputProfil();
            }
        }

        public string BaseProfil
        {
            get { return _BaseProfil; }
            set
            {
                _BaseProfil = value;
                RaisePropertyChanged(nameof(BaseProfil));
                TransformCommand.RaiseCanExecuteChanged();

                OnBaseProfil();
            }
        }

        public string OutputProfil
        {
            get { return _OutputProfil; }
            set
            {
                _OutputProfil = value;
                RaisePropertyChanged(nameof(OutputProfil));
                TransformCommand.RaiseCanExecuteChanged();
            }
        }

        public string InputProfilOutput
        {
            get { return _InputProfilOutput; }
            set
            {
                _InputProfilOutput = value;
                RaisePropertyChanged(nameof(InputProfilOutput));
            }
        }


        #endregion

        #region Properties Datas



        #endregion

        private void OnInputProfil()
        {
            if (!File.Exists(InputProfil)) return;

            if(_InputProfilWatcher != null)
                _InputProfilWatcher.Changed -= OnChanged;

            string pathGetFileName = Path.GetFileName(InputProfil);
            _InputProfilWatcher = new FileSystemWatcher(Path.GetDirectoryName(InputProfil), pathGetFileName);
            _InputProfilWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _InputProfilWatcher.Changed += OnChanged;

            UpdateGetSectionConfig();

            _InputProfilWatcher.EnableRaisingEvents = true;
        }

        private void UpdateGetSectionConfig()
        {
            try
            {
                _InputProfilParser = InputProfil == null ? null : new Parser(_SectionSeparator, InputProfil, true);
                if (_InputProfilParser == null) return;

                var ProfilSection = _InputProfilParser.GetSection("profile_0") ?? _InputProfilParser.GetSection("profile");
                if (ProfilSection != null)
                {
                    var layer_height = ProfilSection.ContainsKey("layer_height") ? ProfilSection.GetString("layer_height") : "-";
                    var print_speed = ProfilSection.ContainsKey("print_speed") ? ProfilSection.GetString("print_speed") : "-";
                    var print_temperature = ProfilSection.ContainsKey("print_temperature") ? ProfilSection.GetString("print_temperature") : "-";
                    var fill_density = ProfilSection.ContainsKey("fill_density") ? ProfilSection.GetString("fill_density") : "-";
                    var offset_value = ProfilSection.ContainsKey("offset_value") ? ProfilSection.GetString("offset_value") : "0";

                    InputProfilOutput = $"layer_height {layer_height}mm, print_speed {print_speed}, print_temperature {print_temperature}°, offset_value {offset_value}mm, fill_density {fill_density}%";
                }
            }
            catch (System.IO.IOException)
            {
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle , new Action(()=> {
                UpdateGetSectionConfig();
            }));
        }

        private void OnBaseProfil()
        {
            if (!File.Exists(BaseProfil)) return;

            _BaseProfilParser = BaseProfil == null ? null : new Parser(_SectionSeparator, BaseProfil, true);
            if (_BaseProfilParser == null) return;
        }

        private string OpenProfile(string initialDirectory)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Profile files (*.ini)|*.ini|All files (*.*)|*.*";
            openFileDialog.CheckFileExists = false;

            openFileDialog.InitialDirectory = initialDirectory;

            if (openFileDialog.ShowDialog() == true)
            {
                string lProfilePath = openFileDialog.FileName;

                if (string.IsNullOrWhiteSpace(lProfilePath)) return null;

                return lProfilePath;
            }
            return null;
        }

        private string GetUserCuraProfil()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cura");
        }

        public TransformViewModel()
        {
            var lCuraProfilPath = GetUserCuraProfil();
            if (Directory.Exists(lCuraProfilPath))
            {
                var lCuras = Directory.GetDirectories(lCuraProfilPath);

                var lDagoma = lCuras.Where(p => p.ToLower().Contains("dagoma")).FirstOrDefault();
                var lCura = lCuras.Where(p => !p.ToLower().Contains("dagoma")).FirstOrDefault();

                if (lDagoma != null)
                {
                    var lDagomaPathCurrent = Path.Combine(lDagoma, "current_profile.ini");
                    if (File.Exists(lDagomaPathCurrent))
                        InputProfil = lDagomaPathCurrent;
                }

                if (lCura != null)
                {
                    var lCuraPathCurrent = Path.Combine(lCura, "current_profile.ini");
                    if (File.Exists(lCuraPathCurrent))
                        BaseProfil = lCuraPathCurrent;
                }
            }
        }

    }
}
