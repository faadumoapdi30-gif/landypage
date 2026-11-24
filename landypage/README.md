# Little by Little

A simple ASP.NET Core MVC landing page for an e-learning website.

## Deploy to Render

1. Make sure the repository contains this `landypage/` project folder.
2. In Render, create a **Blueprint**, connect that repository, and set the Blueprint file path to `landypage/render.yaml`.
3. Render builds from the `landypage` subdirectory and deploys the web service.

The service root is `landypage`, so its Dockerfile path is `Dockerfile` and its build context is `.`. If configuring the web service manually, use those same paths and set the root directory to `landypage`. The Dockerfile builds with the .NET 10 SDK and runs the published app on Render's `PORT`. No database or extra service is required. The repository can contain other projects; this service only builds `landypage`.

## Run locally with Docker

From this folder, build and run the image:

```sh
docker build -t landypage .
docker run --rm -p 8080:10000 -e PORT=10000 landypage
```

Then open `http://localhost:8080`.
