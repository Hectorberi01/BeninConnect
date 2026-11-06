# Contexte de build : racine du dépôt (Directory.*.props partagés).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY server/src/ server/src/
RUN dotnet publish server/src/BeninConnect.Api/BeninConnect.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Dossier des photos (volume) : appartient à l'utilisateur non root de l'image.
RUN mkdir -p /app/media/uploads && chown -R $APP_UID /app/media
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "BeninConnect.Api.dll"]
