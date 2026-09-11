FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish src/Stpetertest/Stpetertest.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Runs unprivileged; the base image ships this user for exactly this.
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Stpetertest.dll"]
