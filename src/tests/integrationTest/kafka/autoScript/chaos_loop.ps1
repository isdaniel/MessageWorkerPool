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

# Run dotnet test
dotnet test IntegrationTester.kafka.sln --configuration Release --logger:"console;verbosity=detailed"
