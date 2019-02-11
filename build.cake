#addin Cake.AWS.S3&version=0.6.6

var stackName = "aws-service-test2";

var vcsRef = EnvironmentVariable("VCSREF") ?? "";
var vcsBranch = EnvironmentVariable("VCSBRANCH") ?? "";

var bucketName = EnvironmentVariable("S3BUCKET") ?? "";

var defaultRegion = EnvironmentVariable("AWS_DEFAULT_REGION") ?? "";
var secretKey = EnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
var accessKey = EnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";

var tag = $"{vcsRef}-{vcsBranch}".Replace('/', '-');
var lambdaFilename = $"aws-ws-2-lambda-{tag}.zip";

var deploymentState = "dev";

var target = Argument("target", "Default");

Task("Default")
  .IsDependentOn("Clean")
  .IsDependentOn("Deploy");

Task("Clean")
  .Does(() => {
    if (DirectoryExists("./src/aws-ws-lambda-test/bin"))
    {
      DeleteDirectory("./src/aws-ws-lambda-test/bin", new DeleteDirectorySettings {
        Recursive = true
      });
    }
    
    if (DirectoryExists("./src/aws-ws-lambda-test/obj"))
    {
      DeleteDirectory("./src/aws-ws-lambda-test/obj", new DeleteDirectorySettings {
        Recursive = true
      });
    }

    if (DirectoryExists("./deploy"))
    {
      DeleteDirectory("./deploy", new DeleteDirectorySettings {
        Recursive = true
      });
    }
  });

Task("Build")
  .Does(() => {
    var settings = new DotNetCoreBuildSettings
    {
        Configuration = "Debug"
    };

    DotNetCoreBuild("./src/", settings);
  });

Task("Publish")
  .Does(() => {
    var settings = new DotNetCorePublishSettings
    {
        Configuration = "Release",
    };

    DotNetCorePublish("./src/", settings);

    CreateDirectory("deploy");

    Zip("./src/aws-ws-lambda-test/bin/Release/netcoreapp2.1/publish", $"./deploy/{lambdaFilename}");
  });

Task("Deploy-Lambda")
  .IsDependentOn("Publish")
  .Does(async () => {
    await S3Upload($"./deploy/{lambdaFilename}", $"{lambdaFilename}",
      new UploadSettings()
        .SetAccessKey(accessKey)
        .SetSecretKey(secretKey)
        .SetRegion(defaultRegion)
        .SetBucketName(bucketName));
  });

Task("Deploy-Stack")
  .Does(() => {
    var result = RunCommand(Context, "aws", new ProcessSettings {
        Arguments = $"cloudformation deploy --stack-name {stackName}-api --template-file gateway.yaml --capabilities CAPABILITY_IAM --parameter-overrides BucketName={bucketName} LambdaPackage={lambdaFilename} Stage={deploymentState}",
        WorkingDirectory = new DirectoryPath("./aws/")
    });

    if (result != 0) {
      throw new Exception("aws cloudformation deploy failed.");
    }

    result = RunCommand(Context, "aws", new ProcessSettings {
        Arguments = $"cloudformation describe-stacks --stack-name aws-service-test2-api --query 'Stacks[0].Outputs[0].OutputValue'",
        WorkingDirectory = new DirectoryPath("./aws/")
    });

    if (result != 0) {
      throw new Exception("aws cloudformation describe-stacks failed.");
    }
  });

Task("Deploy")
  .IsDependentOn("Deploy-Lambda")
  .IsDependentOn("Deploy-Stack")
  .Does(() => {
  });

Task("Recall")
  .Does(() => {
      var result = RunCommand(Context, "aws", new ProcessSettings {
          Arguments = $"cloudformation delete-stack --stack-name {stackName}-api",
          WorkingDirectory = new DirectoryPath("./aws/")
      });

      if (result != 0) {
        throw new Exception("aws cloudformation delete-stack failed.");
      }

      result = RunCommand(Context, "aws", new ProcessSettings {
          Arguments = $"cloudformation wait stack-delete-complete --stack-name {stackName}-api",
          WorkingDirectory = new DirectoryPath("./aws/")
      });

      if (result != 0) {
        throw new Exception("aws wait stack-delete-complete failed.");
      };
  });

public static int RunCommand(ICakeContext context, string command, ProcessSettings settings = null) {
  if (settings == null) {
    settings = new ProcessSettings();
  }

  if (context.IsRunningOnUnix()) {
    return context.StartProcess(command, settings);
  } else {
    settings.Arguments.Prepend($"/c \"{command}\"");

    return context.StartProcess("cmd", settings);
  }
}

RunTarget(target);
