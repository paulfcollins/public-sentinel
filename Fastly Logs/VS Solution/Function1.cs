using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Threading.Tasks;

namespace fastlylogs
{
    public static class Function1
    {
        [FunctionName("FastlyLogIngestion")]
        public static void Run([BlobTrigger("fastly-logs/{name}", Connection = "storageString")]Stream myBlob, string name, ILogger log, ExecutionContext context)
        {  
        //https://docs.microsoft.com/en-us/rest/api/loganalytics/create-request
       
            //Get the application settings
            //You need to have the following defined for this to work:
            //"storageString": storage endpoint for the blob to monitor
            //"workspaceID": The GUID ID of the Log Analytics workspace
            //"logAnalyticsURL": "https://<yourworkspaceID>.ods.opinsights.azure.com/api/logs?api-version=2016-04-01",
            //"sharedKey": the shared key for your workspace,
            //"logType":  a string which will be the custom log type

            //https://www.koskila.net/how-to-access-azure-function-apps-settings-from-c/
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var logAnalyticsURL = config["logAnalyticsURL"];
            var workspaceID = config["workspaceID"];
            var sharedKey = config["sharedKey"];
            var logType = config["logType"];

            String outputJSON = "[\n";
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            using (var reader = new StreamReader(myBlob))
            {
                //write out the first line so we can include the comma at the start of following lines
                var line = reader.ReadLine();
                outputJSON = outputJSON + line;

                //Read the rest of the file
                while (!reader.EndOfStream)
                {
                    //Read our lines one by one and split into values
                    line = reader.ReadLine();
                    //Add the line to the output
                    outputJSON = outputJSON + ",\n" + line;
                }

                //Close the JSON
                outputJSON = outputJSON + "\n]";

                log.LogInformation("Added: " + outputJSON);
            }

            //upload the data

            //Get the current time
            //https://docs.microsoft.com/en-us/dotnet/api/system.datetime.now?view=netframework-4.8
            DateTime timestamp = DateTime.Now;

            //Set up variables with required date formats for current time
            //Note you may want to take the time generated from the actual log time, perhaps the time of the Blob creation, or a line in the file?
            //You may also want to decontruct the file into lines and send each one as a log event with it's correct time
            //https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings
            String xMSDate = timestamp.ToString("R");
            String timeGenerated = timestamp.ToString("O");

            //https://docs.microsoft.com/en-us/azure/azure-monitor/platform/data-collector-api
            // Create a hash for the API signature
            var datestring = DateTime.UtcNow.ToString("r");
            var jsonBytes = Encoding.UTF8.GetBytes(outputJSON);
            string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + xMSDate + "\n/api/logs";
            string hashedString = "";
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(sharedKey);
            byte[] messageBytes = encoding.GetBytes(stringToHash);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                hashedString = Convert.ToBase64String(hash);
            }

            string signature = "SharedKey " + workspaceID + ":" + hashedString;

            try
            {
                System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", logType);
                client.DefaultRequestHeaders.Add("Authorization", signature);
                client.DefaultRequestHeaders.Add("x-ms-date", xMSDate);
                client.DefaultRequestHeaders.Add("time-generated-field", timeGenerated);

                System.Net.Http.HttpContent httpContent = new StringContent(outputJSON, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(logAnalyticsURL), httpContent);

                System.Net.Http.HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
                log.LogInformation("Return Result: " + result);
            }
            catch (Exception excep)
            {
                log.LogInformation("API Post Exception: " + excep.Message);
            }
        }
    }
}
