#!/bin/bash

set -a
source ./env/.env
source ./env/.local_env
set +a

for i in {1..10}
do
    docker start $(docker ps -a --filter "name=envpreparatory" --format "{{.ID}}")
    echo "Waiting for 5 seconds before running .NET testing iteration $i"
    sleep 5
    echo "Running .NET testing iteration $i"
    dotnet test IntegrationTester.rabbitmq.sln --configuration Release --logger:"console;verbosity=detailed"
done
