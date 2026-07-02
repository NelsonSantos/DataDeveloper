using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DataDeveloper.Data.Models;
using DataDeveloper.ViewModels;

namespace DataDeveloper.Views;

public partial class ManageConnectionGroupsDialog : Window
{
    public ManageConnectionGroupsDialog()
    {
        InitializeComponent();
    }

    private void GroupName_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: ConnectionGroup group } ||
            DataContext is not ManageConnectionGroupsViewModel viewModel)
            return;

        viewModel.RenameCommand.Execute(group).Subscribe();
    }
}
