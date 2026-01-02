FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy shared projects
COPY ["src/Shared/Models/PersonalUniverse.Shared.Models/PersonalUniverse.Shared.Models.csproj", "src/Shared/Models/PersonalUniverse.Shared.Models/"]
COPY ["src/Shared/Contracts/PersonalUniverse.Shared.Contracts/PersonalUniverse.Shared.Contracts.csproj", "src/Shared/Contracts/PersonalUniverse.Shared.Contracts/"]

# Copy Storage service project
COPY ["src/Services/Storage/PersonalUniverse.Storage.API/PersonalUniverse.Storage.API.csproj", "src/Services/Storage/PersonalUniverse.Storage.API/"]

# Restore dependencies
RUN dotnet restore "src/Services/Storage/PersonalUniverse.Storage.API/PersonalUniverse.Storage.API.csproj"

# Copy all source files
COPY . .

# Build the project
WORKDIR "/src/src/Services/Storage/PersonalUniverse.Storage.API"
RUN dotnet build "PersonalUniverse.Storage.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PersonalUniverse.Storage.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PersonalUniverse.Storage.API.dll"]
