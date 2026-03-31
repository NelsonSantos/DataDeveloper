using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DataDeveloper.Enums;
using DataDeveloper.Models;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class AppDialogWindow : Window
{
    private Button? _defaultButton;
    private Button? _cancelButton;

    public AppDialogWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        KeyDown += OnKeyDown;
    }

    public AppDialogWindow(AppDialogViewModel viewModel)
        : this()
    {
        Title = viewModel.Title;
        Content = BuildContent(viewModel);
    }

    private void OnButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: DialogResult result })
            Close(result);
    }

    private Button CreateButton(AppDialogButton button)
    {
        var control = new Button
        {
            Content = button.Label,
            Tag = button.Result,
            MinWidth = 96,
            Padding = new Thickness(18, 10),
            Margin = new Thickness(8, 0, 0, 0),
            CornerRadius = new CornerRadius(10),
            Foreground = Brushes.White,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse(button.IsPrimary ? "#FF5F7FE3" : "#FF4A505C")),
            Background = new SolidColorBrush(Color.Parse(button.IsPrimary ? "#FF4D6DD1" : "#FF343A44")),
            IsDefault = button.IsDefault,
            IsCancel = button.IsCancel
        };
        control.Click += OnButtonClick;
        if (button.IsDefault)
            _defaultButton = control;
        if (button.IsCancel)
            _cancelButton = control;
        return control;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        _defaultButton?.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Close((_defaultButton?.Tag as DialogResult?) ?? DialogResult.Ok);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close((_cancelButton?.Tag as DialogResult?) ?? DialogResult.Cancel);
            e.Handled = true;
        }
    }

    private Control BuildContent(AppDialogViewModel viewModel)
    {
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        foreach (var button in viewModel.Buttons)
            buttonsPanel.Children.Add(CreateButton(button));

        var iconBorder = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = viewModel.IconBackground,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = viewModel.IconGlyph,
                FontFamily = FontFamily.Parse("avares://DataDeveloper/Assets/Fonts/fa-solid-900.ttf#Font Awesome 5 Pro Solid"),
                FontSize = 22,
                FontWeight = FontWeight.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            }
        };

        var contentColumn = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = viewModel.Title,
                    FontSize = 20,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#FFF6F7FB"))
                },
                new ScrollViewer
                {
                    MaxHeight = 280,
                    MaxWidth = 440,
                    Content = new TextBlock
                    {
                        Text = viewModel.Message,
                        FontSize = 14,
                        LineHeight = 21,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#FFD6DAE2"))
                    }
                }
            }
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 16,
            Children =
            {
                iconBorder,
                contentColumn
            }
        };
        Grid.SetColumn(contentColumn, 1);

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 18,
            Children =
            {
                headerGrid,
                buttonsPanel
            }
        };
        Grid.SetRow(buttonsPanel, 1);

        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24),
            Background = new SolidColorBrush(Color.Parse("#FF25282E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FF3A404A")),
            BorderThickness = new Thickness(1),
            Child = layout
        };
    }
}
