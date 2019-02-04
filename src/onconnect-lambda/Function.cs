using System.Collections.Generic;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace com.drysdalewilson.onconnect
{
    public class Function
    {
        public APIGatewayProxyResponse Handler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "connected",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
            };

            return response;
        }
    }
}
