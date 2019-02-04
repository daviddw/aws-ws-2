#addin Cake.AWS.S3&version=0.6.6

var stackName = "aws-service-test2";

var vcsRef = EnvironmentVariable("VCSREF") ?? "";
var vcsBranch = EnvironmentVariable("VCSBRANCH") ?? "";

var bucketName = EnvironmentVariable("S3BUCKET") ?? "";

var defaultRegion = EnvironmentVariable("AWS_DEFAULT_REGION") ?? "";
var secretKey = EnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
var accessKey = EnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";

var tag = $"{vcsRef}-{vcsBranch}".Replace('/', '-');
var onconnectLambdaFilename = $"aws-ws-2-lambda-onconnect-{tag}.zip";
var ondisconnectLambdaFilename = $"aws-ws-2-lambda-ondisconnect-{tag}.zip";

var target = Argument("target", "Default");

Task("Default")
  .IsDependentOn("Clean")
  .IsDependentOn("Build")
  .IsDependentOn("Deploy");

Task("Clean")
  .Does(() => {
    if (DirectoryExists("./src/onconnect-lambda/bin"))
    {
      DeleteDirectory("./src/onconnect-lambda/bin", new DeleteDirectorySettings {
        Recursive = true
      });
    }
    
    if (DirectoryExists("./src/onconnect-lambda/obj"))
    {
      DeleteDirectory("./src/onconnect-lambda/obj", new DeleteDirectorySettings {
        Recursive = true
      });
    }

    if (DirectoryExists("./src/ondisconnect-lambda/bin"))
    {
      DeleteDirectory("./src/ondisconnect-lambda/bin", new DeleteDirectorySettings {
        Recursive = true
      });
    }
    
    if (DirectoryExists("./src/ondisconnect-lambda/obj"))
    {
      DeleteDirectory("./src/ondisconnect-lambda/obj", new DeleteDirectorySettings {
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
        Configuration = "Release"
    };

    DotNetCorePublish("./src/", settings);

    CreateDirectory("deploy");

    Zip("./src/onconnect-lambda/bin/Release/netcoreapp2.1/publish", $"./deploy/{onconnectLambdaFilename}");

    Zip("./src/ondisconnect-lambda/bin/Release/netcoreapp2.1/publish", $"./deploy/{ondisconnectLambdaFilename}");
  });

Task("Deploy-Lambdas")
  .IsDependentOn("Publish")
  .Does(async () => {
    await S3Upload($"./deploy/{onconnectLambdaFilename}", $"{onconnectLambdaFilename}",
      new UploadSettings()
        .SetAccessKey(accessKey)
        .SetSecretKey(secretKey)
        .SetRegion(defaultRegion)
        .SetBucketName(bucketName));

    await S3Upload($"./deploy/{ondisconnectLambdaFilename}", $"{ondisconnectLambdaFilename}",
      new UploadSettings()
        .SetAccessKey(accessKey)
        .SetSecretKey(secretKey)
        .SetRegion(defaultRegion)
        .SetBucketName(bucketName));
  });

Task("Deploy-Stack")
  .Does(() => {
    RunCommand(Context, "aws", new ProcessSettings {
        Arguments = $"cloudformation deploy --stack-name {stackName}-api --template-file gateway.yaml --capabilities CAPABILITY_IAM --parameter-overrides BucketName={bucketName} OnConnectLambdaPackage={onconnectLambdaFilename} OnDisconnectLambdaPackage={ondisconnectLambdaFilename}",
        WorkingDirectory = new DirectoryPath("./aws/")
    });
  });

Task("Deploy")
  .IsDependentOn("Deploy-Lambdas")
  .IsDependentOn("Deploy-Stack")
  .Does(() => {
  });

Task("Recall")
  .Does(() => {
      RunCommand(Context, "aws", new ProcessSettings {
          Arguments = $"cloudformation delete-stack --stack-name {stackName}-api",
          WorkingDirectory = new DirectoryPath("./aws/")
      });

      RunCommand(Context, "aws", new ProcessSettings {
          Arguments = $"cloudformation wait stack-delete-complete --stack-name {stackName}-api",
          WorkingDirectory = new DirectoryPath("./aws/")
      });
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