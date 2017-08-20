using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Batch;

namespace BatchSample.Helpers
{
    /// <summary>
    /// Custom implementation of <see cref="HttpBatchHandler"/> that encodes the HTTP request/response messages as MIME multipart.
    /// </summary>
    /// <remarks>
    /// By default, it buffers the HTTP request messages in memory during parsing.
    /// </remarks>
    public class BatchHandler : HttpBatchHandler
    {
        private BatchExecutionOrder _executionOrder;
        private const string MultiPartContentSubtype = "mixed";
        private const string MultiPartMixed = "multipart/mixed";

        /// <inheritdoc />
        public BatchHandler(HttpServer httpServer) : base(httpServer)
        {
            ExecutionOrder = BatchExecutionOrder.Sequential;
            SupportedContentTypes = new List<string> { MultiPartMixed };
        }

        /// <summary>
        /// Gets the supported content types for the batch request.
        /// </summary>
        public IList<string> SupportedContentTypes { get; private set; }

        /// <summary>
        /// Gets or sets the execution order for the batch requests. The default execution order is sequential.
        /// </summary>
        /// <exception cref="System.ComponentModel.InvalidEnumArgumentException">value</exception>
        public BatchExecutionOrder ExecutionOrder
        {
            get
            {
                return _executionOrder;
            }
            set
            {
                if (!Enum.IsDefined(typeof(BatchExecutionOrder), value))
                {
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(BatchExecutionOrder));
                }
                _executionOrder = value;
            }
        }

        /// <summary>
        /// Creates the batch response message.
        /// </summary>
        /// <param name="responses">The responses for the batch requests.</param>
        /// <param name="request">The original request containing all the batch requests.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The batch response message.</returns>
        public virtual Task<HttpResponseMessage> CreateResponseMessageAsync(IList<HttpResponseMessage> responses, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (responses == null)
            {
                throw new ArgumentNullException(nameof(responses));
            }
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            MultipartContent batchContent = new MultipartContent(MultiPartContentSubtype);

            foreach (HttpResponseMessage batchResponse in responses)
            {
                batchContent.Add(new HttpMessageContent(batchResponse));
            }

            HttpResponseMessage response = request.CreateResponse();
            response.Content = batchContent;
            return Task.FromResult(response);
        }

        /// <inheritdoc/>
        public override async Task<HttpResponseMessage> ProcessBatchAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ValidateRequest(request);

            IList<HttpRequestMessage> subRequests = await ParseBatchRequestsAsync(request, cancellationToken);

            try
            {
                IList<HttpResponseMessage> responses = await ExecuteRequestMessagesAsync(subRequests, cancellationToken);
                return await CreateResponseMessageAsync(responses, request, cancellationToken);
            }
            finally
            {
                foreach (HttpRequestMessage subRequest in subRequests)
                {
                    request.RegisterForDispose(subRequest.GetResourcesForDisposal());
                    request.RegisterForDispose(subRequest);
                }
            }
        }

        /// <summary>
        /// Executes the batch request messages.
        /// </summary>
        /// <param name="requests">The collection of batch request messages.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A collection of <see cref="HttpResponseMessage"/> for the batch requests.</returns>
        public virtual async Task<IList<HttpResponseMessage>> ExecuteRequestMessagesAsync(IEnumerable<HttpRequestMessage> requests, CancellationToken cancellationToken)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            List<HttpResponseMessage> responses = new List<HttpResponseMessage>();

            try
            {
                switch (ExecutionOrder)
                {
                    case BatchExecutionOrder.Sequential:
                        foreach (HttpRequestMessage request in requests)
                        {
                            responses.Add(await Invoker.SendAsync(request, cancellationToken));
                        }
                        break;

                    case BatchExecutionOrder.NonSequential:
                        responses.AddRange(await Task.WhenAll(requests.Select(request => Invoker.SendAsync(request, cancellationToken))));
                        break;
                }
            }
            catch
            {
                foreach (HttpResponseMessage response in responses)
                {
                    if (response != null)
                    {
                        response.Dispose();
                    }
                }
                throw;
            }

            return responses;
        }

        /// <summary>
        /// Converts the incoming batch request into a collection of request messages.
        /// </summary>
        /// <param name="request">The request containing the batch request messages.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A collection of <see cref="HttpRequestMessage"/>.</returns>
        public virtual async Task<IList<HttpRequestMessage>> ParseBatchRequestsAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            List<HttpRequestMessage> requests = new List<HttpRequestMessage>();
            cancellationToken.ThrowIfCancellationRequested();
            MultipartStreamProvider streamProvider = await request.Content.ReadAsMultipartAsync(cancellationToken);
            foreach (HttpContent httpContent in streamProvider.Contents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HttpRequestMessage innerRequest = await httpContent.ReadAsHttpRequestMessageAsync(cancellationToken);
                innerRequest.CopyBatchRequestProperties(request);
                requests.Add(innerRequest);
            }
            return requests;
        }

        /// <summary>
        /// Validates the incoming request that contains the batch request messages.
        /// </summary>
        /// <param name="request">The request containing the batch request messages.</param>
        public virtual void ValidateRequest(HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Content == null)
            {
                throw new HttpResponseException(request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    "BatchRequestMissingContent"));
            }

            MediaTypeHeaderValue contentType = request.Content.Headers.ContentType;
            if (contentType == null)
            {
                throw new HttpResponseException(request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    "BatchContentTypeMissing"));
            }

            if (!SupportedContentTypes.Contains(contentType.MediaType, StringComparer.OrdinalIgnoreCase))
            {
                throw new HttpResponseException(request.CreateErrorResponse(HttpStatusCode.BadRequest, "BatchMediaTypeNotSupported"));
            }
        }
    }
}