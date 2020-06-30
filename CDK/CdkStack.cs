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

            var appSecurityGroup = new SecurityGroup(vpc, "AppSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows HTTP access to the application."
            });

            //var db = new Database(this, vpc, appSecurityGroup);
            //var instances = new AutoScaledInstances(this, targetPlatform, vpc, appSecurityGroup, db.Password);
            var instances = new AutoScaledInstances(this, targetPlatform, vpc, appSecurityGroup);
            new CICD(this, targetPlatform, instances.Result);
        }
    }
}
