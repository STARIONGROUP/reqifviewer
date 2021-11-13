FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src

COPY Nuget.Config .
COPY reqifviewer reqifviewer
RUN dotnet restore --configfile Nuget.Config reqifviewer
RUN dotnet build reqifviewer -c Release -o /app/build

FROM build AS publish
RUN dotnet publish reqifviewer -c Release -o /app/publish

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/publish/wwwroot .
COPY reqifviewer/nginx.conf /etc/nginx/nginx.conf