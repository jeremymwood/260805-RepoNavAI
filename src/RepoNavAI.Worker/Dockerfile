FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props RepoNavAI.sln ./
COPY src/RepoNavAI.Domain/RepoNavAI.Domain.csproj src/RepoNavAI.Domain/
COPY src/RepoNavAI.Application/RepoNavAI.Application.csproj src/RepoNavAI.Application/
COPY src/RepoNavAI.Infrastructure/RepoNavAI.Infrastructure.csproj src/RepoNavAI.Infrastructure/
COPY src/RepoNavAI.Worker/RepoNavAI.Worker.csproj src/RepoNavAI.Worker/
RUN dotnet restore src/RepoNavAI.Worker/RepoNavAI.Worker.csproj
COPY src/ src/
RUN dotnet publish src/RepoNavAI.Worker/RepoNavAI.Worker.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","RepoNavAI.Worker.dll"]
