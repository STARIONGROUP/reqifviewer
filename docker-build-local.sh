#!/bin/bash

# Exit on error
set -e

# Ensure version is passed
if [ -z "$1" ]; then
  echo "Usage: $0 <version>"
  echo "Example: $0 x.y.z"
  exit 1
fi

VERSION="$1"

ECHO "Pull latest version of mcr.microsoft.com/dotnet/sdk:9.0"

docker pull mcr.microsoft.com/dotnet/sdk:9.0

echo "Building local Docker image for version: $VERSION"

docker build \
  -f reqifviewer/Dockerfile \
  -t stariongroup/reqifviewer:latest \
  -t stariongroup/reqifviewer:$VERSION \
  .

echo "Build complete."
echo "Tags: latest, $VERSION"