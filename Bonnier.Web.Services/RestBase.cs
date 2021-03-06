﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Bonnier.Web.Services
{
	public abstract class RestBase
	{
		public enum Method
		{
			Get,
			Post,
			Put,
			Delete
		}

		public string Username { get; set; }
		public string Secret { get; set; }
		public bool PostJson { get; set; }
		public string Response { get; set; }

		protected RestBase(string username, string secret)
		{
			Username = username;
			Secret = secret;
		}

		protected abstract string GetServiceUrl ();

		protected virtual IBaseResultProvider<ServiceItem> OnCreateItem()
		{
			return new ServiceItem (Username, Secret);
		}

		protected virtual IBaseResultProvider<ServiceResult> OnCreateResult()
		{
			return new ServiceResult (Username, Secret);
		}

		public string[] UrlencodeDictionary(Dictionary<string,string> dictionary)
		{
			if (dictionary.Count <= 0) return new string[] {};
			var output = new string[dictionary.Count];

			var i = 0;
			foreach (var item in dictionary)
			{
				output[i] = item.Key + "=" + item.Value;
				i++;
			}

			return output;
		}

		public object Api(string url)
		{
			return Api(url, Method.Get);
		}

		public object Api()
		{
			return Api("", Method.Get);
		}

		public object Api(Method method, params string[] data)
		{
			return Api("", method, data);
		}

		public object Api(string url, Method method, params string[] data)
		{
			HttpWebRequest request = (HttpWebRequest) WebRequest.Create((GetServiceUrl().Trim('/') + "/" + url).Trim());

			var postData = new List<string>();

			// Add custom post-data
			if (data.Length > 0)
			{
				postData.AddRange(data);
			}

			var headers = new WebHeaderCollection
			{
				String.Format("Authorization: Basic {0}", Encoding.Base64Encode(Username + ":" + Secret))
			};

			postData.Add("_method=" + Enum.GetName(typeof (Method), method).ToUpper());

			request.Headers = headers;
			request.Timeout = 10000;

			ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

			request.ContentLength = data.Length;

			// Urlencode post-data
			if (postData.Count > 0)
			{
				for (var i = 0; i < postData.Count; i++)
				{
					var tmp = postData[i].Split('=');
					tmp[1] = Uri.EscapeDataString(tmp[1]);
					postData[i] = tmp[0] + "=" + tmp[1];
				}
			}

			if (method != Method.Get)
			{
				request.Method = "POST";

				string post = string.Empty;

				// If we post as json, set the correct format
				if (PostJson)
				{
					request.ContentType = "text/json";
					post =  JsonConvert.SerializeObject(postData.ToArray());
				}
				else
				{
					request.ContentType = "application/x-www-form-urlencoded";
					post = String.Join("&", postData.ToArray());
				}

				// Set content length
				request.ContentLength = post.Length;

				// Add post data in either json or string
				using (var streamWriter = new StreamWriter(request.GetRequestStream()))
				{
					streamWriter.Write(post);
				}
			}

			string output;

			try
			{
				using (var response = request.GetResponse())
				{
					var responseStream = response.GetResponseStream();
					if (responseStream == null)
					{
						throw new ApiException("Response was empty");
					}

					using (var reader = new StreamReader(responseStream))
					{
						output = reader.ReadToEnd();
					}
				}
			}
			catch (WebException ex)
			{
				var responseStream = ex.Response.GetResponseStream();
				if (responseStream == null)
				{
					throw new ApiException("Response was empty");
				}

				using (var reader = new StreamReader(responseStream))
				{
					output = reader.ReadToEnd();
				}
			}

			if (string.IsNullOrEmpty(output))
			{
				throw new ApiException("Response was empty");
			}

			JObject result = JObject.Parse(output);

			//dynamic result = Json.Decode(output);

			if (result == null)
			{
				throw new ApiException("Response was empty");
			}

			if (result.Property("status") != null)
			{
				int status = (result.GetValue("status") != null) ? result.GetValue("status").Value<int>() : 0;
				throw new ApiException(result.GetValue("error").Value<string>(), status);
			}

			// We have recieved a collection of items
			if (result.Property("rows") != null)
			{
				ServiceResult collection = (ServiceResult)OnCreateResult();
				collection.Response = output;

				var rows = new List<ServiceItem>();

				foreach (dynamic row in result.Property("rows").Values())
				{
					ServiceItem instance = (ServiceItem)OnCreateItem();
					instance.SetRow(row);
					rows.Add(instance);
				}

				collection.SetRows(rows);
				return collection;
			}

			ServiceItem single = (ServiceItem)OnCreateItem();
			single.SetRow((dynamic)result);
			return single;
		}
	}
}
