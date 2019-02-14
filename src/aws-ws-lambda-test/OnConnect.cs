using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ws_lambda_test
{
    public class OnConnect
    {
        public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            var client = new AmazonDynamoDBClient();

            var attributes = new Dictionary<string, AttributeValue>();

            try
            {
                attributes["connectionId"] = new AttributeValue { S = input.RequestContext.ConnectionId };
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

            var request = new PutItemRequest
            {
                TableName = Environment.ExpandEnvironmentVariables("%TABLE_NAME%"),
                Item = attributes
            };

            var result = await client.PutItemAsync(request);

            return new APIGatewayProxyResponse
            {
                StatusCode = (int)result.HttpStatusCode,
                Body = (result.HttpStatusCode == HttpStatusCode.OK) ? "connected" : "failed to connect",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
            };
        }
    }
}
