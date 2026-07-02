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

namespace DataDeveloper.ViewModels;

public class ManageConnectionGroupsViewModel : ViewModelBase
{
    private readonly IConnectionGroupRepository _connectionGroupRepository;
    private readonly IDialogService _dialogService;

    public ManageConnectionGroupsViewModel(IConnectionGroupRepository connectionGroupRepository, IDialogService dialogService)
    {
        _connectionGroupRepository = connectionGroupRepository;
        _dialogService = dialogService;

        Groups = new ObservableCollection<ConnectionGroup>(_connectionGroupRepository.LoadAll());

        AddCommand = ReactiveCommand.Create(AddGroup);
        RenameCommand = ReactiveCommand.Create<ConnectionGroup>(RenameGroup);
        DeleteCommand = ReactiveCommand.CreateFromTask<ConnectionGroup>(DeleteGroupAsync);
        CloseCommand = ReactiveCommand.Create<StyledElement>(Close);
    }

    public ObservableCollection<ConnectionGroup> Groups { get; }

    public ReactiveCommand<Unit, Unit> AddCommand { get; }
    public ReactiveCommand<ConnectionGroup, Unit> RenameCommand { get; }
    public ReactiveCommand<ConnectionGroup, Unit> DeleteCommand { get; }
    public ReactiveCommand<StyledElement, Unit> CloseCommand { get; }

    private void AddGroup()
    {
        var group = new ConnectionGroup { Id = Guid.NewGuid(), Name = "New group" };
        _connectionGroupRepository.Save(group);
        Groups.Add(group);
    }

    private void RenameGroup(ConnectionGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
            return;

        group.Name = group.Name.Trim();
        _connectionGroupRepository.Save(group);
    }

    private async Task DeleteGroupAsync(ConnectionGroup group)
    {
        var result = await _dialogService.ShowDialogAsync(
            $"Delete group \"{group.Name}\"? Connections in this group will become ungrouped.",
            "Manage groups...",
            DialogButtons.YesNo,
            DialogIcon.Question);

        if (result != DialogResult.Yes)
            return;

        _connectionGroupRepository.Delete(group.Id);
        Groups.Remove(group);
    }

    private void Close(StyledElement element)
    {
        var window = element.GetParentWindow();
        window?.Close();
    }
}
