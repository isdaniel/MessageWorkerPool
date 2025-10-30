FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
COPY ./ /sourceCode
WORKDIR /sourceCode
RUN dotnet restore src/Examples/DotNetWorker.sln
RUN dotnet publish src/Examples/DotNetWorker/WorkerHost/WorkerHost.csproj -c Release -o /app/host
RUN dotnet publish src/Examples/DotNetWorker/WorkerClient/WorkerClient.csproj -c Release -o /app/WorkerClient

# WorkerPool runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 as server
WORKDIR /app
COPY --from=build /app .

# Expose metrics port for Prometheus
EXPOSE 9464

CMD ["dotnet","host/WorkerHost.dll"]
