﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using StackExchange.Net;

namespace StackExchange.Chat
{
	public class Message
	{
		public class Revision
		{
			public string Text { get; private set; }

			public int AuthorId { get; private set; }

			public string AuthorName { get; private set; }

			// Good luck paring that.
			public string Timestamp { get; private set; }



			internal Revision(string text, int authorId, string authorName, string timestamp)
			{
				Text = text;
				AuthorId = authorId;
				AuthorName = authorName;
				Timestamp = timestamp;
			}
		}

		private const string messageTextUrl = "https://{0}/message/{1}?plain=true";
		private readonly CookieManager cMan;

		public string Host { get; private set; }

		public int Id { get; private set; }

		public int AuthorId { get; private set; }

		public string AuthorName { get; private set; }

		public string Text { get; private set; }

		public int Stars { get; private set; }

		public bool IsPinned { get; private set; }

		public int PinnedBy { get; private set; }

		public ReadOnlyCollection<Revision> Revisions { get; private set; }



		public Message(string host, int messageId, IEnumerable<Net.Cookie> authCookies = null)
		{
			if (authCookies != null)
			{
				cMan = new CookieManager(authCookies);
			}

			Host = host;
			Id = messageId;

			Text = GetTextWithStatus(Host, Id, cMan, out var status);

			if (status != HttpStatusCode.OK)
			{
				throw new Exception($"Unable to fetch message {Id}: {status}.");
			}

			var endpoint = $"https://{Host}/messages/{Id}/history";
			var html = HttpRequest.Get(endpoint, cMan);
			var dom = new HtmlParser().Parse(html);

			FetchHistory(dom);
			Stars = GetStars(dom, out var pinnerId);
			IsPinned = pinnerId != null;
			PinnedBy = pinnerId ?? 0;
		}



		public static bool operator ==(Message a, Message b)
		{
			var aNull = ReferenceEquals(a, null);
			var bNull = ReferenceEquals(b, null);

			if (aNull && bNull)
			{
				return true;
			}

			if (!aNull)
			{
				return a.Equals(b);
			}

			return b.Equals(a);
		}

		public static bool operator !=(Message a, Message b) => !(a == b);

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(obj, null)) return false;

			var m = obj as Message;

			if (ReferenceEquals(m, null)) return false;

			return m.Id == Id;
		}

		public override int GetHashCode() => Id;

		public override string ToString()
		{
			if (Text == null)
			{
				throw new NullReferenceException();
			}

			return Text;
		}

		public static bool Exists(string host, int messageId, CookieManager cookieManager = null)
		{
			GetTextWithStatus(host, messageId, cookieManager, out var status);

			return status == HttpStatusCode.OK;
		}

		public static string GetText(string host, int messageId, CookieManager cookieManager = null)
		{
			return GetTextWithStatus(host, messageId, cookieManager, out var status);
		}



		private static string GetTextWithStatus(string host, int messageId, CookieManager cookieManager, out HttpStatusCode status)
		{
			var endpoint = string.Format(messageTextUrl, host, messageId);
			var text = HttpRequest.GetWithStatus(endpoint, cookieManager, out status);

			return status == HttpStatusCode.OK ? text : null;
		}

		private void FetchHistory(IHtmlDocument dom)
		{
			var monos = dom.QuerySelectorAll("#content h2:nth-of-type(2) ~ div");
			var revs = new List<Revision>();

			foreach (var mono in monos)
			{
				var messageText = mono.QuerySelector(".message-source").TextContent;

				if (string.IsNullOrEmpty(messageText))
				{
					continue;
				}

				var authorA = mono.QuerySelector("a");
				var authorName = authorA.TextContent;
				var authorIdStr = authorA.Attributes["href"].Value.Split('/')[2];
				var authorId = int.Parse(authorIdStr);
				var timestamp = mono.QuerySelector(".timestamp").TextContent;

				var r = new Revision(messageText, authorId, authorName, timestamp);

				if (revs.Count == 0)
				{
					revs.Add(r);
				}
				else
				{
					revs.Insert(0, r);
				}
			}

			AuthorId = revs[0].AuthorId;
			AuthorName = revs[0].AuthorName;
			Revisions = new ReadOnlyCollection<Revision>(revs);
		}

		private int GetStars(IHtmlDocument dom, out int? pinnedBy)
		{
			var hasStars = dom.QuerySelector(".stars") != null;

			pinnedBy = null;

			if (!hasStars)
			{
				return 0;
			}

			var starCount = dom
				.QuerySelector(".times")
				?.TextContent ?? "1";

			var pinnedByStr = dom
				.QuerySelector("#content p a")
				?.Attributes["href"]
				?.Value
				.Split('/')[2];

			if (int.TryParse(pinnedByStr, out var temp))
			{
				pinnedBy = temp;
			}

			return int.Parse(starCount);
		}
	}
}