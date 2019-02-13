using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Newtonsoft.Json.Linq;

namespace ws_lambda_test
{
    public class SqsSendMessage
    {
        public async Task<APIGatewayProxyResponse> Handler(SQSEvent input, ILambdaContext context)
        {
            var client = new AmazonDynamoDBClient();

            var scanRequest = new ScanRequest
            {
                TableName = Environment.ExpandEnvironmentVariables("%TABLE_NAME%"),
                ProjectionExpression = "connectionId"
            };

            ScanResponse connections = null;

            try
            {
                connections = await client.ScanAsync(scanRequest);
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

            var config = new AmazonApiGatewayManagementApiConfig
            {
                ServiceURL = Environment.ExpandEnvironmentVariables("%WSENDPOINT%")
            };

            Console.WriteLine(config.ServiceURL);

            var apiClient = new AmazonApiGatewayManagementApiClient(config);

            var connectionIds = connections.Items.Select(item => item["connectionId"].S).ToList();

            foreach (var connectionId in connectionIds)
            {
                foreach (var record in input.Records)
                {
                    var data = record.Body;
                    var byteArray = Encoding.UTF8.GetBytes(data);
                    var postData = new MemoryStream(byteArray);

                    try
                    {
                        var postToRequest = new PostToConnectionRequest
                        {
                            ConnectionId = connectionId,
                            Data = postData
                        };

                        await apiClient.PostToConnectionAsync(postToRequest);
                    }
                    catch (GoneException)
                    {
                        Console.WriteLine($"Found dead connection, deleting {connectionId}");

                        var attributes = new Dictionary<string, AttributeValue>();

                        attributes["connectionId"] = new AttributeValue { S = connectionId };

                        var deleteRequest = new DeleteItemRequest
                        {
                            TableName = Environment.ExpandEnvironmentVariables("%TABLE_NAME%"),
                            Key = attributes
                        };

                        try
                        {
                            await client.DeleteItemAsync(deleteRequest);
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
                }
            }

            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "data sent",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
            };
        }
    }
}
