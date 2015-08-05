﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RingCentral.Http;

namespace RingCentral
{
    public class Platform
    {
        private const string ACCESS_TOKEN_TTL = "3600"; // 60 minutes
        private const string REFRESH_TOKEN_TTL = "36000"; // 10 hours
        private const string REFRESH_TOKEN_TTL_REMEMBER = "604800"; // 1 week
        private const string TOKEN_ENDPOINT = "/restapi/oauth/token";
        private const string REVOKE_ENDPOINT = "/restapi/oauth/revoke";
        
        protected Auth Auth;
        
        private HttpClient _client;

        public Platform(string appKey, string appSecret, string apiEndPoint)
        {
            AppKey = appKey;
            AppSecret = appSecret;
            ApiEndpoint = apiEndPoint;
            Auth = new Auth();
            _client = new HttpClient {BaseAddress = new Uri(ApiEndpoint)};
            _client.DefaultRequestHeaders.Add("SDK-Agent", "Ring Central C# SDK");
            
        }

        private string AppKey { get; set; }
        private string AppSecret { get; set; }
        private string ApiEndpoint { get; set; }

        private List<KeyValuePair<string, string>> QueryParameters { get; set; }
        private Dictionary<string, string> Body { get; set; }

        private string StringBody { get; set; }


        /// <summary>
        ///     Method to generate Access Token and Refresh Token to establish an authenticated session
        /// </summary>
        /// <param name="userName">Login of RingCentral user</param>
        /// <param name="password">Password of the RingCentral User</param>
        /// <param name="extension">Optional: Extension number to login</param>
        /// <param name="isRemember">If set to true, refresh token TTL will be one week, otherwise it's 10 hours</param>
        /// <returns>string response of Authenticate result.</returns>
        public string Authenticate(string userName, string password, string extension, bool isRemember)
        {

            Body = new Dictionary<string, string>
                             {
                                 {"username", userName},
                                 {"password", Uri.EscapeUriString(password)},
                                 {"extension", extension },
                                 {"grant_type", "password"},
                                 {"access_token_ttl", ACCESS_TOKEN_TTL},
                                 {"refresh_token_ttl", isRemember ? REFRESH_TOKEN_TTL_REMEMBER : REFRESH_TOKEN_TTL}
                             };

            string result = AuthPostRequest(TOKEN_ENDPOINT);
            Auth.SetRemember(isRemember);
            Auth.SetData(JObject.Parse(result));

            return result;
        }

        /// <summary>
        ///     Refreshes expired Access token during valid lifetime of Refresh Token
        /// </summary>
        /// <returns>string response of Refresh result</returns>
        public string Refresh()
        {
            if (!Auth.IsRefreshTokenValid()) throw new Exception("Refresh Token has Expired");

            Body = new Dictionary<string, string>
                             {
                                 {"grant_type", "refresh_token"},
                                 {"refresh_token", Auth.GetRefreshToken()},
                                 {"access_token_ttl", ACCESS_TOKEN_TTL},
                                 {"refresh_token_ttl", Auth.IsRemember() ? REFRESH_TOKEN_TTL_REMEMBER : REFRESH_TOKEN_TTL}
                             };

            string result = AuthPostRequest(TOKEN_ENDPOINT);

            Auth.SetData(JObject.Parse(result));

            return result;
        }

        /// <summary>
        ///     Revokes the already granted access to stop application activity
        /// </summary>
        /// <returns>string response of Revoke result</returns>
        public string Revoke()
        {
            Body = new Dictionary<string, string>
                             {
                                 {"token", Auth.GetAccessToken()}
                             };

            Auth.Reset();

            return AuthPostRequest(REVOKE_ENDPOINT);
        }

        /// <summary>
        ///     Authentication, Refresh and Revoke requests all require an Authentication Header Value of "Basic".  This is a
        ///     special
        ///     method to handle those requests.
        /// </summary>
        /// <param name="endPoint">
        ///     This endpoint will be the value passed in depending on the request issued (<c>Authenticate</c>,
        ///     <c>Refresh</c>, <c>Revoke</c>)
        /// </param>
        /// <returns>string response of the AuthPostRequest</returns>
        public string AuthPostRequest(string endPoint)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", GetApiKey());

            HttpResponseMessage result = _client.PostAsync(endPoint, GetFormParameters()).Result;

            return result.Content.ReadAsStringAsync().Result;
        }

        /// <summary>
        ///     A HTTP POST request.  If StringBody is set via <c>SetJsonData</c> it will set the content type of application/json.
        ///     If form paramaters are set via <c>AddFormParameter</c> then it will post those values
        /// </summary>
        /// <param name="endPoint">The Endpoint of the POST request targeted</param>
        /// <returns>The string value of the POST request result</returns>
        public Response PostRequest(string endPoint)
        {
            if (!IsAccessValid()) throw new Exception("Access has Expired");

            HttpContent httpContent = GetHttpContent(_client);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.GetAccessToken());

            Task<HttpResponseMessage> postResult = _client.PostAsync(endPoint, httpContent);

            return SetResponse(postResult);
        }

        /// <summary>
        ///     A HTTP GET request.  If query parameters are set via <c>AddQueryParameters</c> then they will be included in the
        ///     GET request
        /// </summary>
        /// <param name="endPoint">The Endpoint of the GET request</param>
        /// <returns>string response of the GET request</returns>
        public Response GetRequest(string endPoint)
        {
            if (!IsAccessValid()) throw new Exception("Access has Expired");

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.GetAccessToken());

            endPoint += GetQuerystring();

            Task<HttpResponseMessage> result = _client.GetAsync(endPoint);

            ClearQueryParameters();

            return SetResponse(result);
        }

        /// <summary>
        ///     A HTTP DELETE request.
        /// </summary>
        /// <param name="endPoint">The Endpoint of the DELETE request</param>
        /// <returns>string response of the DELETE request</returns>
        public Response DeleteRequest(string endPoint)
        {
            if (!IsAccessValid()) throw new Exception("Access has Expired");

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.GetAccessToken());

            endPoint += GetQuerystring();

            Task<HttpResponseMessage> deleteResult = _client.DeleteAsync(endPoint);

            return SetResponse(deleteResult);
        }

        /// <summary>
        ///     A HTTP PUT request.  If StringBody is set via <c>SetJsonData</c> it will set the content type of application/json.
        ///     If form paramaters are set via <c>AddFormParameter</c> then it will post those values
        /// </summary>
        /// <param name="endPoint">The Endpoint of the PUT request</param>
        /// <returns>string response of the PUT request</returns>
        public Response PutRequest(string endPoint)
        {
            if (!IsAccessValid()) throw new Exception("Access has Expired");

            HttpContent httpContent = GetHttpContent(_client);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.GetAccessToken());

            endPoint += GetQuerystring();

            Task<HttpResponseMessage> putResult = _client.PutAsync(endPoint, httpContent);

            return SetResponse(putResult);
        }

        private static Response SetResponse(Task<HttpResponseMessage> responseMessage)
        {
            int statusCode = Convert.ToInt32(responseMessage.Result.StatusCode);
            string body = responseMessage.Result.Content.ReadAsStringAsync().Result;
            HttpContentHeaders headers = responseMessage.Result.Content.Headers;

            return new Response(statusCode, body, headers);
        }

        private HttpContent GetHttpContent(HttpClient client)
        {
            HttpContent httpContent;

            if (StringBody != null)
            {
                httpContent = new StringContent(StringBody, Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
            }
            else
            {
                httpContent = GetFormParameters();
            }

            return httpContent;
        }

        /// <summary>
        ///     Gets the query string after they were set by <c>AddQueryParameters</c>
        /// </summary>
        /// <returns>A query string</returns>
        public string GetQuerystring()
        {
            if (QueryParameters == null || !QueryParameters.Any()) return "";

            string querystring = "?";

            KeyValuePair<string, string> last = QueryParameters.Last();

            foreach (var parameter in QueryParameters)
            {
                querystring = querystring + (parameter.Key + "=" + parameter.Value);
                if (!parameter.Equals(last))
                {
                    querystring += "&";
                }
            }

            return querystring;
        }

        /// <summary>
        ///     Clears the Query Parameters
        /// </summary>
        public void ClearQueryParameters()
        {
            QueryParameters = new List<KeyValuePair<string, string>>();
        }

        /// <summary>
        ///     Adds a query parameter so that when an appropriate request is issued a query string can be formed
        /// </summary>
        /// <param name="queryField">the Field name of a query field/value pairing</param>
        /// <param name="queryValue">the value of a query field/value pairing</param>
        public void AddQueryParameters(string queryField, string queryValue)
        {
            if (QueryParameters == null)
            {
                QueryParameters = new List<KeyValuePair<string, string>>();
            }

            QueryParameters.Add(new KeyValuePair<string, string>(queryField, queryValue));
        }

        /// <summary>
        ///     Adds a form parameter so that when necessary, form parameters can be populated for a HTTP request
        /// </summary>
        /// <param name="formName">The form name of the name/value pairing</param>
        /// <param name="formValue">The form value of the name/value pairing</param>
        public void AddFormParameter(string formName, string formValue)
        {
            if (Body == null)
            {
                Body = new Dictionary<string, string>();
            }

            Body.Add(formName, formValue);
        }

        /// <summary>
        ///     Gets the form parameters
        /// </summary>
        /// <returns>FormURLEncoded Form parameters</returns>
        public HttpContent GetFormParameters()
        {
            List<KeyValuePair<string, string>> formBodyList = Body.ToList();

            return new FormUrlEncodedContent(formBodyList);
        }

        /// <summary>
        ///     Clears the Form Parameters
        /// </summary>
        public void ClearFormParameters()
        {
            Body = new Dictionary<string, string>();
        }

        /// <summary>
        ///     Sets the json data based on a string input
        /// </summary>
        /// <param name="jsonData">The json data</param>
        public void SetJsonData(string jsonData)
        {
            StringBody = jsonData;
        }

        /// <summary>
        ///     Gets the json data that was set
        /// </summary>
        /// <returns>json data</returns>
        public string GetJsonData()
        {
            return StringBody;
        }


        /// <summary>
        ///     Clears the json data that was set
        /// </summary>
        public void ClearJsonData()
        {
            StringBody = null;
        }

        private String GetApiKey()
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(AppKey + ":" + AppSecret);
            return Convert.ToBase64String(byteArray);
        }

        public HttpClient GetClient()
        {
            return _client;
        }

        public void SetClient(HttpClient client)
        {
            _client = client;
        }

        public void SetXhttpOverRideHeader(string method)
        {
            var allowedMethods = new List<string>(new[] {"GET", "POST", "PUT", "DELETE"});

            if (method != null && allowedMethods.Contains(method.ToUpper()))
            {
                 _client.DefaultRequestHeaders.Add("X-HTTP-Method-Override", method.ToUpper());
            } 
        }

        public void SetUserAgentHeader(string header)
        {
            _client.DefaultRequestHeaders.Add("User-Agent", header);
        }

        public bool IsAccessValid()
        {
            if (Auth.IsAccessTokenValid())
            {
                return true;
            }

            if (Auth.IsRefreshTokenValid())
            {
                Refresh();
                return true;
            }
            return false;
        }
    }
}