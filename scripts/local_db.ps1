$env:DYNAMODB_INSTALL_DIR = "$pwd\.dynamodb"

if(!(Test-Path -Path $env:DYNAMODB_INSTALL_DIR )) {
  mkdir $env:DYNAMODB_INSTALL_DIR -Force | Out-Null
  $env:DYNAMO_ARCHIVE_FILE = [System.IO.Path]::GetTempFileName()
  (New-Object System.Net.WebClient).DownloadFile("https://s3-us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz", $env:DYNAMO_ARCHIVE_FILE)
  & cmd.exe '/C 7z x %DYNAMO_ARCHIVE_FILE% -so | 7z x -aoa -si -ttar -o%DYNAMODB_INSTALL_DIR%'
}

$args = @("-Djava.library.path=$env:DYNAMODB_INSTALL_DIR\DynamoDBLocal_lib", "-Djava.net.preferIPv4Stack=true", "-Xss1m", "-Xms320m", "-Xmx320m", "-jar", "$env:DYNAMODB_INSTALL_DIR\DynamoDBLocal.jar", "-port", "8000", "-inMemory", "-sharedDb")
& java.exe $args