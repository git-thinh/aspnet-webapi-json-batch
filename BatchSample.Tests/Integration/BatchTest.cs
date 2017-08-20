using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http;
using BatchSample.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace BatchSample.Tests.Integration
{
    [TestClass]
    public class BatchTest : IDisposable
    {
        private readonly HttpServer _server;
        private string _url = "http://localhost:50232/";
        private const string ApplicationJsonContentType = "application/json";

        public BatchTest()
        {
            var config = new HttpConfiguration();

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpBatchRoute(
                routeName: "WebApiBatchJson",
                routeTemplate: "api/$batchJson",
                batchHandler: new JsonBatchHandler(GlobalConfiguration.DefaultServer));

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;

            config.EnsureInitialized();

            _server = new HttpServer(config);
        }

        [TestMethod]
        public void CreateUser()
        {
            var client = new HttpClient();

            var requestJson =
                "[{ \"relativeUrl\": \"api/User\", \"method\": \"POST\", \"body\": { \"username\": \"tony\", \"firstName\": \"Tony\", \"lastName\": \"Monata\" }}, { \"relativeUrl\": \"api/User\",  \"method\": \"POST\", \"body\": { \"username\": \"flint\",  \"firstName\": \"Captain\", \"lastName\": \"Flint\" } }]";

            var request = CreateRequest("api/$batchJson", ApplicationJsonContentType, HttpMethod.Post, requestJson);

            using (HttpResponseMessage response = client.SendAsync(request).Result)
            {
                Assert.IsNotNull(response.Content);
                Assert.AreEqual("application/json", response.Content.Headers.ContentType.MediaType);

                Assert.IsNotInstanceOfType(response.Content, typeof(ObjectContent<HttpError>));

                var responseContent = response.Content.ReadAsAsync<IList<JsonResponseMessage>>().Result;

                Assert.AreEqual(2, responseContent.Count);
                Assert.AreEqual(200, responseContent[0].code);
                Assert.AreEqual(200, responseContent[1].code);
            }

            request.Dispose();
        }

        public void Dispose()
        {
            if (_server != null)
            {
                _server.Dispose();
            }
        }

        private HttpRequestMessage CreateRequest(string url, string contentType, HttpMethod method, string content)
        {
            var request = new HttpRequestMessage {RequestUri = new Uri(_url + url)};

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
            request.Method = method;

            if (string.IsNullOrEmpty(content)) return request;

            var jsonContent = new StringContent(content, Encoding.UTF8, contentType);
            request.Content = jsonContent;

            return request;
        }
    }
}
