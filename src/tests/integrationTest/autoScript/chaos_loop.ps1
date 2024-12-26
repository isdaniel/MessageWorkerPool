$env:RABBITMQ_HOSTNAME = "127.0.0.1"
$env:QUEUENAME = "integration-queue"
$env:RABBITMQ_PORT = "5672"
$env:EXCHANGENAME = "integration-exchange"
$env:DBConnection = "Data Source=127.0.0.1;Initial Catalog=orleans;User ID=sa;Password=test.123;TrustServerCertificate=true;"
$env:TOTAL_MESSAGE_COUNT = "100000"

for ($i = 1; $i -le 5; $i++) {
    Write-Host "Execution #$i"
    
    # Start the container
    $containerId = docker ps -a --filter "name=publisher" --format "{{.ID}}"
    if ($containerId) {
        docker start $containerId
        Write-Host "Started container with ID: $containerId"
    } else {
        Write-Host "No container found with the name 'publisher'."
    }
    Start-Sleep -Seconds 2
	
    # Run dotnet test
    dotnet test --configuration Release --logger:"console;verbosity=detailed"
}


