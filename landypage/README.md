# Little by Little

A simple ASP.NET Core MVC landing page for an e-learning website.

## Deploy to Render

1. Push this project folder to a GitHub or GitLab repository.
2. In Render, create a **Blueprint** and connect that repository.
3. Render will read `render.yaml`, build the Docker image, and deploy the web service.

The Dockerfile builds with the .NET 10 SDK and runs the published app on Render's `PORT`. No database or extra service is required.

## Run locally with Docker

From this folder, build and run the image:

```sh
docker build -t landypage .
docker run --rm -p 8080:8080 -e PORT=8080 landypage
```

Then open `http://localhost:8080`.
