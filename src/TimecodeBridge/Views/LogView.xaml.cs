using System.Collections.Specialized;
using System.Windows.Controls;

namespace TimecodeBridge.Views;

public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();

        // Auto-scroll to the latest log entry
        Loaded += (_, _) =>
        {
            if (LogListView.ItemsSource is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += (_, _) =>
                {
                    if (LogListView.Items.Count > 0)
                    {
                        LogListView.ScrollIntoView(LogListView.Items[LogListView.Items.Count - 1]);
                    }
                };
            }
        };
    }
}
