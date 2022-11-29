using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SnowflakeExternalFunctions.HelperClasses.Exceptions;
using SnowflakeExternalFunctions.HelperClasses.Snowflake;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SnowflakeExternalFunctions.UnprotectedApiExample
{
    public static class UnprotectedApiExample
    {
        // Initialize HTTP Client
        static HttpClient client = new HttpClient();

        [FunctionName("UnprotectedApiExample")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function UnprotectedApiExample processed a request.");

            // Get request parameters sent from Snowflake
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            SnowflakeData data = JsonConvert.DeserializeObject<SnowflakeData>(requestBody);

            string apiBaseUrl = "https://api.agify.io";
            // Or read it from config
            // string apiBaseUrl = Environment.GetEnvironmentVariable("ApiBaseUrl", EnvironmentVariableTarget.Process);

            var sfReturnData = new SnowflakeData();
            long counter = 0;
            foreach (var dataItem in data.data)
            {
                List<dynamic> dynamicList = new List<dynamic>();
                dynamicList.Add(counter++);

                // Get the name parameter sent from Snowflake
                string name = $"{dataItem[1]}";
                try
                {
                    // Make HTTP calls to third-party API with parameters
                    HttpResponseMessage response = await client.GetAsync($"{apiBaseUrl}?name={name}");

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsAsync<dynamic>();

                        dynamicList.Add(responseContent);
                    }
                    else
                    {
                        var body = response.Content.ReadAsStringAsync().Result;
                        throw new Exception(body);
                    }


                }
                catch (Exception ex)
                {
                    // Return a special response with an exception message for failed requests if they happen
                    log.LogError(ex, ex.Message);
                    dynamicList.Add(new ExternalFunctionException { ExceptionMessage = $"name: {name} errorMessage: {ex.Message}" });
                }
                sfReturnData.data.Add(dynamicList);

            }

            return new JsonResult(sfReturnData);
        }
    }
}
