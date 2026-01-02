FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Services/EventService/PersonalUniverse.EventService.API/PersonalUniverse.EventService.API.csproj", "src/Services/EventService/PersonalUniverse.EventService.API/"]
COPY ["src/Shared/Contracts/PersonalUniverse.Shared.Contracts/PersonalUniverse.Shared.Contracts.csproj", "src/Shared/Contracts/PersonalUniverse.Shared.Contracts/"]
RUN dotnet restore "src/Services/EventService/PersonalUniverse.EventService.API/PersonalUniverse.EventService.API.csproj"
COPY . .
WORKDIR "/src/src/Services/EventService/PersonalUniverse.EventService.API"
RUN dotnet build "PersonalUniverse.EventService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PersonalUniverse.EventService.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PersonalUniverse.EventService.API.dll"]
