FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY CnuFacebookAPI.csproj .
RUN dotnet restore CnuFacebookAPI.csproj

COPY . .
RUN dotnet publish CnuFacebookAPI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CnuFacebookAPI.dll"]
