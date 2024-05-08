FROM mcr.microsoft.com/dotnet/sdk:8.0 as build

ARG packages_token
WORKDIR /app

COPY *.csproj ./
COPY nuget.config ./
RUN dotnet restore -p:RestoreUseSkipNonexistentTargets=false

COPY . ./

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 as run
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "rabbitmq.api.dll"]
