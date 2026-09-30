FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FreightLink.Api.csproj ./
RUN dotnet restore FreightLink.Api.csproj
COPY . .
RUN dotnet publish FreightLink.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN mkdir -p /app/storage/uploads && chown -R $APP_UID:$APP_UID /app/storage
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "FreightLink.Api.dll"]
