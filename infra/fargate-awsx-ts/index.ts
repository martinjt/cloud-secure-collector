import * as pulumi from "@pulumi/pulumi";
import * as aws from "@pulumi/aws";
import * as awsx from "@pulumi/awsx";
import * as docker from "@pulumi/docker";

var config = new pulumi.Config();
var honeycombApiKey = config.requireSecret("honeycomb-api-key");

var repository = new awsx.ecr.Repository("collector", {
    forceDelete: true
});

var registryInfo = repository.url.apply(async repositoryUrl => {
    const url = new URL("https://" + repositoryUrl);
    const registryId = url.hostname.split(".")[0];
    var creds = await aws.ecr.getCredentials({
        registryId: registryId
    })

    const decodedCredentials = Buffer.from(creds.authorizationToken, "base64").toString();
    const [username, password] = decodedCredentials.split(":");
    if (!password || !username) {
        throw new Error("Invalid credentials");
    }
    return {
        server: creds.proxyEndpoint,
        username: username,
        password: password,
    };
});

var image = new docker.Image("collector", {
    imageName: repository.url,
    build: {
        context: "../../docker-collector"
    },
    registry: {
        server: repository.url,
        username: registryInfo.username,
        password: registryInfo.password
    }
});

const ecrAccessRole = new aws.iam.Role("ecrAccessRole", {
    assumeRolePolicy: JSON.stringify({
        Version: "2012-10-17",
        Statement: [{
            Action: "sts:AssumeRole",
            Effect: "Allow",
            Sid: "",
            Principal: {
                Service: "build.apprunner.amazonaws.com",
            },
        }],
    }),
});

const pullImagePolicy = new aws.iam.Policy("pullImagePolicy", {
    policy: JSON.stringify({
        Version: "2012-10-17",
        Statement: [{
            Action: [
                "ecr:GetAuthorizationToken",
                "ecr:BatchCheckLayerAvailability",
                "ecr:GetDownloadUrlForLayer",
                "ecr:BatchGetImage",
                "ecr:DescribeImages",],
            Effect: "Allow",
            Resource: "*",
        }],
    }),
});

const pullImageRolePolicy = new aws.iam.RolePolicyAttachment("pullImageRolePolicy", {
    role: ecrAccessRole,
    policyArn: pullImagePolicy.arn,
});

const cluster = new aws.ecs.Cluster("collector");

const lb = new awsx.lb.ApplicationLoadBalancer("lb", {
    defaultTargetGroup: {
        port: 4318,
        healthCheck: {
            port: "13133",   
        }
    }
});

const service = new awsx.ecs.FargateService("collector", {
    cluster: cluster.arn,
    assignPublicIp: true,
    name: "collector",
    desiredCount: 3,
    taskDefinitionArgs: {
        family: "collector",
        container: {
            name: "collector",
            image: image.imageName,
            cpu: 128,
            memory: 512,
            essential: true,
            environment: [
                {
                    name: "HONEYCOMB_API_KEY",
                    value: honeycombApiKey,
                }
            ],
            portMappings: [
                {
                    containerPort: 4317,
                    hostPort: 4317,
                    targetGroup: lb.defaultTargetGroup,
                },
                {
                    containerPort: 4318,
                    hostPort: 4318,
                    targetGroup: lb.defaultTargetGroup,
                },
                {
                    containerPort: 13133,
                    hostPort: 13133,
                    targetGroup: lb.defaultTargetGroup,
                },
            ],
        },
    },
});

export const containerUrl = pulumi.interpolate `http://${lb.loadBalancer.dnsName}/v1/trace`;
