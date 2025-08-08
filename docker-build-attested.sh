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
BUILDER="buildkit-container"

# Create builder if it doesn't exist
if ! docker buildx inspect "$BUILDER" >/dev/null 2>&1; then
  echo "Creating BuildKit builder: $BUILDER"
  docker buildx create --name "$BUILDER" --driver docker-container --use
  docker buildx inspect --bootstrap
else
  docker buildx use "$BUILDER"
fi

ECHO "Pull latest version of mcr.microsoft.com/dotnet/sdk:9.0"

docker pull mcr.microsoft.com/dotnet/sdk:9.0

echo "Building and Pushing Docker image with SBOM and provenance for version: $VERSION"

docker buildx build \
  --platform=linux/amd64 \
  -f reqifviewer/Dockerfile \
  -t stariongroup/reqifviewer:latest \
  -t stariongroup/reqifviewer:$VERSION \
  --sbom=true \
  --provenance=true \
  --push \
  .

echo "Build complete."
echo "Tags: latest, $VERSION"
echo "Provenance attached as image metadata"