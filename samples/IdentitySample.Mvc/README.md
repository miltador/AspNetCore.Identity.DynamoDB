## How to Run

You need to have DynamoDB exposed through `127.0.0.1:8000`. If not, you can get it up through [Docker](https://www.docker.com/):

```bash
docker run -p 8000:8000 dwmkerr/dynamodb
```

After that, you can run the application with below commands:

```bash
dotnet restore
dotnet run
```