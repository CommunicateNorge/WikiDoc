using System;
using System.Text;
using System.Net;
using System.Web;
using System.IO;
using System.Web.Script.Serialization;
//using RestSharp.Authenticators;

namespace Wiki.Utility
{
    public class AuthenticationHelper
    {
        private static OAuthToken cachedAppToken;
        private static Object TokenLock = new object();
        private static JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();

        /// <summary>
        /// Gets the authorization header bearer string which can be used as the Authorization header-value
        /// for a request to the Mdm.Api & Sp.Api
        /// </summary>
        /// <returns>Bearer string with access token</returns>
        public static String GetAuthorizationHeaderBearerString(String resource, String clientId, String clientSecret, String endpoint)
        {
            OAuthToken token = GetOAuthTokenAsApp(resource, clientId, clientSecret, endpoint);
            return "Bearer " + token.access_token;
        }

        public static String GetAuthorizationHeaderBearerString(String resource, String clientId, String clientSecret, String endpoint, String userUpn, String userPwd)
        {
            OAuthToken token = GetOAuthTokenAsUser(userUpn, userPwd, resource, clientId, clientSecret, endpoint);
            return "Bearer " + token.access_token;
        }

        ///// <summary>
        ///// Gets the rest client authenticator.
        ///// </summary>
        ///// <returns></returns>
        //public static JwtAuthenticator GetRestClientAuthenticator()
        //{
        //    OAuthToken token = GetOAuthTokenAsApp();
        //    return new JwtAuthenticator(token.access_token);
        //}

        /// <summary>
        /// Gets the authentication header value.
        /// </summary>
        /// <returns></returns>
        public static System.Net.Http.Headers.AuthenticationHeaderValue GetAuthenticationHeaderValue(String resource, String clientId, String clientSecret, String endpoint)
        {
            OAuthToken token = GetOAuthTokenAsApp(resource, clientId, clientSecret, endpoint);
            return new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.access_token);
        }
        public static System.Net.Http.Headers.AuthenticationHeaderValue GetAuthenticationHeaderValue(String resource, String clientId, String clientSecret, String endpoint, String userUpn, String userPwd)
        {
            OAuthToken token = GetOAuthTokenAsUser(userUpn, userPwd, resource, clientId, clientSecret, endpoint);
            return new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.access_token);
        }

        /// <summary>
        /// Sets the authorization header for calls against Sp- and Mdm.Api, on a HttpWebRequest-object.
        /// </summary>
        /// <param name="request">The request</param>
        public static void SetAuthorizationHeader(HttpWebRequest request, String resource, String clientId, String clientSecret, String endpoint)
        {
            request.PreAuthenticate = true;
            request.Headers.Add("Authorization", AuthenticationHelper.GetAuthorizationHeaderBearerString(resource, clientId, clientSecret, endpoint));
        }


        /// <summary>
        /// Checks if the cached token is about to expire
        /// </summary>
        /// <returns>True if the token is expired</returns>
        private static bool ShouldRenewToken()
        {
            if (cachedAppToken != null)
            {
                long epochNow = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                long validTo = Convert.ToInt64(cachedAppToken.expires_on);
                if (epochNow < (validTo - 90))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets an OAuth2.0 authentication token for an App from Rema's auth-endpoint.
        /// </summary>
        /// <param name="clientId">ClientId of the application in AD</param>
        /// <param name="clientSecret">ClientSecret of the application in AD</param>
        /// <param name="resource">The resource the token should be valid for</param>
        /// <returns>OAuthToken-object with an access token string</returns>
        public static OAuthToken GetOAuthTokenAsApp(String resource, String clientId, String clientSecret, String endpoint)
        {
            string bodyData = "grant_type=client_credentials&client_id="
                             + HttpUtility.UrlEncode(clientId)
                             + "&client_secret="
                             + HttpUtility.UrlEncode(clientSecret)
                             + "&resource="
                             + HttpUtility.UrlEncode(resource);

            return GetOAuthToken(bodyData, endpoint);
        }

        /// <summary>
        /// Gets an OAuth2.0 authentication token for an App from a tenant's auth-endpoint.
        /// </summary>
        /// <param name="clientId">ClientId of the application in AD</param>
        /// <param name="clientSecret">ClientSecret of the application in AD</param>
        /// <param name="resource">The resource the token should be valid for</param>
        /// <param name="AADInstance">The aad instance.</param>
        /// <param name="tenant">The tenant.</param>
        /// <returns>OAuthToken-object with an access token string</returns>
        public static OAuthToken GetOAuthTokenAsApp(String resource, String clientId, String clientSecret, String AADInstance, String tenant)
        {
            string bodyData = "grant_type=client_credentials&client_id="
                             + HttpUtility.UrlEncode(clientId)
                             + "&client_secret="
                             + HttpUtility.UrlEncode(clientSecret)
                             + "&resource="
                             + HttpUtility.UrlEncode(resource);

            return GetOAuthToken(bodyData, GetAuthenticationEndpoint(AADInstance, tenant));
        }


        /// <summary>
        /// Gets an OAuth2.0 authentication token for a specific user from Rema's auth-endpoint.
        /// </summary>
        /// <param name="clientId">ClientId of the application in AD</param>
        /// <param name="clientSecret">ClientSecret of the application in AD</param>
        /// <param name="resource">The resource the token should be valid for</param>
        /// <param name="userUpn">The UPN of a user</param>
        /// <param name="userPwd">A user's password</param>
        /// <returns>OAuthToken-object with an access token string</returns>
        public static OAuthToken GetOAuthTokenAsUser(String userUpn, String userPwd, String resource, String clientId, String clientSecret, String endpoint)
        {
            string bodyData = "grant_type=password&client_id="
                             + HttpUtility.UrlEncode(clientId)
                             + "&client_secret="
                             + HttpUtility.UrlEncode(clientSecret)
                             + "&resource="
                             + HttpUtility.UrlEncode(resource)
                             + "&username="
                             + HttpUtility.UrlEncode(userUpn)
                             + "&password="
                             + HttpUtility.UrlEncode(userPwd);

            return GetOAuthToken(bodyData, endpoint);
        }

        /// <summary>
        /// Gets an OAuth2.0 authentication token as a user from a tenant's auth-endpoint.
        /// </summary>
        /// <param name="clientId">ClientId of the application in AD</param>
        /// <param name="clientSecret">ClientSecret of the application in AD</param>
        /// <param name="resource">The resource the token should be valid for</param>
        /// <param name="userUpn">The UPN of a user</param>
        /// <param name="userPwd">A user's password</param>
        /// <param name="AADInstance">The aad instance.</param>
        /// <param name="tenant">The tenant.</param>
        /// <returns></returns>
        public static OAuthToken GetOAuthTokenAsUser(String userUpn, String userPwd, String resource, String clientId, String clientSecret, String AADInstance, String tenant)
        {
            string bodyData = "grant_type=password&client_id="
                             + HttpUtility.UrlEncode(clientId)
                             + "&client_secret="
                             + HttpUtility.UrlEncode(clientSecret)
                             + "&resource="
                             + HttpUtility.UrlEncode(resource)
                             + "&username="
                             + HttpUtility.UrlEncode(userUpn)
                             + "&password="
                             + HttpUtility.UrlEncode(userPwd);

            return GetOAuthToken(bodyData, GetAuthenticationEndpoint(AADInstance, tenant));
        }

        /// <summary>
        /// Creates an authentication url from an aadinstance and a tenant
        /// </summary>
        /// <param name="AADInstance">The aad instance</param>
        /// <param name="tenant">The tenant id</param>
        /// <returns>OAuth token endpoint</returns>
        private static string GetAuthenticationEndpoint(String AADInstance, String tenant)
        {
            return AADInstance.TrimEnd('/') + "/" + tenant + "/oauth2/token";
        }

        /// <summary>
        /// Gets an OAuth2.0 authentication token from Rema's auth-endpoint.
        /// </summary>
        /// <param name="clientId">ClientId of the application in AD</param>
        /// <param name="clientSecret">ClientSecret of the application in AD</param>
        /// <param name="redirectUri">Redirect Uri</param>
        /// <param name="refreshToken">Refresh token retrieved from signing in to /token/authorize endpoint</param>
        /// <returns>OAuthToken-object</returns>
        /// <exception cref="System.Exception">
        /// No response
        /// or
        /// Not authorized
        /// or
        /// Not found
        /// or
        /// API get auth token error
        /// </exception>
        private static OAuthToken GetOAuthToken(String bodyData, String url)
        {
            System.Net.HttpWebRequest tokenRequest = (HttpWebRequest)WebRequest.Create(url);
            tokenRequest.Method = "POST";
            tokenRequest.Accept = "application/json";

            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] authData = encoding.GetBytes(bodyData);

            // Set the content type of the data being posted.
            tokenRequest.ContentType = "application/x-www-form-urlencoded";

            // Set the content length of the string being posted.
            tokenRequest.ContentLength = authData.Length;

            // Add body content to the request
            using (Stream newStream = tokenRequest.GetRequestStream())
            {
                newStream.Write(authData, 0, authData.Length);
            }

            HttpWebResponse tokenResponse = null;
            try
            {
                tokenResponse = (HttpWebResponse)tokenRequest.GetResponse();
                string responseJson = null;
                using (var reader = new System.IO.StreamReader(tokenResponse.GetResponseStream(), UTF8Encoding.UTF8))
                {
                    responseJson = reader.ReadToEnd();
                }
                return jsonSerializer.Deserialize<OAuthToken>(responseJson);
            }
            catch (WebException exception)
            {
                tokenResponse = ((HttpWebResponse)exception.Response);

                if (tokenResponse == null)
                {
                    throw new Exception("No response", exception);
                }

                if (tokenResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Not authorized", exception);
                }

                if (tokenResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception("Not found", exception);
                }

                throw new Exception("API get auth token error", exception);
            }
        }


    }

    public class OAuthToken
    {
        public string token_type { get; set; }
        public string expires_in { get; set; }
        public string scope { get; set; }
        public string expires_on { get; set; }
        public string not_before { get; set; }
        public string resource { get; set; }
        public string access_token { get; set; }
        public string refresh_token { get; set; }
    }
}
