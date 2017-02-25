## How to Run

You need to have DynamoDB exposed through `localhost:8000`. If not, you can get it up through [Docker](https://www.docker.com/):

```bash
docker run -p 8000:8000 dwmkerr/dynamodb -sharedDb
```

After that, you can run the application with below commands:

```bash
dotnet restore
dotnet run
```