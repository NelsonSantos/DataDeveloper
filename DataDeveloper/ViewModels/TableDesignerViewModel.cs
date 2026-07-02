using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataDeveloper.Core;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services.TableDesigner;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace DataDeveloper.ViewModels;

public sealed class TableDesignerViewModel : ViewModelBase
{
    private readonly IConnectionSettings _connectionSettings;
    private readonly Func<string, Task<bool>> _applyScript;
    private readonly IDialogService _dialogService;
    private readonly ISchemaExplorer? _schemaExplorer;
    private readonly TableDefinition? _originalDefinition;

    public TableDesignerViewModel(
        IConnectionSettings connectionSettings,
        Func<string, Task<bool>> applyScript,
        IDialogService dialogService,
        ISchemaExplorer? schemaExplorer = null)
        : this(connectionSettings, applyScript, dialogService, schemaExplorer, originalDefinition: null)
    {
    }

    /// <summary>
    /// Creates the designer pre-populated for editing an existing table. <paramref name="originalDefinition"/>
    /// is the table's current state as loaded from the database (see <c>TableDefinitionLoader</c>); it is
    /// kept immutable and diffed against the editable grid state to produce an ALTER TABLE script instead
    /// of a CREATE TABLE script.
    /// </summary>
    public static TableDesignerViewModel CreateForEdit(
        IConnectionSettings connectionSettings,
        TableDefinition originalDefinition,
        Func<string, Task<bool>> applyScript,
        IDialogService dialogService,
        ISchemaExplorer? schemaExplorer = null)
    {
        return new TableDesignerViewModel(connectionSettings, applyScript, dialogService, schemaExplorer, originalDefinition);
    }

    private TableDesignerViewModel(
        IConnectionSettings connectionSettings,
        Func<string, Task<bool>> applyScript,
        IDialogService dialogService,
        ISchemaExplorer? schemaExplorer,
        TableDefinition? originalDefinition)
    {
        _connectionSettings = connectionSettings;
        _applyScript = applyScript;
        _dialogService = dialogService;
        _schemaExplorer = schemaExplorer;
        _originalDefinition = originalDefinition;
        IsEditMode = originalDefinition is not null;

        AvailableDataTypes = new ObservableCollection<ProviderDataTypeOption>(
            ProviderDataTypeCatalog.GetDataTypes(connectionSettings.DatabaseType));
        LoadReferenceTables();

        AddColumnCommand = ReactiveCommand.Create(AddColumn);
        RemoveColumnCommand = ReactiveCommand.Create<TableDesignerColumnViewModel>(column => Columns.Remove(column));
        AddForeignKeyCommand = ReactiveCommand.Create(AddForeignKey);
        RemoveForeignKeyCommand = ReactiveCommand.Create<TableDesignerForeignKeyViewModel>(foreignKey => ForeignKeys.Remove(foreignKey));
        AddIndexCommand = ReactiveCommand.Create(AddIndex);
        RemoveIndexCommand = ReactiveCommand.Create<TableDesignerIndexViewModel>(index => Indexes.Remove(index));
        ApplyCommand = ReactiveCommand.CreateFromTask<Window>(ApplyAsync);

        Columns.CollectionChanged += ItemsOnCollectionChanged;
        ForeignKeys.CollectionChanged += ItemsOnCollectionChanged;
        Indexes.CollectionChanged += ItemsOnCollectionChanged;

        this.WhenAnyValue(vm => vm.SchemaName, vm => vm.TableName, vm => vm.PrimaryKeyName).Subscribe(_ => RefreshGeneratedSql());

        if (originalDefinition is not null)
            LoadFromDefinition(originalDefinition);
        else
            AddColumn();

        RefreshGeneratedSql();
    }

    public DatabaseType DatabaseType => _connectionSettings.DatabaseType;
    public bool SupportsSchemaName => DatabaseType != DatabaseType.SqLite;
    public bool IsEditMode { get; }
    /// <summary>
    /// Schema moves stay out of scope even in edit mode (a materially different, riskier
    /// operation than a same-schema rename that hasn't been reviewed per-provider); table
    /// rename is in scope, so the table name field is always editable.
    /// </summary>
    public bool CanEditSchemaName => !IsEditMode;
    public ObservableCollection<ProviderDataTypeOption> AvailableDataTypes { get; }
    public ObservableCollection<string> AvailableColumnNames { get; } = new();
    public ObservableCollection<TableDesignerReferenceTableOption> AvailableReferenceTables { get; } = new();
    public ObservableCollection<string> ValidationMessages { get; } = new();
    public ObservableCollection<TableDesignerColumnViewModel> Columns { get; } = new();
    public ObservableCollection<TableDesignerForeignKeyViewModel> ForeignKeys { get; } = new();
    public ObservableCollection<TableDesignerIndexViewModel> Indexes { get; } = new();
    public string[] ReferentialActions { get; } = ["", "no action", "cascade", "set null", "set default", "restrict"];

    [Reactive] public string SchemaName { get; set; } = string.Empty;
    [Reactive] public string TableName { get; set; } = "NewTable";
    [Reactive] public string PrimaryKeyName { get; set; } = string.Empty;
    [Reactive] public string GeneratedSql { get; set; } = string.Empty;
    [Reactive] public string ValidationMessage { get; set; } = string.Empty;
    [Reactive] public bool HasValidationMessages { get; set; }

    public ReactiveCommand<Unit, Unit> AddColumnCommand { get; }
    public ReactiveCommand<TableDesignerColumnViewModel, Unit> RemoveColumnCommand { get; }
    public ReactiveCommand<Unit, Unit> AddForeignKeyCommand { get; }
    public ReactiveCommand<TableDesignerForeignKeyViewModel, Unit> RemoveForeignKeyCommand { get; }
    public ReactiveCommand<Unit, Unit> AddIndexCommand { get; }
    public ReactiveCommand<TableDesignerIndexViewModel, Unit> RemoveIndexCommand { get; }
    public ReactiveCommand<Window, Unit> ApplyCommand { get; }

    private void LoadFromDefinition(TableDefinition definition)
    {
        SchemaName = definition.SchemaName;
        TableName = definition.TableName;
        PrimaryKeyName = definition.PrimaryKey.Name;

        foreach (var column in definition.Columns)
        {
            var dataType = AvailableDataTypes.FirstOrDefault(type =>
                string.Equals(type.Name, column.DataType, StringComparison.OrdinalIgnoreCase))
                ?? ProviderDataTypeCatalog.GetDefaultDataType(DatabaseType);

            var columnViewModel = new TableDesignerColumnViewModel(AvailableDataTypes, dataType, isExistingColumn: true)
            {
                Name = column.Name,
                OriginalName = column.OriginalName ?? column.Name,
                Length = column.Length,
                Precision = column.Precision,
                Scale = column.Scale,
                IsNullable = column.IsNullable,
                IsIdentity = column.IsIdentity,
                IsPrimaryKey = definition.PrimaryKey.ColumnNames.Contains(column.Name, StringComparer.OrdinalIgnoreCase),
                DefaultValue = column.DefaultValue
            };

            foreach (var (key, value) in column.ProviderOptions)
                columnViewModel.ProviderOptions[key] = value;

            Columns.Add(columnViewModel);
        }

        foreach (var foreignKey in definition.ForeignKeys)
        {
            var foreignKeyViewModel = new TableDesignerForeignKeyViewModel(
                AvailableColumnNames, AvailableReferenceTables, LoadReferenceColumnsAsync, RefreshGeneratedSql, DatabaseType != DatabaseType.Oracle)
            {
                Name = foreignKey.Name,
                ReferencedSchemaName = foreignKey.ReferencedSchemaName,
                ReferencedTableName = foreignKey.ReferencedTableName,
                OnDeleteAction = foreignKey.OnDeleteAction,
                OnUpdateAction = foreignKey.OnUpdateAction
            };

            // Match by table name only: every provider's GetTableStatement() returns unqualified
            // names for the reference-table catalog (see SplitObjectName/LoadReferenceTables),
            // so option.SchemaName is always empty even when the loaded FK's ReferencedSchemaName
            // (from the live database) is populated (e.g. SQL Server "dbo", Postgres "public").
            // Requiring schema equality here would never match and leave the combo empty.
            var referencedTable = AvailableReferenceTables.FirstOrDefault(option =>
                string.Equals(option.TableName, foreignKey.ReferencedTableName, StringComparison.OrdinalIgnoreCase));

            if (referencedTable is not null)
                foreignKeyViewModel.SelectedReferencedTable = referencedTable;

            foreignKeyViewModel.ColumnMappings.Clear();
            for (var index = 0; index < foreignKey.ColumnNames.Count; index++)
            {
                foreignKeyViewModel.ColumnMappings.Add(new TableDesignerForeignKeyColumnMappingViewModel(
                    AvailableColumnNames, foreignKeyViewModel.AvailableReferencedColumnNames)
                {
                    LocalColumnName = foreignKey.ColumnNames[index],
                    ReferencedColumnName = index < foreignKey.ReferencedColumnNames.Count
                        ? foreignKey.ReferencedColumnNames[index]
                        : string.Empty
                });
            }

            ForeignKeys.Add(foreignKeyViewModel);
        }

        foreach (var index in definition.Indexes)
        {
            var indexViewModel = new TableDesignerIndexViewModel(AvailableColumnNames, RefreshGeneratedSql, DatabaseType)
            {
                Name = index.Name,
                IsUnique = index.IsUnique,
                IsClustered = index.ProviderOptions.TryGetValue("clustered", out var clustered) && string.Equals(clustered, "true", StringComparison.OrdinalIgnoreCase),
                FillFactor = index.ProviderOptions.GetValueOrDefault("fillfactor", string.Empty),
                IndexMethod = index.ProviderOptions.GetValueOrDefault("using", string.Empty),
                WherePredicate = index.ProviderOptions.GetValueOrDefault("where", string.Empty)
            };

            indexViewModel.Columns.Clear();
            foreach (var indexColumn in index.Columns)
            {
                indexViewModel.Columns.Add(new TableDesignerIndexColumnViewModel(AvailableColumnNames)
                {
                    ColumnName = indexColumn.Name,
                    Descending = indexColumn.Descending,
                    PrefixLength = indexColumn.ProviderOptions.GetValueOrDefault("prefix_length", string.Empty)
                });
            }

            Indexes.Add(indexViewModel);
        }
    }

    private void AddColumn()
    {
        var dataType = ProviderDataTypeCatalog.GetDefaultDataType(DatabaseType);
        var column = new TableDesignerColumnViewModel(AvailableDataTypes, dataType)
        {
            Name = Columns.Count == 0 ? "Id" : $"Column{Columns.Count + 1}",
            IsNullable = Columns.Count != 0,
            IsPrimaryKey = Columns.Count == 0,
            IsIdentity = Columns.Count == 0 && dataType.SupportsIdentity
        };

        Columns.Add(column);
    }

    private void AddForeignKey()
    {
        ForeignKeys.Add(new TableDesignerForeignKeyViewModel
            (AvailableColumnNames, AvailableReferenceTables, LoadReferenceColumnsAsync, RefreshGeneratedSql, DatabaseType != DatabaseType.Oracle)
        {
            Name = BuildUniqueDefaultName("FK", ForeignKeys.Select(foreignKey => foreignKey.Name)),
            OnDeleteAction = "",
            OnUpdateAction = ""
        });
    }

    private void AddIndex()
    {
        Indexes.Add(new TableDesignerIndexViewModel
            (AvailableColumnNames, RefreshGeneratedSql, DatabaseType)
        {
            Name = BuildUniqueDefaultName("IX", Indexes.Select(index => index.Name))
        });
    }

    private string BuildDefaultName(string prefix)
    {
        return string.IsNullOrWhiteSpace(TableName)
            ? prefix
            : $"{prefix}_{TableName}";
    }

    private string BuildUniqueDefaultName(string prefix, IEnumerable<string> existingNames)
    {
        var baseName = BuildDefaultName(prefix);
        var names = existingNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!names.Contains(baseName))
            return baseName;

        for (var index = 2; ; index++)
        {
            var name = $"{baseName}_{index}";
            if (!names.Contains(name))
                return name;
        }
    }


    private async Task ApplyAsync(Window window)
    {
        RefreshGeneratedSql();
        if (string.IsNullOrWhiteSpace(GeneratedSql))
        {
            await _dialogService.ShowMessageAsync(
                string.IsNullOrWhiteSpace(ValidationMessage) ? "No valid table script was generated." : ValidationMessage,
                "Table designer");
            return;
        }

        if (IsEditMode)
        {
            var destructiveChanges = BuildDestructiveChangeSummary(BuildDefinition(new List<string>()));
            if (destructiveChanges.Count > 0)
            {
                var confirmDestructive = await _dialogService.ShowDialogAsync(
                    "This will apply the following destructive changes:" + Environment.NewLine +
                    string.Join(Environment.NewLine, destructiveChanges.Select(change => "- " + change)),
                    "Confirm destructive changes",
                    DialogButtons.YesNo,
                    DialogIcon.Warning);

                if (confirmDestructive != DialogResult.Yes)
                    return;
            }
        }

        var result = await _dialogService.ShowDialogAsync(
            $"Apply this table script to {_connectionSettings.Name}?",
            IsEditMode ? "Edit table" : "Create table",
            DialogButtons.YesNo,
            DialogIcon.Question);

        if (result != DialogResult.Yes)
            return;

        if (await _applyScript(GeneratedSql))
            window.Close(true);
    }

    private List<string> BuildDestructiveChangeSummary(TableDefinition current)
    {
        if (_originalDefinition is null)
            return [];

        var summary = new List<string>();
        var currentColumnNames = current.Columns.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentColumnOriginalNames = current.Columns
            .Where(column => !string.IsNullOrEmpty(column.OriginalName))
            .Select(column => column.OriginalName!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var column in _originalDefinition.Columns.Where(column =>
                     !currentColumnNames.Contains(column.Name) && !currentColumnOriginalNames.Contains(column.Name)))
        {
            summary.Add($"Drop column '{column.Name}'");
        }

        if (_originalDefinition.PrimaryKey.ColumnNames.Count > 0 &&
            !_originalDefinition.PrimaryKey.ColumnNames.SequenceEqual(current.PrimaryKey.ColumnNames, StringComparer.OrdinalIgnoreCase))
        {
            summary.Add($"Drop primary key '{_originalDefinition.PrimaryKey.Name}'");
        }

        var currentForeignKeyNames = current.ForeignKeys.Select(foreignKey => foreignKey.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var foreignKey in _originalDefinition.ForeignKeys.Where(foreignKey => !currentForeignKeyNames.Contains(foreignKey.Name)))
            summary.Add($"Drop foreign key '{foreignKey.Name}'");

        var currentIndexNames = current.Indexes.Select(index => index.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var index in _originalDefinition.Indexes.Where(index => !currentIndexNames.Contains(index.Name)))
            summary.Add($"Drop index '{index.Name}'");

        return summary;
    }

    private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged -= ItemOnPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged += ItemOnPropertyChanged;
        }

        RefreshAvailableColumnNames();
        RefreshGeneratedSql();
    }

    private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is TableDesignerColumnViewModel && e.PropertyName == nameof(TableDesignerColumnViewModel.Name))
            RefreshAvailableColumnNames();

        RefreshGeneratedSql();
    }

    private void RefreshGeneratedSql()
    {
        ValidationMessages.Clear();
        try
        {
            var table = BuildDefinition(ValidationMessages);

            if (IsEditMode)
            {
                if (DatabaseType == DatabaseType.SqLite)
                    AddSqLiteUnsupportedDiffWarnings(_originalDefinition!, table, ValidationMessages);

                GeneratedSql = TableDdlScriptBuilder.BuildAlterTableScript(DatabaseType, _originalDefinition!, table);
            }
            else
            {
                GeneratedSql = TableDdlScriptBuilder.BuildCreateTableScript(DatabaseType, table);
            }
        }
        catch (Exception ex)
        {
            GeneratedSql = string.Empty;
            ValidationMessages.Add(ex.Message);
        }

        ValidationMessage = string.Join(Environment.NewLine, ValidationMessages);
        HasValidationMessages = ValidationMessages.Count > 0;
    }

    private static void AddSqLiteUnsupportedDiffWarnings(TableDefinition original, TableDefinition current, ICollection<string> messages)
    {
        var currentColumnNames = current.Columns.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (original.Columns.Any(column => !currentColumnNames.Contains(column.Name)))
        {
            messages.Add(
                "SQLite does not support dropping columns through ALTER TABLE; this change requires a table rebuild and is not applied.");
        }

        if (!original.PrimaryKey.ColumnNames.SequenceEqual(current.PrimaryKey.ColumnNames, StringComparer.OrdinalIgnoreCase))
        {
            messages.Add(
                "SQLite does not support changing the primary key through ALTER TABLE; this change requires a table rebuild and is not applied.");
        }

        var currentForeignKeyNames = current.ForeignKeys.Select(foreignKey => foreignKey.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (original.ForeignKeys.Any(foreignKey => !currentForeignKeyNames.Contains(foreignKey.Name)) ||
            current.ForeignKeys.Any(foreignKey => !original.ForeignKeys.Any(originalForeignKey =>
                string.Equals(originalForeignKey.Name, foreignKey.Name, StringComparison.OrdinalIgnoreCase))))
        {
            messages.Add(
                "SQLite does not support adding or dropping foreign keys through ALTER TABLE; this change requires a table rebuild and is not applied.");
        }

        var originalIndexNames = original.Indexes.Select(index => index.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentIndexNames = current.Indexes.Select(index => index.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!originalIndexNames.SetEquals(currentIndexNames))
        {
            messages.Add(
                "SQLite index changes are not applied by Edit table in this version; add or drop indexes with a separate script.");
        }
    }

    private TableDefinition BuildDefinition(ICollection<string> messages)
    {
        var table = new TableDefinition
        {
            SchemaName = Clean(SchemaName),
            TableName = Clean(TableName)
        };

        foreach (var column in Columns)
        {
            var columnDefinition = new TableColumnDefinition
            {
                OriginalName = column.OriginalName,
                Name = Clean(column.Name),
                DataType = column.SelectedDataType?.Name ?? string.Empty,
                Length = column.SupportsLength ? NormalizePositive(column.Length) : null,
                Precision = column.SupportsPrecision ? NormalizePositive(column.Precision) : null,
                Scale = column.SupportsScale ? NormalizePositive(column.Scale) : null,
                IsNullable = column.IsNullable && !column.IsPrimaryKey,
                IsIdentity = column.IsIdentity && column.SupportsIdentity,
                DefaultValue = Clean(column.DefaultValue)
            };

            foreach (var (key, value) in column.ProviderOptions)
                columnDefinition.ProviderOptions[key] = value;

            table.Columns.Add(columnDefinition);
        }

        foreach (var column in Columns.Where(column => column.IsPrimaryKey))
            table.PrimaryKey.ColumnNames.Add(Clean(column.Name));

        if (table.PrimaryKey.ColumnNames.Count > 0)
            table.PrimaryKey.Name = string.IsNullOrWhiteSpace(PrimaryKeyName) ? BuildDefaultName("PK") : Clean(PrimaryKeyName);

        var foreignKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var foreignKey in ForeignKeys)
        {
            var definition = BuildForeignKeyDefinition(foreignKey);
            if (string.IsNullOrWhiteSpace(definition.Name))
                definition.Name = BuildUniqueDefaultName("FK", foreignKeyNames);

            if (!TryValidateForeignKeyDefinition(table, definition, messages))
                continue;

            if (!foreignKeyNames.Add(definition.Name))
            {
                messages.Add($"Foreign key '{definition.Name}' has a duplicated name.");
                continue;
            }

            table.ForeignKeys.Add(definition);
        }

        var indexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var index in Indexes)
        {
            var definition = new TableIndexDefinition
            {
                Name = string.IsNullOrWhiteSpace(index.Name) ? BuildUniqueDefaultName("IX", indexNames) : Clean(index.Name),
                IsUnique = index.IsUnique
            };

            if (DatabaseType == DatabaseType.SqlServer)
            {
                if (index.IsClustered)
                    definition.ProviderOptions["clustered"] = "true";
                if (int.TryParse(index.FillFactor, out var fillFactor) && fillFactor is > 0 and <= 100)
                    definition.ProviderOptions["fillfactor"] = fillFactor.ToString();
            }

            if (DatabaseType == DatabaseType.PostgresSql)
            {
                if (!string.IsNullOrWhiteSpace(index.IndexMethod) && !string.Equals(index.IndexMethod, "btree", StringComparison.OrdinalIgnoreCase))
                    definition.ProviderOptions["using"] = index.IndexMethod.Trim();
                if (!string.IsNullOrWhiteSpace(index.WherePredicate))
                    definition.ProviderOptions["where"] = index.WherePredicate.Trim();
            }

            foreach (var column in ReadIndexColumns(index))
            {
                var indexColumn = new TableIndexColumnDefinition
                {
                    Name = column.Name,
                    Descending = column.Descending
                };

                if (DatabaseType == DatabaseType.MySql)
                {
                    var matchingColumn = index.Columns.FirstOrDefault(candidate =>
                        string.Equals(Clean(candidate.ColumnName), column.Name, StringComparison.OrdinalIgnoreCase));

                    if (matchingColumn is not null && int.TryParse(matchingColumn.PrefixLength, out var prefixLength) && prefixLength > 0)
                        indexColumn.ProviderOptions["prefix_length"] = prefixLength.ToString();
                }

                definition.Columns.Add(indexColumn);
            }

            if (!TryValidateIndexDefinition(table, definition, messages))
                continue;

            if (!indexNames.Add(definition.Name))
            {
                messages.Add($"Index '{definition.Name}' has a duplicated name.");
                continue;
            }

            table.Indexes.Add(definition);
        }

        AddTableWarnings(table, messages);
        return table;
    }

    private static TableForeignKeyDefinition BuildForeignKeyDefinition(TableDesignerForeignKeyViewModel foreignKey)
    {
        var referencedTable = foreignKey.SelectedReferencedTable;
        var definition = new TableForeignKeyDefinition
        {
            Name = Clean(foreignKey.Name),
            ReferencedSchemaName = string.IsNullOrWhiteSpace(foreignKey.ReferencedSchemaName)
                ? referencedTable?.SchemaName ?? string.Empty
                : Clean(foreignKey.ReferencedSchemaName),
            ReferencedTableName = referencedTable?.TableName ?? Clean(foreignKey.ReferencedTableName),
            OnDeleteAction = Clean(foreignKey.OnDeleteAction),
            OnUpdateAction = Clean(foreignKey.OnUpdateAction)
        };
        definition.ColumnNames.AddRange(ReadForeignKeyColumns(foreignKey));
        definition.ReferencedColumnNames.AddRange(ReadForeignKeyReferencedColumns(foreignKey));
        return definition;
    }

    private void LoadReferenceTables()
    {
        if (_schemaExplorer is null)
            return;

        var tablesFolder = _schemaExplorer.RootConnections
            .FirstOrDefault()
            ?.Children
            .FirstOrDefault(child => child.NodeType == NodeType.Tables);

        if (tablesFolder is null)
            return;

        foreach (var tableNode in tablesFolder.Children.Where(child => child.NodeType == NodeType.Table))
        {
            var (schemaName, tableName) = SplitObjectName(tableNode.Name);
            AvailableReferenceTables.Add(new TableDesignerReferenceTableOption(tableNode.Name, schemaName, tableName, tableNode));
        }
    }

    private async Task<IReadOnlyList<string>> LoadReferenceColumnsAsync(TableDesignerReferenceTableOption? table)
    {
        if (table?.Node is null || _schemaExplorer is null)
            return [];

        var columnsFolder = table.Node.Children.FirstOrDefault(child => child.NodeType == NodeType.Columns);
        if (columnsFolder is null)
            return [];

        if (columnsFolder.CanLoad)
            await _schemaExplorer.LoadTableColumnsAsync(columnsFolder);

        return columnsFolder.Children
            .Where(child => child.NodeType == NodeType.Column)
            .Select(child => child.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
    }

    private static (string SchemaName, string TableName) SplitObjectName(string objectName)
    {
        var parts = objectName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 1
            ? (string.Join(".", parts.Take(parts.Length - 1)), parts[^1])
            : (string.Empty, objectName);
    }

    private static bool TryValidateForeignKeyDefinition(
        TableDefinition table,
        TableForeignKeyDefinition foreignKey,
        ICollection<string> messages)
    {
        if (IsEmptyForeignKey(foreignKey))
            return false;

        var columnNames = GetColumnNames(table);
        var foreignKeyName = string.IsNullOrWhiteSpace(foreignKey.Name) ? "Foreign key" : $"Foreign key '{foreignKey.Name}'";
        var warnings = new List<string>();

        if (foreignKey.ColumnNames.Count == 0)
            warnings.Add($"{foreignKeyName} must have at least one local column.");

        foreach (var columnName in foreignKey.ColumnNames)
        {
            if (!columnNames.Contains(columnName))
                warnings.Add($"{foreignKeyName} uses unknown local column '{columnName}'.");
        }

        if (string.IsNullOrWhiteSpace(foreignKey.ReferencedTableName))
            warnings.Add($"{foreignKeyName} must have a referenced table.");

        if (foreignKey.ReferencedColumnNames.Count == 0)
            warnings.Add($"{foreignKeyName} must have at least one referenced column.");

        if (foreignKey.ColumnNames.Count > 0 &&
            foreignKey.ReferencedColumnNames.Count > 0 &&
            foreignKey.ColumnNames.Count != foreignKey.ReferencedColumnNames.Count)
        {
            warnings.Add($"{foreignKeyName} must have the same number of local and referenced columns.");
        }

        var duplicatedLocalColumn = FindDuplicatedName(foreignKey.ColumnNames);
        if (!string.IsNullOrWhiteSpace(duplicatedLocalColumn))
            warnings.Add($"{foreignKeyName} uses local column '{duplicatedLocalColumn}' more than once.");

        var duplicatedReferencedColumn = FindDuplicatedName(foreignKey.ReferencedColumnNames);
        if (!string.IsNullOrWhiteSpace(duplicatedReferencedColumn))
            warnings.Add($"{foreignKeyName} uses referenced column '{duplicatedReferencedColumn}' more than once.");

        foreach (var warning in warnings)
            messages.Add(warning);

        return warnings.Count == 0;
    }

    private static bool TryValidateIndexDefinition(
        TableDefinition table,
        TableIndexDefinition index,
        ICollection<string> messages)
    {
        if (IsEmptyIndex(index))
            return false;

        var columnNames = GetColumnNames(table);
        var indexName = string.IsNullOrWhiteSpace(index.Name) ? "Index" : $"Index '{index.Name}'";
        var warnings = new List<string>();

        if (index.Columns.Count == 0)
            warnings.Add($"{indexName} must have at least one column.");

        foreach (var column in index.Columns)
        {
            if (!columnNames.Contains(column.Name))
                warnings.Add($"{indexName} uses unknown column '{column.Name}'.");
        }

        var duplicatedColumn = FindDuplicatedName(index.Columns.Select(column => column.Name));
        if (!string.IsNullOrWhiteSpace(duplicatedColumn))
            warnings.Add($"{indexName} uses column '{duplicatedColumn}' more than once.");

        foreach (var warning in warnings)
            messages.Add(warning);

        return warnings.Count == 0;
    }

    private static string? FindDuplicatedName(IEnumerable<string> names)
    {
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
    }

    private void AddTableWarnings(TableDefinition table, ICollection<string> messages)
    {
        foreach (var column in table.Columns.Where(column => column.IsIdentity && !string.IsNullOrWhiteSpace(column.DefaultValue)))
            messages.Add($"Column '{column.Name}' has both identity and default expression; the default expression will be omitted.");

        if (DatabaseType == DatabaseType.MySql && table.ForeignKeys.Count > 0)
        {
            messages.Add(
                "MySQL foreign keys require InnoDB-compatible tables. The generated table will use InnoDB, and referenced tables must also use InnoDB.");
        }

        if (DatabaseType == DatabaseType.Oracle &&
            table.ForeignKeys.Any(foreignKey => !string.IsNullOrWhiteSpace(foreignKey.OnUpdateAction)))
        {
            messages.Add("Oracle foreign keys do not support ON UPDATE actions; the selected action will be omitted.");
        }
    }

    private static HashSet<string> GetColumnNames(TableDefinition table)
    {
        return table.Columns
            .Select(column => column.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshAvailableColumnNames()
    {
        var names = Columns
            .Select(column => Clean(column.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = AvailableColumnNames.Count - 1; index >= 0; index--)
        {
            if (!names.Contains(AvailableColumnNames[index], StringComparer.OrdinalIgnoreCase))
                AvailableColumnNames.RemoveAt(index);
        }

        foreach (var name in names)
        {
            if (!AvailableColumnNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                AvailableColumnNames.Add(name);
        }
    }

    private static IReadOnlyList<string> ReadForeignKeyColumns(TableDesignerForeignKeyViewModel foreignKey)
    {
        var selectedColumns = foreignKey.ColumnMappings
            .Select(mapping => Clean(mapping.LocalColumnName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return selectedColumns.Count > 0
            ? selectedColumns
            : SplitCsv(foreignKey.ColumnNames).ToList();
    }

    private static IReadOnlyList<string> ReadForeignKeyReferencedColumns(TableDesignerForeignKeyViewModel foreignKey)
    {
        var selectedColumns = foreignKey.ColumnMappings
            .Select(mapping => Clean(mapping.ReferencedColumnName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return selectedColumns.Count > 0
            ? selectedColumns
            : SplitCsv(foreignKey.ReferencedColumnNames).ToList();
    }

    private static IReadOnlyList<TableDesignerIndexColumnSelection> ReadIndexColumns(TableDesignerIndexViewModel index)
    {
        var selectedColumns = index.Columns
            .Select(column => new TableDesignerIndexColumnSelection(Clean(column.ColumnName), column.Descending))
            .Where(column => !string.IsNullOrWhiteSpace(column.Name))
            .ToList();

        return selectedColumns.Count > 0
            ? selectedColumns
            : SplitCsv(index.ColumnNames)
                .Select(name => new TableDesignerIndexColumnSelection(name, false))
                .ToList();
    }

    private static bool IsEmptyForeignKey(TableForeignKeyDefinition foreignKey)
    {
        return foreignKey.ColumnNames.Count == 0 &&
               string.IsNullOrWhiteSpace(foreignKey.ReferencedTableName) &&
               foreignKey.ReferencedColumnNames.Count == 0;
    }

    private static bool IsEmptyIndex(TableIndexDefinition index)
    {
        return index.Columns.Count == 0;
    }

    private static int? NormalizePositive(int? value)
    {
        return value is > 0 ? value : null;
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static IEnumerable<string> SplitCsv(string? value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));
    }
}

public sealed class TableDesignerColumnViewModel : ViewModelBase
{
    public TableDesignerColumnViewModel(
        ObservableCollection<ProviderDataTypeOption> availableDataTypes,
        ProviderDataTypeOption selectedDataType,
        bool isExistingColumn = false)
    {
        AvailableDataTypes = availableDataTypes;
        SelectedDataType = selectedDataType;
        IsExistingColumn = isExistingColumn;

        this.WhenAnyValue(vm => vm.SelectedDataType).Subscribe(type =>
        {
            if (type is null)
                return;

            Length = type.SupportsLength ? type.DefaultLength : null;
            Precision = type.SupportsPrecision ? type.DefaultPrecision : null;
            Scale = type.SupportsScale ? type.DefaultScale : null;
            IsIdentity = IsIdentity && type.SupportsIdentity;
            this.RaisePropertyChanged(nameof(SupportsLength));
            this.RaisePropertyChanged(nameof(SupportsPrecision));
            this.RaisePropertyChanged(nameof(SupportsScale));
            this.RaisePropertyChanged(nameof(SupportsIdentity));
            this.RaisePropertyChanged(nameof(CanEditLength));
            this.RaisePropertyChanged(nameof(CanEditPrecision));
            this.RaisePropertyChanged(nameof(CanEditScale));
            this.RaisePropertyChanged(nameof(CanEditIdentity));
        });

        this.WhenAnyValue(vm => vm.IsPrimaryKey).Subscribe(isPrimaryKey =>
        {
            if (isPrimaryKey)
                IsNullable = false;

            this.RaisePropertyChanged(nameof(CanEditNullable));
        });
    }

    public ObservableCollection<ProviderDataTypeOption> AvailableDataTypes { get; }
    public bool SupportsLength => SelectedDataType?.SupportsLength == true;
    public bool SupportsPrecision => SelectedDataType?.SupportsPrecision == true;
    public bool SupportsScale => SelectedDataType?.SupportsScale == true;
    public bool SupportsIdentity => SelectedDataType?.SupportsIdentity == true;
    public bool CanEditNullable => !IsPrimaryKey;

    /// <summary>
    /// True when this column was loaded from an existing table (edit mode). Existing columns can
    /// be renamed, retyped (type/length/precision/scale/nullable/default), removed, and toggled
    /// in/out of the primary key. Identity is intentionally excluded from editing on an existing
    /// column even where the underlying provider would allow it (MySQL/Postgres do; SQL Server
    /// and Oracle effectively don't via a simple ALTER) — kept uniform across providers rather
    /// than conditionally enabled, since <see cref="TableDdlScriptBuilder"/> does not diff
    /// identity changes on matched columns. See <see cref="TableDesignerViewModel.CreateForEdit"/>.
    /// </summary>
    public bool IsExistingColumn { get; }
    public bool CanEditLength => SupportsLength;
    public bool CanEditPrecision => SupportsPrecision;
    public bool CanEditScale => SupportsScale;
    public bool CanEditIdentity => SupportsIdentity && !IsExistingColumn;

    /// <summary>The column's name as loaded from the database (edit mode); null for a new column.</summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// Provider-specific data carried through from the loaded column definition (for example
    /// SQL Server's default-constraint name), needed to generate a correct ALTER when the value
    /// changes. Not user-editable.
    /// </summary>
    public Dictionary<string, string> ProviderOptions { get; } = new(StringComparer.OrdinalIgnoreCase);

    [Reactive] public string Name { get; set; } = string.Empty;
    [Reactive] public ProviderDataTypeOption? SelectedDataType { get; set; }
    [Reactive] public int? Length { get; set; }
    [Reactive] public int? Precision { get; set; }
    [Reactive] public int? Scale { get; set; }
    [Reactive] public bool IsNullable { get; set; } = true;
    [Reactive] public bool IsIdentity { get; set; }
    [Reactive] public bool IsPrimaryKey { get; set; }
    [Reactive] public string DefaultValue { get; set; } = string.Empty;
}

public sealed class TableDesignerReferenceTableOption
{
    public TableDesignerReferenceTableOption(string displayName, string schemaName, string tableName, SchemaNode node)
    {
        DisplayName = displayName;
        SchemaName = schemaName;
        TableName = tableName;
        Node = node;
    }

    public string DisplayName { get; }
    public string SchemaName { get; }
    public string TableName { get; }
    public SchemaNode Node { get; }

    public override string ToString() => DisplayName;
}

public sealed record TableDesignerIndexColumnSelection(string Name, bool Descending);

public sealed class TableDesignerForeignKeyViewModel : ViewModelBase
{
    private string _onDeleteAction = string.Empty;
    private string _onUpdateAction = string.Empty;
    private TableDesignerReferenceTableOption? _selectedReferencedTable;
    private readonly Action _changed;
    private readonly Func<TableDesignerReferenceTableOption?, Task<IReadOnlyList<string>>> _loadReferenceColumns;

    public TableDesignerForeignKeyViewModel()
        : this(new ObservableCollection<string>(), new ObservableCollection<TableDesignerReferenceTableOption>(), _ => Task.FromResult<IReadOnlyList<string>>([]), () => { })
    {
    }

    public TableDesignerForeignKeyViewModel(
        ObservableCollection<string> availableColumnNames,
        ObservableCollection<TableDesignerReferenceTableOption> availableReferenceTables,
        Func<TableDesignerReferenceTableOption?, Task<IReadOnlyList<string>>> loadReferenceColumns,
        Action changed,
        bool supportsOnUpdateAction = true)
    {
        AvailableColumnNames = availableColumnNames;
        AvailableReferenceTables = availableReferenceTables;
        _loadReferenceColumns = loadReferenceColumns;
        _changed = changed;
        SupportsOnUpdateAction = supportsOnUpdateAction;
        AddColumnMappingCommand = ReactiveCommand.Create(AddColumnMapping);
        RemoveColumnMappingCommand = ReactiveCommand.Create<TableDesignerForeignKeyColumnMappingViewModel>(mapping => ColumnMappings.Remove(mapping));
        ColumnMappings.CollectionChanged += ColumnMappingsOnCollectionChanged;
        AddColumnMapping();
    }

    public ObservableCollection<string> AvailableColumnNames { get; }
    public ObservableCollection<TableDesignerReferenceTableOption> AvailableReferenceTables { get; }
    public ObservableCollection<string> AvailableReferencedColumnNames { get; } = new();
    public ObservableCollection<TableDesignerForeignKeyColumnMappingViewModel> ColumnMappings { get; } = new();
    public string[] ReferentialActions { get; } = ["", "no action", "cascade", "set null", "set default", "restrict"];
    public bool SupportsOnUpdateAction { get; }
    public ReactiveCommand<Unit, Unit> AddColumnMappingCommand { get; }
    public ReactiveCommand<TableDesignerForeignKeyColumnMappingViewModel, Unit> RemoveColumnMappingCommand { get; }

    [Reactive] public string Name { get; set; } = string.Empty;
    [Reactive] public string ColumnNames { get; set; } = string.Empty;
    [Reactive] public string ReferencedSchemaName { get; set; } = string.Empty;
    [Reactive] public string ReferencedTableName { get; set; } = string.Empty;
    [Reactive] public string ReferencedColumnNames { get; set; } = string.Empty;
    public TableDesignerReferenceTableOption? SelectedReferencedTable
    {
        get => _selectedReferencedTable;
        set
        {
            if (IsSameReferenceTable(_selectedReferencedTable, value))
                return;

            this.RaiseAndSetIfChanged(ref _selectedReferencedTable, value);
            _ = LoadReferencedColumnsForSelectionAsync(value);
        }
    }

    public string OnDeleteAction
    {
        get => _onDeleteAction;
        set
        {
            if (value is null)
                return;

            this.RaiseAndSetIfChanged(ref _onDeleteAction, value);
        }
    }

    public string OnUpdateAction
    {
        get => _onUpdateAction;
        set
        {
            if (value is null)
                return;

            this.RaiseAndSetIfChanged(ref _onUpdateAction, value);
        }
    }

    private void AddColumnMapping()
    {
        ColumnMappings.Add(new TableDesignerForeignKeyColumnMappingViewModel(
            AvailableColumnNames,
            AvailableReferencedColumnNames)
        {
            LocalColumnName = AvailableColumnNames.FirstOrDefault() ?? string.Empty,
            ReferencedColumnName = AvailableReferencedColumnNames.FirstOrDefault() ?? string.Empty
        });
    }

    private static bool IsSameReferenceTable(TableDesignerReferenceTableOption? current, TableDesignerReferenceTableOption? next)
    {
        if (current is null && next is null)
            return true;

        if (current is null || next is null)
            return false;

        return string.Equals(current.DisplayName, next.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadReferencedColumnsForSelectionAsync(TableDesignerReferenceTableOption? table)
    {
        ReferencedSchemaName = table?.SchemaName ?? string.Empty;
        ReferencedTableName = table?.TableName ?? string.Empty;

        AvailableReferencedColumnNames.Clear();
        var columns = await _loadReferenceColumns(table);
        foreach (var column in columns)
            AvailableReferencedColumnNames.Add(column);

        foreach (var mapping in ColumnMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.ReferencedColumnName) ||
                !AvailableReferencedColumnNames.Contains(mapping.ReferencedColumnName, StringComparer.OrdinalIgnoreCase))
            {
                mapping.ReferencedColumnName = AvailableReferencedColumnNames.FirstOrDefault() ?? string.Empty;
            }
        }

        _changed();
    }

    private void ColumnMappingsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged -= MappingOnPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged += MappingOnPropertyChanged;
        }

        this.RaisePropertyChanged(nameof(ColumnMappings));
        _changed();
    }

    private void MappingOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _changed();
    }
}

public sealed class TableDesignerForeignKeyColumnMappingViewModel : ViewModelBase
{
    private string _localColumnName = string.Empty;
    private string _referencedColumnName = string.Empty;

    public TableDesignerForeignKeyColumnMappingViewModel()
        : this(new ObservableCollection<string>(), new ObservableCollection<string>())
    {
    }

    public TableDesignerForeignKeyColumnMappingViewModel(
        ObservableCollection<string> availableColumnNames,
        ObservableCollection<string> availableReferencedColumnNames)
    {
        AvailableColumnNames = availableColumnNames;
        AvailableReferencedColumnNames = availableReferencedColumnNames;
    }

    public ObservableCollection<string> AvailableColumnNames { get; }
    public ObservableCollection<string> AvailableReferencedColumnNames { get; }

    public string LocalColumnName
    {
        get => _localColumnName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            this.RaiseAndSetIfChanged(ref _localColumnName, value);
        }
    }

    public string ReferencedColumnName
    {
        get => _referencedColumnName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            this.RaiseAndSetIfChanged(ref _referencedColumnName, value);
        }
    }
}

public sealed class TableDesignerIndexViewModel : ViewModelBase
{
    private readonly Action _changed;

    public TableDesignerIndexViewModel()
        : this(new ObservableCollection<string>(), () => { })
    {
    }

    public TableDesignerIndexViewModel(ObservableCollection<string> availableColumnNames, Action changed, DatabaseType databaseType = DatabaseType.SqlServer)
    {
        AvailableColumnNames = availableColumnNames;
        _changed = changed;
        SupportsClusteredOption = databaseType == DatabaseType.SqlServer;
        SupportsPostgresOptions = databaseType == DatabaseType.PostgresSql;
        SupportsMySqlPrefixLength = databaseType == DatabaseType.MySql;
        AddColumnCommand = ReactiveCommand.Create(AddColumn);
        RemoveColumnCommand = ReactiveCommand.Create<TableDesignerIndexColumnViewModel>(column => Columns.Remove(column));
        Columns.CollectionChanged += ColumnsOnCollectionChanged;
        AddColumn();
    }

    public ObservableCollection<string> AvailableColumnNames { get; }
    public ObservableCollection<TableDesignerIndexColumnViewModel> Columns { get; } = new();
    public ReactiveCommand<Unit, Unit> AddColumnCommand { get; }
    public ReactiveCommand<TableDesignerIndexColumnViewModel, Unit> RemoveColumnCommand { get; }
    public bool SupportsClusteredOption { get; }
    public bool SupportsPostgresOptions { get; }
    public bool SupportsMySqlPrefixLength { get; }

    [Reactive] public string Name { get; set; } = string.Empty;
    [Reactive] public string ColumnNames { get; set; } = string.Empty;
    [Reactive] public bool IsUnique { get; set; }

    /// <summary>SQL Server only: render "clustered" instead of the implicit nonclustered.</summary>
    [Reactive] public bool IsClustered { get; set; }

    /// <summary>SQL Server only: fill factor percentage (1-100), empty for provider default.</summary>
    [Reactive] public string FillFactor { get; set; } = string.Empty;

    /// <summary>PostgreSQL only: index access method (btree/gin/gist/hash); empty means the provider default (btree).</summary>
    [Reactive] public string IndexMethod { get; set; } = string.Empty;

    /// <summary>PostgreSQL only: partial-index WHERE predicate.</summary>
    [Reactive] public string WherePredicate { get; set; } = string.Empty;

    public string[] IndexMethods { get; } = ["", "btree", "gin", "gist", "hash"];

    private void AddColumn()
    {
        Columns.Add(new TableDesignerIndexColumnViewModel(AvailableColumnNames)
        {
            ColumnName = AvailableColumnNames.FirstOrDefault() ?? string.Empty
        });
    }

    private void ColumnsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged -= ColumnOnPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged += ColumnOnPropertyChanged;
        }

        this.RaisePropertyChanged(nameof(Columns));
        _changed();
    }

    private void ColumnOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _changed();
    }
}

public sealed class TableDesignerIndexColumnViewModel : ViewModelBase
{
    private string _columnName = string.Empty;

    public TableDesignerIndexColumnViewModel()
        : this(new ObservableCollection<string>())
    {
    }

    public TableDesignerIndexColumnViewModel(ObservableCollection<string> availableColumnNames)
    {
        AvailableColumnNames = availableColumnNames;
    }

    public ObservableCollection<string> AvailableColumnNames { get; }
    [Reactive] public bool Descending { get; set; }

    /// <summary>MySQL only: index key prefix length for this column; empty means the whole column.</summary>
    [Reactive] public string PrefixLength { get; set; } = string.Empty;

    public string ColumnName
    {
        get => _columnName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            this.RaiseAndSetIfChanged(ref _columnName, value);
        }
    }
}
