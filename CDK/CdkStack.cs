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
    public class CdkStack : Stack
    {
        public const string SourceBucketName = "us-east-1.andyhoppatamazon.com";
        public static string SourceBucketKey { get; set; }

        internal CdkStack(Construct scope, string id, IStackProps props = null) : 
            base(scope, id, props)
        {
            var targetPlatform = new CfnParameter(this, "TargetPlatform", new CfnParameterProps
            {
                AllowedValues = new[] { "Linux", "Windows" },
                Type = "String",
                Default = "Linux"
            });

            var vpc = new Vpc(this, "VPC", new VpcProps
            {
                Cidr = "10.0.0.0/16",
                MaxAzs = 2,
                NatGatewayProvider = NatProvider.Gateway(),
                NatGateways = 2,
                NatGatewaySubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC, OnePerAz = true },
                SubnetConfiguration = new[]
                {
                    new SubnetConfiguration { CidrMask = 24, Name = "PublicSubnet", SubnetType = SubnetType.PUBLIC },
                    new SubnetConfiguration { CidrMask = 24, Name = "PrivateSubnet", SubnetType = SubnetType.PRIVATE },
                }
            });

            var instancesResult = BuildLoadBalancedInstances(targetPlatform, vpc);
            BuildCICD(targetPlatform, instancesResult);
        }

        private class LoadBalancedInstancesResult
        {
            public AutoScalingGroup AutoScalingGroup { get; set; }
            public ApplicationTargetGroup TargetGroup { get; set; }
        }

        private LoadBalancedInstancesResult BuildLoadBalancedInstances(CfnParameter targetPlatform, Vpc vpc)
        {
            IMachineImage selectedImage;
            
            if (targetPlatform.ValueAsString == "Windows")
            {
                var userData = UserData.ForWindows();
                userData.AddCommands(
                    "New-Item -Path c:\\temp -ItemType Directory -Force",
                    $"Read-S3Object -BucketName aws-codedeploy-{this.Region}/latest -Key codedeploy-agent.msi -File c:\\temp\\codedeploy-agent.msi",
                    "Start-Process -Wait -FilePath c:\\temp\\codedeploy-agent.msi -WindowStyle Hidden"
                );
                selectedImage = new WindowsImage(
                    WindowsVersion.WINDOWS_SERVER_2019_ENGLISH_CORE_BASE,
                    new WindowsImageProps
                    {
                        UserData = userData
                    });
            }
            else
            {
                var userData = UserData.ForLinux(new LinuxUserDataOptions { Shebang = "#!/bin/bash -xe" });
                userData.AddCommands(
                    "exec > >(tee /var/log/user-data.log|logger -t user-data -s 2>/dev/console) 2>&1",
                    "yum -y update",
                    "yum install -y aws-cli ruby jq",
                    "sleep 60s",
                    "cd /tmp/",
                    $"curl -O https://aws-codedeploy-{this.Region}.s3.amazonaws.com/latest/install",
                    "chmod +x ./install",
                    "if ./install auto; then",
                    "    echo \"CodeDeploy Agent installation completed successfully!\"",
                    "    exit 0",
                    "else",
                    "    echo \"CodeDeploy Agent installation failed, please investigate.\"",
                    "    rm -f /tmp/install",
                    "    exit 1",
                    "fi",
                    "rm -rf /tmp/*"
                );
                selectedImage = new AmazonLinuxImage(new AmazonLinuxImageProps
                {
                    Edition = AmazonLinuxEdition.STANDARD,
                    Virtualization = AmazonLinuxVirt.HVM,
                    Generation = AmazonLinuxGeneration.AMAZON_LINUX_2,
                    Storage = AmazonLinuxStorage.EBS,
                    UserData = userData                    
                });
            };

            var albSecurityGroup = new SecurityGroup(vpc, "LoadBalancerSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows HTTP access to the application."
            });
            var alb = new ApplicationLoadBalancer(this, "ApplicationLoadBalancer", new ApplicationLoadBalancerProps
            {
                InternetFacing = true,
                Vpc = vpc,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
                SecurityGroup = albSecurityGroup,
                IpAddressType = IpAddressType.IPV4,
                Http2Enabled = true
            });
            var albTargetGroup = new ApplicationTargetGroup(this, "ApplicationTargetGroup", new ApplicationTargetGroupProps
            {
                Vpc = vpc,
                Port = 80,
                Protocol = ApplicationProtocol.HTTP,
                TargetType = TargetType.INSTANCE,
                HealthCheck = new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck
                {
                    Timeout = Duration.Seconds(5),
                    Interval = Duration.Seconds(10),
                    HealthyThresholdCount = 2
                }
            });
            var albListener = new ApplicationListener(this, "ApplicationListener", new ApplicationListenerProps
            {
                Port = 80,
                Protocol = ApplicationProtocol.HTTP,
                DefaultAction = ListenerAction.Forward(new[] { albTargetGroup }),
                LoadBalancer = alb
            });

            new CfnCondition(this, "TargetWindows", new CfnConditionProps {
              Expression = Fn.ConditionEquals(targetPlatform, "Windows")
            });

            var asg = new AutoScalingGroup(this, "ApplicationASG", new AutoScalingGroupProps
            {
                Vpc = vpc,
                MinCapacity = 2,
                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.MEDIUM),
                MachineImage = selectedImage,
                BlockDevices = new[] {
                    new Amazon.CDK.AWS.AutoScaling.BlockDevice() {
                        DeviceName = "/dev/xvda",
                        Volume = Amazon.CDK.AWS.AutoScaling.BlockDeviceVolume.Ebs(
                            30,
                            new Amazon.CDK.AWS.AutoScaling.EbsDeviceOptions {
                                VolumeType = Amazon.CDK.AWS.AutoScaling.EbsDeviceVolumeType.GP2,
                                DeleteOnTermination = true
                            }
                        )}
                },
                AssociatePublicIpAddress = false,
                //Role = new Role(this, "AppInstanceRole", new RoleProps
                //{
                //    AssumedBy = new ServicePrincipal("ec2.amazonaws.com"),
                //    Description = "Allows SSM and CodeDeploy access.",
                //    ManagedPolicies = new[] {
                //            ManagedPolicy.FromAwsManagedPolicyName("AmazonSSMManagedInstanceCore"),
                //            ManagedPolicy.FromAwsManagedPolicyName("service-role/AmazonEC2RoleforAWSCodeDeploy"),
                //        }
                //}),
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE }
            });
            asg.Role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AmazonSSMManagedInstanceCore"));
            Tag.Add(asg, "Application", StackName);

            var appSecurityGroup = new SecurityGroup(vpc, "AppSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows HTTP access to the application."
            });
            appSecurityGroup.AddIngressRule(albSecurityGroup, Port.Tcp(80), "Allow HTTP");
            asg.AddSecurityGroup(appSecurityGroup);
            return new LoadBalancedInstancesResult
            {
                AutoScalingGroup = asg,
                TargetGroup = albTargetGroup
            };
        }

        private void BuildCICD(CfnParameter targetPlatform, LoadBalancedInstancesResult instanceInfo)
        {
            var artifactBucket = new Bucket(this, "ArtifactBucket");
            var repo = new Repository(this, "ApplicationRepository", new RepositoryProps
            {
                RepositoryName = StackName,
                Description = $"Contains the code for the {StackName} application."
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

            var build = new PipelineProject(this, "ApplicationBuild", new PipelineProjectProps
            {
                //Role = new Role(this, "BuildServiceRole", new RoleProps
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
            var codeDeployApplication = new ServerApplication(this, "ApplicationDeployment", new ServerApplicationProps
            {
                ApplicationName = StackName
            });
            var deploymentGroup = new ServerDeploymentGroup(this, "ApplicationDeploymentGroup", new ServerDeploymentGroupProps
            {
                Application = codeDeployApplication,
                DeploymentGroupName = $"{StackName.ToLower()}-deployment-group",
                Role = new Role(this, "DeploymentServiceRole", new RoleProps
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
            var pipeline = new Pipeline(this, "ApplicationPipeline", new PipelineProps
            {
                ArtifactBucket = artifactBucket,
                PipelineName = $"{StackName}Pipeline",
                //Role = new Role(this, "PipelineServiceRole", new RoleProps
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
