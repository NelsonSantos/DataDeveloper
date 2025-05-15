// Refactored version: DataGrid using Avalonia's drawing API with ObservableCollection support and reactive updates (via PropertyChanged)

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace MyApp.Controls
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
            
            _dataGrid.Bind(InnerDataGrid.HeadersProperty, this.GetBindingObservable(HeadersProperty));
            _dataGrid.Bind(InnerDataGrid.DataProperty, this.GetBindingObservable(DataProperty));
            


        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _dataGrid.DataContext = this.DataContext;
        }

        private class InnerDataGrid : Control
        {
            public static readonly StyledProperty<ObservableCollection<string>> HeadersProperty =
                AvaloniaProperty.Register<SkiaDataGridControl, ObservableCollection<string>>(nameof(Headers));

            public static readonly StyledProperty<ObservableCollection<IEnumerable<object>>> DataProperty =
                AvaloniaProperty.Register<SkiaDataGridControl, ObservableCollection<IEnumerable<object>>>(nameof(Data));

            public InnerDataGrid()
            {
                this.PropertyChanged += (_, e) =>
                {
                    if (e.Property == HeadersProperty)
                    {
                        if (Headers != null)
                            Headers.CollectionChanged += (_, _) => InvalidateVisual();
                        InvalidateVisual();
                    }
                    else if (e.Property == DataProperty)
                    {
                        if (Data != null)
                            Data.CollectionChanged += (_, _) => InvalidateVisual();
                        
                        InvalidateVisual();
                    }
                };           
            }

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

            private const int DefaultCellWidth = 120;
            private const int CellHeight = 30;
            private const int HeaderHeight = 40;

            public override void Render(DrawingContext context)
            {
                base.Render(context);
                
                if (Headers == null || Data == null)
                    return;

                int columnCount = Headers.Count;
                double[] columnWidths = new double[columnCount];
                for (int i = 0; i < columnCount; i++) columnWidths[i] = DefaultCellWidth;

                var borderPen = new Pen(Brushes.Gray, 1);
                var headerBrush = Brushes.LightGray;
                var backgroundBrush = Brushes.White;
                var textBrush = Brushes.Black;
                var typeface = new Typeface("Arial");

                double x = 0;
                // Draw headers
                for (int col = 0; col < columnCount; col++)
                {
                    double width = columnWidths[col];
                    var rect = new Rect(x, 0, width, HeaderHeight);
                    context.DrawRectangle(headerBrush, borderPen, rect);

                    var formattedText = new FormattedText(Headers[col], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, textBrush);
                    context.DrawText(formattedText, new Point(x + 5, 10));

                    x += width;
                }

                // Draw rows
                for (int row = 0; row < Data.Count; row++)
                {
                    x = 0;
                    var rowItems = Data[row];
                    var rowArray = rowItems is object[] arr ? arr : new List<object>(rowItems).ToArray();

                    for (int col = 0; col < columnCount; col++)
                    {
                        double width = columnWidths[col];
                        double y = HeaderHeight + row * CellHeight;
                        var rect = new Rect(x, y, width, CellHeight);
                        context.DrawRectangle(backgroundBrush, borderPen, rect);

                        if (col < rowArray.Length)
                        {
                            string value = rowArray[col]?.ToString() ?? "";
                            var formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, textBrush);
                            context.DrawText(formattedText, new Point(x + 5, y + 8));
                        }
                        x += width;
                    }
                }

                try
                {
                    Width = columnCount * DefaultCellWidth;
                    Height = HeaderHeight + Data.Count * CellHeight;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}



/*
        public override void Render(DrawingContext context)
   {
       base.Render(context);

       if (Headers == null || Data == null)
           return;

       var headerList = Headers.ToArray();
       int columnCount = headerList.Count();
       double[] columnWidths = new double[columnCount];
       for (int i = 0; i < columnCount; i++) columnWidths[i] = DefaultCellWidth;

       double yOffset = -_scrollY;
       double x = 0;

       var borderPen = new Pen(Brushes.Gray, 1);
       var headerBrush = Brushes.LightGray;
       var backgroundBrush = Brushes.White;
       var textBrush = Brushes.Black;
       var typeface = new Typeface("Arial");

       // Draw headers
       for (int col = 0; col < columnCount; col++)
       {
           double width = columnWidths[col];
           var rect = new Rect(x, yOffset, width, HeaderHeight);
           context.DrawRectangle(headerBrush, borderPen, rect);

           var formattedText = new FormattedText(headerList[col], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, textBrush);
           context.DrawText(formattedText, new Point(x + 5, yOffset + 10));

           x += width;
       }

       var dataList = Data.ToArray();
       // Draw rows
       for (int row = 0; row < dataList.Count(); row++)
       {
           x = 0;
           var dataRow = dataList[row].ToArray();
           for (int col = 0; col < columnCount; col++)
           {
               double width = columnWidths[col];
               double y = yOffset + HeaderHeight + row * CellHeight;
               var rect = new Rect(x, y, width, CellHeight);
               context.DrawRectangle(backgroundBrush, borderPen, rect);

               if (col < dataList[row].Count())
               {
                   string value = dataRow[col]?.ToString() ?? "";
                   var formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 14, textBrush);
                   context.DrawText(formattedText, new Point(x + 5, y + 8));
               }
               x += width;
           }
       }
   }

 */