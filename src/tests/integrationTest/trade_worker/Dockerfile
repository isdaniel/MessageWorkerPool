FROM mcr.microsoft.com/dotnet/sdk:6.0 as build
COPY ./ /sourceCode
WORKDIR /sourceCode/tests/integrationTest
RUN dotnet build ./WorkerBuilder.sln --configuration Release
RUN mkdir -p /app && \
    cp ./trade_worker/WorkerCode/* /app -R && \
    cp ./trade_worker/FibonacciWorkerApp/ /app/FibonacciWorkerApp -R && \
    cp ./trade_worker/BalanceWorkerApp/ /app/BalanceWorkerApp -R

# WorkerPool runtime
FROM mcr.microsoft.com/dotnet/aspnet:6.0 as server
# install worker client runtime
RUN apt-get update && \
    apt-get install procps -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .
CMD ["sh", "-c", "sleep 3 && exec dotnet TradeWorker.dll"]
