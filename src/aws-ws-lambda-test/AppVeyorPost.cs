using System;
using System.Collections.Generic;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace ws_lambda_test
{
    public class AppVeyorPost
    {
        public APIGatewayProxyResponse Handler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            var config = new AmazonSimpleNotificationServiceConfig();
            config.ServiceURL = Environment.ExpandEnvironmentVariables("%SNSENDPOINT%");

            var client = new AmazonSimpleNotificationServiceClient(config);

            var request = new PublishRequest
            {
                TopicArn = Environment.ExpandEnvironmentVariables("%TOPICARN%"),
                Message = input.Body,
            };

            try
            {
                client.PublishAsync(request).Wait();
            }
            catch(Exception e)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = e.Message,
                    Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
                };
            }

            context.Logger.LogLine("PublishAsync completed\n");

            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = input.Body,
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
            };
        }
    }
}
