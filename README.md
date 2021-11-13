# reqifviewer

The reqifviewer is a web application to inspect and navigate ReqIF files. The web application is developed using Blazor and depends on ReqIFSharp for ReqIF processing.

### Build and Deploy using Docker

reqifviewer is dockerized and pushed to dockerhub using a GitHub action that is triggered by pushing a tag that has the following naming convention `web-app-x.y.z`, where x.y.z is the version numbr following SEMVER.
The server where the docker container is hosted automatically pulls latest using watchtower

The reqifviewer SPA is built using docker and the result is a Docker container ready to be deployed (or pushed to Docker Hub). The Docker file is located in the reqifviewer project folder.

> The Docker command needs to be executed from the commandline in the **solution** folder.

```
$ ./solutionfolder# docker build -f reqifviewer/Dockerfile -t rheagroup/reqifviewer:latest .
$ ./solutionfolder# docker run -p 8080:80 --name reqifviewer rheagroup/reqifviewer:latest
```

Push to docker hub

```
$ ./solutionfolder# docker push rheagroup/rheagroup:latest