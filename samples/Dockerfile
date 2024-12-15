FROM mcr.microsoft.com/dotnet/sdk:6.0 as build
COPY ./ /sourceCode
WORKDIR /sourceCode
RUN dotnet restore
RUN dotnet publish -c Release -o /app
COPY ./ProcessBin /app/ProcessBin

# WorkerPool runtime
FROM mcr.microsoft.com/dotnet/aspnet:6.0 as server
# install worker client runtime
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    wget \
    gnupg && \
    wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
    dotnet-runtime-3.1 && \
    apt-get install procps -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .
#COPY ./ProcessBin/ ../ProcessBin/
#CMD ["sh", "-c", "while :; do sleep 3600; done"]
CMD ["dotnet","WorkerSample.dll"]

