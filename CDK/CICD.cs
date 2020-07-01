using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodeDeploy;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using System.Collections.Generic;

namespace Cdk
{
    internal class CICD
    {
        public const string SourceBucketName = "us-east-1.andyhoppatamazon.com";
        public static string SourceBucketKey { get; set; }

        internal CICD(
            CdkStack stack, 
            CfnParameter targetPlatform, 
            LoadBalancedInstancesResult frontEndInstanceInfo,
            LoadBalancedInstancesResult restAPIInstanceInfo)
        {
            var artifactBucket = new Bucket(stack, "ArtifactBucket");
            var repo = new Repository(stack, "ApplicationRepository", new RepositoryProps
            {
                RepositoryName = stack.StackName,
                Description = $"Contains the code for the {stack.StackName} application."
            });
            var cfnRepo = repo.Node.DefaultChild as Amazon.CDK.AWS.CodeCommit.CfnRepository;
            cfnRepo.Code = new CfnRepository.CodeProperty
            {
                S3 = new CfnRepository.S3Property
                {
                    Bucket = SourceBucketName,
                    Key = SourceBucketKey
                }
            };

            var build = new PipelineProject(stack, "ApplicationBuild", new PipelineProjectProps
            {
                Environment = new BuildEnvironment
                {
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_3,
                    EnvironmentVariables = new Dictionary<string, IBuildEnvironmentVariable> {
                        { 
                            "TARGET_OS",
                            new BuildEnvironmentVariable {
                                Type = BuildEnvironmentVariableType.PLAINTEXT,
                                Value = targetPlatform.Value 
                            } 
                        }
                    }
                }
            });
            var frontEndApplication = new ServerApplication(stack, "FrontEnd", new ServerApplicationProps
            {
                ApplicationName = $"{stack.StackName}-FrontEnd"
            });
            var frontEndDeploymentGroup = new ServerDeploymentGroup(stack, "FrontEndDeploymentGroup", new ServerDeploymentGroupProps
            {
                Application = frontEndApplication,
                DeploymentGroupName = $"{stack.StackName.ToLower()}-frontend-deployment-group",
                //Role = new Role(stack, "DeploymentServiceRole", new RoleProps
                //{
                //    AssumedBy = new ServicePrincipal("codedeploy.amazonaws.com"),
                //    Description = "Allows Application Deployment.",
                //    ManagedPolicies = new[] { ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSCodeDeployRole") }
                //}),
                //AutoRollback = new AutoRollbackConfig { FailedDeployment = true },
                //DeploymentConfig = ServerDeploymentConfig.HALF_AT_A_TIME,
                LoadBalancer = LoadBalancer.Application(frontEndInstanceInfo.TargetGroup),
                AutoScalingGroups = new[] { frontEndInstanceInfo.AutoScalingGroup }
            });
            var restApiApplication = new ServerApplication(stack, "RestAPI", new ServerApplicationProps
            {
                ApplicationName = $"{stack.StackName}-RESTAPI"
            });
            var restApiDeploymentGroup = new ServerDeploymentGroup(stack, "RestAPIDeploymentGroup", new ServerDeploymentGroupProps
            {
                Application = restApiApplication,
                DeploymentGroupName = $"{stack.StackName.ToLower()}-restapi-deployment-group",
                //AutoRollback = new AutoRollbackConfig { FailedDeployment = true },
                //DeploymentConfig = ServerDeploymentConfig.HALF_AT_A_TIME,
                LoadBalancer = LoadBalancer.Application(restAPIInstanceInfo.TargetGroup),
                AutoScalingGroups = new[] { restAPIInstanceInfo.AutoScalingGroup }
            });
            var sourceOutput = new Artifact_();
            var frontEndArtifacts = new Artifact_("FrontEnddOutput");
            var restAPIArtifacts = new Artifact_("RESTAPIOutput");
            var pipeline = new Pipeline(stack, "ApplicationPipeline", new PipelineProps
            {
                ArtifactBucket = artifactBucket,
                PipelineName = $"{stack.StackName}-Pipeline",
                Stages = new[]
                  {
                    new Amazon.CDK.AWS.CodePipeline.StageProps
                    {
                        StageName = "Source",
                        Actions = new []
                        {
                            new CodeCommitSourceAction(new CodeCommitSourceActionProps
                            {
                                ActionName = "Source",
                                Repository = repo,
                                Output = sourceOutput
                            })
                        }
                    },
                    new Amazon.CDK.AWS.CodePipeline.StageProps
                    {
                        StageName = "Build",
                        Actions = new []
                        {
                            new CodeBuildAction(new CodeBuildActionProps
                            {
                                ActionName = "CodeBuild",
                                Project = build,
                                Input = sourceOutput,
                                Outputs = new [] { frontEndArtifacts, restAPIArtifacts }
                            })
                        }
                    },
                    new Amazon.CDK.AWS.CodePipeline.StageProps
                    {
                        StageName = "Deploy",
                        Actions = new []
                        {
                            new CodeDeployServerDeployAction(new CodeDeployServerDeployActionProps {
                                ActionName = "CodeDeploy",
                                DeploymentGroup = frontEndDeploymentGroup,
                                Input = frontEndArtifacts
                            }),
                            new CodeDeployServerDeployAction(new CodeDeployServerDeployActionProps {
                                ActionName = "CodeDeploy",
                                DeploymentGroup = restApiDeploymentGroup,
                                Input = restAPIArtifacts
                            })
                        }
                    }
                }
            });
        }
    }
}
