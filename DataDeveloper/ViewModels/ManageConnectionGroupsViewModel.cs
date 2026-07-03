using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using DataDeveloper.Core;
using DataDeveloper.Data.Models;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public sealed class ConnectionGroupRow : ReactiveObject
{
    public ConnectionGroupRow(ConnectionGroup group)
    {
        Group = group;
        DraftName = group.Name;
    }

    public ConnectionGroup Group { get; }
    [Reactive] public bool IsEditing { get; set; }
    [Reactive] public string DraftName { get; set; } = string.Empty;
}

public class ManageConnectionGroupsViewModel : ViewModelBase
{
    private readonly IConnectionGroupRepository _connectionGroupRepository;
    private readonly IDialogService _dialogService;

    public ManageConnectionGroupsViewModel(IConnectionGroupRepository connectionGroupRepository, IDialogService dialogService)
    {
        _connectionGroupRepository = connectionGroupRepository;
        _dialogService = dialogService;

        Groups = new ObservableCollection<ConnectionGroupRow>();
        foreach (var group in _connectionGroupRepository.LoadAll())
            Groups.Add(new ConnectionGroupRow(group));

        AddCommand = ReactiveCommand.Create(AddGroup);
        EditCommand = ReactiveCommand.Create<ConnectionGroupRow>(BeginEdit);
        ConfirmCommand = ReactiveCommand.Create<ConnectionGroupRow>(ConfirmEdit);
        DeleteCommand = ReactiveCommand.CreateFromTask<ConnectionGroupRow>(DeleteGroupAsync);
        CloseCommand = ReactiveCommand.Create<StyledElement>(Close);
    }

    public ObservableCollection<ConnectionGroupRow> Groups { get; }

    public ReactiveCommand<Unit, Unit> AddCommand { get; }
    public ReactiveCommand<ConnectionGroupRow, Unit> EditCommand { get; }
    public ReactiveCommand<ConnectionGroupRow, Unit> ConfirmCommand { get; }
    public ReactiveCommand<ConnectionGroupRow, Unit> DeleteCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CloseCommand { get; }

    private void AddGroup()
    {
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = string.Empty };
        Groups.Add(new ConnectionGroupRow(group) { IsEditing = true, DraftName = string.Empty });
    }

    private void BeginEdit(ConnectionGroupRow row)
    {
        row.DraftName = row.Group.Name;
        row.IsEditing = true;
    }

    private void ConfirmEdit(ConnectionGroupRow row)
    {
        var trimmed = row.DraftName.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        row.Group.Name = trimmed;
        _connectionGroupRepository.Save(row.Group);
        row.IsEditing = false;
    }

    private async Task DeleteGroupAsync(ConnectionGroupRow row)
    {
        var result = await _dialogService.ShowDialogAsync(
            $"Delete group \"{row.Group.Name}\"? Connections in this group will become ungrouped.",
            "Manage groups...",
            DialogButtons.YesNo,
            DialogIcon.Question);

        if (result != DialogResult.Yes)
            return;

        _connectionGroupRepository.Delete(row.Group.Id);
        Groups.Remove(row);
    }

    private void Close(StyledElement element)
    {
        var window = element.GetParentWindow();
        window?.Close();
    }
}
