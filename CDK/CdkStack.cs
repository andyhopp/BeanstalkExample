using Amazon.CDK;
using Amazon.CDK.AWS.EC2;

namespace Cdk
{
    public class CdkStack : Stack
    {
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

            var externalLoadBalancerSecurityGroup = new SecurityGroup(vpc, "ExternalLoadBalancerSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows HTTP access to the application."
            });
            var frontEndSecurityGroup = new SecurityGroup(vpc, "UISecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows HTTP access to the UI."
            });
            frontEndSecurityGroup.AddIngressRule(externalLoadBalancerSecurityGroup, Port.Tcp(80), "Allow HTTP");
            var internalLoadBalancerSecurityGroup = new SecurityGroup(vpc, "InternalLoadBalancerSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows HTTP access to the REST API."
            });
            var restApiSecurityGroup = new SecurityGroup(vpc, "ApiSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows HTTP access to the Rest API."
            });


            var db = new Database(this, vpc, restApiSecurityGroup);

            var policy = new Amazon.CDK.AWS.IAM.Policy(this, 
                "DBPasswordSecretAccess",
                new Amazon.CDK.AWS.IAM.PolicyProps
                {
                    PolicyName = "AllowPasswordAccess",
                    Statements = new[] {
                        new Amazon.CDK.AWS.IAM.PolicyStatement(new Amazon.CDK.AWS.IAM.PolicyStatementProps {
                            Effect = Amazon.CDK.AWS.IAM.Effect.ALLOW,
                            Actions = new [] {
                                "secretsmanager:GetSecretValue"
                            },
                            Resources = new [] { db.Password.SecretArn }
                        })
                    }
                });
            var restApiInstances = new AutoScaledInstances(this, targetPlatform, vpc, false, internalLoadBalancerSecurityGroup, restApiSecurityGroup, db, policy: policy);
            var frontEndInstances = new AutoScaledInstances(this, targetPlatform, vpc, true, externalLoadBalancerSecurityGroup, frontEndSecurityGroup, restApiLoadBalancer: restApiInstances.Result.LoadBalancer);
            //var instances = new AutoScaledInstances(this, targetPlatform, vpc, appSecurityGroup);
            new CICD(this, targetPlatform, frontEndInstances.Result, restApiInstances.Result);
        }
    }
}
