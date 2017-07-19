## WikiDoc

This is WikiDoc, a documentation framework for IT-Solutions. We had the basic idea to make a targeted Wiki solution to automaticly and manually docuement individual components and procedures in a unified way on one single web-site. 

## Disclaimer
We are currently updating the readme and documentation for the project and ironing out the set up process. If you want to download the source now i wont't stop you but be prepared to make it work on your own. Hopefully a full setup guide and documentation will be up and running pretty soon.  

## Documentation

The main documentation will be contained within WikiDoc itself at some point. For now onjoy this diagram that explains how the project functions in broad strokes:

![alt text](http://i.imgur.com/DLZcDMS.png "Diagram WikiDoc")

## Getting started

### Config file
Firstly you need to clone the repo and start it in Visual Studio. The solution will probably not run off the bat as you need a configuration file. You can use this sample configuration file to get started:

```{
  "Name": "Demo",
  "PrimaryAzureStorage": "**", //Input needed
  "SecondaryAzureStorage": "**", //Input needed
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
}
```

You actually only need 2 inputs to this file to make it work and that is 2 connection strings to 2 Azure storage accounts. The project will use these to create its own containers later. Put the configuration file in the configurations folder. 

## License

[GNU General Public License v3.0](https://github.com/CommunicateNorge/WikiDoc/blob/master/LICENSE)