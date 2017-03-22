# ASP.NET Core Identity DynamoDB Store Samples

This folder contains various samples to showcase how to use ASP.NET Core Identity with this DynamoDB store provider.
There is not a huge difference to the standard use of ASP.NET Core Identity apart from hooking the DynamoDB store provider. 
So, it's always helpful to go through [the ASP.NET Core Identity documentation first](https://docs.asp.net/en/latest/security/authentication/identity.html) if you haven't already.

## Prerequisites

In order to run any of the sample here, you need DynamoDB exposed through `127.0.0.1:8000`. If you have [Docker](https://www.docker.com/) on you box, you can easily have one by executing the below command:

```bash
docker run -p 8000:8000 dwmkerr/dynamodb -sharedDb
```

You also need [.NET Core SDK](https://www.microsoft.com/net/core) installed to be able to execute `dotnet` commands.

## Samples

 * [IdentitySample.Mvc](./IdentitySample.Mvc): This is [the exact sample in Identity repository](https://github.com/aspnet/Identity/tree/1.0.0/samples/IdentitySample.Mvc), but it works with this DynamoDB provider rather than the EntityFramework one.
