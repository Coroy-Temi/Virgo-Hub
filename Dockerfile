FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY VirgoHub.Shared/VirgoHub.Shared.csproj VirgoHub.Shared/
COPY VirgoHub.WebAdmin/VirgoHub.WebAdmin.csproj VirgoHub.WebAdmin/
RUN dotnet restore VirgoHub.WebAdmin/VirgoHub.WebAdmin.csproj

COPY . .
RUN dotnet publish VirgoHub.WebAdmin/VirgoHub.WebAdmin.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "VirgoHub.WebAdmin.dll"]
