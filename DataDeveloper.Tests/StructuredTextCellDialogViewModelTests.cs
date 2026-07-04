using DataDeveloper.NextGrid.Renderers;
using DataDeveloper.ViewModels;
using Xunit;

namespace DataDeveloper.Tests;

public sealed class StructuredTextCellDialogViewModelTests
{
    [Fact]
    public void Initialize_Json_ShowsValueExactlyAsStoredWithoutAutoFormatting()
    {
        var model = new StructuredTextCellDialogViewModel();

        model.Initialize("{\"a\":1,\"b\":[1,2]}", isEditable: true, StructuredTextKind.Json);

        Assert.Equal("{\"a\":1,\"b\":[1,2]}", model.Text);
        Assert.DoesNotContain("\n", model.Text);
        Assert.Equal("JSON", model.Title);
        Assert.True(model.ShowOk);
        Assert.False(model.ShowSaveCancel);
    }

    [Fact]
    public void Initialize_Xml_ShowsValueExactlyAsStoredWithoutAutoFormatting()
    {
        var model = new StructuredTextCellDialogViewModel();

        model.Initialize("<root><a>1</a></root>", isEditable: true, StructuredTextKind.Xml);

        Assert.Equal("<root><a>1</a></root>", model.Text);
        Assert.Equal("XML", model.Title);
        Assert.True(model.ShowOk);
        Assert.False(model.ShowSaveCancel);
    }

    [Fact]
    public void PrettifyingUnchangedMinifiedValue_BecomesDirtyAndAllowsSavingPrettyForm()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("{\"a\":1,\"b\":2}", isEditable: true, StructuredTextKind.Json);

        Assert.True(model.ShowOk);

        model.PrettifyCommand.Execute().Subscribe();

        Assert.Contains("\n", model.Text);
        Assert.True(model.ShowSaveCancel);
        Assert.False(model.ShowOk);
        Assert.Equal(model.Text, model.CurrentText);
    }

    [Fact]
    public void MinifyingUnchangedPrettyValue_BecomesDirtyAndAllowsSavingMinifiedForm()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("{\n  \"a\": 1\n}", isEditable: true, StructuredTextKind.Json);

        Assert.True(model.ShowOk);

        model.MinifyCommand.Execute().Subscribe();

        Assert.DoesNotContain("\n", model.Text);
        Assert.True(model.ShowSaveCancel);
        Assert.False(model.ShowOk);
    }

    [Fact]
    public void Initialize_EditableAndUnchanged_ShowsOkOnly()
    {
        var model = new StructuredTextCellDialogViewModel();

        model.Initialize("{\"a\":1}", isEditable: true, StructuredTextKind.Json);

        Assert.True(model.ShowOk);
        Assert.False(model.ShowSaveCancel);
        Assert.False(model.ShowClose);
    }

    [Fact]
    public void Initialize_NotEditable_ShowsCloseOnly()
    {
        var model = new StructuredTextCellDialogViewModel();

        model.Initialize("{\"a\":1}", isEditable: false, StructuredTextKind.Json);

        Assert.False(model.ShowOk);
        Assert.False(model.ShowSaveCancel);
        Assert.True(model.ShowClose);
    }

    [Fact]
    public void EditingText_SwitchesFromOkToSaveCancel()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("{\"a\":1}", isEditable: true, StructuredTextKind.Json);

        model.Text = "{\"a\":2}";

        Assert.False(model.ShowOk);
        Assert.True(model.ShowSaveCancel);
    }

    [Fact]
    public void PrettifyCommand_IndentsCurrentJsonText()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("{\"a\":1}", isEditable: true, StructuredTextKind.Json);

        model.Text = "{\"a\":1,\"b\":2}";
        model.PrettifyCommand.Execute().Subscribe();

        Assert.Contains("\n", model.Text);
    }

    [Fact]
    public void MinifyCommand_CompactsCurrentJsonText()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("{\"a\":1,\"b\":2}", isEditable: true, StructuredTextKind.Json);

        model.MinifyCommand.Execute().Subscribe();

        Assert.Equal("{\"a\":1,\"b\":2}", model.Text);
        Assert.DoesNotContain("\n", model.Text);
    }

    [Fact]
    public void PrettifyCommand_IndentsCurrentXmlText()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("<root><a>1</a></root>", isEditable: true, StructuredTextKind.Xml);

        model.Text = "<root><a>1</a><b>2</b></root>";
        model.PrettifyCommand.Execute().Subscribe();

        Assert.Contains("\n", model.Text);
    }

    [Fact]
    public void MinifyCommand_CompactsCurrentXmlText()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("<root>\n  <a>1</a>\n</root>", isEditable: true, StructuredTextKind.Xml);

        model.MinifyCommand.Execute().Subscribe();

        Assert.DoesNotContain("\n", model.Text);
    }

    [Fact]
    public void CurrentText_ReflectsText()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("{\"a\":1}", isEditable: true, StructuredTextKind.Json);

        model.Text = "{\"a\":2}";

        Assert.Equal(model.Text, model.CurrentText);
    }

    [Fact]
    public void InvalidJsonWarning_DoesNotBlockEditingOrDirtyState()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("{\"a\":1}", isEditable: true, StructuredTextKind.Json);

        model.Text = "not valid json";

        Assert.True(model.HasInvalidTextWarning);
        Assert.Contains("JSON", model.InvalidWarningMessage);
        Assert.True(model.ShowSaveCancel);
        Assert.Equal("not valid json", model.CurrentText);
    }

    [Fact]
    public void InvalidXmlWarning_DoesNotBlockEditingOrDirtyState()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("<root/>", isEditable: true, StructuredTextKind.Xml);

        model.Text = "not valid xml";

        Assert.True(model.HasInvalidTextWarning);
        Assert.Contains("XML", model.InvalidWarningMessage);
        Assert.True(model.ShowSaveCancel);
        Assert.Equal("not valid xml", model.CurrentText);
    }

    [Fact]
    public void Initialize_None_HidesFormattingButtonsAndNeverWarns()
    {
        var model = new StructuredTextCellDialogViewModel();

        model.Initialize("plain free text", isEditable: true, StructuredTextKind.None);

        Assert.Equal("Text", model.Title);
        Assert.False(model.ShowFormattingButtons);
        Assert.False(model.HasInvalidTextWarning);
    }

    [Fact]
    public void Initialize_NoneWithEmptyValue_StaysNoneUntilRecognizableContentIsTyped()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize(null, isEditable: true, StructuredTextKind.None);

        Assert.Equal(StructuredTextKind.None, model.Kind);
        Assert.False(model.ShowFormattingButtons);

        model.Text = "{\"a\":1}";

        Assert.Equal(StructuredTextKind.Json, model.Kind);
        Assert.Equal("JSON", model.Title);
        Assert.True(model.ShowFormattingButtons);
    }

    [Fact]
    public void Initialize_NoneWithEmptyValue_SwitchesToXmlWhenXmlIsTyped()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize(null, isEditable: true, StructuredTextKind.None);

        model.Text = "<root><a>1</a></root>";

        Assert.Equal(StructuredTextKind.Xml, model.Kind);
        Assert.Equal("XML", model.Title);
    }

    [Fact]
    public void Kind_StaysStickyWhileTextIsTemporarilyInvalidMidEdit()
    {
        var model = new StructuredTextCellDialogViewModel();
        model.Initialize("{\"a\":1}", isEditable: true, StructuredTextKind.Json);

        model.Text = "{\"a\":";

        Assert.Equal(StructuredTextKind.Json, model.Kind);
        Assert.True(model.ShowFormattingButtons);
        Assert.True(model.HasInvalidTextWarning);
    }
}
