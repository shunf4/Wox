using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using NLog;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin;

namespace Wox.ViewModel
{
    public class ResultsViewModel : BaseModel
    {
        #region Private Fields

        public ResultCollection Results { get; }

        private readonly Settings _settings;
        private int MaxResults => _settings?.MaxResultsToShow ?? 6;
        private readonly object _collectionLock = new object();

        public object DelayedOpenResultCommandInvocationAndOngoingQueryLock = new object();
        public Tuple<ICommand, object> DelayedOpenResultCommandInvocation = null;

        public bool hasOngoingQuery = false;

        public ResultsViewModel()
        {
            Results = new ResultCollection();
            BindingOperations.EnableCollectionSynchronization(Results, _collectionLock);
            // We don't need to deselect ? Selecting the first item after list changed seems to be enough
            //Results.CollectionChangedPre += (token) =>
            //{
            //    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            //    {
            //        // Deselect currently selected item
            //        // So that ListBox's Selector won't change currently selected index, after we reset the list

            //        // SelectedItem = null;
            //    }));
            //};

            Results.CollectionChangedPost += (token, currQueryEnded) =>
            {
                // Select the first item, after query ended
                if (currQueryEnded)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SelectedIndex = 0;
                    }));
                }

                Tuple<ICommand, object> delayedOpenResultCommandInvocationToExecute = null;
                lock (DelayedOpenResultCommandInvocationAndOngoingQueryLock)
                {
                    if (hasOngoingQuery && currQueryEnded)
                    {
                        hasOngoingQuery = false;
                        if (DelayedOpenResultCommandInvocation != null)
                        {
                            delayedOpenResultCommandInvocationToExecute = DelayedOpenResultCommandInvocation;
                            DelayedOpenResultCommandInvocation = null;
                        }
                    }
                }
                if (delayedOpenResultCommandInvocationToExecute != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        delayedOpenResultCommandInvocationToExecute.Item1.Execute(delayedOpenResultCommandInvocationToExecute.Item2);
                    }));
                }
            };
        }

        public void SetHasOngoingQuery()
        {
            lock (DelayedOpenResultCommandInvocationAndOngoingQueryLock)
            {
                hasOngoingQuery = true;
            }
        }


        public ResultsViewModel(Settings settings) : this()
        {
            _settings = settings;
            _settings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_settings.MaxResultsToShow))
                {
                    OnPropertyChanged(nameof(MaxHeight));
                }
            };
        }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public Func<double> LastCompletedQueryListBoxHeightGetter = null;

        public Action LastCompletedQueryListBoxHeightCacheRequester = null;

        #endregion

        #region Properties

        public int MaxHeight => MaxResults * 50;

        private int _selectedIndex;
        public int SelectedIndex {
            get { return _selectedIndex; }
            set { _selectedIndex = value; }
        }

        public ResultViewModel SelectedItem { get; set; }

        private Thickness _margin;
        public Thickness Margin {
            get { return _margin; }
            set { _margin = value; Logger.WoxInfo($"Margin set to: {Margin}"); }
        }
        public Visibility Visbility { get; set; } = Visibility.Collapsed;

        #endregion

        #region Private Methods

        private int NewIndex(int i)
        {
            var n = Results.Count;
            if (n > 0)
            {
                i = (n + i) % n;
                return i;
            }
            else
            {
                // SelectedIndex returns -1 if selection is empty.
                return -1;
            }
        }

        private int ContainedIndex(int i)
        {
            var n = Results.Count;
            if (n > 0)
            {
                if (i < 0)
                {
                    return 0;
                }
                if (i >= n)
                {
                    return n - 1;
                }
                return i;
            }
            else
            {
                // SelectedIndex returns -1 if selection is empty.
                return -1;
            }
        }


        #endregion

        #region Public Methods

        public void SelectNextResult()
        {
            SelectedIndex = ContainedIndex(SelectedIndex + 1);
        }

        public void SelectPrevResult()
        {
            SelectedIndex = ContainedIndex(SelectedIndex - 1);
        }

        public void SelectNextPage()
        {
            SelectedIndex = ContainedIndex(SelectedIndex + MaxResults);
        }

        public void SelectPrevPage()
        {
            SelectedIndex = ContainedIndex(SelectedIndex - MaxResults);
        }

        public void SelectFirstResult()
        {
            SelectedIndex = NewIndex(0);
        }

        public void Clear()
        {
            lock (_collectionLock)
            {
                Results.RemoveAll();
            }
        }

        public int Count => Results.Count;

        public void AddResults(List<Result> newRawResults, string resultId)
        {
            CancellationToken token = new CancellationTokenSource().Token;
            List<ResultsForUpdate> updates = new List<ResultsForUpdate>()
            {
                new ResultsForUpdate(newRawResults, resultId, token)
            };
            AddResults(updates, true);
        }

        /// <summary>
        /// To avoid deadlock, this method should not called from main thread
        /// </summary>
        public void AddResults(List<ResultsForUpdate> updates, Nullable<bool> overwriteQueryEnded)
        {
            var updatesNotCanceled = updates.Where(u => !u.Token.IsCancellationRequested);

            CancellationToken token;
            try
            {
                token = updatesNotCanceled.Select(u => u.Token).Distinct().First();
            }
            catch (InvalidOperationException)
            {
                // This is common, so WoxError -> WoxDebug
                Logger.WoxDebug("more than one not canceled query result in same batch processing"); // ?
                return;
            }

            bool queryEnded = updatesNotCanceled.Select(u => (!u.Token.IsCancellationRequested) && u.Countdown != null && u.Countdown.IsSet).Aggregate(false, (localQueryEnded, x) => localQueryEnded || x);
            if (overwriteQueryEnded.HasValue)
            {
                queryEnded = overwriteQueryEnded.Value;
            }
            bool shouldRefreshQueryResultsView;
            shouldRefreshQueryResultsView = queryEnded;
            shouldRefreshQueryResultsView = shouldRefreshQueryResultsView || true;

            List<ResultViewModel> newResults = null;

            newResults = NewResults(updatesNotCanceled.ToList(), token);
            Logger.WoxTrace($"newResults {newResults.Count}");

            Action beforeUpdateAdjustMargin = () =>
            {
                if (newResults.Count > 0 && shouldRefreshQueryResultsView)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (LastCompletedQueryListBoxHeightCacheRequester != null)
                        {
                            LastCompletedQueryListBoxHeightCacheRequester.Invoke();
                        }
                    }));
                    //Margin = new Thickness { Top = 8 };
                }
                else if (!shouldRefreshQueryResultsView)
                {
                    double x = LastCompletedQueryListBoxHeightGetter.Invoke();
                    Logger.WoxInfo($"LastCompletedQueryListBoxHeightGetter.Invoke(): {x}");
                    Margin = new Thickness { Top = x };
                }
                else
                {
                    //Margin = new Thickness { Top = 0 };
                }
                // Application.Current.MainWindow.UpdateLayout();
            };

            Action afterUpdateAdjustMargin = () =>
            {
                if (newResults.Count > 0 && shouldRefreshQueryResultsView)
                {
                    Margin = new Thickness { Top = 8 };
                }
                else if (!shouldRefreshQueryResultsView)
                {
                    //double x = LastCompletedQueryListBoxHeightGetter.Invoke();
                    //Logger.WoxInfo($"LastCompletedQueryListBoxHeightGetter.Invoke(): {x}");
                    //Margin = new Thickness { Top = x };
                }
                else
                {
                    Margin = new Thickness { Top = 0 };
                }
            };

            Application.Current.Dispatcher.Invoke(beforeUpdateAdjustMargin);

            // https://stackoverflow.com/questions/14336750
            lock (_collectionLock)
            {
                if (shouldRefreshQueryResultsView)
                {
                    Results.Update(newResults, token, afterUpdateAdjustMargin, queryEnded);
                } else
                {
                    Results.Update(new List<ResultViewModel>(), token, afterUpdateAdjustMargin, queryEnded);
                }

            }

        }

        private List<ResultViewModel> NewResults(List<ResultsForUpdate> updates, CancellationToken token)
        {
            if (token.IsCancellationRequested) { return Results.ToList(); }
            var newResults = Results.ToList();
            if (updates.Count > 0)
            {
                if (token.IsCancellationRequested) { return Results.ToList(); }
                List<Result> resultsFromUpdates = updates.SelectMany(u => u.Results).ToList();

                if (token.IsCancellationRequested) { return Results.ToList(); }
                newResults.RemoveAll(r => updates.Any(u => u.ID == r.Result.PluginID));

                if (token.IsCancellationRequested) { return Results.ToList(); }
                IEnumerable<ResultViewModel> vm = resultsFromUpdates.Select(r => new ResultViewModel(r));
                newResults.AddRange(vm);

                if (token.IsCancellationRequested) { return Results.ToList(); }
                List<ResultViewModel> sorted = newResults.OrderByDescending(r => r.Result.Score).Take(MaxResults * 4).ToList();

                return sorted;
            }
            else
            {
                return Results.ToList();
            }
        }

        #endregion

        #region FormattedText Dependency Property
        public static readonly DependencyProperty FormattedTextProperty = DependencyProperty.RegisterAttached(
            "FormattedText",
            typeof(Inline),
            typeof(ResultsViewModel),
            new PropertyMetadata(null, FormattedTextPropertyChanged));

        public static void SetFormattedText(DependencyObject textBlock, IList<int> value)
        {
            textBlock.SetValue(FormattedTextProperty, value);
        }

        public static Inline GetFormattedText(DependencyObject textBlock)
        {
            return (Inline)textBlock.GetValue(FormattedTextProperty);
        }

        private static void FormattedTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var textBlock = d as TextBlock;
            if (textBlock == null) return;

            var inline = (Inline)e.NewValue;

            textBlock.Inlines.Clear();
            if (inline == null) return;

            textBlock.Inlines.Add(inline);
        }
        #endregion

        public delegate void NotifyCollectionChangedPreEventHandler(CancellationToken token);
        public delegate void NotifyCollectionChangedPostEventHandler(CancellationToken token, bool queryEnded);
        public class ResultCollection : Collection<ResultViewModel>, INotifyCollectionChanged
        {
            public event NotifyCollectionChangedPreEventHandler CollectionChangedPre;
            public event NotifyCollectionChangedEventHandler CollectionChanged;
            public event NotifyCollectionChangedPostEventHandler CollectionChangedPost;

            public void RemoveAll()
            {
                this.Clear();
                if (CollectionChanged != null)
                {
                    CollectionChanged.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }

            public void Update(List<ResultViewModel> newItems, CancellationToken token, Action afterUpdate, bool queryEnded)
            {
                if (token.IsCancellationRequested) { return; }

                this.Clear();
                foreach (var i in newItems)
                {
                    if (token.IsCancellationRequested) { break; }
                    this.Add(i);
                }
                if (CollectionChangedPre != null)
                {
                    CollectionChangedPre.Invoke(token);
                }
                if (CollectionChanged != null)
                {
                    // wpf use directx / double buffered already, so just reset all won't cause ui flickering
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CollectionChanged.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                        if (CollectionChangedPost != null)
                        {
                            // Dispatch again: to make sure CollectionChangedPost is
                            // invoked _after_ CollectionChanged (and its consequences)
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                CollectionChangedPost.Invoke(token, queryEnded);
                            }));
                        }
                        afterUpdate.Invoke();
                    }));
                }
            }
        }
    }
}
