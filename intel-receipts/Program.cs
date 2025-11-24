using Azure;
using Azure.AI.DocumentIntelligence;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;


//dotnet add package Azure.AI.DocumentIntelligence

class Program
{
    static async Task Main(string[] args)
    {
        Console.Write("Enter the full path to the folder with receipt images: ");
        string folderPath = Console.ReadLine();

        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine("Invalid folder path.");
            return;
        }

        string endpoint = "AI_FOUNDRY_AI_SERVICE_ENDPOINT";
        string apiKey = "AI_FOUNDRY_API_KEY"; // Replace with your actual API Key

        AzureKeyCredential credential = new AzureKeyCredential(apiKey);
        DocumentIntelligenceClient client = new DocumentIntelligenceClient(new Uri(endpoint), credential);

        string[] imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
        // Prepare a list to store the receipt data
        List<List<string>> tableRows = new List<List<string>>();
  
        var ncount = 0;
        foreach (var imagePath in imageFiles)
        {
            ncount++;
            string extension = Path.GetExtension(imagePath).ToLower();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".pdf")
                continue;

            Console.WriteLine($"\nProcessing file: {ncount}/{imageFiles.Count()}");
            var receiptData = await ReceiptCheckAsync(client, imagePath);
            tableRows.Add(receiptData);
        }
        Console.WriteLine("\n\n Information collected\nn");
        PrintReceiptTable2(tableRows);
    }


    static async Task<List<string>> ReceiptCheckAsync(DocumentIntelligenceClient client, string imagePath)
    {
        using FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
        BinaryData content = BinaryData.FromStream(stream);

        // Directly pass the file path to AnalyzeDocumentAsync
        Operation<AnalyzeResult> operation = await client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-receipt",
            content // Provide the image path directly
        );
        AnalyzeResult result = operation.Value;

        List<string> rowData = new List<string> { Path.GetFileName(imagePath) }; // Start with the filename as the first column

        foreach (AnalyzedDocument doc in result.Documents)
        {
            string GetString(DocumentField field) => field?.FieldType == DocumentFieldType.String ? field.ValueString : null;
            double? GetCurrency(DocumentField field) => field?.FieldType == DocumentFieldType.Currency ? field.ValueCurrency?.Amount : null;
            DateTimeOffset? GetDate(DocumentField field) => field?.FieldType == DocumentFieldType.Date ? field.ValueDate : null;

            var fields = doc.Fields;

            // Extract fields from the receipt
            string vendor = fields.TryGetValue("MerchantName", out var nameField) ? GetString(nameField) : null;
            string address = fields.TryGetValue("MerchantAddress", out var addrField) ? GetString(addrField) : null;
            var date = fields.TryGetValue("TransactionDate", out var dateField) ? GetDate(dateField) : null;

            double? subtotal = fields.TryGetValue("Subtotal", out var subtotalField) ? GetCurrency(subtotalField) : null;
            double? tax = fields.TryGetValue("TotalTax", out var taxField) ? GetCurrency(taxField) : null;
            double? tip = fields.TryGetValue("Tip", out var tipField) ? GetCurrency(tipField) : null;
            double? total = fields.TryGetValue("Total", out var totalField) ? GetCurrency(totalField) : null;

            // Add the extracted data as the row for this receipt
            rowData.Add(vendor);
            rowData.Add(address);
            rowData.Add(date?.ToString() ?? "");
            rowData.Add(subtotal?.ToString("C") ?? "");
            rowData.Add(tax?.ToString("C") ?? "");
            rowData.Add(tip?.ToString("C") ?? "");
            rowData.Add(total?.ToString("C") ?? "");
        }

        return rowData;
    }

    static void PrintReceiptTable(List<List<string>> tableRows)
    {
        // Define column headers
        string header1 = "File Name";
        string header2 = "Vendor";
        string header3 = "Address";
        string header4 = "Date";
        string header5 = "Subtotal";
        string header6 = "Tax";
        string header7 = "Tip";
        string header8 = "Total";

        // Calculate column widths based on longest value
        int col1Width = Math.Max(header1.Length, "File Name".Length);
        int col2Width = Math.Max(header2.Length, "Vendor".Length);
        int col3Width = Math.Max(header3.Length, "Address".Length);
        int col4Width = Math.Max(header4.Length, "Date".Length);
        int col5Width = Math.Max(header5.Length, "Subtotal".Length);
        int col6Width = Math.Max(header6.Length, "Tax".Length);
        int col7Width = Math.Max(header7.Length, "Tip".Length);
        int col8Width = Math.Max(header8.Length, "Total".Length);

        // Format for printing rows
        string rowFormat = $"| {{0,-{col1Width}}} | {{1,-{col2Width}}} | {{2,-{col3Width}}} | {{3,-{col4Width}}} | {{4,-{col5Width}}} | {{5,-{col6Width}}} | {{6,-{col7Width}}} | {{7,-{col8Width}}} |";

        // Print the header
        Console.WriteLine(string.Format(rowFormat, header1, header2, header3, header4, header5, header6, header7, header8));
        Console.WriteLine($"|{new string('-', col1Width + 2)}|{new string('-', col2Width + 2)}|{new string('-', col3Width + 2)}|{new string('-', col4Width + 2)}|{new string('-', col5Width + 2)}|{new string('-', col6Width + 2)}|{new string('-', col7Width + 2)}|{new string('-', col8Width + 2)}|");

        // Print each row of data
        foreach (var row in tableRows)
        {
            Console.WriteLine(string.Format(rowFormat, row.ToArray()));
        }

        Console.WriteLine("\n\n");
    }

static void PrintReceiptTable2(List<List<string>> tableRows)
{
    // Check if tableRows is null or empty
    if (tableRows == null || tableRows.Count == 0)
    {
        Console.WriteLine("No data to display.");
        return;
    }

    // Define column headers
    string header1 = "File Name";
    string header2 = "Vendor";
    string header3 = "Address";
    string header4 = "Date";
    string header5 = "Subtotal";
    string header6 = "Tax";
    string header7 = "Tip";
    string header8 = "Total";

    // List to calculate max length for each column
    List<int> columnWidths = new List<int>
    {
        header1.Length,
        header2.Length,
        header3.Length,
        header4.Length,
        header5.Length,
        header6.Length,
        header7.Length,
        header8.Length
    };

    // Calculate the maximum length for each column (header + row content)
    foreach (var row in tableRows)
    {
        if (row == null) continue; // Skip null rows
        columnWidths[0] = Math.Max(columnWidths[0], row[0]?.Length ?? 0);
        columnWidths[1] = Math.Max(columnWidths[1], row[1]?.Length ?? 0);
        columnWidths[2] = Math.Max(columnWidths[2], row[2]?.Length ?? 0);
        columnWidths[3] = Math.Max(columnWidths[3], row[3]?.Length ?? 0);
        columnWidths[4] = Math.Max(columnWidths[4], row[4]?.Length ?? 0);
        columnWidths[5] = Math.Max(columnWidths[5], row[5]?.Length ?? 0);
        columnWidths[6] = Math.Max(columnWidths[6], row[6]?.Length ?? 0);
        columnWidths[7] = Math.Max(columnWidths[7], row[7]?.Length ?? 0);
    }

    // Format for printing rows
    string rowFormat = $"| {{0,-{columnWidths[0]}}} | {{1,-{columnWidths[1]}}} | {{2,-{columnWidths[2]}}} | {{3,-{columnWidths[3]}}} | {{4,-{columnWidths[4]}}} | {{5,-{columnWidths[5]}}} | {{6,-{columnWidths[6]}}} | {{7,-{columnWidths[7]}}} |";

    // Print the header
    Console.WriteLine(string.Format(rowFormat, header1, header2, header3, header4, header5, header6, header7, header8));
    Console.WriteLine($"|{new string('-', columnWidths[0] + 2)}|{new string('-', columnWidths[1] + 2)}|{new string('-', columnWidths[2] + 2)}|{new string('-', columnWidths[3] + 2)}|{new string('-', columnWidths[4] + 2)}|{new string('-', columnWidths[5] + 2)}|{new string('-', columnWidths[6] + 2)}|{new string('-', columnWidths[7] + 2)}|");

    // Print each row of data
    foreach (var row in tableRows)
    {
        if (row == null) continue; // Skip null rows
        Console.WriteLine(string.Format(rowFormat, row.ToArray()));
    }

    Console.WriteLine("\n\n");
}



}
