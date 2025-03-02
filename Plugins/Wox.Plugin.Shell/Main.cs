using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WindowsInput;
using WindowsInput.Native;
using NLog;
using Wox.Infrastructure.Hotkey;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using Keys = System.Windows.Forms.Keys;
using Wox.Infrastructure.UI;
using System.Runtime.InteropServices;

namespace Wox.Plugin.Shell
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu, ISavable
    {
        private const string Image = "Images/shell.png";
        private PluginInitContext _context;
        private bool _winRStroked;
        private bool _winFStroked;
        private bool _winSStroked;
        private readonly KeyboardSimulator _keyboardSimulator = new KeyboardSimulator(new InputSimulator());

        private readonly Settings _settings;
        private readonly PluginJsonStorage<Settings> _storage;
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public Main()
        {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
        }

        public void Save()
        {
            _storage.Save();
        }


        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            string cmd = query.Search;
            if (string.IsNullOrEmpty(cmd))
            {
                return ResultsFromlHistory();
            }
            else
            {
                var queryCmd = GetCurrentCmd(cmd);
                results.Add(queryCmd);
                var history = GetHistoryCmds(cmd, queryCmd);
                results.AddRange(history);

                try
                {
                    string basedir = null;
                    string dir = null;
                    string excmd = Environment.ExpandEnvironmentVariables(cmd);
                    if (Directory.Exists(excmd) && (cmd.EndsWith("/") || cmd.EndsWith(@"\")))
                    {
                        basedir = excmd;
                        dir = cmd;
                    }
                    else if (Directory.Exists(Path.GetDirectoryName(excmd) ?? string.Empty))
                    {
                        basedir = Path.GetDirectoryName(excmd);
                        var dirn = Path.GetDirectoryName(cmd);
                        dir = (dirn.EndsWith("/") || dirn.EndsWith(@"\")) ? dirn : cmd.Substring(0, dirn.Length + 1);
                    }

                    if (basedir != null)
                    {
                        var autocomplete = Directory.GetFileSystemEntries(basedir).
                            Select(o => dir + Path.GetFileName(o)).
                            Where(o => o.StartsWith(cmd, StringComparison.OrdinalIgnoreCase) &&
                                       !results.Any(p => o.Equals(p.Title, StringComparison.OrdinalIgnoreCase)) &&
                                       !results.Any(p => o.Equals(p.Title, StringComparison.OrdinalIgnoreCase))).ToList();
                        autocomplete.Sort();
                        results.AddRange(autocomplete.ConvertAll(m => new Result
                        {
                            Title = m,
                            IcoPath = Image,
                            Action = c =>
                            {
                                Execute(Process.Start, PrepareProcessStartInfo(m), m, _settings.RunAsAdministrator);
                                return true;
                            }
                        }));
                    }
                }
                catch (Exception e)
                {
                    Logger.WoxError($"Exception when query for <{query}>", e);
                }
                return results;
            }
        }

        private List<Result> GetHistoryCmds(string cmd, Result result)
        {
            IEnumerable<Result> history = _settings.Count.Where(o => o.Key.IndexOf(cmd, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(o => o.Value)
                .Select(m =>
                {
                    if (m.Key == cmd)
                    {
                        result.SubTitle = string.Format(_context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"), m.Value);
                        return null;
                    }

                    var ret = new Result
                    {
                        Title = m.Key,
                        SubTitle = string.Format(_context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"), m.Value),
                        IcoPath = Image,
                        Action = c =>
                        {
                            Execute(Process.Start, PrepareProcessStartInfo(m.Key), m.Key, _settings.RunAsAdministrator);
                            return true;
                        }
                    };
                    return ret;
                }).Where(o => o != null).Take(20);
            return history.ToList();
        }

        private Result GetCurrentCmd(string cmd)
        {
            Result result = new Result
            {
                Title = cmd,
                Score = 5000,
                SubTitle = _context.API.GetTranslation("wox_plugin_cmd_execute_through_shell"),
                IcoPath = Image,
                Action = c =>
                {
                    Execute(Process.Start, PrepareProcessStartInfo(cmd), cmd, _settings.RunAsAdministrator);
                    return true;
                }
            };

            return result;
        }

        private List<Result> ResultsFromlHistory()
        {
            IEnumerable<Result> history = _settings.Count.OrderByDescending(o => o.Value)
                .Select(m => new Result
                {
                    Title = m.Key,
                    SubTitle = string.Format(_context.API.GetTranslation("wox_plugin_cmd_cmd_has_been_executed_times"), m.Value),
                    IcoPath = Image,
                    Action = c =>
                    {
                        Execute(Process.Start, PrepareProcessStartInfo(m.Key), m.Key, _settings.RunAsAdministrator);
                        return true;
                    }
                }).Take(20);
            return history.ToList();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ShellExecute(
        IntPtr hwnd,
        string lpOperation,
        string lpFile,
        string lpParameters,
        string lpDirectory,
        int nShowCmd);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CommandLineToArgvW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
            out int pNumArgs);

        public static void ExecuteLikeRunDialogBox(string command, bool runAsAdministrator = false)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                //throw new ArgumentException("Command cannot be empty or whitespace.", nameof(command));
                return;
            }

            string[] args = SplitCommandLine(command);
            if (args.Length == 0) {
                //throw new ArgumentException("Invalid command.");
                return;
            }

            string executable = args[0];
            string parameters = string.Join(" ", args.Skip(1).Select(ArgEscape));

            IntPtr result = ShellExecute(
                hwnd: IntPtr.Zero,
                lpOperation: runAsAdministrator ? "runas" : "open",
                lpFile: executable,
                lpParameters: parameters.Length > 0 ? parameters : null,
                lpDirectory: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                nShowCmd: 5 /* SW_SHOW */);

            // Check for errors (values <= 32 indicate failure)
            long errorCode = result.ToInt64();
            if (errorCode <= 32)
                throw new Win32Exception((int)errorCode);
        }

        private static string ArgEscape(string arg)
        {
            // Escape arguments containing spaces or quotes
            if (arg.Contains(' ') || arg.Contains('"') || arg.Contains('\t'))
                return "\"" + arg.Replace("\"", "\\\"") + "\"";
            else
                return arg;
        }

        private static string[] SplitCommandLine(string commandLine)
        {
            int argc;
            IntPtr argv = CommandLineToArgvW(commandLine, out argc);
            if (argv == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                string[] args = new string[argc];
                for (int i = 0; i < argc; i++)
                {
                    IntPtr ptr = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(ptr);
                }
                return args;
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }

        private ProcessStartInfo PrepareProcessStartInfo(string command, bool runAsAdministrator = false)
        {
            command = command.Trim();
            command = Environment.ExpandEnvironmentVariables(command);
            var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var runAsAdministratorArg = !runAsAdministrator && !_settings.RunAsAdministrator ? "" : "runas";

            ProcessStartInfo info;
            if (_settings.Shell == Shell.Cmd)
            {
                // var arguments = _settings.LeaveShellOpen ? $"/k \"{command}\"" : $"/c \"{command}\" & pause";
                var arguments = _settings.LeaveShellOpen ? $"/k \"{command}\"" : $"/c \"{command}\"";

                info = ShellCommand.SetProcessStartInfo("cmd.exe", workingDirectory, arguments, runAsAdministratorArg);
            }
            else if (_settings.Shell == Shell.Powershell)
            {
                string arguments;
                if (_settings.LeaveShellOpen)
                {
                    arguments = $"-NoExit \"{command}\"";
                }
                else
                {
                    // arguments = $"\"{command} ; Read-Host -Prompt \\\"Press Enter to continue\\\"\"";
                    arguments = $"\"{command} ; \"";
                }

                info = ShellCommand.SetProcessStartInfo("powershell.exe", workingDirectory, arguments, runAsAdministratorArg);
            }
            else if (_settings.Shell == Shell.RunCommand)
            {
                var parts = command.Split(new[] { ' ' }, 2);
                if (parts.Length == 2)
                {
                    var filename = parts[0];
                    if (ExistInPath(filename))
                    {
                        var arguments = parts[1];
                        info = ShellCommand.SetProcessStartInfo(filename, workingDirectory, arguments, runAsAdministratorArg);
                    }
                    else
                    {
                        info = ShellCommand.SetProcessStartInfo(command, verb: runAsAdministratorArg);
                    }
                }
                else
                {
                    info = ShellCommand.SetProcessStartInfo(command, verb: runAsAdministratorArg);
                }
            }
            else if (_settings.Shell == Shell.Bash && _settings.SupportWSL)
            {
                string arguments;
                if (_settings.LeaveShellOpen)
                {
                    // FIXME: How to deal with commands containing single quote?
                    arguments = $"-c \'{command} ; $SHELL\'";
                }
                else
                {
                    arguments = $"-c \'{command} ; echo -n Press any key to exit... ; read -n1\'";
                }
                info = new ProcessStartInfo
                {
                    FileName = "bash.exe",
                    Arguments = arguments
                };
            }
            else
            {
                throw new NotImplementedException();
            }

            info.UseShellExecute = true;

            _settings.AddCmdHistory(command);

            return info;
        }

        private void Execute(Func<ProcessStartInfo, Process> startProcess,ProcessStartInfo info, string cmd, bool runAsAdministrator)
        {
            try
            {
                if (_settings.Shell == Shell.Cmd && !_settings.LeaveShellOpen)
                {
                    if (Directory.Exists(cmd) || File.Exists(cmd))
                    {
                        ExecuteLikeRunDialogBox("\"" + cmd + "\"", runAsAdministrator);
                    }
                    else
                    {
                        ExecuteLikeRunDialogBox(cmd, runAsAdministrator);
                    }
                }
                else
                {
                    startProcess(info);
                }
            }
            catch (FileNotFoundException e)
            {
                var name = "Plugin: Shell";
                var message = $"Command not found: {e.Message}";
                _context.API.ShowMsg(name, message);
            }
            catch(Win32Exception e)
            {
                var name = "Plugin: Shell";
                var message = $"Error running the command: {e.Message}";
                _context.API.ShowMsg(name, message);
            }
        }

        private bool ExistInPath(string filename)
        {
            if (File.Exists(filename))
            {
                return true;
            }
            else
            {
                var values = Environment.GetEnvironmentVariable("PATH");
                if (values != null)
                {
                    foreach (var path in values.Split(';'))
                    {
                        var path1 = Path.Combine(path, filename);
                        var path2 = Path.Combine(path, filename + ".exe");
                        if (File.Exists(path1) || File.Exists(path2))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                else
                {
                    return false;
                }
            }
        }

        public void Init(PluginInitContext context)
        {
            this._context = context;
            context.API.GlobalKeyboardEvent += API_GlobalKeyboardEvent;
        }

        bool API_GlobalKeyboardEvent(int keyevent, int vkcode, SpecialKeyState state)
        {
            if (_settings.ReplaceWinR)
            {
                if (keyevent == (int)KeyEvent.WM_KEYDOWN && vkcode == (int)Keys.R && state.WinPressed && !state.AltPressed && !state.CtrlPressed && !state.ShiftPressed)
                {
                    _winRStroked = true;
                    OnWinRPressed();
                    return false;
                }
                if (keyevent == (int)KeyEvent.WM_KEYUP && _winRStroked && vkcode == (int)Keys.LWin)
                {
                    _winRStroked = false;
                    _keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL);
                    return false;
                }
            }
            if (_settings.ReplaceWinF)
            {
                if (keyevent == (int)KeyEvent.WM_KEYDOWN && vkcode == (int)Keys.F && state.WinPressed && !state.AltPressed && !state.CtrlPressed && !state.ShiftPressed)
                {
                    _winFStroked = true;
                    OnWinRPressed();
                    return false;
                }
                if (keyevent == (int)KeyEvent.WM_KEYUP && _winFStroked && vkcode == (int)Keys.LWin)
                {
                    _winFStroked = false;
                    _keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL);
                    return false;
                }
            }
            if (_settings.ReplaceWinS)
            {
                if (keyevent == (int)KeyEvent.WM_KEYDOWN && vkcode == (int)Keys.S && state.WinPressed && !state.AltPressed && !state.CtrlPressed && !state.ShiftPressed)
                {
                    _winSStroked = true;
                    OnWinRPressed();
                    return false;
                }
                if (keyevent == (int)KeyEvent.WM_KEYUP && _winSStroked && vkcode == (int)Keys.LWin)
                {
                    _winSStroked = false;
                    _keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL);
                    return false;
                }
            }
            return true;
        }

        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Process.GetCurrentProcess().Id;
            int activeProcId;
            GetWindowThreadProcessId(activatedHandle, out activeProcId);

            return activeProcId == procId;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        private void OnWinRPressed()
        {
            // Sometimes may fail to bring to foreground
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                //_context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeywords[0]}{Plugin.Query.TermSeperater}");
                _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeywords[0]}");
                _context.API.ShowApp();
            }));
            Task.Delay(10).ContinueWith(t => {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!ApplicationIsActivated())
                    {
                        _context.API.ShowApp();
                    }
                }));
            });
            Task.Delay(50).ContinueWith(t => {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!ApplicationIsActivated())
                    {
                        _context.API.ShowApp();
                    }
                }));
            });
            Task.Delay(100).ContinueWith(t =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!ApplicationIsActivated())
                    {
                        _context.API.ShowApp();
                    }
                }));
            });
            Task.Delay(200).ContinueWith(t => {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!ApplicationIsActivated())
                    {
                        _context.API.ShowApp();
                    }
                }));
            });
            Task.Delay(400).ContinueWith(t => {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!ApplicationIsActivated())
                    {
                        _context.API.ShowApp();
                    }
                }));
            });
        }

        public Control CreateSettingPanel()
        {
            return new CMDSetting(_settings);
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_cmd_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_cmd_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var resultlist = new List<Result>
            {
                new Result
                {
                    Title = _context.API.GetTranslation("wox_plugin_cmd_run_as_different_user"),
                    Action = c =>
                    {
                        Task.Run(() =>Execute(ShellCommand.RunAsDifferentUser, PrepareProcessStartInfo(selectedResult.Title), selectedResult.Title, false));
                        return true;
                    },
                    IcoPath = "Images/app.png"
                },
                new Result
                {
                    Title = _context.API.GetTranslation("wox_plugin_cmd_run_as_administrator"),
                    Action = c =>
                    {
                        Execute(Process.Start, PrepareProcessStartInfo(selectedResult.Title, true), selectedResult.Title, true);
                        return true;
                    },
                    IcoPath = Image
                }
            };

            return resultlist;
        }
    }
}
