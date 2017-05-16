using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Wiki.Models;
using Wiki.Utility;

namespace PosLogRelayService
{
    public class RestClient
    {
        private HttpClient _client;

        public RestClient(AuthenticationInfo auth, List<Header> headers)
        {

            _client = new HttpClient();

            if(auth != null)
            {
                if (auth.GetAuthType() == AuthType.OAuthApp)
                    _client.DefaultRequestHeaders.Authorization = AuthenticationHelper.GetAuthenticationHeaderValue(auth.Resource, auth.ClientId, auth.ClientSecret, auth.AuthenticationEndpoint);
                else if (auth.GetAuthType() == AuthType.OAuthUser)
                    _client.DefaultRequestHeaders.Authorization = AuthenticationHelper.GetAuthenticationHeaderValue(auth.Resource, auth.ClientId, auth.ClientSecret, auth.AuthenticationEndpoint, auth.Username, auth.Pwd);
                else if (auth.GetAuthType() == AuthType.Basic)
                {
                    String encodedCreds = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(auth.Username + ":" + auth.Pwd));
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", encodedCreds);
                }
				else if (auth.GetAuthType() == AuthType.SAS)
				{
					_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("SharedAccessSignature", auth.SAS);
				}
			}

			if(headers != null)
			{
				foreach (var item in headers)
				{
					_client.DefaultRequestHeaders.Add(item.Type, item.Value);

				}
			}
		}

        public async Task<String> GetString(string url)
        {
            try
            {
                String response = await _client.GetStringAsync(url);
                return response;
            }
            catch
            {
                return null;
            }
        }
    }
}
