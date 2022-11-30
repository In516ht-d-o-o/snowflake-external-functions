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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SnowflakeExternalFunctions.VtigerExample
{
    public class VtigerExample
    {
        static HttpClient client = new HttpClient();

        [FunctionName("VtigerExample")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function VtigerExample processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            SnowflakeData data = JsonConvert.DeserializeObject<SnowflakeData>(requestBody);

            // Read required Vtiger access credentials form environment variables
            string apiBaseUrl = Environment.GetEnvironmentVariable("VtigerEndpoint", EnvironmentVariableTarget.Process);
            string userName = Environment.GetEnvironmentVariable("VtigerUserName", EnvironmentVariableTarget.Process);
            string accessKey = Environment.GetEnvironmentVariable("AccessKey", EnvironmentVariableTarget.Process);

            var authenticationString = $"{userName}:{accessKey}";
            var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

            if (client.DefaultRequestHeaders.Contains("Authorization"))
            {
                client.DefaultRequestHeaders.Remove("Authorization");
            }
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64EncodedAuthenticationString}");


            var sfReturnData = new SnowflakeData();
            long counter = 0;
            foreach (var dataItem in data.data)
            {
                List<dynamic> dynamicList = new List<dynamic>();
                dynamicList.Add(counter++);

                string vtigerQuery = $"{dataItem[1]}";
                try
                {

                    HttpResponseMessage response = await client.GetAsync($"{apiBaseUrl}/query?query={vtigerQuery}");

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

                    log.LogError(ex, ex.Message);
                    dynamicList.Add(new ExternalFunctionException { ExceptionMessage = $"VtigerQueryInput: {vtigerQuery} errorMessage: {ex.Message}" });
                }
                sfReturnData.data.Add(dynamicList);

            }

            return new JsonResult(sfReturnData);
        }

        }
}
