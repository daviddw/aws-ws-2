using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace ws_lambda_test
{
    public class OnDisconnect
    {
        public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            var client = new AmazonDynamoDBClient();

            var attributes = new Dictionary<string, AttributeValue>();

            try
            {
                attributes["connectionId"] = new AttributeValue { S = input.RequestContext.ConnectionId };
            }
            catch (Exception e)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = e.Message,
                    Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
                };
            }

            var request = new DeleteItemRequest
            {
                TableName = Environment.ExpandEnvironmentVariables("%TABLE_NAME%"),
                Key = attributes
            };

            var result = await client.DeleteItemAsync(request);

            return new APIGatewayProxyResponse
            {
                StatusCode = (int)result.HttpStatusCode,
                Body = (result.HttpStatusCode == HttpStatusCode.OK) ? "disconnected" : "failed to disconnect",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
            };
        }
    }
}
