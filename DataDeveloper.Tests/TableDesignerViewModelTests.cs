using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using DataDeveloper.Data.Enums;
using DataDeveloper.Data.Interfaces;
using DataDeveloper.Data.Models;
using DataDeveloper.Data.Models.TableDesigner;
using DataDeveloper.Data.Services;
using DataDeveloper.Enums;
using DataDeveloper.Interfaces;
using DataDeveloper.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataDeveloper.Tests;

public class TableDesignerViewModelTests
{
    [Fact]
    public void ForeignKeyWithNullReferentialActions_DoesNotClearGeneratedSql()
    {
        var viewModel = CreateViewModel();

        viewModel.Columns.Add(new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "CustomerId",
            IsNullable = false
        });

        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "CustomerId",
            ReferencedTableName = "Customers",
            ReferencedColumnNames = "Id",
            OnDeleteAction = null!,
            OnUpdateAction = null!
        });

        Assert.Contains(
            """
            foreign key ([CustomerId])
                    references [Customers] ([Id])
            """,
            viewModel.GeneratedSql);
        Assert.Equal(string.Empty, viewModel.ValidationMessage);
    }

    [Fact]
    public void PrimaryKeyName_GeneratesNamedPrimaryKey()
    {
        var viewModel = CreateViewModel();

        viewModel.PrimaryKeyName = "PK_Custom";

        Assert.Contains("constraint [PK_Custom] primary key ([Id])", viewModel.GeneratedSql);
    }

    [Fact]
    public void ReorderingColumns_UpdatesCompositePrimaryKeyOrder()
    {
        var viewModel = CreateViewModel();
        viewModel.Columns.Add(new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "TenantId",
            IsNullable = false,
            IsPrimaryKey = true
        });

        Assert.Contains("primary key ([Id], [TenantId])", viewModel.GeneratedSql);

        viewModel.Columns.Move(1, 0);

        Assert.Contains("primary key ([TenantId], [Id])", viewModel.GeneratedSql);
    }

    [Fact]
    public void ForeignKeyWithUnknownLocalColumn_KeepsValidSqlAndShowsWarning()
    {
        var viewModel = CreateViewModel();

        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "MissingCustomerId",
            ReferencedTableName = "Customers",
            ReferencedColumnNames = "Id"
        });

        Assert.Contains("uses unknown local column 'MissingCustomerId'", viewModel.ValidationMessage);
        Assert.Contains("create table [NewTable]", viewModel.GeneratedSql);
        Assert.DoesNotContain("foreign key", viewModel.GeneratedSql);
    }

    [Fact]
    public void ForeignKeyReferentialActionCanBeCleared()
    {
        var viewModel = CreateViewModel();
        viewModel.Columns.Add(new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "CustomerId",
            IsNullable = false
        });
        var foreignKey = new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "CustomerId",
            ReferencedTableName = "Customers",
            ReferencedColumnNames = "Id",
            OnDeleteAction = "cascade"
        };
        viewModel.ForeignKeys.Add(foreignKey);

        Assert.Contains("on delete cascade", viewModel.GeneratedSql);

        foreignKey.OnDeleteAction = string.Empty;

        Assert.DoesNotContain("on delete cascade", viewModel.GeneratedSql);
        Assert.Contains("references [Customers] ([Id])", viewModel.GeneratedSql);
    }

    [Fact]
    public void ForeignKeyReferentialActionsIgnoreNullValuesFromVisualUnload()
    {
        var foreignKey = new TableDesignerForeignKeyViewModel
        {
            OnDeleteAction = "cascade",
            OnUpdateAction = "restrict"
        };

        foreignKey.OnDeleteAction = null!;
        foreignKey.OnUpdateAction = null!;

        Assert.Equal("cascade", foreignKey.OnDeleteAction);
        Assert.Equal("restrict", foreignKey.OnUpdateAction);
    }

    [Fact]
    public void ForeignKeyManualReferencedSchemaOverridesSelectedReferenceTableSchema()
    {
        var viewModel = CreateViewModel(DatabaseType.MySql);
        var selectedReferenceTable = new TableDesignerReferenceTableOption("clientes", string.Empty, "clientes", null!);
        viewModel.Columns[0].Name = "idCliente";
        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable",
            SelectedReferencedTable = selectedReferenceTable,
            ReferencedSchemaName = "crm",
            ColumnNames = "idCliente",
            ReferencedColumnNames = "idCliente",
            OnDeleteAction = "cascade",
            OnUpdateAction = "cascade"
        });

        Assert.Contains("references `crm`.`clientes` (`idCliente`)", viewModel.GeneratedSql);
    }

    [Fact]
    public void MySqlForeignKey_ShowsInnoDbCompatibilityWarning()
    {
        var viewModel = CreateViewModel(DatabaseType.MySql);
        viewModel.Columns[0].Name = "idCliente";
        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable",
            ReferencedTableName = "clientes",
            ColumnNames = "idCliente",
            ReferencedColumnNames = "idCliente"
        });

        Assert.Contains("MySQL foreign keys require InnoDB-compatible tables.", viewModel.ValidationMessage);
        Assert.Contains("engine=InnoDB", viewModel.GeneratedSql);
    }

    [Fact]
    public void OracleForeignKeyWithOnUpdate_ShowsUnsupportedActionWarningAndOmitsAction()
    {
        var viewModel = CreateViewModel(DatabaseType.Oracle);
        viewModel.Columns[0].Name = "CUSTOMER_ID";
        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NEWTABLE_CUSTOMERS",
            ReferencedTableName = "CUSTOMERS",
            ColumnNames = "CUSTOMER_ID",
            ReferencedColumnNames = "CUSTOMER_ID",
            OnUpdateAction = "cascade"
        });

        Assert.Contains("Oracle foreign keys do not support ON UPDATE actions;", viewModel.ValidationMessage);
        Assert.DoesNotContain("on update cascade", viewModel.GeneratedSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForeignKeyViewModel_CanDisableOnUpdateAction()
    {
        var foreignKey = new TableDesignerForeignKeyViewModel(
            new(),
            new(),
            _ => Task.FromResult<IReadOnlyList<string>>([]),
            () => { },
            supportsOnUpdateAction: false);

        Assert.False(foreignKey.SupportsOnUpdateAction);
    }

    [Fact]
    public void IndexWithUnknownColumn_KeepsValidSqlAndShowsWarning()
    {
        var viewModel = CreateViewModel();

        viewModel.Indexes.Add(new TableDesignerIndexViewModel
        {
            Name = "IX_NewTable_Missing",
            ColumnNames = "MissingColumn"
        });

        Assert.Contains("uses unknown column 'MissingColumn'", viewModel.ValidationMessage);
        Assert.Contains("create table [NewTable]", viewModel.GeneratedSql);
        Assert.DoesNotContain("create index", viewModel.GeneratedSql);
    }

    [Fact]
    public void ForeignKeyWithDuplicatedName_KeepsFirstForeignKeyAndShowsWarning()
    {
        var viewModel = CreateViewModel();
        viewModel.Columns.Add(new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "CustomerId",
            IsNullable = false
        });

        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "CustomerId",
            ReferencedTableName = "Customers",
            ReferencedColumnNames = "Id"
        });
        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "CustomerId",
            ReferencedTableName = "CustomersArchive",
            ReferencedColumnNames = "Id"
        });

        Assert.Contains("Foreign key 'FK_NewTable_Customers' has a duplicated name.", viewModel.ValidationMessage);
        Assert.Contains("references [Customers] ([Id])", viewModel.GeneratedSql);
        Assert.DoesNotContain("references [CustomersArchive] ([Id])", viewModel.GeneratedSql);
    }

    [Fact]
    public void IndexWithDuplicatedColumn_KeepsValidSqlAndShowsWarning()
    {
        var viewModel = CreateViewModel();

        viewModel.Indexes.Add(new TableDesignerIndexViewModel
        {
            Name = "IX_NewTable_Id",
            ColumnNames = "Id, Id"
        });

        Assert.Contains("uses column 'Id' more than once", viewModel.ValidationMessage);
        Assert.Contains("create table [NewTable]", viewModel.GeneratedSql);
        Assert.DoesNotContain("create index [IX_NewTable_Id]", viewModel.GeneratedSql);
    }

    [Fact]
    public void IndexColumnDescending_GeneratesDescendingIndexColumn()
    {
        var viewModel = CreateViewModel();
        var index = new TableDesignerIndexViewModel(viewModel.AvailableColumnNames, () => { })
        {
            Name = "IX_NewTable_Id"
        };
        index.Columns[0].ColumnName = "Id";
        index.Columns[0].Descending = true;
        viewModel.Indexes.Add(index);

        Assert.Contains("create index [IX_NewTable_Id]", viewModel.GeneratedSql);
        Assert.Contains("on [NewTable] ([Id] desc);", viewModel.GeneratedSql);
    }

    [Fact]
    public void ForeignKeyWithMismatchedColumnCounts_KeepsValidSqlAndShowsWarning()
    {
        var viewModel = CreateViewModel();
        viewModel.Columns.Add(new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "TenantId",
            IsNullable = false
        });

        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "Id, TenantId",
            ReferencedTableName = "Customers",
            ReferencedColumnNames = "Id"
        });

        Assert.Contains("same number of local and referenced columns", viewModel.ValidationMessage);
        Assert.Contains("create table [NewTable]", viewModel.GeneratedSql);
        Assert.DoesNotContain("foreign key", viewModel.GeneratedSql);
    }

    [Fact]
    public void ForeignKeyColumnMappings_ArePreservedWhenSwitchingToIndexOperations()
    {
        var viewModel = CreateViewModel();
        viewModel.Columns.Add(new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "CustomerId",
            IsNullable = false
        });

        var foreignKey = new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "CustomerId",
            ReferencedTableName = "Customers",
            ReferencedColumnNames = "Id"
        };
        viewModel.ForeignKeys.Add(foreignKey);

        var index = new TableDesignerIndexViewModel(viewModel.AvailableColumnNames, () => { })
        {
            Name = "IX_NewTable_Id"
        };
        index.Columns[0].ColumnName = "Id";
        viewModel.Indexes.Add(index);

        Assert.Contains("references [Customers] ([Id])", viewModel.GeneratedSql);
        Assert.Contains("create index [IX_NewTable_Id]", viewModel.GeneratedSql);
        Assert.Equal("FK_NewTable_Customers", foreignKey.Name);
        Assert.Equal("Customers", foreignKey.ReferencedTableName);
        Assert.Equal("CustomerId", foreignKey.ColumnNames);
    }

    [Fact]
    public void ClearingOnDeleteAndOnUpdate_RemovesActionsFromGeneratedSql()
    {
        var viewModel = CreateViewModel();
        viewModel.Columns.Add(new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "CustomerId",
            IsNullable = false
        });

        var foreignKey = new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "CustomerId",
            ReferencedTableName = "Customers",
            ReferencedColumnNames = "Id",
            OnDeleteAction = "cascade",
            OnUpdateAction = "set null"
        };
        viewModel.ForeignKeys.Add(foreignKey);

        Assert.Contains("on delete cascade", viewModel.GeneratedSql);
        Assert.Contains("on update set null", viewModel.GeneratedSql);

        foreignKey.OnDeleteAction = string.Empty;
        foreignKey.OnUpdateAction = string.Empty;

        Assert.DoesNotContain("on delete", viewModel.GeneratedSql);
        Assert.DoesNotContain("on update", viewModel.GeneratedSql);
        Assert.Contains("references [Customers] ([Id])", viewModel.GeneratedSql);
        Assert.Equal(string.Empty, viewModel.ValidationMessage);
    }

    [Fact]
    public void MixedValidAndInvalidForeignKeys_KeepsValidFkInSqlAndShowsWarningForInvalid()
    {
        var viewModel = CreateViewModel();
        viewModel.Columns.Add(new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "CustomerId",
            IsNullable = false
        });

        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Customers",
            ColumnNames = "CustomerId",
            ReferencedTableName = "Customers",
            ReferencedColumnNames = "Id"
        });

        viewModel.ForeignKeys.Add(new TableDesignerForeignKeyViewModel
        {
            Name = "FK_NewTable_Invalid",
            ColumnNames = "MissingColumn",
            ReferencedTableName = "SomeTable",
            ReferencedColumnNames = "Id"
        });

        Assert.Contains("create table [NewTable]", viewModel.GeneratedSql);
        Assert.Contains("references [Customers] ([Id])", viewModel.GeneratedSql);
        Assert.DoesNotContain("references [SomeTable]", viewModel.GeneratedSql);
        Assert.Contains("uses unknown local column 'MissingColumn'", viewModel.ValidationMessage);
    }

    [Fact]
    public void MixedValidAndInvalidIndexes_KeepsValidIndexInSqlAndShowsWarningForInvalid()
    {
        var viewModel = CreateViewModel();

        var validIndex = new TableDesignerIndexViewModel(viewModel.AvailableColumnNames, () => { })
        {
            Name = "IX_NewTable_Id"
        };
        validIndex.Columns[0].ColumnName = "Id";
        viewModel.Indexes.Add(validIndex);

        viewModel.Indexes.Add(new TableDesignerIndexViewModel
        {
            Name = "IX_NewTable_Missing",
            ColumnNames = "MissingColumn"
        });

        Assert.Contains("create table [NewTable]", viewModel.GeneratedSql);
        Assert.Contains("create index [IX_NewTable_Id]", viewModel.GeneratedSql);
        Assert.DoesNotContain("create index [IX_NewTable_Missing]", viewModel.GeneratedSql);
        Assert.Contains("uses unknown column 'MissingColumn'", viewModel.ValidationMessage);
    }

    [Fact]
    public void MarkingColumnAsPrimaryKey_ForcesNotNullableAndDisablesNullEditing()
    {
        var viewModel = CreateViewModel();
        var column = new TableDesignerColumnViewModel(
            viewModel.AvailableDataTypes,
            viewModel.AvailableDataTypes[0])
        {
            Name = "TenantId",
            IsNullable = true
        };

        Assert.True(column.CanEditNullable);

        column.IsPrimaryKey = true;

        Assert.False(column.IsNullable);
        Assert.False(column.CanEditNullable);

        column.IsPrimaryKey = false;

        Assert.True(column.CanEditNullable);
    }

    [Fact]
    public async Task CreateForEdit_ForeignKeyWithNonEmptyReferencedSchema_PreselectsReferencedTable()
    {
        // Regression test: every provider's schema-tree table catalog returns unqualified names
        // (see LoadReferenceTables/SplitObjectName), so matching a loaded FK's non-empty
        // ReferencedSchemaName (e.g. SQL Server "dbo") against the catalog's schema used to
        // always fail and leave the "referenced table" combo empty.
        var original = BuildOriginalOrdersDefinition();
        original.ForeignKeys[0].ReferencedSchemaName = "dbo";

        var connectionSettings = new ConnectionSettings { Id = Guid.NewGuid(), Name = "Test", DatabaseType = DatabaseType.SqlServer };
        var schemaExplorer = new SchemaExplorer(new FakeTableCatalogDatabaseProvider("Customers"), connectionSettings);
        await schemaExplorer.InitializeSchemaNode();

        var viewModel = TableDesignerViewModel.CreateForEdit(
            connectionSettings,
            original,
            _ => Task.FromResult(true),
            new NoOpDialogService(),
            schemaExplorer);

        var foreignKey = Assert.Single(viewModel.ForeignKeys);
        Assert.NotNull(foreignKey.SelectedReferencedTable);
        Assert.Equal("Customers", foreignKey.SelectedReferencedTable!.TableName);
    }

    [Fact]
    public void CreateForEdit_PrePopulatesColumnsForeignKeysAndIndexesFromDefinition()
    {
        var original = BuildOriginalOrdersDefinition();

        var viewModel = CreateEditViewModel(original);

        Assert.Equal("dbo", viewModel.SchemaName);
        Assert.Equal("Orders", viewModel.TableName);
        Assert.Equal("PK_Orders", viewModel.PrimaryKeyName);
        Assert.Equal(2, viewModel.Columns.Count);
        Assert.Contains(viewModel.Columns, column => column.Name == "Id" && column.IsPrimaryKey);
        Assert.Contains(viewModel.Columns, column => column.Name == "CustomerId");
        Assert.Single(viewModel.ForeignKeys);
        Assert.Equal("FK_Orders_Customers", viewModel.ForeignKeys[0].Name);
        Assert.Single(viewModel.Indexes);
        Assert.Equal("IX_Orders_CustomerId", viewModel.Indexes[0].Name);
    }

    [Fact]
    public void CreateForEdit_ExistingColumnsAllowRetyping_ButNotIdentityToggle()
    {
        var original = BuildOriginalOrdersDefinition();
        var viewModel = CreateEditViewModel(original);

        // Type/length/nullable/default are editable on existing columns in phase 2; identity
        // stays locked uniformly across providers (see IsExistingColumn doc comment).
        Assert.All(viewModel.Columns, column => Assert.False(column.CanEditIdentity));
        Assert.All(viewModel.Columns, column => Assert.True(column.CanEditLength || !column.SupportsLength));

        viewModel.Columns.Add(new TableDesignerColumnViewModel(viewModel.AvailableDataTypes, viewModel.AvailableDataTypes[0])
        {
            Name = "Notes"
        });

        var newColumn = Assert.Single(viewModel.Columns, column => column.Name == "Notes");
        Assert.False(newColumn.IsExistingColumn);
    }

    [Fact]
    public void CreateForEdit_TableNameIsEditable_SchemaNameIsReadOnly()
    {
        var viewModel = CreateEditViewModel(BuildOriginalOrdersDefinition());

        Assert.False(viewModel.CanEditSchemaName);
    }

    [Fact]
    public void CreateForEdit_AddedColumn_GeneratesAlterTableScript()
    {
        var viewModel = CreateEditViewModel(BuildOriginalOrdersDefinition());

        viewModel.Columns.Add(new TableDesignerColumnViewModel(viewModel.AvailableDataTypes, viewModel.AvailableDataTypes[0])
        {
            Name = "Notes",
            IsNullable = true
        });

        Assert.Contains("alter table [dbo].[Orders] add", viewModel.GeneratedSql);
        Assert.DoesNotContain("create table", viewModel.GeneratedSql);
    }

    [Fact]
    public async Task Apply_WithDestructiveChanges_ShowsConfirmationListingDrops()
    {
        var original = BuildOriginalOrdersDefinition();
        var capturingDialogService = new CapturingDialogService();
        var viewModel = TableDesignerViewModel.CreateForEdit(
            new ConnectionSettings { Id = Guid.NewGuid(), Name = "Test", DatabaseType = DatabaseType.SqlServer },
            original,
            _ => Task.FromResult(true),
            capturingDialogService);

        var customerIdColumn = Assert.Single(viewModel.Columns, column => column.Name == "CustomerId");
        viewModel.Columns.Remove(customerIdColumn);
        viewModel.ForeignKeys.Clear();

        await viewModel.ApplyCommand.Execute(null!).ToTask();

        Assert.Contains(capturingDialogService.CapturedMessages, message => message.Contains("Drop column 'CustomerId'"));
        Assert.Contains(capturingDialogService.CapturedMessages, message => message.Contains("Drop foreign key 'FK_Orders_Customers'"));
    }

    [Fact]
    public void CreateForEdit_SqLite_DroppedColumnIsNotEmittedAndShowsWarning()
    {
        var original = BuildOriginalOrdersDefinition(DatabaseType.SqLite);
        var viewModel = CreateEditViewModel(original, DatabaseType.SqLite);

        var customerIdColumn = Assert.Single(viewModel.Columns, column => column.Name == "CustomerId");
        viewModel.Columns.Remove(customerIdColumn);

        Assert.DoesNotContain("drop column", viewModel.GeneratedSql);
        Assert.Contains("SQLite does not support dropping columns", viewModel.ValidationMessage);
    }

    [Fact]
    public void CreateForEdit_TableWithoutPrimaryKey_CanMarkExistingColumnAsPrimaryKey()
    {
        var original = new TableDefinition { SchemaName = "dbo", TableName = "Logs" };
        original.Columns.Add(new TableColumnDefinition { Name = "Id", DataType = "int", IsNullable = false });
        original.Columns.Add(new TableColumnDefinition { Name = "Message", DataType = "nvarchar", Length = 200, IsNullable = true });

        var viewModel = CreateEditViewModel(original);
        var idColumn = Assert.Single(viewModel.Columns, column => column.Name == "Id");

        Assert.True(idColumn.IsExistingColumn);

        idColumn.IsPrimaryKey = true;

        Assert.False(idColumn.IsNullable);
        Assert.Contains("alter table [dbo].[Logs] add constraint", viewModel.GeneratedSql);
        Assert.Contains("primary key ([Id])", viewModel.GeneratedSql);
    }

    [Fact]
    public void CreateForEdit_RenamingExistingColumn_TracksOriginalNameAndGeneratesRenameNotDropAdd()
    {
        var original = BuildOriginalOrdersDefinition();
        var viewModel = CreateEditViewModel(original);
        var customerIdColumn = Assert.Single(viewModel.Columns, column => column.Name == "CustomerId");

        Assert.Equal("CustomerId", customerIdColumn.OriginalName);

        customerIdColumn.Name = "ClientId";

        Assert.DoesNotContain("drop column", viewModel.GeneratedSql);
        Assert.DoesNotContain("add column", viewModel.GeneratedSql);
        Assert.Contains("sp_rename", viewModel.GeneratedSql);
        Assert.Contains("ClientId", viewModel.GeneratedSql);
    }

    [Fact]
    public async Task Apply_RenamingExistingColumn_DoesNotShowDestructiveDropConfirmation()
    {
        var original = BuildOriginalOrdersDefinition();
        var capturingDialogService = new CapturingDialogService();
        var viewModel = TableDesignerViewModel.CreateForEdit(
            new ConnectionSettings { Id = Guid.NewGuid(), Name = "Test", DatabaseType = DatabaseType.SqlServer },
            original,
            _ => Task.FromResult(true),
            capturingDialogService);

        var customerIdColumn = Assert.Single(viewModel.Columns, column => column.Name == "CustomerId");
        customerIdColumn.Name = "ClientId";

        await viewModel.ApplyCommand.Execute(null!).ToTask();

        Assert.DoesNotContain(capturingDialogService.CapturedMessages, message => message.Contains("Drop column"));
    }

    [Fact]
    public void CreateForEdit_RenamingTable_GeneratesRenameStatement()
    {
        var original = BuildOriginalOrdersDefinition();
        var viewModel = CreateEditViewModel(original);

        viewModel.TableName = "Purchases";

        Assert.Contains("sp_rename '[dbo].[Orders]', 'Purchases'", viewModel.GeneratedSql);
    }

    [Fact]
    public void CreateForEdit_ExistingPrimaryKeyColumn_CanBeUnchecked_GeneratesDropPrimaryKey()
    {
        var original = BuildOriginalOrdersDefinition();
        var viewModel = CreateEditViewModel(original);
        var idColumn = Assert.Single(viewModel.Columns, column => column.Name == "Id");

        idColumn.IsPrimaryKey = false;

        Assert.Contains("alter table [dbo].[Orders] drop constraint [PK_Orders];", viewModel.GeneratedSql);
    }

    private static TableDefinition BuildOriginalOrdersDefinition(DatabaseType databaseType = DatabaseType.SqlServer)
    {
        var table = new TableDefinition
        {
            SchemaName = databaseType == DatabaseType.SqlServer ? "dbo" : string.Empty,
            TableName = "Orders"
        };
        table.Columns.Add(new TableColumnDefinition { Name = "Id", DataType = "int", IsNullable = false, IsIdentity = true });
        table.Columns.Add(new TableColumnDefinition { Name = "CustomerId", DataType = "int", IsNullable = false });
        table.PrimaryKey.Name = "PK_Orders";
        table.PrimaryKey.ColumnNames.Add("Id");
        table.ForeignKeys.Add(new TableForeignKeyDefinition
        {
            Name = "FK_Orders_Customers",
            ReferencedTableName = "Customers"
        });
        table.ForeignKeys[0].ColumnNames.Add("CustomerId");
        table.ForeignKeys[0].ReferencedColumnNames.Add("Id");
        table.Indexes.Add(new TableIndexDefinition { Name = "IX_Orders_CustomerId" });
        table.Indexes[0].Columns.Add(new TableIndexColumnDefinition { Name = "CustomerId" });
        return table;
    }

    private static TableDesignerViewModel CreateEditViewModel(TableDefinition original, DatabaseType databaseType = DatabaseType.SqlServer)
    {
        return TableDesignerViewModel.CreateForEdit(
            new ConnectionSettings { Id = Guid.NewGuid(), Name = "Test", DatabaseType = databaseType },
            original,
            _ => Task.FromResult(true),
            new NoOpDialogService());
    }

    private static TableDesignerViewModel CreateViewModel(DatabaseType databaseType = DatabaseType.SqlServer)
    {
        return new TableDesignerViewModel(
            new ConnectionSettings
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                DatabaseType = databaseType
            },
            _ => Task.FromResult(true),
            new NoOpDialogService());
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public List<string> CapturedMessages { get; } = new();

        public Task<DialogResult> ShowDialogAsync(string message, string? title = null, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Info)
        {
            CapturedMessages.Add(message);
            return Task.FromResult(DialogResult.Cancel);
        }

        public Task<DialogResult> ShowDialogResult(string message, string? title = null) => Task.FromResult(DialogResult.Cancel);
        public Task ShowMessageAsync(string message, string? title = null) => Task.CompletedTask;
        public Task ShowAboutAsync(string version, Func<Task> checkForUpdatesAsync) => Task.CompletedTask;
        public Task<DialogResult> ShowReleaseUpdateAsync(string message, string? title = null) => Task.FromResult(DialogResult.Cancel);
        public Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFileAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenDatabaseFileAsync(string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveJsonFileDialogAsync(string? suggestedName = null, string? title = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenJsonFileDialogAsync(string? title = null) => Task.FromResult<string?>(null);
    }

    private sealed class NoOpDialogService : IDialogService
    {
        public Task<DialogResult> ShowDialogAsync(string message, string? title = null, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Info)
        {
            return Task.FromResult(DialogResult.Cancel);
        }

        public Task<DialogResult> ShowDialogResult(string message, string? title = null)
        {
            return Task.FromResult(DialogResult.Cancel);
        }

        public Task ShowMessageAsync(string message, string? title = null)
        {
            return Task.CompletedTask;
        }

        public Task ShowAboutAsync(string version, Func<Task> checkForUpdatesAsync)
        {
            return Task.CompletedTask;
        }

        public Task<DialogResult> ShowReleaseUpdateAsync(string message, string? title = null)
        {
            return Task.FromResult(DialogResult.Cancel);
        }

        public Task<string?> ShowSaveFileDialogAsync(string? suggestedName = null, string? title = null)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ShowOpenFileAsync(string? title = null)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ShowOpenDatabaseFileAsync(string? title = null)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ShowCreateDatabaseFileAsync(string? suggestedName = null, string? title = null)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ShowSaveJsonFileDialogAsync(string? suggestedName = null, string? title = null)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> ShowOpenJsonFileDialogAsync(string? title = null)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeTableCatalogDatabaseProvider : IDatabaseProvider
    {
        private readonly string[] _tableNames;

        public FakeTableCatalogDatabaseProvider(params string[] tableNames)
        {
            _tableNames = tableNames;
        }

        public DbConnection GetConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        public TestConnectionResult TestConnection() => new(true, "ok");
        public IReadOnlyList<string> GetAvailableDatabaseNames() => [];

        public string GetTableStatement() =>
            "select value as Name from json_each('[" + string.Join(",", _tableNames.Select(name => $"\"{name}\"")) + "]')";

        public string GetViewStatement() => "select cast(null as text) as Name where 1 = 0";
        public string GetColumnStatement() => "select cast(null as text) as Name where 1 = 0";
        public string GetProcedureStatement() => "select cast(null as text) as Name, cast(null as text) as SpecificName where 1 = 0";
        public string GetFunctionStatement() => "select cast(null as text) as Name, cast(null as text) as SpecificName, cast(null as text) as DataType where 1 = 0";
        public string GetRoutineParameterStatement() => "select cast(null as text) as Name where 1 = 0";
        public string GetColumnDefaultValueStatement() => "select cast(null as text) as ColumnName where 1 = 0";
        public string GetPrimaryKeyStatement() => "select cast(null as text) as ColumnName where 1 = 0";
        public string GetForeignKeyStatement() => "select cast(null as text) as ColumnName where 1 = 0";
        public string GetIndexStatement() => "select cast(null as text) as ColumnName where 1 = 0";
    }
}
