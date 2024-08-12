# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

COPY Nuget.Config .
COPY reqifviewer reqifviewer
RUN dotnet restore --configfile Nuget.Config reqifviewer
RUN dotnet build --no-restore reqifviewer -c Release -o /app/build

RUN dotnet build "reqifviewer/reqifviewer.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish reqifviewer -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0.7-alpine3.19 AS final

WORKDIR /app
RUN mkdir /app/logs

COPY --from=publish /app/publish .

# Create a non-root user and give this user access to the working directory
RUN chown -R $APP_UID /app
USER $APP_UID 

ENTRYPOINT ["dotnet", "reqifviewer.dll"]