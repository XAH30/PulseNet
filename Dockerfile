FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json PulseNet.sln ./
COPY src/PulseNet.Api/PulseNet.Api.csproj src/PulseNet.Api/
RUN dotnet restore src/PulseNet.Api/PulseNet.Api.csproj

COPY src/PulseNet.Api/ src/PulseNet.Api/
RUN dotnet publish src/PulseNet.Api/PulseNet.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PulseNet.Api.dll"]
