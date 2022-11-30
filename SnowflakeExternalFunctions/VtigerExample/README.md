# API with basic access authentication protection example (Vtiger)

- [API with basic access authentication protection example (Vtiger)](#api-with-basic-access-authentication-protection-example-vtiger)
  - [Configuration](#configuration)
  - [Vtiger CRM API integration code](#vtiger-crm-api-integration-code)
  - [Vtiger CRM usage example](#vtiger-crm-usage-example)

---

For this example, we will integrate the [Vtiger CRM](https://www.vtiger.com/) API into our Azure Function wrapper. Inside the [Vtiger API documentation](https://www.vtiger.com/docs/rest-api-for-vtiger) we found that it uses [basic access authentication](https://en.wikipedia.org/wiki/Basic_access_authentication) so we prepared a [configuration](#configuration) file with these fields:
 - endpointUrl, 
 - username,  
 - accessKey

## Configuration
> **NOTE:**
> Before you begin with the next step create a `local.settings.json` file with these properties inside a project root. This configuration file will be used when running Azure functions locally for testing. Collect your credentials from the Vtiger portal and fill out the configuration file.

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "VtigerEndpoint": "https://your_instance.odx.vtiger.com/restapi/v1/vtiger/default",
    "VtigerUserName": "yourUserName",
    "AccessKey": "yourAccessKey"
  }
}
```

When you publish to the Azure cloud append these settings in `Settings > Configuration` with this format: 
```json
  {
    "name": "VtigerEndpoint",
    "value": "https://your_instance.odx.vtiger.com/restapi/v1/vtiger/default",
    "slotSetting": false
  },
  {
    "name": "VtigerUserName",
    "value": "yourUserName",
    "slotSetting": false
  },
  {
    "name": "AccessKey",
    "value": "yourAccessKey",
    "slotSetting": false
  } 
``` 






## Vtiger CRM API integration code
After the configuration file was added and filled out with credentials you can run the function.

```c#
// Read required Vtiger access credentials form environment variables (configuration file)
string apiBaseUrl = Environment.GetEnvironmentVariable("VtigerEndpoint", EnvironmentVariableTarget.Process);
string userName = Environment.GetEnvironmentVariable("VtigerUserName", EnvironmentVariableTarget.Process);
string accessKey = Environment.GetEnvironmentVariable("AccessKey", EnvironmentVariableTarget.Process);
```

For basic authentication we need to add an authorization header to every API call so we added this header to our C# HTTP client.

```C#
// HTTP client is specified outside of azure function Run
// static HttpClient client = new HttpClient();
// ... 

var authenticationString = $"{userName}:{accessKey}";
var base64EncodedAuthenticationString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));

if (client.DefaultRequestHeaders.Contains("Authorization"))
{
    client.DefaultRequestHeaders.Remove("Authorization");
}
client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64EncodedAuthenticationString}");

```

Full Azure function code can be found in [VtigerExample.cs](./VtigerExample.cs) file. This function implements a call to vtiger query endpoint and can forward the result back to Snowflake.

## Vtiger CRM usage example

This example takes a `query_string` as a parameter ([query docs](https://www.vtiger.com/docs/rest-api-for-vtiger#/Query)):

```JSON
{
    "data":[
        [0, "Select count(*) from Potentials where potentialname like 'T%';"]
    ]
}
```
And this is the result of the query:
```JSON
{
    "data": [
        [
            0,
            {
                "success": true,
                "result": [
                    {
                        "count": "56"
                    }
                ]
            }
        ]
    ]
}
```