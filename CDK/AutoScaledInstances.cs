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
using Amazon.CDK.AWS.SecretsManager;
using System.Collections.Generic;

namespace Cdk
{
    internal class LoadBalancedInstancesResult
    {
        public AutoScalingGroup AutoScalingGroup { get; set; }
        public ApplicationTargetGroup TargetGroup { get; set; }
    }

    internal class AutoScaledInstances
    {
        internal LoadBalancedInstancesResult Result { get; }
        internal AutoScaledInstances(CdkStack stack, CfnParameter targetPlatform, Vpc vpc, SecurityGroup appSecurityGroup) //, Secret password)
        {
            IMachineImage selectedImage;

            bool targetWindows = false;
            
            if (targetWindows)
            {
                var userData = UserData.ForWindows();
                userData.AddCommands(
                    "New-Item -Path c:\\temp -ItemType Directory -Force",
                    $"Read-S3Object -BucketName aws-codedeploy-{stack.Region}/latest -Key codedeploy-agent.msi -File c:\\temp\\codedeploy-agent.msi",
                    "Start-Process -Wait -FilePath c:\\temp\\codedeploy-agent.msi -WindowStyle Hidden"
                );
                selectedImage = new WindowsImage(
                    WindowsVersion.WINDOWS_SERVER_2019_ENGLISH_CORE_BASE,
                    new WindowsImageProps
                    {
                        UserData = userData
                    }
                );
            }
            else
            {
                var userData = UserData.ForLinux(new LinuxUserDataOptions { Shebang = "#!/bin/bash -xe" });
                userData.AddCommands(
                    "exec > >(tee /var/log/user-data.log|logger -t user-data -s 2>/dev/console) 2>&1",
                    "yum install -y aws-cli ruby jq",
                    "yum -y update",
                    "cd /tmp/",
                    $"curl -O https://aws-codedeploy-{stack.Region}.s3.amazonaws.com/latest/install",
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
            var alb = new ApplicationLoadBalancer(stack, "ApplicationLoadBalancer", new ApplicationLoadBalancerProps
            {
                InternetFacing = true,
                Vpc = vpc,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
                SecurityGroup = albSecurityGroup,
                IpAddressType = IpAddressType.IPV4,
                Http2Enabled = true
            });
            var albTargetGroup = new ApplicationTargetGroup(stack, "ApplicationTargetGroup", new ApplicationTargetGroupProps
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
            var albListener = new ApplicationListener(stack, "ApplicationListener", new ApplicationListenerProps
            {
                Port = 80,
                Protocol = ApplicationProtocol.HTTP,
                DefaultAction = ListenerAction.Forward(new[] { albTargetGroup }),
                LoadBalancer = alb
            });

            var asg = new AutoScalingGroup(stack, "ApplicationASG", new AutoScalingGroupProps
            {
                Vpc = vpc,
                MinCapacity = 2,
                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.MEDIUM),
                MachineImage = selectedImage,
                BlockDevices = new[] {
                    new Amazon.CDK.AWS.AutoScaling.BlockDevice() {
                        DeviceName = "/dev/xvda",
                        Volume = Amazon.CDK.AWS.AutoScaling.BlockDeviceVolume.Ebs(
                            targetWindows ? 30: 8,
                            new Amazon.CDK.AWS.AutoScaling.EbsDeviceOptions {
                                VolumeType = Amazon.CDK.AWS.AutoScaling.EbsDeviceVolumeType.GP2,
                                DeleteOnTermination = true
                            }
                        )}
                },
                AssociatePublicIpAddress = false,
                //Role = new Role(stack, "AppInstanceRole", new RoleProps
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
            //asg.Role.AttachInlinePolicy(new Policy(stack, "DBPasswordSecretAccess", new PolicyProps { 
            //    PolicyName = "AllowPasswordAccess",
            //    Statements = new[] {
            //        new PolicyStatement(new PolicyStatementProps {
            //            Effect = Effect.ALLOW,
            //            Actions = new [] {
            //                "secretsmanager:GetSecretValue"
            //            },
            //            Resources = new [] { password.SecretArn }
            //        })
            //    }
            //}));
            asg.Role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AmazonSSMManagedInstanceCore"));
            asg.Role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXRayDaemonWriteAccess"));
            Tag.Add(asg, "Application", stack.StackName);

            // Enable access from the ALB
            appSecurityGroup.AddIngressRule(albSecurityGroup, Port.Tcp(80), "Allow HTTP");
            asg.AddSecurityGroup(appSecurityGroup);
            Result = new LoadBalancedInstancesResult
            {
                AutoScalingGroup = asg,
                TargetGroup = albTargetGroup
            };
        }
    }
}