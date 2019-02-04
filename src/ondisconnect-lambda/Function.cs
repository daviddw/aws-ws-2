using System.Collections.Generic;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace com.drysdalewilson.ondisconnect
{
    public class Function
    {
        public APIGatewayProxyResponse Handler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "disconnected",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } },
            };

            return response;
        }
    }
}
