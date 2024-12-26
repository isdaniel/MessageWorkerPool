param (
    [string]$containerName
)

# Function to get container status
function Get-ContainerStatus {
    param (
        [string]$ContainerName
    )
    # Parse the status of the specified container
    $containerInfo = docker ps -a | Select-String -Pattern $ContainerName
    if ($containerInfo) {
        # Extract the status column from the `docker ps` output
        $status = ($containerInfo -split '\s{2,}' | Where-Object { $_ -match "Up|Exited" })
		if ($status -match '^(Up|Exited)') {
			return $matches[1]
		}
    }
    else {
        Write-Host "Container '$ContainerName' not found."
        return $null
    }


	$status = ($containerInfo -split '\s{2,}' | Where-Object { $_ -match "Up|Exited" })
	if ($status -match '^(Up|Exited)') {
		$statusType = $matches[1]
		Write-Host $statusType  # Output: Up
	}
}

# Function to start or stop a container based on its status
function Toggle-ContainerState {
    param (
        [string]$ContainerId,
        [string]$Status
    )

    if ($Status -match "Up") {
        Write-Host "Container is running. Stopping container..."
        docker stop $ContainerId
		Sleeping-InRange -Minimum 20 -Maximum 30
    }
    elseif ($Status -match "Exited") {
        Write-Host "Container is stopped. Starting container..."
        docker start $ContainerId
		Sleeping-InRange -Minimum 120 -Maximum 160
    }
    else {
        Write-Host "Unknown container state: $Status"
    }
}

function Sleeping-InRange{
	param (
        [int]$Minimum,
		[int]$Maximum
    )
	$randomSleep = Get-Random -Minimum $Minimum -Maximum $Maximum
	Write-Host "Sleeping for $randomSleep seconds..."
	Start-Sleep -Seconds $randomSleep
}

# Function to manage container operations in a loop
function Manage-Container {
    param (
        [string]$ContainerName
    )

    while ($true) {
		Write-Host "Checking container status..."
        #docker ps -a --filter "name=$container_name" --format "{{.ID}} {{.Status}} {{.Names}}"
		$containerInfo = docker ps -a --filter "name=$ContainerName" --format "{{.ID}} {{.Status}}"
        if ($containerInfo) {
            # Split the output to extract container ID and status
            $parts = $containerInfo -split '\s+', 2
            $containerId = $parts[0]
            $status = $parts[1]

            Write-Host "Container ID: $containerId"
            Write-Host "Container Status: $status"

            # Toggle container state based on the status
            Toggle-ContainerState -ContainerId $containerId -Status $status
        }
        else {
            Write-Host "Container '$ContainerName' not found. Retrying in 5 seconds..."
            Start-Sleep -Seconds 5
        }
	}

}

Manage-Container -ContainerName $containerName
