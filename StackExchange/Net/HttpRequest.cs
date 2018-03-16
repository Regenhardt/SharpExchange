﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using RestSharp;

namespace StackExchange.Net
{
	public class HttpRequest
	{
		private readonly List<Method> supportedMethods;
		private readonly List<HttpStatusCode> handleRedirectCodes;
		private bool sent;

		public Method Verb { get; set; }

		public string Endpoint { get; set; }

		public CookieManager Cookies { get; set; }



		public HttpRequest()
		{
			supportedMethods = new List<Method>
			{
				Method.GET,
				Method.POST
			};

			handleRedirectCodes = new List<HttpStatusCode>
			{
				HttpStatusCode.Found,
				HttpStatusCode.Moved
			};
		}



		public static string Get(string endpoint)
		{
			return new HttpRequest
			{
				Verb = Method.GET,
				Endpoint = endpoint
			}.Send().Content;
		}

		public static string Get(string endpoint, CookieManager cookies)
		{
			return new HttpRequest
			{
				Verb = Method.GET,
				Endpoint = endpoint,
				Cookies = cookies
			}.Send().Content;
		}

		public RestResponse Send() => Send(null);

		public RestResponse Send(object data)
		{
			if (!supportedMethods.Contains(Verb))
			{
				throw new VerbNotSupportedException(Verb);
			}

			if ((!Endpoint?.StartsWith("http")) ?? true)
			{
				throw new InvalidEndpointException($"The endpoint '{Endpoint}' is not valid.");
			}

			if (sent)
			{
				throw new RequestAlreadySentException();
			}

			var endpointUri = new Uri(Endpoint);
			var baseUrl = $"{endpointUri.Scheme}://{endpointUri.Host}";

			var client = new RestClient(baseUrl)
			{
				FollowRedirects = false
			};

			var request = new RestRequest(endpointUri.PathAndQuery, Verb);

			if (Cookies != null)
			{
				var validCookies = Cookies.Cookies.Where(x =>
				{
					var domain = x.Domain;

					if (x.Domain.StartsWith("."))
					{
						domain = x.Domain.Remove(0, 1);
					}

					return endpointUri.Host.EndsWith(domain);
				});

				foreach (var c in validCookies)
				{
					request.AddCookie(c.Name, c.Value);
				}
			}

			if (data != null)
			{
				request.AddObject(data);
			}

			var response = (RestResponse)client.Execute(request);

			Cookies?.Add(response.Cookies);

			if (handleRedirectCodes.Contains(response.StatusCode))
			{
				response = HandleRedirect(response, baseUrl);
			}

			sent = true;

			return response;
		}



		private RestResponse HandleRedirect(RestResponse response, string baseUrl)
		{
			var redirectTo = response.Headers
				.Where(x => x.Name == "Location")
				.Select(x => new Uri(x.Value.ToString(), UriKind.RelativeOrAbsolute))
				.ToArray();

			if (redirectTo.Length != 1)
			{
				throw new InvalidRedirectException();
			}

			var endpoint = redirectTo[0].OriginalString;

			if (!redirectTo[0].IsAbsoluteUri)
			{
				endpoint = baseUrl + endpoint;
			}

			return new HttpRequest
			{
				Verb = Method.GET,
				Endpoint = endpoint,
				Cookies = Cookies
			}.Send();
		}
	}
}