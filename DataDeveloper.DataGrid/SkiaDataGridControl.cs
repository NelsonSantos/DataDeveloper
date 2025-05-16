using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;

namespace DataDeveloper.DataGrid
{
    public class SkiaDataGridControl : UserControl
    {
        private ScrollViewer _scrollViewer;
        private InnerDataGrid _dataGrid;

        public static readonly StyledProperty<ObservableCollection<string>> HeadersProperty =
            AvaloniaProperty.Register<SkiaDataGridControl, ObservableCollection<string>>(nameof(Headers));

        public static readonly StyledProperty<ObservableCollection<IEnumerable<object>>> DataProperty =
            AvaloniaProperty.Register<SkiaDataGridControl, ObservableCollection<IEnumerable<object>>>(nameof(Data));

        public ObservableCollection<string> Headers
        {
            get => GetValue(HeadersProperty);
            set => SetValue(HeadersProperty, value);
        }

        public ObservableCollection<IEnumerable<object>> Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public SkiaDataGridControl()
        {
            _dataGrid = new InnerDataGrid();
            _scrollViewer = new ScrollViewer
            {
                Content = _dataGrid,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            
            Content = _scrollViewer;
            Background = Brushes.LightGray;
            
            _dataGrid.Bind(InnerDataGrid.HeadersProperty, this.GetBindingObservable(HeadersProperty));
            _dataGrid.Bind(InnerDataGrid.DataProperty, this.GetBindingObservable(DataProperty));
        }
        
        protected override Size MeasureOverride(Size availableSize)
        {
            _dataGrid.Measure(availableSize);
            return _dataGrid.DesiredSize;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _dataGrid.DataContext = this.DataContext;
        }
    }
}
