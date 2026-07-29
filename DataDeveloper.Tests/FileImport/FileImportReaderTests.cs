using System.IO.Compression;
using System.Text;
using DataDeveloper.Data.Services.FileImport;
using Xunit;

namespace DataDeveloper.Tests.FileImport;

public class FileImportReaderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var path in _tempFiles.Where(File.Exists))
            File.Delete(path);
    }

    [Fact]
    public void ReadPreview_Csv_ExtractsHeadersAndSampleRows()
    {
        var path = CreateTempFile(".csv", "Id,Name,Active\n1,Alice,true\n2,Bob,false\n");

        var preview = FileImportReader.ReadPreview(path, sampleRowCount: 10);

        Assert.Equal(new[] { "Id", "Name", "Active" }, preview.Headers);
        Assert.Equal(2, preview.SampleRows.Count);
        Assert.Equal("1", preview.SampleRows[0][0]);
        Assert.Equal("Alice", preview.SampleRows[0][1]);
        Assert.Equal("Bob", preview.SampleRows[1][1]);
    }

    [Fact]
    public void ReadPreview_Csv_GeneratesPlaceholderNamesForBlankHeaderCells()
    {
        var path = CreateTempFile(".csv", "Id,,Active\n1,x,true\n");

        var preview = FileImportReader.ReadPreview(path, sampleRowCount: 10);

        Assert.Equal(new[] { "Id", "Column2", "Active" }, preview.Headers);
    }

    [Fact]
    public void ReadPreview_Csv_RespectsSampleRowCountLimit()
    {
        var rows = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"{i},name{i}"));
        var path = CreateTempFile(".csv", $"Id,Name\n{rows}\n");

        var preview = FileImportReader.ReadPreview(path, sampleRowCount: 5);

        Assert.Equal(5, preview.SampleRows.Count);
    }

    [Fact]
    public void ReadAllRows_Csv_StreamsEveryDataRowExcludingHeader()
    {
        var path = CreateTempFile(".csv", "Id,Name\n1,Alice\n2,Bob\n3,Carol\n");

        var rows = FileImportReader.ReadAllRows(path).ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("1", rows[0][0]);
        Assert.Equal("Carol", rows[2][1]);
    }

    [Fact]
    public void ReadPreview_Xlsx_ExtractsHeadersAndRows()
    {
        var path = CreateTempXlsx();

        var preview = FileImportReader.ReadPreview(path, sampleRowCount: 10);

        Assert.Equal(new[] { "Id", "Name" }, preview.Headers);
        Assert.Equal(2, preview.SampleRows.Count);
        Assert.Equal(1d, preview.SampleRows[0][0]);
        Assert.Equal("Alice", preview.SampleRows[0][1]);
        Assert.Equal("Bob", preview.SampleRows[1][1]);
    }

    [Theory]
    [InlineData("file.csv", true)]
    [InlineData("file.xls", true)]
    [InlineData("file.xlsx", true)]
    [InlineData("FILE.XLSX", true)]
    [InlineData("file.txt", false)]
    [InlineData("file", false)]
    public void IsSupportedFile_RecognizesOnlyCsvXlsXlsx(string fileName, bool expected)
    {
        Assert.Equal(expected, FileImportReader.IsSupportedFile(fileName));
    }

    private string CreateTempFile(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        File.WriteAllText(path, content, Encoding.UTF8);
        _tempFiles.Add(path);
        return path;
    }

    private string CreateTempXlsx()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        _tempFiles.Add(path);

        using (var stream = new FileStream(path, FileMode.Create))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RelsXml);
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", SheetXml);
        }

        return path;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string ContentTypesXml =
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
<Default Extension=""xml"" ContentType=""application/xml""/>
<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
<Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
</Types>";

    private const string RelsXml =
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";

    private const string WorkbookXml =
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
<sheets>
<sheet name=""Sheet1"" sheetId=""1"" r:id=""rId1""/>
</sheets>
</workbook>";

    private const string WorkbookRelsXml =
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
</Relationships>";

    private const string SheetXml =
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
<sheetData>
<row r=""1"">
<c r=""A1"" t=""inlineStr""><is><t>Id</t></is></c>
<c r=""B1"" t=""inlineStr""><is><t>Name</t></is></c>
</row>
<row r=""2"">
<c r=""A2""><v>1</v></c>
<c r=""B2"" t=""inlineStr""><is><t>Alice</t></is></c>
</row>
<row r=""3"">
<c r=""A3""><v>2</v></c>
<c r=""B3"" t=""inlineStr""><is><t>Bob</t></is></c>
</row>
</sheetData>
</worksheet>";
}
