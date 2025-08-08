# reqifviewer

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=coverage)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=ncloc)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=sqale_index)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=STARIONGROUP_reqifviewer&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=STARIONGROUP_reqifviewer)

The reqifviewer is a web application to inspect and navigate [ReqIF](https://www.omg.org/spec/ReqIF/1.2/About-ReqIF/) files. The web application is developed using Blazor and depends on [ReqIFSharp](https://reqifsharp.org) for [ReqIF](https://www.omg.org/spec/ReqIF/1.2/About-ReqIF/) processing. 

> Visit https://viewer.reqifsharp.org to see the application in action.

## Build and Deploy using Docker

The reqifviewer SPA is built using docker and the result is a Docker container ready to be deployed (or pushed to Docker Hub). The Docker file is located in the reqifviewer project folder.

Two scripts are provided to create a docker image:
  - `docker-build-local.sh`: creates an image that can be run locally with `docker run -p 8080:80 --name reqifviewer stariongroup/reqifviewer:latest`
  - `docker-build-attested.sh`: creates an image that is attested and includes an SBOM. This is immediately pushed to docker hub

> both scripts need to be run from a linux command line (like the console in GitExtensions)

## Autodeployment

reqifviewer is dockerized and pushed to [dockerhub](https://hub.docker.com/repository/docker/STARIONGROUP/reqifviewer) using a GitHub action that is triggered by pushing a **tag** that has the following naming convention `web-app-x.y.z`, where x.y.z is the version numbr following [SEMVER](https://semver.org/). The server where the docker container is hosted automatically pulls latest using [watchtower](https://github.com/containrrr/watchtower), find it at https://viewer.reqifsharp.org.

## Build Status

GitHub actions are used to build and test the library

Branch | Build Status
------- | :------------
Master | ![Build Status](https://github.com/STARIONGROUP/reqifviewer/actions/workflows/CodeQuality.yml/badge.svg?branch=master)
Development | ![Build Status](https://github.com/STARIONGROUP/reqifviewer/actions/workflows/CodeQuality.yml/badge.svg?branch=development)

## Software Bill of Materials (SBOM) and Provenance

As part of our commitment to security, transparency, and traceability, this Docker image includes a Software Bill of Materials (SBOM) and Provenance information. These are automatically generated during the build process, providing detailed insights into the components, their licenses, versions, and the integrity of the image itself.
What is Included:

### SBOM (Software Bill of Materials):

  - A comprehensive list of all open-source and third-party components included in this Docker image.
  - Tracks software dependencies, licenses, and versions.
  - Helps with vulnerability management by allowing users to quickly identify potential risks tied to specific components.

### Provenance:

  - A record of the image's origin and build process, providing traceability and assurance regarding the integrity of the image.
  - This ensures that the image was built using the declared sources and under the specified conditions, helping verify its authenticity and consistency.

### Why SBOM and Provenance?

  - Improved Transparency: Provides full visibility into the open-source and third-party components included in the image.
  - Security Assurance: Enables easier tracking of vulnerabilities associated with specific components, promoting proactive security measures.
  - Compliance: Ensures adherence to licensing requirements and simplifies audits of dependencies and build processes.
  - Image Integrity: Provenance guarantees that the image is built as expected, without unauthorized modifications.

# License

The reqifviewer is provided to the community under the Apache License 2.0.

# Contributions

Contributions to the code-base are welcome. However, before we can accept your contributions we ask any contributor to sign the Contributor License Agreement (CLA) and send this digitaly signed to s.gerene@stariongroup.eu. You can find the CLA's in the CLA folder.