FROM mcr.microsoft.com/dotnet/sdk:6.0 as build
COPY ./ /sourceCode
WORKDIR /sourceCode
RUN dotnet restore
RUN dotnet publish -c Release -o /app
COPY ./Process /app/Process

FROM mcr.microsoft.com/dotnet/aspnet:6.0 as server
WORKDIR /app
COPY --from=build /app .
COPY ./Process/Group/ ../Process/Group/
CMD ["dotnet","NodeWorker.dll"]

