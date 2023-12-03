using System.Runtime.Remoting.Contexts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Wox
{
    [Synchronization]
    public partial class ResultListBox
    {
        private Point _lastpos;
        private ListBoxItem curItem = null;
        public ResultListBox()
        {
            InitializeComponent();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                ScrollIntoView(e.AddedItems[0]);
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            curItem = (ListBoxItem)sender;
            var p = e.GetPosition((IInputElement)sender);
            _lastpos = p;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition((IInputElement)sender);
            if (_lastpos != p)
            {
                // shunf4: Don't do that.
                // ((ListBoxItem)sender).IsSelected = true;
            }
        }

        private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //if (curItem != null)
            //{
            //    curItem.IsSelected = true;
            //}
            if (curItem != null)
            {
                if (curItem.IsSelected)
                {
                    return;
                }
                else
                {
                    curItem.IsSelected = true;
                    e.Handled = true;
                }
            }
        }
    }
}
