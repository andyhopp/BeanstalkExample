using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SecretsManager;
using Amazon.CDK.AWS.StepFunctions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cdk
{
    internal class Database
    {
        internal ISecret Password { get; }
        public DatabaseInstance DatabaseResource { get; }
        public string ServerAddress { get; }

        internal Database(CdkStack stack, Vpc vpc, SecurityGroup asgSecurityGroup)
        {            
            var dbSecurityGroup = new SecurityGroup(vpc, "DBSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Allows database access to the specified."
            });
            dbSecurityGroup.AddIngressRule(asgSecurityGroup, Port.Tcp(1433), "Allow SQL Server");

            var db = new DatabaseInstance(stack, $"{stack.StackName}-DatabaseCluster", new DatabaseInstanceProps
            {
                Vpc = vpc,
                VpcPlacement = new SubnetSelection { SubnetType = SubnetType.PRIVATE },
                SecurityGroups = new[] { dbSecurityGroup },
                Engine = DatabaseInstanceEngine.SQL_SERVER_EX,
                MasterUsername = "sa",
                AllocatedStorage = 20,
                MultiAz = false,
                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.SMALL),
                DeletionProtection = false
            });
            DatabaseResource = db;
            ServerAddress = db.DbInstanceEndpointAddress;
            Password = db.Secret;
        }
    }
}
