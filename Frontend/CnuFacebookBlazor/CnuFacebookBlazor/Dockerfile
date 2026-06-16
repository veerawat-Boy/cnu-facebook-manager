FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY CnuFacebookBlazor.csproj .
RUN dotnet restore CnuFacebookBlazor.csproj

COPY . .
RUN dotnet publish CnuFacebookBlazor.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CnuFacebookBlazor.dll"]
