using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Batch;
using Newtonsoft.Json.Linq;

namespace BatchSample.Helpers
{
    public class JsonBatchHandler : BatchHandler
    {
        private const string TextJson = "text/json";
        private const string ApplicationJsonContentType = "application/json";

        /// <inheritdoc />
        public JsonBatchHandler(HttpServer httpServer) : base(httpServer)
        {
            SupportedContentTypes.Add(TextJson);
            SupportedContentTypes.Add(ApplicationJsonContentType);
        }

        /// <inheritdoc />
        public override async Task<IList<HttpRequestMessage>> ParseBatchRequestsAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var jsonSubRequests = await request.Content.ReadAsAsync<JsonRequestMessage[]>(cancellationToken);

            // Creating simple requests, and check for the body
            var subRequests = jsonSubRequests.Select(r =>
            {
                var subRequestUri = new Uri(request.RequestUri, "/" + r.relativeUrl);
                var req = new HttpRequestMessage(new HttpMethod(r.method), subRequestUri);

                // Add the body
                if (r.body != null)
                {
                    var content = new StringContent(r.body.ToString(), Encoding.UTF8, ApplicationJsonContentType);
                    req.Content = content;
                }

                // Add the headers
                foreach (var item in request.Headers)
                {
                    req.Headers.Add(item.Key, item.Value);
                }

                req.CopyBatchRequestProperties(request);

                return req;
            });

            return subRequests.ToList();
        }

        /// <inheritdoc />
        public override async Task<HttpResponseMessage> CreateResponseMessageAsync(IList<HttpResponseMessage> responses,
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (responses == null)
            {
                throw new ArgumentNullException(nameof(responses));
            }
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            List<JsonResponseMessage> jsonResponses = new List<JsonResponseMessage>();
            foreach (var subResponse in responses)
            {
                var jsonResponse = new JsonResponseMessage
                {
                    code = (int) subResponse.StatusCode
                };
                foreach (var header in subResponse.Headers)
                {
                    jsonResponse.headers.Add(header.Key, String.Join(",", header.Value));
                }
                if (subResponse.Content != null)
                {
                    jsonResponse.body = await subResponse.Content.ReadAsStringAsync();
                    foreach (var header in subResponse.Content.Headers)
                    {
                        jsonResponse.headers.Add(header.Key, String.Join(",", header.Value));
                    }
                }
                jsonResponses.Add(jsonResponse);
            }

            return request.CreateResponse(HttpStatusCode.OK, jsonResponses);
        }
    }

    public class JsonResponseMessage
    {
        public JsonResponseMessage()
        {
            headers = new Dictionary<string, string>();
        }

        // ReSharper disable once InconsistentNaming
        public int code { get; set; }


        // ReSharper disable once InconsistentNaming
        public Dictionary<string, string> headers { get; set; }


        // ReSharper disable once InconsistentNaming
        public string body { get; set; }
    }

    public class JsonRequestMessage
    {
        // ReSharper disable once InconsistentNaming
        public string method { get; set; }

        // ReSharper disable once InconsistentNaming
        public string relativeUrl { get; set; }

        public JObject body { get; set; }
    }
}