FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
RUN apt-get update
RUN apt-get install --no-install-recommends --yes python3
RUN dotnet workload install wasm-tools

WORKDIR /src

COPY Nuget.Config .
COPY ReqifViewer.Infrastructure ReqifViewer.Infrastructure
COPY reqifviewer reqifviewer
RUN dotnet restore --configfile Nuget.Config reqifviewer
RUN dotnet build reqifviewer -c Release -o /app/build

FROM build AS publish
RUN dotnet publish reqifviewer -c Release -o /app/publish

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/publish/wwwroot .
COPY reqifviewer/nginx.conf /etc/nginx/nginx.conf