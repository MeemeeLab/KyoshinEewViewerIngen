using DmdataSharp.Authentication;
using DmdataSharp.Authentication.OAuth;
using DmdataSharp.Exceptions;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace DmdataSharp
{
	/// <summary>
	/// DmdataApiClientの初期化クラス
	/// </summary>
	public class DmdataDistributorApiClientBuilder
	{
		private DmdataDistributorApiClientBuilder(HttpClient httpClient)
		{
			HttpClient = httpClient;
		}

		/// <summary>
		/// デフォルト構成のBuilderを取得
		/// <para></para>
		/// </summary>
		public static DmdataDistributorApiClientBuilder Default
			=> new(new HttpClient(new HttpClientHandler()
			{
#if NET472 || NETSTANDARD2_0
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
#else
				AutomaticDecompression = DecompressionMethods.All
#endif
			})
			{
				Timeout = TimeSpan.FromMilliseconds(5000)
			});

		/// <summary>
		/// APIコールに使用するHttpClient
		/// </summary>
		public HttpClient HttpClient { get; private set; }
		private Authenticator? Authenticator { get; set; }
		private string BaseUrl { get; set; }

		/// <summary>
		/// 独自のHttpClientを使用してBuilderを作成する
		/// </summary>
		/// <param name="client"></param>
		/// <returns></returns>
		public static DmdataDistributorApiClientBuilder UseOwnHttpClient(HttpClient client)
			=> new(client);

		/// <summary>
		/// APIのタイムアウトを指定する
		/// </summary>
		/// <param name="time">タイムアウト</param>
		public DmdataDistributorApiClientBuilder Timeout(TimeSpan time)
		{
			HttpClient.Timeout = time;
			return this;
		}
		/// <summary>
		/// リファラを設定する
		/// </summary>
		/// <param name="referrer">設定するリファラ</param>
		public DmdataDistributorApiClientBuilder Referrer(Uri? referrer)
		{
			HttpClient.DefaultRequestHeaders.Referrer = referrer;
			return this;
		}
		/// <summary>
		/// UserAgentを設定する
		/// </summary>
		/// <param name="userAgent">UserAgent</param>
		public DmdataDistributorApiClientBuilder UserAgent(string userAgent)
		{
			HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
			return this;
		}
		/// <summary>
		/// ベースURLを設定する
		/// </summary>
		/// <param name="userAgent">UserAgent</param>
		public DmdataDistributorApiClientBuilder UseBaseUrl(string baseUrl)
		{
			this.BaseUrl = baseUrl;
			return this;
		}

		/// <summary>
		/// APIキーを使用してAPIの認証を行う
		/// </summary>
		/// <param name="apiKey">APIキー</param>
		/// <returns></returns>
		public DmdataDistributorApiClientBuilder UseApiKey(string apiKey)
		{
			Authenticator = new ApiKeyAuthenticator(apiKey);
			return this;
		}

		/// <summary>
		/// OAuthの認可情報を利用する
		/// </summary>
		/// <param name="credential">認可情報</param>
		/// <returns></returns>
		public DmdataDistributorApiClientBuilder UseOAuth(OAuthCredential credential)
		{
			Authenticator = new OAuthAuthenticator(credential);
			return this;
		}

		/// <summary>
		/// OAuthのClientCredential認証を行う
		/// </summary>
		/// <param name="clientId">クライアントID</param>
		/// <param name="clientSecret">クライアントシークレット</param>
		/// <param name="scopes">認可を求めるスコープ</param>
		/// <returns></returns>
		[Obsolete("廃止予定です。UseOAuthを利用してください")]
		public DmdataDistributorApiClientBuilder UseOAuthClientCredential(string clientId, string clientSecret, string[] scopes)
		{
			Authenticator = new OAuthAuthenticator(new OAuthClientCredential(HttpClient, scopes, clientId, clientSecret));
			return this;
		}

		/// <summary>
		/// OAuthのリフレッシュトークンによる認証を使用する
		/// </summary>
		/// <param name="clientId">クライアントID</param>
		/// <param name="scopes">認可を求めるスコープ</param>
		/// <param name="refreshToken">リフレッシュトークン</param>
		/// <param name="accessToken">アクセストークン(存在する場合)</param>
		/// <param name="accessTokenExpire">アクセストークンの有効期限</param>
		/// <param name="dpopKey">DPoPに使用する公開鍵と秘密鍵のペア nullの場合DPoPは使用されない</param>
		/// <returns></returns>
		[Obsolete("廃止予定です。UseOAuthを利用してください")]
		public DmdataDistributorApiClientBuilder UseOAuthRefreshToken(string clientId, string[] scopes, string refreshToken, string? accessToken, DateTime? accessTokenExpire, ECDsa? dpopKey)
		{
			Authenticator = new OAuthAuthenticator(new OAuthRefreshTokenCredential(HttpClient, scopes, clientId, refreshToken, accessToken, accessTokenExpire, dpopKey));
			return this;
		}

		/// <summary>
		/// 独自の認証方法でAPIの認証を行う
		/// </summary>
		/// <param name="authenticator">APIキー</param>
		/// <returns></returns>
		public DmdataDistributorApiClientBuilder UseAuthenticator(Authenticator authenticator)
		{
			Authenticator = authenticator;
			return this;
		}

		/// <summary>
		/// API V2クライアントの初期化を行う
		/// </summary>
		/// <returns>API V2クライアントのインスタンス</returns>
		public DmdataDistributorV2ApiClient BuildV2ApiClient()
		{
			if (Authenticator is null)
				throw new DmdataException("認証方法が指定されていません。 UseApiKey などを使用して認証方法を決定してください。");
			return new DmdataDistributorV2ApiClient(HttpClient, Authenticator, this.BaseUrl);
		}

		/// <summary>
		/// 任意のAPIクライアントの初期化を行う
		/// </summary>
		/// <returns>API V2クライアントのインスタンス</returns>
		public T Build<T>() where T : DmdataApi
		{
			if (Authenticator is null)
				throw new DmdataException("認証方法が指定されていません。 UseApiKey などを使用して認証方法を決定してください。");
			var ins = Activator.CreateInstance(typeof(T), new object[] { HttpClient, Authenticator });
			if (ins is not T api)
				throw new DmdataException("Apiインスタンスの生成に失敗しました");
			return api;
		}
	}
}
