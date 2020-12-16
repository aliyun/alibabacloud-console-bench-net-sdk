using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Auth;
using Aliyun.Acs.Core.Exceptions;
using Aliyun.Acs.Core.Http;
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Core.Reader;
using Aliyun.Acs.Core.Regions;
using Aliyun.Acs.Core.Retry;
using Aliyun.Acs.Core.Retry.Condition;
using Aliyun.Acs.Core.Transform;
using Aliyun.Acs.Core.Utils;
using Newtonsoft.Json;

namespace isv_net_sdk
{
    public class ConsoleAcsClient : DefaultAcsClient
    {

        private readonly UserAgent userAgentConfig = new UserAgent();
        private readonly RetryPolicy retryPolicy;
        private string pathPattern = "/api/acs/openapi";
        private Dictionary<string, string> queryParam = new Dictionary<string, string>();
        private string endpoint = string.Empty;


        public ConsoleAcsClient(): base()
        {
            
        }

        public ConsoleAcsClient(IClientProfile profile):base(profile)
        {

        }

        public ConsoleAcsClient(IClientProfile profile, AlibabaCloudCredentials credentials) : base(profile, credentials)
        {

        }

        public ConsoleAcsClient(IClientProfile profile, AlibabaCloudCredentialsProvider credentialsProvider) : base(profile, credentialsProvider)
        {

        }

        public override HttpResponse DoAction<T>(AcsRequest<T> request, bool autoRetry, int maxRetryNumber,
            string regionId,
            AlibabaCloudCredentials credentials, Signer signer, FormatType? format, List<Endpoint> endpoints)
        {
            var httpStatusCode = "";
            var retryAttemptTimes = 0;
            ClientException exception;
            RetryPolicyContext retryPolicyContext;

            do
            {
                try
                {
                    var watch = Stopwatch.StartNew();

                    FormatType? requestFormatType = request.AcceptFormat;
                    format = requestFormatType;

                    ProductDomain domain;
                    if (!string.IsNullOrEmpty(this.endpoint)) 
                    {
                        domain = new ProductDomain("", this.endpoint);
                    }
                    else
                    {
                        domain = request.ProductDomain ??
                        Aliyun.Acs.Core.Regions.Endpoint.FindProductDomain(regionId, request.Product, endpoints);
                    }

                    if (null == domain)
                    {
                        throw new ClientException("SDK.InvalidRegionId", "Can not find endpoint to access.");
                    }

                    var userAgent = UserAgent.Resolve(request.GetSysUserAgentConfig(), userAgentConfig);
                    DictionaryUtil.Add(request.Headers, "User-Agent", userAgent);
                    DictionaryUtil.Add(request.Headers, "x-acs-version", request.Version);
                    if (!string.IsNullOrWhiteSpace(request.ActionName))
                    {
                        DictionaryUtil.Add(request.Headers, "x-acs-action", request.ActionName);
                    }
                    
                    var httpRequest = SignRequest(request, signer, credentials, format, domain);
                    Helper.RunInstanceMethod(typeof(DefaultAcsClient), "ResolveTimeout", this, new object[] { httpRequest, request.Product, request.Version, request.ActionName });
                    SetHttpsInsecure(IgnoreCertificate);
                    ResolveProxy(httpRequest, request);
                    var response = GetResponse(httpRequest);

                    httpStatusCode = response.Status.ToString();
                    Helper.RunInstanceMethod(typeof(DefaultAcsClient), "PrintHttpDebugMsg", this, new object[] { request, response });
                    watch.Stop();
                    return response;
                }
                catch (ClientException ex)
                {
                    retryPolicyContext = new RetryPolicyContext(ex, httpStatusCode, retryAttemptTimes, request.Product,
                        request.Version,
                        request.ActionName, RetryCondition.BlankStatus);

                    exception = ex;
                }

                Thread.Sleep(retryPolicy.GetDelayTimeBeforeNextRetry(retryPolicyContext));
            } while ((retryPolicy.ShouldRetry(retryPolicyContext) & RetryCondition.NoRetry) != RetryCondition.NoRetry);

            if (exception != null)
            {
                throw new ClientException(exception.ErrorCode, exception.ErrorMessage);
            }

            return null;
        }

        public new CommonResponse GetCommonResponse(CommonRequest request)
        {
            var httpResponse = DoAction(request.BuildRequest());
            string data = null;
            if (httpResponse.Content != null)
            {
                data = Encoding.UTF8.GetString(httpResponse.Content);
            }

            var response = new CommonResponse
            {
                Data = data,
                HttpResponse = httpResponse,
                HttpStatus = httpResponse.Status
            };

            return response;
        }

        private HttpRequest SignRequest<T>(AcsRequest<T> request, Signer signer, AlibabaCloudCredentials credentials, FormatType? format, ProductDomain domain)
        {
            var map = new Dictionary<string, string>(request.QueryParameters);

            var imutableMap = new Dictionary<string, string>();
            imutableMap.Add("Product", request.Product);
            if (map.ContainsKey("Action"))
            {
                imutableMap.Add("Action", map["Action"]);
                map.Remove("Action");
            }
            if (map.ContainsKey("Version"))
            {
                imutableMap.Add("Version", map["Version"]);
                map.Remove("Version");
            }
            if (map.ContainsKey("Format"))
            {
                imutableMap.Add("Format", map["Format"]);
                map.Remove("Format");
            }
            map.Add("RegionId", request.RegionId);
            string queryJson = JsonConvert.SerializeObject(map);
            imutableMap.Add("Params", queryJson);
            imutableMap.Add("RegionId", request.RegionId);

            if (null != signer && null != credentials)
            {
                string signature = string.Empty;
                var accessKeyId = credentials.GetAccessKeyId();
                var accessSecret = credentials.GetAccessKeySecret();

                var sessionCredentials = credentials as BasicSessionCredentials;
                var sessionToken = sessionCredentials == null ? null : sessionCredentials.GetSessionToken();
                if (sessionToken != null)
                {
                    DictionaryUtil.Add(request.QueryParameters, "SecurityToken", sessionToken);
                }

                var credential = credentials as BearerTokenCredential;
                var bearerToken = credential == null ? null : credential.GetBearerToken();
                if (bearerToken != null)
                {
                    DictionaryUtil.Add(request.QueryParameters, "BearerToken", bearerToken);
                }

                RefreshSignParameters(imutableMap, signer, accessKeyId, format);

                var paramsToSign = new Dictionary<string, string>(imutableMap);
                if (request.BodyParameters != null && request.BodyParameters.Count > 0)
                {
                    var formParams = new Dictionary<string, string>(request.BodyParameters);
                    var formStr = AcsRequest<T>.ConcatQueryString(formParams);
                    var formData = System.Text.Encoding.UTF8.GetBytes(formStr);
                    request.SetContent(formData, "UTF-8", FormatType.FORM);
                    foreach (var formParam in formParams)
                    {
                        DictionaryUtil.Add(paramsToSign, formParam.Key, formParam.Value);
                    }
                }

                var strToSign = request.Composer.ComposeStringToSign(request.Method, null, signer, paramsToSign, null, null);
                signature = signer.SignString(strToSign, accessSecret + "&");
                DictionaryUtil.Add(imutableMap, "Signature", signature);

                request.StringToSign = strToSign;
            }

            foreach (var keypair in queryParam)
            {
                imutableMap.Add(keypair.Key, keypair.Value);
            }

            request.Url = ComposeUrl(request, domain.DomainName, imutableMap);
            return request;
        }

        private void RefreshSignParameters(Dictionary<string, string> immutableMap,
            Signer signer, string accessKeyId, FormatType? format)
        {
            DictionaryUtil.Add(immutableMap, "Timestamp", ParameterHelper.FormatIso8601Date(DateTime.UtcNow));
            DictionaryUtil.Add(immutableMap, "SignatureMethod", signer.GetSignerName());
            DictionaryUtil.Add(immutableMap, "SignatureVersion", signer.GetSignerVersion());
            DictionaryUtil.Add(immutableMap, "SignatureNonce", Guid.NewGuid().ToString());
            DictionaryUtil.Add(immutableMap, "AccessKeyId", accessKeyId);

            if (null != format)
            {
                DictionaryUtil.Add(immutableMap, "Format", format.ToString());
            }

            if (signer.GetSignerType() != null)
            {
                DictionaryUtil.Add(immutableMap, "SignatureType", signer.GetSignerType());
            }

        }


        public string ComposeUrl<T>(AcsRequest<T> request, string endpoint, Dictionary<string, string> immutableMap)
        {
            var urlBuilder = new StringBuilder("");
            urlBuilder.Append(request.Protocol.ToString().ToLower());
            urlBuilder.Append("://").Append(endpoint);
            if (!urlBuilder.ToString().Contains("?"))
            {
                urlBuilder.Append(pathPattern);
                urlBuilder.Append("?");
            }

            var query = AcsRequest<T>.ConcatQueryString(immutableMap);
            return urlBuilder.Append(query).ToString();
        }

        private void ResolveProxy<T>(HttpRequest httpRequest, AcsRequest<T> request) where T : AcsResponse
        {
            string authorization;
            string proxy;
            var noProxy = GetNoProxy() == null ? null : GetNoProxy().Split(',');

            if (request.Protocol == ProtocolType.HTTP)
            {
                proxy = GetHttpProxy();
            }
            else
            {
                proxy = GetHttpsProxy();
            }

            if (!string.IsNullOrEmpty(proxy))
            {
                var originProxyUri = new Uri(proxy);
                Uri finalProxyUri;
                if (!string.IsNullOrEmpty(originProxyUri.UserInfo))
                {
                    authorization =
                        Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(originProxyUri.UserInfo));
                    finalProxyUri = new Uri(originProxyUri.Scheme + "://" + originProxyUri.Authority);
                    var userInfoArray = originProxyUri.UserInfo.Split(':');
                    ICredentials credential = new NetworkCredential(userInfoArray[0], userInfoArray[1]);

                    httpRequest.WebProxy = new WebProxy(finalProxyUri, false, noProxy, credential);

                    if (httpRequest.Headers.ContainsKey("Authorization"))
                    {
                        httpRequest.Headers.Remove("Authorization");
                    }

                    httpRequest.Headers.Add("Authorization", "Basic " + authorization);
                }
                else
                {
                    finalProxyUri = originProxyUri;
                    httpRequest.WebProxy = new WebProxy(finalProxyUri, false, noProxy);
                }
            }
        }

        private T ParseAcsResponse<T>(AcsRequest<T> request, HttpResponse httpResponse) where T : AcsResponse
        {
            var format = httpResponse.ContentType;

            if (httpResponse.isSuccess())
            {
                return ReadResponse(request, httpResponse, format);
            }

            try
            {
                var error = ReadError(request, httpResponse, format);
                if (null != error.ErrorCode)
                {
                    if (500 <= httpResponse.Status)
                    {
                        throw new ServerException(error.ErrorCode,
                            string.Format("{0}, the request url is {1}, the RequestId is {2}.", error.ErrorMessage,
                                httpResponse.Url ?? "empty", error.RequestId));
                    }

                    if (400 == httpResponse.Status && (error.ErrorCode.Equals("SignatureDoesNotMatch") ||
                            error.ErrorCode.Equals("IncompleteSignature")))
                    {
                        var errorMessage = error.ErrorMessage;
                        var re = new Regex(@"string to sign is:", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        var matches = re.Match(errorMessage);

                        if (matches.Success)
                        {
                            var errorStringToSign = errorMessage.Substring(matches.Index + matches.Length);

                            if (request.StringToSign.Equals(errorStringToSign))
                            {
                                throw new ClientException("SDK.InvalidAccessKeySecret",
                                    "Specified Access Key Secret is not valid.", error.RequestId);
                            }
                        }
                    }

                    throw new ClientException(error.ErrorCode, error.ErrorMessage, error.RequestId);
                }
            }
            catch (ServerException ex)
            {
                throw new ServerException(ex.ErrorCode, ex.ErrorMessage, ex.RequestId);
            }
            catch (ClientException ex)
            {
                throw new ClientException(ex.ErrorCode, ex.ErrorMessage, ex.RequestId);
            }

            var t = Activator.CreateInstance<T>();
            t.HttpResponse = httpResponse;
            return t;
        }

        private T ReadResponse<T>(AcsRequest<T> request, HttpResponse httpResponse, FormatType? format)
        where T : AcsResponse
        {
            var reader = ReaderFactory.CreateInstance(format);
            var context = new UnmarshallerContext();
            var body = Encoding.UTF8.GetString(httpResponse.Content);

            context.ResponseDictionary = request.CheckShowJsonItemName() ?
                reader.Read(body, request.ActionName) :
                reader.ReadForHideArrayItem(body, request.ActionName);

            context.HttpResponse = httpResponse;
            return request.GetResponse(context);
        }

        private AcsError ReadError<T>(AcsRequest<T> request, HttpResponse httpResponse, FormatType? format)
        where T : AcsResponse
        {
            var responseEndpoint = "Error";
            var reader = ReaderFactory.CreateInstance(format);
            var context = new UnmarshallerContext();
            var body = Encoding.Default.GetString(httpResponse.Content);
            context.ResponseDictionary =
                null == reader ? new Dictionary<string, string>() : reader.Read(body, responseEndpoint);

            var error = new AcsError();
            error.HttpResponse = context.HttpResponse;
            var map = context.ResponseDictionary;
            error.RequestId = context.StringValue("Error.requestId") == null ? context.StringValue("Error.RequestId") : context.StringValue("Error.requestId");
            error.ErrorCode = context.StringValue("Error.code") == null ? context.StringValue("Error.Code") : context.StringValue("Error.code");
            error.ErrorMessage = context.StringValue("Error.message") == null ? context.StringValue("Error.Message") : context.StringValue("Error.message");

            return error;

        }

        public new T GetAcsResponse<T>(AcsRequest<T> request) where T : AcsResponse
        {
            var httpResponse = DoAction(request);
            return ParseAcsResponse(request, httpResponse);
        }

        public new T GetAcsResponse<T>(AcsRequest<T> request, bool autoRetry, int maxRetryNumber) where T : AcsResponse
        {
            var httpResponse = DoAction(request, autoRetry, maxRetryNumber);
            return ParseAcsResponse(request, httpResponse);
        }

        public new T GetAcsResponse<T>(AcsRequest<T> request, IClientProfile profile) where T : AcsResponse
        {
            var httpResponse = DoAction(request, profile);
            return ParseAcsResponse(request, httpResponse);
        }

        public new T GetAcsResponse<T>(AcsRequest<T> request, string regionId, Credential credential)
        where T : AcsResponse
        {
            var httpResponse = DoAction(request, regionId, credential);
            return ParseAcsResponse(request, httpResponse);
        }

        public void AddQueryParam(string key, string value)
        {
            queryParam.Add(key, value);
        }

        public void DeleteQueryParam(string key)
        {
            queryParam.Remove(key);
        }

        public string PathPattern
        {
            get
            {
                return pathPattern;
            }
            set
            {
                pathPattern = value;
            }
        }

        public string Endpoint
        { 
            get
            {
                return endpoint;
            }
            set
            {
                endpoint = value;
            }
        }
    }
}