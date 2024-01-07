# Secured OpenTelemetry Collector in AWS fargate

This is an example of how to deploy a secured OpenTelemetry Collector with the AWS Fargate Service with Pulumi. The code is in typescript.

Fargate does give you a public IP for the service, however, you need an ALB to support SSL offload if you don't want to do SSL in the collector endpoints which is what I recommend. This also allows for more filtering to happen to secure it publicly.

## Process

This repo creates: 

* AWS ECR repository
* AWS ECS Cluster
* AWS ALB
* AWS Fargate Service and associated resource
* IAM role and policy for Fargate => ECR access

The collector is set to send data to Honeycomb using an Honeycomb API key to show how to pass environment variables to the container.

## Notes on hidden resources

Although there are only really 4 resources created, that isn't the entire picture. What I've done is used Pulumi Crosswalk to abstract some of the required resources. Therefore on top of the resources described above, there is:

* A VPC
* Subnets
* Route Table
* Security Groups
* Target Group and Listener
* Cloudwatch Log Group
* Lifecycle Policy for ECR

Although Pulumi's AWSx package simplifies and hides these complexities, these are still resources that need managing and maintaining. They could each be changed in the portal and go wrong in ways that you'll be required to fix.

## Deploying

Login to pulumi:

```shell
cd infra/fargate-awsx-ts
mkdir ../state
pulumi login file://../state
```

Create a stack
```shell
pulumi stack select
```

Add config to the stack
```shell
pulumi config set aws:region eu-west-1
pulumi config set honeycomb-api-key <your-key> --secret
```

Deploy the stack
```shell
pulumi up
```

You'll get an output similar to this:

```text
Outputs:
    collector-url        : "https://{your-collector-hostname}/v1/traces"
```