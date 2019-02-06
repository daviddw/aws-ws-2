using System;
using System.Collections.Generic;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace com.drysdale_wilson.ws_lambda_test
{
    public class SendMessage
    {
        public APIGatewayProxyResponse Handler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "sendmessage",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
            };

            return response;
        }
    }
}
