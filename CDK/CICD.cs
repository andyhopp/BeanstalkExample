using Amazon.CDK;
using Amazon.CDK.AWS.AutoScaling;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodeDeploy;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using System.Collections.Generic;

namespace Cdk
{
    internal class CICD
    {
        public const string SourceBucketName = "us-east-1.andyhoppatamazon.com";
        public static string SourceBucketKey { get; set; }

        internal CICD(CdkStack stack, CfnParameter targetPlatform, LoadBalancedInstancesResult instanceInfo)
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
                //Role = new Role(stack, "BuildServiceRole", new RoleProps
                //{
                //    AssumedBy = new ServicePrincipal("codebuild.amazonaws.com"),
                //    Description = "Allows S3 and CodeDeploy access.",
                //    ManagedPolicies = new[] { ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole") },
                //    InlinePolicies = {
                //        {
                //            "ArtifactAccess",
                //            new PolicyDocument(new PolicyDocumentProps {
                //                Statements = new [] {
                //                    new PolicyStatement(new PolicyStatementProps {
                //                        Effect = Effect.ALLOW,
                //                        Resources = new[] { artifactBucket.BucketArn },
                //                        Actions = new[] {
                //                            "s3:GetObject",
                //                            "s3:PutObject",
                //                            "s3:GetObjectVersion"
                //                         }
                //                    })
                //                }
                //            })
                //        }
                //    }
                //}),
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
            var codeDeployApplication = new ServerApplication(stack, "ApplicationDeployment", new ServerApplicationProps
            {
                ApplicationName = stack.StackName
            });
            var deploymentGroup = new ServerDeploymentGroup(stack, "ApplicationDeploymentGroup", new ServerDeploymentGroupProps
            {
                Application = codeDeployApplication,
                DeploymentGroupName = $"{stack.StackName.ToLower()}-deployment-group",
                Role = new Role(stack, "DeploymentServiceRole", new RoleProps
                {
                    AssumedBy = new ServicePrincipal("codedeploy.amazonaws.com"),
                    Description = "Allows Application Deployment.",
                    ManagedPolicies = new[] { ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSCodeDeployRole") }
                }),
                AutoRollback = new AutoRollbackConfig { FailedDeployment = true },
                DeploymentConfig = ServerDeploymentConfig.HALF_AT_A_TIME,
                LoadBalancer = LoadBalancer.Application(instanceInfo.TargetGroup),
                AutoScalingGroups = new[] { instanceInfo.AutoScalingGroup }
            });

            //var pipelinePolicies = new Dictionary<string, PolicyDocument> {
            //    {
            //        "CodeCommitAccess",
            //        new PolicyDocument(new PolicyDocumentProps {
            //            Statements = new [] {
            //                new PolicyStatement(new PolicyStatementProps {
            //                    Effect = Effect.ALLOW,
            //                    Resources = new[] { repo.RepositoryArn },
            //                    Actions = new[] {
            //                        "codecommit:GetBranch",
            //                        "codecommit:GetCommit",
            //                        "codecommit:UploadArchive",
            //                        "codecommit:GetUploadArchiveStatus",
            //                        "codecommit:CancelUploadArchive"
            //                    }
            //                })
            //            }
            //        })
            //    },
            //    {
            //        "ArtifactAccess",
            //        new PolicyDocument(new PolicyDocumentProps {
            //            Statements = new [] {
            //                new PolicyStatement(new PolicyStatementProps {
            //                    Effect = Effect.ALLOW,
            //                    Resources = new[] { artifactBucket.BucketArn },
            //                    Actions = new[] {
            //                        "s3:PutObject",
            //                        "s3:GetObject",
            //                        "s3:GetObjectVersion",
            //                        "s3:GetBucketVersioning"
            //                    }
            //                })
            //            }
            //        })
            //    },
            //    {
            //        "CodeBuildAccess",
            //        new PolicyDocument(new PolicyDocumentProps {
            //            Statements = new [] {
            //                new PolicyStatement(new PolicyStatementProps {
            //                    Effect = Effect.ALLOW,
            //                    Resources = new[] { build.ProjectArn },
            //                    Actions = new[] {
            //                        "codebuild:StartBuild",
            //                        "codebuild:BatchGetBuilds",
            //                        "iam:PassRole"
            //                    }
            //                })
            //            }
            //        })
            //    },
            //    {
            //        "ArtifactAccess",
            //        new PolicyDocument(new PolicyDocumentProps {
            //            Statements = new [] {
            //                new PolicyStatement(new PolicyStatementProps {
            //                    Effect = Effect.ALLOW,
            //                    Resources = new[] { codeDeployApplication.ApplicationArn },
            //                    Actions = new[] {
            //                        "codedeploy:RegisterApplicationRevision"
            //                    }
            //                })
            //            }
            //        })
            //    },
            //    {
            //        "DeploymentPermission",
            //        new PolicyDocument(new PolicyDocumentProps {
            //            Statements = new [] {
            //                new PolicyStatement(new PolicyStatementProps {
            //                    Effect = Effect.ALLOW,
            //                    Resources = new[] { deploymentGroup.DeploymentGroupArn },
            //                    Actions = new[] {
            //                        "codedeploy:CreateDeployment"
            //                    }
            //                })
            //            }
            //        })
            //    },
            //    {
            //        "DeploymentPermission",
            //        new PolicyDocument(new PolicyDocumentProps {
            //            Statements = new [] {
            //                new PolicyStatement(new PolicyStatementProps {
            //                    Effect = Effect.ALLOW,
            //                    Resources = new[] { deploymentGroup.DeploymentConfig.DeploymentConfigArn },
            //                    Actions = new[] {
            //                        "codedeploy:GetDeploymentConfig"
            //                    }
            //                })
            //            }
            //        })
            //    }
            //};

            var sourceOutput = new Artifact_();
            var buildArtifacts = new Artifact_("BuildOutput");
            var pipeline = new Pipeline(stack, "ApplicationPipeline", new PipelineProps
            {
                ArtifactBucket = artifactBucket,
                PipelineName = $"{stack.StackName}Pipeline",
                //Role = new Role(stack, "PipelineServiceRole", new RoleProps
                //{
                //    AssumedBy = new ServicePrincipal("codepipeline.amazonaws.com"),
                //    Description = "Allows access to CodeCommit, S3, CodeBuild, and CodeDeploy.",
                //    InlinePolicies = pipelinePolicies,
                //    ManagedPolicies = new[] {
                //            ManagedPolicy.FromAwsManagedPolicyName("AmazonSSMManagedInstanceCore"),
                //            ManagedPolicy.FromAwsManagedPolicyName("service-role/AmazonEC2RoleforAWSCodeDeploy"),
                //        }
                //}),
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
                                Outputs = new [] { buildArtifacts }
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
                                DeploymentGroup = deploymentGroup,
                                Input = buildArtifacts
                            })
                        }
                    }
                }
            });
        }
    }
}
