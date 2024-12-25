FROM mcr.microsoft.com/dotnet/sdk:6.0 as build
COPY ./ /sourceCode
WORKDIR /sourceCode
RUN dotnet restore
RUN dotnet publish -c Release -o /app && cp ./ProcessBin /app/ProcessBin -R

# WorkerPool runtime
FROM mcr.microsoft.com/dotnet/aspnet:6.0 as server
# install worker client runtime
WORKDIR /app
COPY --from=build /app .
CMD ["sh", "-c", "sleep 5 && dotnet TradeWorker.dll"]
