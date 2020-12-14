﻿using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using WordPressPCL.Models;
using WordPressPCL.Models.Exceptions;

namespace WordPressPCL.Utility
{
    /// <summary>
    /// Helper class encapsulates common HTTP requests methods
    /// </summary>
    public class HttpHelper
    {
        private static readonly HttpClient _defaultHttpClient = new HttpClient();
        private readonly HttpClient _httpClient;
        private readonly string _WordpressURI;

        /// <summary>
        /// JSON Web Token
        /// </summary>
        public string JWToken { get; set; }

        /// <summary>
        /// Function called when a HttpRequest response is read
        /// Executed before trying to convert json content to a TClass object.
        /// </summary>
        public Func<string, string> HttpResponsePreProcessing { get; set; }

        /// <summary>
        /// Serialization/Deserialization settings for Json.NET library
        /// https://www.newtonsoft.com/json/help/html/SerializationSettings.htm
        /// </summary>
        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        /// <summary>
        /// Headers returns by WP and http server from last response
        /// </summary>
        public HttpResponseHeaders LastResponseHeaders { get; set; }

        /// <summary>
        /// Constructor
        /// <paramref name="wordpressURI"/>
        /// </summary>
        /// <param name="wordpressURI">base WP REST API endpoint EX. http://demo.com/wp-json/ </param>
        public HttpHelper(string wordpressURI)
        {
            _httpClient = _defaultHttpClient;
            _WordpressURI = wordpressURI;

            // by default don't crash on missing member
            JsonSerializerSettings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };
        }

        /// <summary>
        /// Constructor
        /// <paramref name="httpClient"/>
        /// </summary>
        /// <param name="httpClient">Http client which would be used for sending requests to the WordPress API endpoint.</param>
        public HttpHelper(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _WordpressURI = httpClient.BaseAddress.ToString();

            // by default don't crash on missing member
            JsonSerializerSettings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };
        }

        internal async Task<TClass> GetRequest<TClass>(string route, bool embed, bool isAuthRequired = false)
            where TClass : class
        {
            string embedParam = "";
            if (embed)
            {
                if (route.Contains("?"))
                    embedParam = "&_embed";
                else
                    embedParam = "?_embed";
            }

            try
            {
                HttpResponseMessage response;
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{_WordpressURI}{route}{embedParam}"))
                {
                    if (isAuthRequired)
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JWToken);
                    }
                    response = await _httpClient.SendAsync(requestMessage).ConfigureAwait(false);
                }
                LastResponseHeaders = response.Headers;
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    if (HttpResponsePreProcessing != null)
                        responseString = HttpResponsePreProcessing(responseString);
                    if (JsonSerializerSettings != null)
                        return JsonConvert.DeserializeObject<TClass>(responseString, JsonSerializerSettings);
                    return JsonConvert.DeserializeObject<TClass>(responseString);
                }
                else
                {
                    throw CreateUnexpectedResponseException(response, responseString);
                }
            }
            catch (WPException) { throw; }
            catch (Exception ex)
            {
                Debug.WriteLine("exception thrown: " + ex.Message);
                throw;
            }
        }

        internal async Task<(TClass, HttpResponseMessage)> PostRequest<TClass>(string route, HttpContent postBody, bool isAuthRequired = true)
            where TClass : class
        {
            try
            {
                HttpResponseMessage response;
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_WordpressURI}{route}"))
                {
                    if (isAuthRequired)
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JWToken);
                    }
                    requestMessage.Content = postBody;
                    response = await _httpClient.SendAsync(requestMessage).ConfigureAwait(false);
                }

                LastResponseHeaders = response.Headers;
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    if (HttpResponsePreProcessing != null)
                        responseString = HttpResponsePreProcessing(responseString);
                    if (JsonSerializerSettings != null)
                        return (JsonConvert.DeserializeObject<TClass>(responseString, JsonSerializerSettings), response);
                    return (JsonConvert.DeserializeObject<TClass>(responseString), response);
                }
                else
                {
                    throw CreateUnexpectedResponseException(response, responseString);
                }
            }
            catch (WPException) { throw; }
            catch (Exception ex)
            {
                Debug.WriteLine("exception thrown: " + ex.Message);
                throw;
            }
        }

        internal async Task<bool> DeleteRequest(string route, bool isAuthRequired = true)
        {
            try
            {
                HttpResponseMessage response;
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{_WordpressURI}{route}"))
                {
                    if (isAuthRequired)
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JWToken);
                    }
                    response = await _httpClient.SendAsync(requestMessage).ConfigureAwait(false);
                }

                LastResponseHeaders = response.Headers;
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    throw CreateUnexpectedResponseException(response, responseString);
                }
            }
            catch (WPException) { throw; }
            catch (Exception ex)
            {
                Debug.WriteLine("exception thrown: " + ex.Message);
                throw;
            }
        }

        private static Exception CreateUnexpectedResponseException(HttpResponseMessage response, string responseString)
        {
            Debug.WriteLine(responseString);

            BadRequest badrequest;
            try
            {
                badrequest = JsonConvert.DeserializeObject<BadRequest>(responseString);
            }
            catch (JsonReaderException)
            {
                // the response is not a well formed bad request
                return new WPUnexpectedException(response, responseString);
            }
            return new WPException(badrequest.Message, badrequest);
        }
    }
}
