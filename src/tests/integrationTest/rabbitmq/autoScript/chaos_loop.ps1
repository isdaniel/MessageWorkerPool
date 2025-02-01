function Set-EnvFromFile($filePath) {
    if (Test-Path $filePath) {
        Get-Content $filePath | ForEach-Object {
            if ($_ -match "^\s*([^#][^=]+?)\s*=\s*(.+)\s*$") {
                $key = $matches[1]
                $value = $matches[2]

                # Remove surrounding quotes if they exist
                if ($value.StartsWith('"') -and $value.EndsWith('"')) {
                    $value = $value.Substring(1, $value.Length - 2)
                }

                # Set the environment variable
                Set-Item -Path Env:\$key -Value $value
                Write-Host "Set environment variable: $key=$value"
            }
        }
    } else {
        Write-Host "Error: $filePath not found."
    }
}

Set-EnvFromFile "./env/.env"
Set-EnvFromFile "./env/.local_env"

for ($i = 1; $i -le 20; $i++) {
    Write-Host "Execution #$i"

    # Start the container
    $containerId = docker ps -a --filter "name=envpreparatory" --format "{{.ID}}"
    if ($containerId) {
        docker start $containerId
        Write-Host "Started container with ID: $containerId"
    } else {
        Write-Host "No container found with the name 'envpreparatory'."
    }
    Start-Sleep -Seconds 2

    # Run dotnet test
    dotnet test IntegrationTester.rabbitmq.sln --logger:"console;verbosity=detailed"
}


