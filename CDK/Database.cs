using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SecretsManager;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cdk
{
    internal class Database
    {
        internal Secret Password { get; }
        internal Database(CdkStack stack, Vpc vpc, SecurityGroup asgSecurityGroup)
        {
            Password = new Secret(stack, "DBPassword");
            
            var dbSecurityGroup = new SecurityGroup(vpc, "DBSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows database access to the specified."
            });
            dbSecurityGroup.AddIngressRule(asgSecurityGroup, Port.Tcp(3306), "Allow MySql");

            var cluster = new DatabaseCluster(stack, "Database", new DatabaseClusterProps {
                ClusterIdentifier = $"{stack.StackName.ToLower()}-cluster",
                DefaultDatabaseName = "db",
                Engine = DatabaseClusterEngine.AURORA_MYSQL,
                Instances = 1,
                MasterUser = new Login { 
                    Username = "admin",
                    Password = this.Password.SecretValue
                },
                InstanceProps = new Amazon.CDK.AWS.RDS.InstanceProps
                {
                    InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.SMALL),
                    Vpc = vpc,
                    VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE },
                    SecurityGroups = new[] { dbSecurityGroup }
                }
            });

        }
    }
}
