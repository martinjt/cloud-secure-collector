# Secured OpenTelemetry Collector in Azure Container Apps

This is an example of how to deploy a secured OpenTelemetry Collector with Azure Container Apps with Pulumi. The code is in C#.

Azure Container Apps allows for a single endpoint on Port 443, and doesn't support gRPC, so we'll be using the HTTP endpoint for the collector.

## Process

This repo creates: 

* Resource Group (to contain all the resources)
* Azure Container Registry (for the image)
* Docker Image (using the `docker-collector` directory)
* Azure Container Apps Managed Environment (as a place to deploy the app)
* Azure Container App (the app itself)

The collector is set to send data to Honeycomb using an Honeycomb API key to show how to pass environment variables to the container.

## Notes on credentials.

Due to a limitation with Azure Container Apps and pulling images, the container app has to use the Admin credentials to access the registry. There are 2 alternatives that aren't shown here.

* System Managed Identity for the Container app.
  This requires that you create the container app with a public image first, then create a revision with the private image. That doesn't work for an IaC approach

* User Managed Identity
  This requires the deploying user (your CI/CD user) to have the rights to create and manage access rights in Entra ID. This isn't really an option from a security standpoint.

## Deploying

Login to pulumi:

```shell
cd infra/aca-csharp
mkdir ../state
pulumi login file://../state
```

Create a stack
```shell
pulumi stack select
```

Add config to the stack
```shell
pulumi config set azure-native:location UKSouth
pulumi config set honeycomb-api-key <your-key> --secret
```

Deploy the stack
```shell
pulumi up
```

You'll get an output similar to this:

```text
Outputs:
    collector-url        : "<your collector hostname>"
```