﻿using System;
using System.Linq;
using System.Windows;

using Ookii.Dialogs.Wpf; // may be removed later https://github.com/dotnet/wpf/issues/438



namespace Wox.Plugin.Program
{
    /// <summary>
    /// Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource
    {
        private PluginInitContext _context;
        private ProgramSource _editing;
        private Settings _settings;

        public AddProgramSource(PluginInitContext context, Settings settings)
        {
            InitializeComponent();
            _context = context;
            _settings = settings;
            Directory.Focus();
        }

        public AddProgramSource(ProgramSource edit, Settings settings)
        {
            _editing = edit;
            _settings = settings;

            InitializeComponent();
            Directory.Text = _editing.SettingEditSourceCode;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == true)
            {
                Directory.Text = dialog.SelectedPath;
            }
        }

        private ProgramSource parseDirectoryText(string directoryText)
        {
            var result = new ProgramSource();
            result.SearchOption = System.IO.SearchOption.AllDirectories;
            result.ShouldShowDirAsEntry = false;
            if (directoryText.StartsWith("!"))
            {
                directoryText = directoryText.Substring(1);
                result.SearchOption = System.IO.SearchOption.TopDirectoryOnly;
            }
            if (directoryText.StartsWith("*"))
            {
                directoryText = directoryText.Substring(1);
                result.ShouldShowDirAsEntry = true;
            }
            result.Location = directoryText;
            return result;
        }

        private void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
        {
            /* string s = Environment.ExpandEnvironmentVariables(Directory.Text);
            if (!System.IO.Directory.Exists(s))
            {
                System.Windows.MessageBox.Show(_context.API.GetTranslation("wox_plugin_program_invalid_path"));
                return;
            } */
            if (_editing == null)
            {
                var source = parseDirectoryText(Directory.Text);

                _settings.ProgramSources.Insert(0, source);
            }
            else
            {
                parseDirectoryText(Directory.Text).CopyTo(_editing);
            }

            DialogResult = true;
            Close();
        }
    }
}
