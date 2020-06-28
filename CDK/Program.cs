using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cdk
{
    sealed class Program
    {
        public static async Task Main(string[] args)
        {
            // Archive/Upload the latest code
            var gitHashStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                WorkingDirectory = System.Environment.CurrentDirectory,
                FileName = "git.exe",
                Arguments = "rev-parse HEAD",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            var gitHash = System.Diagnostics.Process.Start(gitHashStartInfo);
            gitHash.WaitForExit();
            var hash = gitHash.StandardOutput.ReadToEnd();

            var archiveFilename = System.Environment.ExpandEnvironmentVariables($"%Temp%\\ASPNETExample-{hash.Substring(0,6)}.zip");
            var gitArchiveStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git.exe",
                Arguments = $"archive {hash} -o {archiveFilename}",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            try
            {
                var gitArchive = System.Diagnostics.Process.Start(gitArchiveStartInfo);
                gitArchive.WaitForExit();
                if (System.IO.File.Exists(archiveFilename))
                {
                    CdkStack.SourceBucketKey = $"samples/{System.IO.Path.GetFileName(archiveFilename)}";
                    var s3Client = new Amazon.S3.AmazonS3Client();
                    await s3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest {
                        BucketName = CdkStack.SourceBucketName,
                        Key = CdkStack.SourceBucketKey
                    });
                }
            }
            finally
            {
                if (System.IO.File.Exists(archiveFilename))
                {
                    System.IO.File.Delete(archiveFilename);
                }
            }

            if (string.IsNullOrEmpty(CdkStack.SourceBucketKey))
                throw new InvalidOperationException("Missing key for source code!");

            var app = new App();
            new CdkStack(app, "DotNetDemo");
            app.Synth();
        }
    }
}
