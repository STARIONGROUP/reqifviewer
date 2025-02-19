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

> The Docker command needs to be executed from the commandline in the **solution** folder.

```
$ ./solutionfolder# docker build -f reqifviewer/Dockerfile -t stariongroup/reqifviewer:latest .
$ ./solutionfolder# docker run -p 8080:8080 --name reqifviewer stariongroup/reqifviewer:latest
```

Push to docker hub

```
$ ./solutionfolder# docker push stariongroup/reqifviewer:latest
```

## Autodeployment

reqifviewer is dockerized and pushed to [dockerhub](https://hub.docker.com/repository/docker/STARIONGROUP/reqifviewer) using a GitHub action that is triggered by pushing a **tag** that has the following naming convention `web-app-x.y.z`, where x.y.z is the version numbr following [SEMVER](https://semver.org/). The server where the docker container is hosted automatically pulls latest using [watchtower](https://github.com/containrrr/watchtower), find it at https://viewer.reqifsharp.org.

## Build Status

GitHub actions are used to build and test the library

Branch | Build Status
------- | :------------
Master | ![Build Status](https://github.com/STARIONGROUP/reqifviewer/actions/workflows/CodeQuality.yml/badge.svg?branch=master)
Development | ![Build Status](https://github.com/STARIONGROUP/reqifviewer/actions/workflows/CodeQuality.yml/badge.svg?branch=development)

# License

The reqifviewer is provided to the community under the Apache License 2.0.

# Contributions

Contributions to the code-base are welcome. However, before we can accept your contributions we ask any contributor to sign the Contributor License Agreement (CLA) and send this digitaly signed to s.gerene@stariongroup.eu. You can find the CLA's in the CLA folder.