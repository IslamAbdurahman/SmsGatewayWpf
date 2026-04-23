using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using ExcelDataReader;

namespace SmsGatewayApp.Services
{
    public class ExcelService
    {
        public List<string> ReadPhoneNumbers(string filePath)
        {
            var phoneNumbers = new List<string>();
            
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

                    // Try to find column named "Phone" or "Raqam" or use the first column
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        string colName = table.Columns[i].ColumnName.ToLower();
                        if (colName.Contains("phone") || colName.Contains("tel") || colName.Contains("raqam"))
                        {
                            phoneColumnIndex = i;
                            break;
                        }
                    }

                    if (phoneColumnIndex == -1 && table.Columns.Count > 0)
                        phoneColumnIndex = 0;

                    if (phoneColumnIndex != -1)
                    {
                        foreach (DataRow row in table.Rows)
                        {
                            string? phone = row[phoneColumnIndex]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(phone))
                            {
                                phoneNumbers.Add(phone);
                            }
                        }
                    }
                }
            }
            return phoneNumbers.Distinct().ToList();
        }
    }
}
