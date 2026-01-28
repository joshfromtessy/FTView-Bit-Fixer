using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using FT_AlarmFixer.Models;

namespace FT_AlarmFixer.Services;

public sealed class ExcelExporter
{
    public void Export(string outputPath, IReadOnlyList<AlarmRow> rows)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Alarm Tags");

        worksheet.Cell(1, 1).Value = "Tag";
        worksheet.Cell(1, 2).Value = "Description";

        var headerRange = worksheet.Range(1, 1, 1, 2);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x33, 0x3F, 0x48);
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thick;
        headerRange.Style.Border.BottomBorderColor = XLColor.FromArgb(0x22, 0x28, 0x30);

        var rowIndex = 2;
        foreach (var row in rows)
        {
            worksheet.Cell(rowIndex, 1).Value = row.Tag;
            worksheet.Cell(rowIndex, 2).Value = row.Description;

            if (rowIndex % 2 == 0)
            {
                var rowRange = worksheet.Range(rowIndex, 1, rowIndex, 2);
                rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xF4, 0xF6, 0xF8);
            }

            rowIndex++;
        }

        var usedRange = worksheet.Range(1, 1, Math.Max(1, rowIndex - 1), 2);
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        usedRange.Style.Border.OutsideBorderColor = XLColor.FromArgb(0x99, 0xA4, 0xAD);
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorderColor = XLColor.FromArgb(0xC7, 0xD0, 0xD8);

        worksheet.Column(1).Width = 40;
        worksheet.Column(2).Width = 90;
        worksheet.Row(1).Height = 22;
        worksheet.SheetView.FreezeRows(1);

        workbook.SaveAs(outputPath);
    }
}
