FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
COPY ./ /sourceCode
WORKDIR /sourceCode
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 as server
WORKDIR /app
RUN apt-get update
RUN apt-get install telnet -y
COPY --from=build /app .
CMD ["dotnet","MessageClient.dll"]
