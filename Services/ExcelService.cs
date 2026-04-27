using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using ExcelDataReader;

namespace SmsGatewayApp.Services
{
    public class ExcelService
    {
        public List<(string Phone, string? Name)> ReadContacts(string filePath)
        {
            var contacts = new List<(string Phone, string? Name)>();
            
            // Required for .NET Core / .NET 10 to support older Excel formats
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                        {
                            UseHeaderRow = true
                        }
                    });

                    var table = result.Tables[0];
                    int phoneColumnIndex = -1;
                    int nameColumnIndex = -1;

                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        string colName = table.Columns[i].ColumnName.ToLower();
                        if (colName.Contains("phone") || colName.Contains("tel") || colName.Contains("raqam") || colName.Contains("nomer"))
                        {
                            phoneColumnIndex = i;
                        }
                        else if (colName.Contains("name") || colName.Contains("ism") || colName.Contains("fio"))
                        {
                            nameColumnIndex = i;
                        }
                    }

                    if (phoneColumnIndex == -1 && table.Columns.Count > 0)
                        phoneColumnIndex = 0;

                    if (phoneColumnIndex != -1)
                    {
                        foreach (DataRow row in table.Rows)
                        {
                            string? phone = row[phoneColumnIndex]?.ToString()?.Trim();
                            string? name = nameColumnIndex != -1 ? row[nameColumnIndex]?.ToString()?.Trim() : null;

                            if (!string.IsNullOrEmpty(phone))
                            {
                                contacts.Add((phone, string.IsNullOrEmpty(name) ? null : name));
                            }
                        }
                    }
                }
            }
            return contacts.GroupBy(c => c.Phone).Select(g => g.First()).ToList();
        }

        public void WriteContacts(string filePath, IEnumerable<SmsGatewayApp.Models.SmsContact> contacts)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Contacts");
                worksheet.Cell(1, 1).Value = "Ism";
                worksheet.Cell(1, 2).Value = "Telefon";

                int row = 2;
                foreach (var contact in contacts)
                {
                    worksheet.Cell(row, 1).Value = contact.Name;
                    worksheet.Cell(row, 2).Value = contact.Phone;
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }
    }
}
