using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;
using SnowflakeExternalFunctions.HelperClasses.Authentication;
using SnowflakeExternalFunctions.HelperClasses.Exceptions;
using SnowflakeExternalFunctions.HelperClasses.Snowflake;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnowflakeExternalFunctions.MicrosoftGraphExample
{
    public class MicrosoftGraphExample
    {
        [FunctionName("MicrosoftGraphExample")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function MicrosoftGraphExample processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            SnowflakeData inputData = JsonConvert.DeserializeObject<SnowflakeData>(requestBody);

            var outputData = new SnowflakeData();

            var authenticate = new Authenticate(log);
            try
            {
                var graphClient = authenticate.GetClient();
                var result = await graphClient
                    .Groups
                    .Request()
                    // .Top(999) default page size is 100 max page size can be 999
                    .GetAsync();

                var groups = new List<Group>();

                // aded paging for large data sets
                var pageIterator = PageIterator<Group>
                    .CreatePageIterator(
                        graphClient,
                        result,
                        // Callback executed for each item in
                        // the collection
                        (m) =>
                        {
                            groups.Add(m);
                            return true;
                        }
                    );

                await pageIterator.IterateAsync();

                foreach (var row in inputData.data)
                {
                    List<dynamic> dynamicRow = new List<dynamic>();
                    var rowNumber = row[0];
                    dynamicRow.Add(rowNumber);
                    dynamicRow.Add(groups.Select(x => new { x.Id, x.DisplayName }));
                    outputData.data.Add(dynamicRow);

                }

                return new JsonResult(outputData);
            }
            catch (Exception ex)
            {

                log.LogError(ex, ex.Message);
                foreach (var item in inputData.data)
                {
                    List<dynamic> dynamicList = new List<dynamic>();
                    dynamicList.Add(item[0]);
                    dynamicList.Add(new ExternalFunctionException { ExceptionMessage = ex.Message });

                    outputData.data.Add(dynamicList);
                }

                return new BadRequestObjectResult(outputData);
            }
        }
    }
}
