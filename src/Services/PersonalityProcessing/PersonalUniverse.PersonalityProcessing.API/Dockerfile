FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Services/PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API/PersonalUniverse.PersonalityProcessing.API.csproj", "src/Services/PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API/"]
COPY ["src/Shared/Models/PersonalUniverse.Shared.Models/PersonalUniverse.Shared.Models.csproj", "src/Shared/Models/PersonalUniverse.Shared.Models/"]
COPY ["src/Shared/Contracts/PersonalUniverse.Shared.Contracts/PersonalUniverse.Shared.Contracts.csproj", "src/Shared/Contracts/PersonalUniverse.Shared.Contracts/"]
COPY ["src/Services/Storage/PersonalUniverse.Storage.API/PersonalUniverse.Storage.API.csproj", "src/Services/Storage/PersonalUniverse.Storage.API/"]
RUN dotnet restore "src/Services/PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API/PersonalUniverse.PersonalityProcessing.API.csproj"
COPY . .
WORKDIR "/src/src/Services/PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API"
RUN dotnet build "PersonalUniverse.PersonalityProcessing.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PersonalUniverse.PersonalityProcessing.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PersonalUniverse.PersonalityProcessing.API.dll"]
