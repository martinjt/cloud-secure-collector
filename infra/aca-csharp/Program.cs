using Pulumi;
using Pulumi.AzureNative.App;
using Pulumi.AzureNative.App.Inputs;
using Pulumi.AzureNative.Resources;
using System.Collections.Generic;
using ACR = Pulumi.AzureNative.ContainerRegistry;

return await Pulumi.Deployment.RunAsync(() =>
{
    var config = new Config();

    var resourceGroup = new ResourceGroup("secure-collector");

    var registry = new ACR.Registry("securecollector", new()
    {
        AdminUserEnabled = true,
        ResourceGroupName = resourceGroup.Name,
        Sku = new ACR.Inputs.SkuArgs
        {
            Name = ACR.SkuName.Basic,
        },
        
    });
    var credentials = Output
        .Tuple(resourceGroup.Name, registry.Name)
        .Apply(items =>
            ACR.ListRegistryCredentials.InvokeAsync(new ACR.ListRegistryCredentialsArgs
            {
                ResourceGroupName = items.Item1,
                RegistryName = items.Item2
            }));
    var adminUsername = credentials.Apply(credentials => Output.CreateSecret(credentials.Username));
    var adminPassword = credentials.Apply(credentials => Output.CreateSecret(credentials.Passwords[0].Value));


    var image = new Pulumi.Docker.Image("collector-image", new()
    {
        ImageName = Output.Format($"{registry.LoginServer}/securecollector:latest"),
        Build = new Pulumi.Docker.Inputs.DockerBuildArgs
        {
            Context = "../../docker-collector/",
        },
        Registry = new Pulumi.Docker.Inputs.RegistryArgs
        {
            Server = registry.LoginServer,
            Username = adminUsername!,
            Password = adminPassword!,
        },
    });

    var honeycombApiKeySecret = new SecretArgs
    {
        Name = "honeycomb-api-key",
        Value = config.RequireSecret("honeycomb-api-key")
    };
    var registryPasswordSecret = new SecretArgs
    {
        Name = "registry-pwd",
        Value = adminPassword!
    };

    var containerAppEnvironment = new ManagedEnvironment("collector-env", new ManagedEnvironmentArgs
    {
        ResourceGroupName = resourceGroup.Name,
        AppLogsConfiguration = new AppLogsConfigurationArgs
        {
            Destination = ""
        },
    });

    var collectorApp = new ContainerApp("collector", new ContainerAppArgs
    {
        EnvironmentId = containerAppEnvironment.Id,
        ResourceGroupName = resourceGroup.Name,
        ContainerAppName = "collector",
        Configuration = new ConfigurationArgs
        {
            Ingress = new IngressArgs
            {
                External = true,
                TargetPort = 4318
            },
            Secrets = {
                honeycombApiKeySecret,
                registryPasswordSecret
            },
            Registries =
            {
                new RegistryCredentialsArgs
                {
                    Server = registry.LoginServer,
                    Username = adminUsername!,
                    PasswordSecretRef = registryPasswordSecret.Name,
                }
            },

        },

        Template = new TemplateArgs
        {
            Scale = new ScaleArgs
            {
                MinReplicas = 1,
                MaxReplicas = 1,
            },
            Containers = {
                new ContainerArgs
                {
                    Name = "collector",
                    Image = image.ImageName,
                    Env = {
                        new EnvironmentVarArgs {
                            SecretRef = honeycombApiKeySecret.Name,
                            Name = "HONEYCOMB_API_KEY"
                        }
                    },
                    Probes = {
                        new ContainerAppProbeArgs {
                            HttpGet = new ContainerAppProbeHttpGetArgs {
                                Path = "/",
                                Port = 13133,
                            },
                            Type = Type.Readiness
                        },
                        new ContainerAppProbeArgs {
                            HttpGet = new ContainerAppProbeHttpGetArgs {
                                Path = "/",
                                Port = 13133,
                            },
                            Type = Type.Liveness
                        }

                    }

                }
            },
        }
    });

    return new Dictionary<string, object?> {
        { "collector-url", collectorApp.LatestRevisionFqdn },
    };

});