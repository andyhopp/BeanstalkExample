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
            var gitStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                WorkingDirectory = System.Environment.CurrentDirectory,
                FileName = "git.exe",
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            gitStartInfo.Arguments = "rev-parse --show-toplevel";
            var gitRoot = System.Diagnostics.Process.Start(gitStartInfo);
            gitRoot.WaitForExit();
            var rootFolder = System.IO.Path.GetFullPath(gitRoot.StandardOutput.ReadToEnd().Trim());

            // Archive/Upload the latest code
            gitStartInfo.WorkingDirectory = rootFolder;
            gitStartInfo.Arguments = "rev-parse HEAD";
            var gitHash = System.Diagnostics.Process.Start(gitStartInfo);
            gitHash.WaitForExit();
            var hash = gitHash.StandardOutput.ReadToEnd().Trim();
            var archiveFilename = System.Environment.ExpandEnvironmentVariables($"%Temp%\\ASPNETExample-{hash.Substring(0,6)}.zip");

            gitStartInfo.Arguments = $"archive {hash} -o \"{archiveFilename}\"";
            var gitArchive = System.Diagnostics.Process.Start(gitStartInfo);
            gitArchive.WaitForExit();
            if (!System.IO.File.Exists(archiveFilename) || new System.IO.FileInfo(archiveFilename).Length == 0)
                throw new InvalidOperationException($"Empty archive! See {archiveFilename} for details.");
            try
            {
                CICD.SourceBucketKey = $"samples/{System.IO.Path.GetFileName(archiveFilename)}";
                var s3Client = new Amazon.S3.AmazonS3Client();
                await s3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest {
                    BucketName = CICD.SourceBucketName,
                    Key = CICD.SourceBucketKey,
                    FilePath = archiveFilename
                });
            }
            finally
            {
                if (System.IO.File.Exists(archiveFilename))
                {
                    System.IO.File.Delete(archiveFilename);
                }
            }

            if (string.IsNullOrEmpty(CICD.SourceBucketKey))
                throw new InvalidOperationException("Missing key for source code!");

            var app = new App();
            new CdkStack(app, "DotNetDemo");
            app.Synth();
        }
    }
}
