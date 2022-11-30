
- [About repo](#about-repo)
- [Query data from third-party APIs inside Snowflake](#query-data-from-third-party-apis-inside-snowflake)
  - [Simple example](#simple-example)
    - [Understanding input and output](#understanding-input-and-output)
    - [Agify implementation](#agify-implementation)
  - [Examples](#examples)
  - [Snowflake integration](#snowflake-integration)
    - [Code snippets](#code-snippets)
- [Cost](#cost)
- [Known limits](#known-limits)

---

# About repo
This repo contains Azure Function examples that integrate third-party APIs and can be integrated with Snowflake External Functions so you can use them from the Snowflake Editor. Its purpose is to provide some examples to illustrate the concept.

You can read the whole concept with an explained example in our [blog post](./) or continue reading here.


# Query data from third-party APIs inside Snowflake

We had a requirement to access data in Snowflake from an external source. Our external source is web application data. Users of the web application constantly change the data so we had to ensure that the data in Snowflake is always relevant.

The traditional approach would be to sync our web application database to Snowflake with ETL jobs. Sync processes can be complex, so we wanted to try an approach where we would get the data on-demand directly from our web application API and we would not need to worry about when and where to sync the data to Snowflake. External data also doesn't need to be stored in Snowflake because it is already stored in the web application.

We found Snowflake external functions in Snowflake documentation that can call cloud infrastructure like Azure functions. We got the idea that we could write our custom logic inside Azure Functions, where we could parse data sent from Snowflake and get our API input parameters. After that, we could implement the required authentication for the third-party API. Then forward those parameters to the API and the results from the API would then be returned back to Snowflake in the required format. In essence, Azure Functions serve as a middleman between Snowflake and third-party APIs. With parameters, we can request only the data we actually need from our source. Parameters can also be used for data manipulation. This can give users write and update capabilities on the source data within the Snowflake editor. 

## Simple example
Let's use a simple example to illustrate the concept using unprotected API [Agify](https://agify.io/) which predicts the age of a name. 

With a traditional GET request example `https://api.agify.io/?name=Michael` we would get a response like this:
```JSON
{
    "name": "Michael",
    "age": 70,
    "count": 233482
}
```

### Understanding input and output

For Azure functions to work with Snowflake external functions it is required that input and output data is in a specific [format](https://docs.snowflake.com/en/sql-reference/external-functions-data-format.html). Snowflake sends and expects data in a scalar format like shown in the snippets below and it is the same for input and output. This scalar format means that if you send 3 row inputs it expects 3 row outputs as a response.
```JSON
{
    "data":[
        [0, "Joe"],     // row 1 
        [1, "Tim"],     // row 2
        [2, "Michael"]  // row 3
    ]
}
```
The first column represents the row number and it is required. In other columns, we can include other parameters. In this example, the second column is an additional parameter of type `varchar` and this will be our input for external API calls.

We created a helper class for input and output data called `SnowflakeData.cs`
```C#
public class SnowflakeData
{
    public List<List<dynamic>> data { get; set; } = new List<List<dynamic>>();
}
```
 With this class, we can directly deserialize data sent from Snowflake to an object that we can work with inside Azure functions.
```C#
public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
    ILogger log)
{

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

    SnowflakeData inputData = JsonConvert.DeserializeObject<SnowflakeData>(requestBody);
    
    var outputData = new SnowflakeData();
    foreach (var row in inputData.data)
    {
        // Row processing
        List<dynamic> dynamicList = new List<dynamic>();
        var rowNumber = row[0];
        var name = row[1];
        dynamicList.Add(rowNumber); 

        // your custom logic
        // dynamicList.Add(new { ADD ADDITIONAL OUTPUT DATA });

        outputData.data.Add(dynamicList);
    }
    return new JsonResult(outputData);

}
```


### Agify implementation
Now let's implement the whole logic for Agify API. For example we can have a table like this, named Users in Snowflake. 

| row_id | user_name |
| -------| ----------|
| 1      | Joe       |
| 2      | Tim       |
| 3      | Michael   |

For these users, we would like to get their age and this data is in another application that has an API endpoint. So let's implement an Azure function that can do that. This is the code for Agify external function:

```C#
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
```
Within the Snowflake editor we can then call our external function with a call like this:
```SQL
 select GetAge(user_name) from Users;
```
> **Note:**
> Azure functions must be deployed to Azure and integrated into Snowflake for this to work. [Integration/Registration Process](#snowflake-integration)

This external function call will create a POST call to a registered Azure function endpoint with body data formatted like this:
```JSON
{
    "data":[
        [0, "Joe"],
        [1, "Tim"],
        [2, "Michael"]
    ]
}
```
And as a result, we will get data output formatted like this with JSON data in the second column:

```JSON
{
    "data": [
        [
            0,
            {
                "name": "Joe",
                "age": 54,
                "count": 56314
            }
        ],
        [
            1,
            {
                "name": "Tim",
                "age": 56,
                "count": 45214
            }
        ],
        [
            2,
            {
                "name": "Michael",
                "age": 70,
                "count": 233482
            }
        ]
    ]
}
```
In Snowflake we can then use JSON processing capabilities to transform raw JSON into a table with [flatten functionality](https://docs.snowflake.com/en/user-guide/json-basics-tutorial-flatten.html). 

When we flatten the JSON result the table could look like this:
| row_id | user_name |age    | count    |
| -------| ----------|-------| ---------|
| 1      | Joe       |54     | 56314    |
| 2      | Tim       |2      | 45214    |
| 3      | Michael   |3      | 233482   |

This is how the architecture looks like for this example:  
![Unprotected API Architecture](./img/unprotectedAPI.png)

Basically, this is how the concept works. Most of the APIs are protected and they have different types of protection so we prepared a few Azure function examples (wrappers) that demonstrate how to call those protected APIs. You can see the examples in our GitHub repository.


## Examples

We prepared:

- A simple example using an [unprotected API](./SnowflakeExternalFunctions/UnprotectedApiExample/UnprotectedApiExample.cs)

- An example with API that uses basic access authentication ([Vtiger CRM integration with Snowflake](./SnowflakeExternalFunctions/VtigerExample/README.md))

- An example that calls the Microsoft Graph API ([Microsoft Graph and Snowflake integration](./SnowflakeExternalFunctions/MicrosoftGraphExample/README.md))


You can join us in the repository and contribute your API integration example. 




## Snowflake integration

Follow the official step-by-step [guide](https://docs.snowflake.com/en/sql-reference/external-functions-creating-azure-ui.html) for integrating external functions, except that you use your own custom Azure functions. That means you deploy them to the cloud and continue with the guide from this [point](https://docs.snowflake.com/en/sql-reference/external-functions-creating-azure-ui-remote-service.html#enable-app-service-authentication-for-the-azure-function-app) on.

### Code snippets

Integration:

```sql
create or replace api integration YOUR_INTEGRATION_NAME
api_provider = azure_api_management
azure_tenant_id = '<azure_tenant_id>'
azure_ad_application_id = '<azure_ad_application_id>'
api_allowed_prefixes = ('<link_to_api_allowed_prefixes>')
enabled = true;
```

Function registration:
```sql
create or replace external function YOUR_FUNCTION_NAME_IN_SNOWFLAKE(user_name varchar)
returns variant
api_integration = YOUR_INTEGRATION_NAME
as '<link_to_api_allowed_prefixes>/<your_azure_function_name_in_api_management>';
```

# Cost 

- [Azure functions pricing](https://azure.microsoft.com/en-us/pricing/details/functions/) is currently very affordable. You can have 1 million executions per month for free. 

- But [API Management pricing](https://azure.microsoft.com/en-us/pricing/details/api-management/) can be a bit more expensive with a minimum cost of around 40 USD per month in developer tier configuration. 
  
- [Snowflake Billing for External Functions Usage](https://docs.snowflake.com/en/sql-reference/external-functions-introduction.html#billing-for-external-functions-usage)

# Known limits

- Snowflake External functions [limitations](https://docs.snowflake.com/en/sql-reference/external-functions-introduction.html#limitations-of-external-functions).
