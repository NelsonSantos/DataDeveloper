using System;
using System.Reactive;
using DataDeveloper.Data.Interfaces;
using Dock.Model.Core;
using ReactiveUI;

namespace DataDeveloper.Interfaces;

public interface IAuxFactory : IFactory
{
    ReactiveCommand<IConnectionSettings, Unit> AddNewConnectionCommand { get; }
}