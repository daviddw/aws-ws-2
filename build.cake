#addin Cake.AWS.S3&version=1.0.0&loaddependencies=true

var stackName = "aws-service-test2";

var vcsRef = EnvironmentVariable("VCSREF") ?? "";
var vcsBranch = EnvironmentVariable("VCSBRANCH")?.Split('/').Last() ?? "";

var bucketName = EnvironmentVariable("S3BUCKET") ?? "";

var defaultRegion = EnvironmentVariable("AWS_DEFAULT_REGION") ?? "";
var secretKey = EnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
var accessKey = EnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";

var tag = $"-{vcsRef}-{vcsBranch}".Replace('/', '-');
var lambdaFilename = $"aws-ws-2-lambda{tag}.zip";

var isMasterBranch = string.IsNullOrWhiteSpace(vcsBranch) || (vcsBranch == "master");

var deploymentState = isMasterBranch ? "prod" : "dev";

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
    
    Console.WriteLine($"Published {bucketName}/{lambdaFilename}");
  });

Task("Deploy-Stack")
  .Does(() => {
    if (isMasterBranch)
    {
      var result = RunCommand(Context, "aws", new ProcessSettings {
          Arguments = $"cloudformation deploy --stack-name {stackName}-queue --template-file sqs.yaml --capabilities CAPABILITY_IAM --parameter-overrides BucketName={bucketName} LambdaPackage={lambdaFilename}",
          WorkingDirectory = new DirectoryPath("./aws/")
      });

      if (result != 0) {
        throw new Exception("aws cloudformation deploy failed.");
      }

      result = RunCommand(Context, "aws", new ProcessSettings {
          Arguments = $"cloudformation deploy --stack-name {stackName}-lambda --template-file lambda.yaml --capabilities CAPABILITY_IAM --parameter-overrides BucketName={bucketName} LambdaPackage={lambdaFilename}",
          WorkingDirectory = new DirectoryPath("./aws/")
      });

      if (result != 0) {
        throw new Exception("aws cloudformation deploy failed.");
      }

      result = RunCommand(Context, "aws", new ProcessSettings {
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
    }
    else
    {
      Console.WriteLine($"aws cloudformation deploy --stack-name {stackName}-api --template-file gateway.yaml --capabilities CAPABILITY_IAM --parameter-overrides BucketName={bucketName} LambdaPackage={lambdaFilename} Stage={deploymentState}");
    }
  });

Task("Deploy")
  .IsDependentOn("Deploy-Lambda")
  .IsDependentOn("Deploy-Stack")
  .Does(() => {
  });

Task("Recall")
  .Does(() => {
      WaitStackDelete(Context, $"{stackName}-api");
      Console.WriteLine($"Deleted stack {stackName}-api");
      WaitStackDelete(Context, $"{stackName}-lambda");
      Console.WriteLine($"Deleted stack {stackName}-lambda");
      WaitStackDelete(Context, $"{stackName}-queue");
      Console.WriteLine($"Deleted stack {stackName}-queue");
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

public static void WaitStackDelete(ICakeContext context, string stack) {
  var result = RunCommand(context, "aws", new ProcessSettings {
    Arguments = $"cloudformation delete-stack --stack-name {stack}",
    WorkingDirectory = new DirectoryPath("./aws/")
  });

  if (result != 0) {
    throw new Exception("aws cloudformation delete-stack failed.");
  }

  result = RunCommand(context, "aws", new ProcessSettings {
    Arguments = $"cloudformation wait stack-delete-complete --stack-name {stack}",
    WorkingDirectory = new DirectoryPath("./aws/")
  });

  if (result != 0) {
    throw new Exception("aws wait stack-delete-complete failed.");
  };
}

RunTarget(target);
