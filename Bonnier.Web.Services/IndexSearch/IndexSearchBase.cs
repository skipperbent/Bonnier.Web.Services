﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bonnier.Web.Services.IndexSearch
{
	public abstract class IndexSearchBase : ServiceItem
	{
		public bool Development { get; set; }
		protected readonly string _type;
		protected IndexSearchBase(string username, string secret, string type) : base(username, secret)
		{
			_type = type;
		}

		protected override string GetServiceUrl()
		{
			var url = Development ? "https://staging-indexdb.whitealbum.dk/api/v1/{0}/" : "https://indexdb.whitealbum.dk/api/v1/{0}/";
			return String.Format(url, _type);
		}
	}
}
