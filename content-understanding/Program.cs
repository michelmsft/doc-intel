using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace AnalyzerClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // --- Configuration ---
            string analyzerId = "GovClearanceAnalyzer";
            string apiVersion = "2025-05-01-preview";
            string endpoint = "https://ai-admin-8795.cognitiveservices.azure.com/";
            string key = "AdVlF0JkQmpp3l3m9TJqvUPoKPWLk5ALBSzhgkfBzSd2ZqQCrUP9JQQJ99BKACL93NaXJ3w3AAAAACOGZDn0"; // <-- Replace with your real key

            // API endpoint URL
            string requestUrl = $"{endpoint}contentunderstanding/analyzers/{analyzerId}:analyze?api-version={apiVersion}";

            // JSON payload
            string jsonPayload = @"
            {
                ""url"": ""https://demoadlsstorageai102.blob.core.windows.net/docs/clearances/Secret_Clearance_Interview_Transcript_George_Hart.pdf?sp=r&st=2025-11-24T06:16:30Z&se=2025-11-26T14:31:30Z&spr=https&sv=2024-11-04&sr=b&sig=IPGmp3FXlJavf%2BbtIin2iecxffIfGQQ0JJ7gCbVRWtU%3D""
            }";

             using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

            Console.WriteLine("Sending analyze request...\n");

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(requestUrl, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine("=== Analyze Response ===");
            Console.WriteLine($"HTTP Status: {response.StatusCode}");
            Console.WriteLine(responseBody);
            Console.WriteLine();

            // --- Extract resultId from response JSON ---
            using var doc = JsonDocument.Parse(responseBody);
            string resultId = doc.RootElement.GetProperty("id").GetString();

            Console.WriteLine($"Result ID: {resultId}");
            Console.WriteLine("Polling for completion...\n");

            // --- Poll for final result ---
            string finalResult = await PollAnalyzerResultAsync(endpoint, resultId, apiVersion, key);

            Console.WriteLine("\n===== FINAL RESULT =====");
            //Console.WriteLine(finalResult);

            //Console.WriteLine("\n===== PARSED FIELD VALUES =====");
            ExtractFields(finalResult);

            Console.WriteLine("\nDone.");
        }

        static async Task<string> PollAnalyzerResultAsync(string endpoint, string resultId, string apiVersion, string key)
        {
            string resultUrl = $"{endpoint}/contentunderstanding/analyzerResults/{resultId}?api-version={apiVersion}";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);

            while (true)
            {
                var response = await httpClient.GetAsync(resultUrl);
                string responseBody = await response.Content.ReadAsStringAsync();

                Console.WriteLine("=== Result Poll ===");
                Console.WriteLine($"HTTP Status: {response.StatusCode}");

                // Show only the first 200 chars
                string preview = responseBody.Length > 200
                    ? responseBody.Substring(0, 200) + "..."
                    : responseBody;

                Console.WriteLine(preview);
                Console.WriteLine();

                // Check for completion
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("status", out var statusElement))
                    {
                        var status = statusElement.GetString() ?? "";
                        if (status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) ||
                            status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                        {
                            // Return full content once done
                            return responseBody;
                        }
                    }
                }
                catch
                {
                    return responseBody; // if malformed JSON, return it
                }

                await Task.Delay(2000); // wait before polling again
            }
        }


        static void ExtractFields(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("result", out var result))
                {
                    Console.WriteLine("No 'result' property found.");
                    return;
                }

                if (!result.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    Console.WriteLine("No 'contents' array found.");
                    return;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("fields", out var fields) ||
                        fields.ValueKind != JsonValueKind.Object)
                    {
                        Console.WriteLine("No 'fields' object found in content.");
                        continue;
                    }

                    Console.WriteLine("=== Extracted Fields ===");

                    foreach (var fieldProp in fields.EnumerateObject())
                    {
                        string fieldName = fieldProp.Name;
                        JsonElement fieldData = fieldProp.Value;

                        if (!fieldData.TryGetProperty("type", out var typeElement))
                            continue;

                        string fieldType = typeElement.GetString() ?? "unknown";

                        // Handle string value
                        if (fieldType == "string")
                        {
                            if (fieldData.TryGetProperty("valueString", out var valueStringElement))
                            {
                                string value = valueStringElement.GetString() ?? "";
                                Console.WriteLine($"{fieldName}: {value}");
                            }
                        }
                        // Handle number value
                        else if (fieldType == "number")
                        {
                            if (fieldData.TryGetProperty("valueNumber", out var valueNumberElement))
                            {
                                double value = valueNumberElement.GetDouble();
                                Console.WriteLine($"{fieldName}: {value}");
                            }
                        }
                        // Handle array (e.g., Items)
                        else if (fieldType == "array")
                        {
                            Console.WriteLine($"{fieldName}:");

                            if (fieldData.TryGetProperty("valueArray", out var valueArrayElement) &&
                                valueArrayElement.ValueKind == JsonValueKind.Array)
                            {
                                int index = 1;
                                foreach (var item in valueArrayElement.EnumerateArray())
                                {
                                    Console.WriteLine($"  Item {index}:");

                                    if (item.TryGetProperty("valueObject", out var valueObjectElement) &&
                                        valueObjectElement.ValueKind == JsonValueKind.Object)
                                    {
                                        foreach (var subProp in valueObjectElement.EnumerateObject())
                                        {
                                            string subName = subProp.Name;
                                            JsonElement subData = subProp.Value;

                                            string subType = subData.GetProperty("type").GetString() ?? "";
                                            string subValue = "";

                                            if (subType == "string" &&
                                                subData.TryGetProperty("valueString", out var subValueStringElement))
                                            {
                                                subValue = subValueStringElement.GetString() ?? "";
                                            }
                                            else if (subType == "number" &&
                                                     subData.TryGetProperty("valueNumber", out var subValueNumberElement))
                                            {
                                                subValue = subValueNumberElement.GetDouble().ToString();
                                            }

                                            Console.WriteLine($"    {subName}: {subValue}");
                                        }
                                    }

                                    index++;
                                }
                            }
                        }
                        // Other types can be handled as needed
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while extracting fields:");
                Console.WriteLine(ex.Message);
            }
        }
    }
}