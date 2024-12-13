FROM mcr.microsoft.com/dotnet/sdk:3.1 as build
COPY ./ /sourceCode
WORKDIR /sourceCode
RUN dotnet restore
RUN dotnet publish -c Release -o /app
COPY ./Process /app/Process

FROM mcr.microsoft.com/dotnet/aspnet:3.1 as server
WORKDIR /app
COPY --from=build /app .
COPY ./Process/Group/ ../Process/Group/
CMD ["dotnet","WorkerSample.dll"]
# CMD ["sh", "-c", "sleep infinity"]