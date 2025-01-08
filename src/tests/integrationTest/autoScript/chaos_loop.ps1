$envFilePath = "./env/.local_env"
if (Test-Path $envFilePath) {
    Get-Content $envFilePath | ForEach-Object {
        if ($_ -match "^\s*([^#][^=]+?)\s*=\s*(.+)\s*$") {
            $key = $matches[1]
            $value = $matches[2]
			
		    if ($value.StartsWith('"') -and $value.EndsWith('"')) {
                $value = $value.Substring(1, $value.Length - 2)
            }
			
            Set-Item -Path Env:\$key -Value $value
            Write-Host "Set environment variable: $key=$value"
        }
    }
} else {
    Write-Host "Error: .env file not found."
}

for ($i = 1; $i -le 25; $i++) {
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
    dotnet test IntegrationTester.sln --logger:"console;verbosity=detailed"
}


