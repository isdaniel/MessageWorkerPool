FROM mcr.microsoft.com/dotnet/sdk:6.0 as build
COPY ./ /sourceCode
WORKDIR /sourceCode
RUN dotnet restore
RUN dotnet publish -c Release -o /app
COPY ./ProcessBin /app/ProcessBin

FROM mcr.microsoft.com/dotnet/aspnet:6.0 as server
WORKDIR /app
COPY --from=build /app .
#COPY ./ProcessBin/ ../ProcessBin/
CMD ["dotnet","WorkerSample.dll"]

