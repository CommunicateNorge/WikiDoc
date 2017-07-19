## WikiDoc

This is WikiDoc, a documentation framework for IT-Solutions. We had the basic idea to make a targeted Wiki solution to automaticly and manually docuement individual components and procedures in a unified way on one single web-site. 

## Code Example

Show what the library does as concisely as possible, developers should be able to figure out **how** your project solves their problem by looking at the code example. Make sure the API you are showing off is obvious, and that your code is short and concise.

## Motivation

A short description of the motivation behind the creation and maintenance of the project. This should explain **why** the project exists.

## Installation

Provide code examples and explanations of how to get the project.

## API Reference

Depending on the size of the project, if it is small and simple enough the reference docs can be added to the README. For medium size to larger projects it is important to at least provide a link to where the API reference docs live.

## Tests

Describe and show how to run the tests with code examples.

## Sample Config File

```{
  "Name": "Demo",
  "PrimaryAzureStorage": "**",
  "SecondaryAzureStorage": "**",
  "PrimaryAzureStorageContainerName": "main",
  "PrimaryAzureStorageTableIndex": "index",
  "PrimaryAzureStorageTableAudit": "audit",
  "SecondaryAzureStorageContainerName": "main",
  "TfsCacheDurationMin": 1440,
  "EnvironmentInfo": [
    {
      "Name": "Main",
      "Container": "main"
    },
    {
      "Name": "Test",
      "Container": "test"
    },
    {
      "Name": "Production",
      "Container": "production"
    }
  ],
  "BizTalk": [
    {
      "Name": "BizTalk",
      "Type": "TFS",
      "User": null,
      "Domain": "test.com",
      "Pwd": null,
      "TfsUri": "",
      "Environments": [
        {
          "EnvNo": 0,
          "Address": ""
        },
        {
          "EnvNo": 1,
          "Address": ""
        },
        {
          "EnvNo": 2,
          "Address": ""
        }
      ],
      "IgnoreApps": []
    }
  ],
  "ApiServices": [
    {
      "Name": "Petstore",
      "Type": "Swagger",
      "Authentication": {
        "Type": "None"
      },
      "Environments": [
        {
          "EnvNo": 1,
          "Address": "https://raw.githubusercontent.com/OAI/OpenAPI-Specification/master/examples/v2.0/json/petstore.json"
        }
      ]
    }
  ],
  "Databases": [ ]
}```

## License

A short snippet describing the license (MIT, Apache, etc.)