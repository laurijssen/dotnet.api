#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
#
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["graphicstransform.api/nuget.config", "graphicstransform.api/"]
COPY ["graphicstransform.api/graphicstransform.csproj", "graphicstransform.api/"]
COPY ["graphicstransform.service/graphicstransform.service.csproj", "graphicstransform.service/"]
RUN dotnet restore "graphicstransform.api/graphicstransform.csproj"
COPY . .
WORKDIR "/src/graphicstransform.api"
RUN dotnet build "graphicstransform.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "graphicstransform.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "graphicstransform.dll"]