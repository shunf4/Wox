using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using NLog;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Program.Programs;
using Wox.Plugin.Program.Views;
using System.Threading;
using System.Windows.Threading;
using System.Windows;

namespace Wox.Plugin.Program
{
    public class Main : ISettingProvider, IPlugin, IPluginI18n, IContextMenu, ISavable, IReloadable
    {
        internal static Win32[] _win32s { get; set; }
        internal static UWP.Application[] _uwps { get; set; }
        internal static Settings _settings { get; set; }

        private static PluginInitContext _context;
        private CancellationTokenSource _updateSource = new CancellationTokenSource();

        private static BinaryStorage<Win32[]> _win32Storage;
        private static BinaryStorage<UWP.Application[]> _uwpStorage;
        private PluginJsonStorage<Settings> _settingsStorage;

        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

        private static System.Windows.Forms.NotifyIcon notify = new System.Windows.Forms.NotifyIcon();

        private static void preloadPrograms()
        {
            Logger.StopWatchNormal("Preload programs cost", () =>
            {
                _win32Storage = new BinaryStorage<Win32[]>("Win32");
                _win32s = _win32Storage.TryLoad(new Win32[] { });
                _uwpStorage = new BinaryStorage<UWP.Application[]>("UWP");
                _uwps = _uwpStorage.TryLoad(new UWP.Application[] { });
            });
            Logger.WoxInfo($"Number of preload win32 programs <{_win32s.Length}>");
            Logger.WoxInfo($"Number of preload uwps <{_uwps.Length}>");
        }

        public void Save()
        {
            _settingsStorage.Save();
            _win32Storage.Save(_win32s);
            _uwpStorage.Save(_uwps);
        }

        private List<Result> Commands()
        {
            var results = new List<Result>();
            results.AddRange(new[]
            {
                new Result
                {
                    Title = "Reindex Programs",
                    SubTitle = "Reindex Programs",
                    Action = c =>
                    {
                        IndexPrograms(true);
                        return true;
                    }
                },
            });
            return results;
        }

        public List<Result> Query(Query query)
        {
            lock(this)
            {
                if (_updateSource != null)
                {
                    if (!_updateSource.IsCancellationRequested)
                    {
                        _updateSource.Cancel();
                        Logger.WoxDebug($"cancel init {_updateSource.Token.GetHashCode()} {Thread.CurrentThread.ManagedThreadId} {query.RawQuery}");
                        _updateSource.Dispose();
                    }
                    else
                    {
                        Logger.WoxDebug($"already cancelled init ... {Thread.CurrentThread.ManagedThreadId} {query.RawQuery}");
                    }
                }

                _updateSource = new CancellationTokenSource();
            }
            
            var token = _updateSource.Token;
            StringMatcher sm = new StringMatcher(token);
            try
            {
                ParallelOptions po = new ParallelOptions();
                po.CancellationToken = token;

                ConcurrentBag<Result> resultRaw = new ConcurrentBag<Result>();

                if (token.IsCancellationRequested) { return new List<Result>(); }
                Parallel.ForEach(_win32s, po, (program, state) =>
                {
                    if (token.IsCancellationRequested) { state.Break(); }
                    if (program.Enabled)
                    {
                        var r = program.Result(query.Search, _context.API, sm);
                        if (r != null && r.Score > 0)
                        {
                            resultRaw.Add(r);
                        }
                    }
                });
                if (token.IsCancellationRequested) { return new List<Result>(); }
                Parallel.ForEach(_uwps, po, (program, state) =>
                {
                    if (token.IsCancellationRequested) { state.Break(); }
                    if (program.Enabled)
                    {
                        var r = program.Result(query.Search, _context.API, sm);
                        if (token.IsCancellationRequested) { state.Break(); }
                        if (r != null && r.Score > 0)
                        {
                            resultRaw.Add(r);
                        }
                    }
                });
                
                foreach (var r in Commands())
                {
                    var titleMatch = StringMatcher.FuzzySearch(query.Search, r.Title);
                    if (titleMatch.Score > 0)
                    {
                        r.Score = titleMatch.Score;
                        r.TitleHighlightData = titleMatch.MatchData;
                        resultRaw.Add(r);
                    }
                }

                if (token.IsCancellationRequested) { return new List<Result>(); }
                OrderedParallelQuery<Result> sorted = null;
                List<Result> results = new List<Result>();

                sorted = resultRaw.AsParallel().WithCancellation(token).OrderByDescending(r => r.Score);

                if (token.IsCancellationRequested) { return new List<Result>(); }

                foreach (Result r in sorted)
                {
                    if (token.IsCancellationRequested) { return new List<Result>(); }
                    var ignored = _settings.IgnoredSequence.Any(entry =>
                    {
                        if (entry.IsRegex)
                        {
                            return Regex.Match(r.Title, entry.EntryString).Success || Regex.Match(r.SubTitle, entry.EntryString).Success;
                        }
                        else
                        {
                            return r.Title.ToLower().Contains(entry.EntryString) || r.SubTitle.ToLower().Contains(entry.EntryString);
                        }
                    });
                    if (!ignored)
                    {
                        results.Add(r);
                    }
                    if (results.Count == 30)
                    {
                        break;
                    }
                }


                return results;
            }
            catch (OperationCanceledException)
            {
                return new List<Result>();
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            loadSettings();

            preloadPrograms();

            Task.Delay(2000).ContinueWith(_ =>
            {
                Logger.WoxInfo("Program: IndexPrograms()");
                IndexPrograms(true);
                Save();
                Logger.WoxInfo("Program: IndexPrograms() Done");
            });

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                DispatcherTimer dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Interval = TimeSpan.FromMinutes(10);
                dispatcherTimer.Tick += (__, ___) =>
                {
                    Logger.WoxInfo("Program: IndexPrograms() in interval loop");
                    IndexPrograms(false);
                    Save();
                    Logger.WoxInfo("Program: IndexPrograms() in interval loop Done");
                };
                dispatcherTimer.Start();
            }));
            
        }

        public void InitSync(PluginInitContext context)
        {
            _context = context;
            loadSettings();
            IndexPrograms(true);
        }

        public void loadSettings()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();
        }

        public static void IndexWin32Programs()
        {
            var win32S = Win32.All(_settings);
            _win32s = win32S;
        }

        public static void IndexUWPPrograms()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;

            var applications = support ? UWP.All() : new UWP.Application[] { };
            _uwps = applications;
        }

        public static void IndexPrograms(bool showBalloonResult)
        {
            var a = Task.Run(() =>
            {
                Logger.StopWatchNormal("Win32 index cost", IndexWin32Programs);
            });

            var b = Task.Run(() =>
            {
                Logger.StopWatchNormal("UWP index cost", IndexUWPPrograms);
            });

            Task.WaitAll(a, b);

            Logger.WoxInfo($"Number of indexed win32 programs <{_win32s.Length}>");
            foreach (var win32 in _win32s)
            {
                Logger.WoxDebug($" win32: <{win32.Name}> <{win32.ExecutableName}> <{win32.FullPath}>");
            }
            Logger.WoxInfo($"Number of indexed uwps <{_uwps.Length}>");
            foreach (var uwp in _uwps)
            {
                Logger.WoxDebug($" uwp: <{uwp.DisplayName}> <{uwp.UserModelId}>");
            }

            _settings.LastIndexTime = DateTime.Today;

            if (showBalloonResult)
            {
                notify.Visible = true;
                notify.Icon = System.Drawing.SystemIcons.Information;
                notify.ShowBalloonTip(3000, "Wox Program Index Done", $"Win32 Progs: {_win32s.Length}; UWP Progs: {_uwps.Length}", System.Windows.Forms.ToolTipIcon.Info);

                new Timer(state =>
                {
                    notify.Visible = false;
                    notify.Visible = true;
                    notify.Visible = false;
                }, null, 3000, 0);
            }

        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(_context, _settings, _win32s, _uwps);
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var menuOptions = new List<Result>();
            var program = selectedResult.ContextData as IProgram;
            if (program != null)
            {
                menuOptions = program.ContextMenus(_context.API);
            }
            return menuOptions;
        }


        public static void StartProcess(Func<ProcessStartInfo, Process> runProcess, ProcessStartInfo info)
        {
            try
            {
                runProcess(info);
            }
            catch (Exception)
            {
                var name = "Plugin: Program";
                var message = $"Unable to start: {info.FileName}";
                _context.API.ShowMsg(name, message, string.Empty);
            }
        }

        public void ReloadData()
        {
            IndexPrograms(true);
        }
    }
}
